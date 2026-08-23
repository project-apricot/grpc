namespace ApricotFramework.Grpc.Client.Authentication.Options;

/// <summary>
/// What token one client asks for where it differs from this service's default.
/// </summary>
/// <remarks>
/// Per client rather than shared, because the point of these is that they differ per callee.
/// </remarks>
public sealed class CallCredentialsOptions
{
    /// <summary>
    /// Gets or sets the resource indicator to request the token for, or null for the service default.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Gets or sets the scopes to request, or null for the service default.
    /// </summary>
    public IReadOnlyList<string>? Scopes { get; set; }
}
