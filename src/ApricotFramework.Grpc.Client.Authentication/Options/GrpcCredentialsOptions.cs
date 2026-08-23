namespace ApricotFramework.Grpc.Client.Authentication.Options;

/// <summary>
/// What presenting a token looks like for every client that presents one.
/// </summary>
public sealed class GrpcCredentialsOptions
{
    /// <summary>
    /// The header a token is presented in unless a deployment says otherwise.
    /// </summary>
    public const string DefaultAuthorizationHeader = "Authorization";

    /// <summary>
    /// Gets or sets the header the access token is presented in. Defaults to
    /// <see cref="DefaultAuthorizationHeader"/>.
    /// </summary>
    /// <remarks>
    /// Worth changing where something between the caller and the callee claims <c>Authorization</c> for
    /// itself — a gateway that authenticates the edge, or a mesh that rewrites it — and the callee needs
    /// the service's own token under a name of its own.
    /// <para>
    /// gRPC lowercases the name and allows letters, digits, <c>_</c>, <c>-</c> and <c>.</c>; a
    /// <c>-bin</c> suffix means a binary value and cannot carry a token.
    /// </para>
    /// </remarks>
    public string AuthorizationHeader { get; set; } = DefaultAuthorizationHeader;
}
