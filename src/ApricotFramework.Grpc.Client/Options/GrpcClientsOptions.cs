namespace ApricotFramework.Grpc.Client.Options;

/// <summary>
/// What every gRPC client this service registers has in common.
/// </summary>
public sealed class GrpcClientsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether call credentials may be sent over a plaintext channel.
    /// Defaults to <see langword="false"/>.
    /// </summary>
    /// <remarks>
    /// Two things, both for a peer this service cannot verify: credentials are allowed onto a plaintext
    /// channel, and a TLS certificate is accepted without being checked. Needed wherever gRPC is served
    /// over plaintext or with a self-signed certificate — a laptop, a docker-compose file, a mesh terminating
    /// TLS at the sidecar — and a fact about the deployment rather than about the code, which is why it
    /// is configuration.
    /// <para>
    /// <strong>It removes the protection that makes a token safe to send.</strong> Anything on the path
    /// can read it and answer in the peer's place. Turning it on is warned about at startup.
    /// </para>
    /// </remarks>
    public bool AllowInsecure { get; set; }

    /// <summary>
    /// Gets or sets how long a unary call may take when the caller sets no deadline of its own. Null,
    /// the default, leaves calls without one.
    /// </summary>
    /// <remarks>
    /// A call with no deadline waits as long as the connection lasts. Setting this is the difference
    /// between a slow dependency being reported as a timeout and it exhausting the caller.
    /// </remarks>
    public TimeSpan? DefaultDeadline { get; set; }
}
