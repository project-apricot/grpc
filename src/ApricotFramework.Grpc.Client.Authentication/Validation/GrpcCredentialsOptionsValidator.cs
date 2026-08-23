using ApricotFramework.Grpc.Client.Authentication.Options;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Authentication.Validation;

/// <summary>
/// Rejects a header gRPC would not accept, at startup rather than on the first call.
/// </summary>
public sealed class GrpcCredentialsOptionsValidator : IValidateOptions<GrpcCredentialsOptions>
{
    /// <summary>
    /// The suffix gRPC reserves for a binary value, which cannot carry a token.
    /// </summary>
    private const string BinarySuffix = "-bin";

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GrpcCredentialsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var header = options.AuthorizationHeader;

        if (string.IsNullOrWhiteSpace(header))
        {
            // gRPC accepts an empty key, so this would otherwise send the token under no name at all.
            return ValidateOptionsResult.Fail("The gRPC authorization header must be named.");
        }

        if (!IsMetadataKey(header))
        {
            return ValidateOptionsResult.Fail(
                $"The gRPC authorization header '{header}' is not a gRPC metadata key. Keys hold letters, digits, underscores, hyphens and dots.");
        }

        if (header.EndsWith(BinarySuffix, StringComparison.OrdinalIgnoreCase))
        {
            return ValidateOptionsResult.Fail(
                $"The gRPC authorization header '{header}' ends in '{BinarySuffix}', which gRPC reserves for a binary value. A token is text.");
        }

        return ValidateOptionsResult.Success;
    }

    /// <summary>
    /// Reports whether gRPC would accept a name as a metadata key.
    /// </summary>
    /// <param name="header">The name to check.</param>
    /// <returns>Whether it is a key. Case is not a fault: gRPC lowercases the name.</returns>
    private static bool IsMetadataKey(string header)
    {
        foreach (var character in header)
        {
            var accepted = char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '.';

            if (!accepted)
            {
                return false;
            }
        }

        return true;
    }

}
