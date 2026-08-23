using ApricotFramework.Grpc.Client.Impl;
using ApricotFramework.Grpc.Client.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Validation;

/// <summary>
/// Vets the client settings at startup: rejects what cannot work and says so about what is merely
/// dangerous.
/// </summary>
/// <remarks>
/// Registration calls <c>ValidateOnStart</c>, so this runs before the host serves anything rather than
/// on the first call.
/// </remarks>
/// <param name="logger">Where to record a setting that is permitted but worth knowing about.</param>
public sealed class GrpcClientsOptionsValidator(ILogger<GrpcClientsOptionsValidator> logger) : IValidateOptions<GrpcClientsOptions>
{
    /// <summary>
    /// Whether the insecure-transport warning has been written.
    /// </summary>
    /// <remarks>
    /// Options are re-validated whenever they are rebuilt — a configuration reload, or a caller taking
    /// <see cref="IOptionsSnapshot{TOptions}"/> per scope. The warning is worth reading once and noise
    /// after that.
    /// </remarks>
    private int warned;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, GrpcClientsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.DefaultDeadline is { } deadline && deadline <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail(
                $"The gRPC client default deadline must be positive, but was {deadline}. Leave it unset for calls with no deadline.");
        }

        if (options.AllowInsecure && Interlocked.Exchange(ref this.warned, 1) == 0)
        {
            GrpcClientLog.InsecureCredentialsAllowed(logger);
        }

        return ValidateOptionsResult.Success;
    }


}
