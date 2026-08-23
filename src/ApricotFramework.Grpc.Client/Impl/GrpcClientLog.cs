using Microsoft.Extensions.Logging;

namespace ApricotFramework.Grpc.Client.Impl;

/// <summary>
/// The log messages the client registration writes.
/// </summary>
internal static partial class GrpcClientLog
{
    /// <summary>
    /// Records that this service is allowed to talk to a peer it has not verified.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Warning,
        Message = "GrpcClients:AllowInsecure is on. gRPC calls may carry this service's access token over plaintext, and any TLS certificate is accepted without being verified, so a peer on the path can read and alter them. Expected on a laptop, in a compose file, or behind a sidecar that terminates TLS; nowhere else.")]
    public static partial void InsecureCredentialsAllowed(ILogger logger);
}
