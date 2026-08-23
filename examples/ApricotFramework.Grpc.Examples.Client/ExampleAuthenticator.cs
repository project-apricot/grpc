using ApricotFramework.Authentication;

namespace ApricotFramework.Grpc.Examples.Client;

/// <summary>
/// Stands in for the client credentials authenticator a real service registers.
/// </summary>
/// <remarks>
/// A real service calls <c>AddClientAuthentication</c> from
/// <c>ApricotFramework.Authentication.AspNetCore</c> and gets tokens from its identity provider. This
/// hands out a fixed string so the example runs with nothing else installed.
/// </remarks>
public sealed class ExampleAuthenticator : IClientAuthenticator
{
    /// <inheritdoc />
    public Task<AuthenticatedClientContext> AuthenticateAsync(
        ClientAuthenticationParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new AuthenticatedClientContext { Token = "example-token" });
    }

    /// <inheritdoc />
    public async Task<T> DoAuthenticatedAsync<T>(
        Func<AuthenticatedClientContext, CancellationToken, Task<T>> securedOperation,
        ClientAuthenticationParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(securedOperation);

        var token = await this.AuthenticateAsync(parameters, cancellationToken).ConfigureAwait(false);

        return await securedOperation(token, cancellationToken).ConfigureAwait(false);
    }
}
