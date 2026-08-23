using System.Text;
using ApricotFramework.ErrorDefinitions;
using ApricotFramework.Grpc.ErrorDefinitions.Contract;
using ApricotFramework.Grpc.ErrorDefinitions.Serialization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GoogleRpcStatus = Google.Rpc.Status;

namespace ApricotFramework.Grpc.ErrorDefinitions;

/// <summary>
/// Carries classified errors over gRPC, in both directions.
/// </summary>
/// <remarks>
/// The status code is the error's kind — the kinds are the sixteen non-OK <c>google.rpc.Code</c> values —
/// and the errors themselves travel as an <c>apricot.errors.v1.ErrorDetails</c> in the status details.
/// <para>
/// Reads never throw on what arrives: a peer that is not an apricot service, or one whose details cannot
/// be parsed, still yields an error classified from the status code alone.
/// </para>
/// </remarks>
public static class GrpcErrorStatus
{
    /// <summary>
    /// The budget used when the caller names none.
    /// </summary>
    private static readonly GrpcErrorOptions DefaultOptions = new();

    /// <summary>
    /// What one error costs beyond its own encoded size: a field tag and a length prefix.
    /// </summary>
    private const int PerErrorAllowance = 6;

    /// <summary>
    /// What the status costs beyond its message: the code, and the message's own tag and length.
    /// </summary>
    private const int StatusOverhead = 8;

    /// <summary>
    /// What packing any errors at all costs: the type URL and the enclosing tags and lengths.
    /// </summary>
    private static readonly int EnvelopeSize = MeasureEnvelope();

    /// <summary>
    /// Builds the status that reports a set of errors.
    /// </summary>
    /// <param name="errors">The errors, most significant first. The first sets the status code and message.</param>
    /// <param name="options">The size budget, or null for the default.</param>
    /// <returns>The <c>google.rpc.Status</c> to send.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors"/> is empty.</exception>
    /// <remarks>
    /// Add further details of your own — a <c>RetryInfo</c>, a <c>Help</c> — to the returned status before
    /// sending it, remembering that they count against the peer's header list too.
    /// </remarks>
    public static GoogleRpcStatus ToStatus(IReadOnlyList<ErrorDefinition> errors, GrpcErrorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(errors);

        if (errors.Count == 0)
        {
            throw new ArgumentException("A status reports at least one error.", nameof(errors));
        }

        var budget = (options ?? DefaultOptions).MaxDetailsBytes;
        var first = errors[0];

        // Half the budget at most: the message also becomes the grpc-message header, which is bounded
        // even when the details are switched off.
        var messageBudget = budget > 0 ? Math.Min(GrpcErrorOptions.MaxMessageBytes, budget / 2) : GrpcErrorOptions.MaxMessageBytes;

        var status = new GoogleRpcStatus
        {
            Code = ErrorKindStatus.ToGrpcStatusCode(first.Kind),
            Message = Shorten(first.Message, messageBudget),
        };

        var details = budget > 0 ? Fit(errors, budget - status.CalculateSize() - EnvelopeSize) : null;

        if (details is not null)
        {
            status.Details.Add(Any.Pack(details));

            // The per-error allowance is an estimate; measuring the whole status is the guarantee.
            while (status.CalculateSize() > budget && details.Errors.Count > 1)
            {
                details.Errors.RemoveAt(details.Errors.Count - 1);
                details.Truncated = true;
                status.Details[0] = Any.Pack(details);
            }
        }

        if (status.CalculateSize() > budget && budget > 0)
        {
            status.Details.Clear();
            status.Message = Shorten(status.Message, budget - StatusOverhead);
        }

        return status;
    }

    /// <summary>
    /// Builds the exception that reports a set of errors.
    /// </summary>
    /// <param name="errors">The errors, most significant first. The first sets the status code and message.</param>
    /// <param name="options">The size budget, or null for the default.</param>
    /// <returns>The exception to throw from a service method or an interceptor.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="errors"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="errors"/> is empty.</exception>
    public static RpcException ToRpcException(IReadOnlyList<ErrorDefinition> errors, GrpcErrorOptions? options = null)
    {
        return ToStatus(errors, options).ToRpcException();
    }

    /// <summary>
    /// Reads the errors a failed call reported.
    /// </summary>
    /// <param name="exception">The exception the call threw.</param>
    /// <returns>
    /// The errors the callee sent, or a single error classified from the status code when it sent none
    /// this library can read.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="exception"/> is null.</exception>
    public static IReadOnlyList<ErrorDefinition> ToErrors(RpcException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var statusKind = ErrorKindStatus.FromGrpcStatusCode((int)exception.StatusCode);
        var details = ReadDetails(exception);

        if (details is null || details.Errors.Count == 0)
        {
            return [Classify(statusKind, exception.Status.Detail)];
        }

        var errors = new List<ErrorDefinition>(details.Errors.Count);

        foreach (var detail in details.Errors)
        {
            errors.Add(new ErrorDefinition
            {
                Kind = string.IsNullOrEmpty(detail.Kind) ? statusKind : detail.Kind,
                Code = string.IsNullOrEmpty(detail.Code)
                    ? ErrorCodes.ForKind(string.IsNullOrEmpty(detail.Kind) ? statusKind : detail.Kind)
                    : detail.Code,
                Message = detail.Message,
                Payload = PayloadStruct.ToPayload(detail.Payload),
            });
        }

        return errors;
    }

    /// <summary>
    /// Describes a failure that carried no readable errors of its own.
    /// </summary>
    /// <param name="kind">The kind the status code classifies the failure as.</param>
    /// <param name="message">Whatever the status said.</param>
    /// <returns>The error standing in for what the callee did not send.</returns>
    private static ErrorDefinition Classify(string kind, string? message)
    {
        return new ErrorDefinition
        {
            Kind = kind,
            Code = ErrorCodes.ForKind(kind),
            Message = message ?? string.Empty,
        };
    }

    /// <summary>
    /// Reads the details of a failed call, tolerating anything that is not this library's contract.
    /// </summary>
    /// <param name="exception">The exception the call threw.</param>
    /// <returns>The errors the callee packed, or null when there are none to read.</returns>
    private static ErrorDetails? ReadDetails(RpcException exception)
    {
        var status = exception.Trailers.GetRpcStatus(ignoreParseError: true);

        if (status is null)
        {
            return null;
        }

        foreach (var detail in status.Details)
        {
            try
            {
                if (detail.TryUnpack<ErrorDetails>(out var errors))
                {
                    return errors;
                }
            }
            catch (InvalidProtocolBufferException)
            {
                // Our type URL over bytes that are not ours. Nothing here is readable.
                return null;
            }
        }

        return null;
    }

    /// <summary>
    /// Packs as much of the errors as the budget allows.
    /// </summary>
    /// <param name="errors">The errors to pack.</param>
    /// <param name="budget">The bytes available to the packed details.</param>
    /// <returns>The details to send, or null when not even one error fits.</returns>
    /// <remarks>
    /// Gives up the payloads before any error, and the last errors before the first: a caller can act on
    /// a kind and a code, and can act on nothing at all if the response fails to arrive.
    /// </remarks>
    private static ErrorDetails? Fit(IReadOnlyList<ErrorDefinition> errors, int budget)
    {
        if (budget <= 0)
        {
            return null;
        }

        var full = Compose(errors, errors.Count, withPayloads: true, truncated: false);

        if (full.CalculateSize() <= budget)
        {
            return full;
        }

        var lean = Compose(errors, errors.Count, withPayloads: false, truncated: true);

        if (lean.CalculateSize() <= budget)
        {
            return lean;
        }

        var fitting = CountThatFit(lean, budget);

        return fitting > 0
            ? Compose(errors, fitting, withPayloads: false, truncated: true)
            : Only(errors[0], budget);
    }

    /// <summary>
    /// Counts how many of the packed errors fit the budget.
    /// </summary>
    /// <param name="details">The errors, already packed without their payloads.</param>
    /// <param name="budget">The bytes available.</param>
    /// <returns>The number of errors that fit, from the first.</returns>
    private static int CountThatFit(ErrorDetails details, int budget)
    {
        var used = 2;
        var count = 0;

        foreach (var detail in details.Errors)
        {
            var cost = detail.CalculateSize() + PerErrorAllowance;

            if (used + cost > budget)
            {
                break;
            }

            used += cost;
            count++;
        }

        return count;
    }

    /// <summary>
    /// Packs the first error alone, cutting its message to whatever room is left.
    /// </summary>
    /// <param name="error">The error to pack.</param>
    /// <param name="budget">The bytes available.</param>
    /// <returns>The details to send, or null when even a kind and a code will not fit.</returns>
    private static ErrorDetails? Only(ErrorDefinition error, int budget)
    {
        var detail = new ErrorDetail { Kind = error.Kind, Code = error.Code };
        var room = budget - detail.CalculateSize() - PerErrorAllowance - 2;

        if (room < 0)
        {
            return null;
        }

        detail.Message = Shorten(error.Message, room);

        var details = new ErrorDetails { Truncated = true };
        details.Errors.Add(detail);

        return details;
    }

    /// <summary>
    /// Packs a number of errors.
    /// </summary>
    /// <param name="errors">The errors to take from.</param>
    /// <param name="count">How many to take, from the first.</param>
    /// <param name="withPayloads">Whether to carry the payloads.</param>
    /// <param name="truncated">Whether something has already been given up.</param>
    /// <returns>The packed errors.</returns>
    private static ErrorDetails Compose(
        IReadOnlyList<ErrorDefinition> errors,
        int count,
        bool withPayloads,
        bool truncated)
    {
        var details = new ErrorDetails { Truncated = truncated };

        for (var index = 0; index < count; index++)
        {
            var error = errors[index];

            var detail = new ErrorDetail
            {
                Kind = error.Kind,
                Code = error.Code,
                Message = error.Message,
            };

            if (withPayloads)
            {
                var payload = PayloadStruct.ToStruct(error.Payload);

                if (payload is not null)
                {
                    detail.Payload = payload;
                }
            }

            details.Errors.Add(detail);
        }

        return details;
    }

    /// <summary>
    /// Measures what an empty set of details costs, so the budget is spent on errors and not guesses.
    /// </summary>
    /// <returns>The encoded size of a status carrying no errors, with room for the length prefixes to grow.</returns>
    private static int MeasureEnvelope()
    {
        var status = new GoogleRpcStatus();
        status.Details.Add(Any.Pack(new ErrorDetails()));

        // Plus, the bytes the nested length prefixes gain as the details fill up.
        return status.CalculateSize() + 8;
    }

    /// <summary>
    /// Cuts text to a number of UTF-8 bytes, never through the middle of a character.
    /// </summary>
    /// <param name="text">The text to cut.</param>
    /// <param name="maxBytes">The most bytes it may occupy.</param>
    /// <returns>The text, or as much of it as fits.</returns>
    private static string Shorten(string text, int maxBytes)
    {
        if (maxBytes <= 0)
        {
            return string.Empty;
        }

        if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
        {
            return text;
        }

        var kept = 0;
        var used = 0;

        foreach (var rune in text.EnumerateRunes())
        {
            used += rune.Utf8SequenceLength;

            if (used > maxBytes)
            {
                break;
            }

            kept += rune.Utf16SequenceLength;
        }

        return text[..kept];
    }
}
