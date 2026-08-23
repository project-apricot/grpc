using System.Net.Http.Headers;
using System.Text;
using ApricotFramework.Authentication;
using Grpc.Core;

namespace ApricotFramework.Grpc.Client.Authentication.Tests;

/// <summary>
/// An authenticator that always answers the same way.
/// </summary>
/// <param name="failure">How it fails, or null to hand out a token.</param>
internal sealed class StubAuthenticator(ClientAuthenticationException? failure = null) : IClientAuthenticator
{
    /// <summary>
    /// Gets what the last call asked for.
    /// </summary>
    internal ClientAuthenticationParameters? Requested { get; private set; }

    /// <inheritdoc />
    public Task<AuthenticatedClientContext> AuthenticateAsync(
        ClientAuthenticationParameters? parameters = null,
        CancellationToken cancellationToken = default)
    {
        this.Requested = parameters;

        return failure is null
            ? Task.FromResult(new AuthenticatedClientContext { Token = "a-token", TokenType = "Bearer" })
            : Task.FromException<AuthenticatedClientContext>(failure);
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

/// <summary>
/// A generated client stands in as this, with one method so a call can actually be made.
/// </summary>
/// <param name="invoker">The invoker the factory builds.</param>
internal sealed class StubClient(CallInvoker invoker)
{
    /// <summary>
    /// The marshaller for the stand-in request and response type.
    /// </summary>
    private static readonly Marshaller<string> Text =
        Marshallers.Create(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);

    /// <summary>
    /// The method every test calls.
    /// </summary>
    private static readonly Method<string, string> Method =
        new(MethodType.Unary, "apricot.Example", "Call", Text, Text);

    /// <summary>
    /// Makes a call, so the credentials run.
    /// </summary>
    /// <returns>The call.</returns>
    internal AsyncUnaryCall<string> Call() => invoker.AsyncUnaryCall(Method, null, new CallOptions(), "request");
}

/// <summary>
/// Answers every request with a failure, having recorded the headers the call carried.
/// </summary>
internal sealed class CapturingHandler : HttpMessageHandler
{
    /// <summary>
    /// Gets the headers of the last request, or null if none was sent.
    /// </summary>
    internal HttpRequestHeaders? Sent { get; private set; }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        this.Sent = request.Headers;

        throw new HttpRequestException("nothing is listening");
    }
}
