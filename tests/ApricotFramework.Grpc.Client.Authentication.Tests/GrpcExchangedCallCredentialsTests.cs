using ApricotFramework.Authentication.ClientCredentials;
using ApricotFramework.Authentication.TokenExchange;
using ApricotFramework.Authentication;
using ApricotFramework.Grpc.Client.Authentication.Extensions;
using ApricotFramework.Grpc.Client.Extensions;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Grpc.Client.Authentication.Tests;

/// <summary>
/// Covers which authenticator a client resolves, which is the whole of the difference between
/// calling as this service and calling as whoever this service is serving.
/// </summary>
public class GrpcExchangedCallCredentialsTests
{
    [Fact]
    public async Task AddGrpcExchangedCallCredentials_AsksTheExchangeAuthenticator()
    {
        var own = new StubAuthenticator();
        var exchanged = new StubAuthenticator();

        await Call(own, exchanged, builder => builder.AddGrpcExchangedCallCredentials(credentials =>
        {
            credentials.Resource = "urn:svc:orders";
            credentials.Scopes = ["orders.agent"];
        }));

        Assert.Equal(["orders.agent"], exchanged.Requested?.Scopes);
        Assert.Null(own.Requested);
    }

    [Fact]
    public async Task AddGrpcCallCredentials_StillAsksTheClientCredentialsAuthenticator()
    {
        // The pair is the point: registering the exchange must not change what an existing client does.
        var own = new StubAuthenticator();
        var exchanged = new StubAuthenticator();

        await Call(own, exchanged, builder => builder.AddGrpcCallCredentials(credentials =>
        {
            credentials.Scopes = ["orders.read"];
        }));

        Assert.Equal(["orders.read"], own.Requested?.Scopes);
        Assert.Null(exchanged.Requested);
    }

    [Fact]
    public async Task AddGrpcExchangedCallCredentials_PassesTheAudiencesThisClientNamed()
    {
        var exchanged = new StubAuthenticator();

        await Call(new StubAuthenticator(), exchanged, builder => builder.AddGrpcExchangedCallCredentials(
            credentials => credentials.Audiences = ["urn:svc:orders"]));

        Assert.Equal(["urn:svc:orders"], exchanged.Requested?.Audiences);
    }

    [Fact]
    public async Task AddGrpcExchangedCallCredentials_WithNobodyToActFor_FailsTheCallAsOurFault()
    {
        // Never Unauthenticated, and never a call without a token: the caller's credential was fine,
        // there was simply nobody for this service to act as.
        var exchanged = new StubAuthenticator(new TokenRequestException(
            TokenRequestFailure.InvalidCredentials,
            "There is no subject to act for, so no token can be exchanged."));

        var thrown = await Assert.ThrowsAsync<RpcException>(async () =>
            await Client(new StubAuthenticator(), exchanged, builder => builder.AddGrpcExchangedCallCredentials()).Call());

        Assert.Equal(StatusCode.Internal, thrown.StatusCode);
        Assert.Contains("StubClient", thrown.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddGrpcExchangedCallCredentials_WithoutTheExchangeRegistered_SaysSoRatherThanCallingAsItself()
    {
        // A host that forgot AddTokenExchangeAuthentication gets a resolution failure, which is loud.
        // The one outcome that must not happen is the call going out as this service instead.
        var services = new ServiceCollection();

        services.AddSingleton<IClientCredentialsAuthenticator>(new StubAuthenticator());
        services.AddGrpcClientsCore(settings => settings.AllowInsecure = true);

        services
            .AddGrpcClient<StubClient>(client => client.Address = new Uri("http://localhost:1"))
            .AllowInsecureTransportIfConfigured()
            .AddGrpcExchangedCallCredentials();

        var client = services
            .BuildServiceProvider(validateScopes: true)
            .GetRequiredService<IGrpcClientProvider>()
            .Create<StubClient>();

        var thrown = await Assert.ThrowsAsync<RpcException>(async () => await client.Call());

        Assert.IsType<InvalidOperationException>(thrown.Status.DebugException);
        Assert.Contains(nameof(ITokenExchangeAuthenticator), thrown.Status.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Makes one call and discards how it failed, since nothing is listening and only what the
    /// credentials asked for is under test.
    /// </summary>
    /// <param name="own">The authenticator for this service's own token.</param>
    /// <param name="exchanged">The authenticator for a token obtained on somebody's behalf.</param>
    /// <param name="configure">How credentials are attached to the client.</param>
    /// <returns>A task that completes once the call has failed.</returns>
    private static async Task Call(
        StubAuthenticator own,
        StubAuthenticator exchanged,
        Func<IHttpClientBuilder, IHttpClientBuilder> configure)
    {
        await Assert.ThrowsAnyAsync<Exception>(async () => await Client(own, exchanged, configure).Call());
    }

    /// <summary>
    /// Builds a client with both authenticators registered, so that only the choice between them is
    /// under test.
    /// </summary>
    /// <param name="own">The authenticator for this service's own token.</param>
    /// <param name="exchanged">The authenticator for a token obtained on somebody's behalf.</param>
    /// <param name="configure">How credentials are attached to the client.</param>
    /// <returns>The client, pointed at an address nothing is listening on.</returns>
    private static StubClient Client(
        StubAuthenticator own,
        StubAuthenticator exchanged,
        Func<IHttpClientBuilder, IHttpClientBuilder> configure)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IClientCredentialsAuthenticator>(own);
        services.AddSingleton<ITokenExchangeAuthenticator>(exchanged);
        services.AddGrpcClientsCore(settings => settings.AllowInsecure = true);

        configure(services
            .AddGrpcClient<StubClient>(client => client.Address = new Uri("http://localhost:1"))
            .AllowInsecureTransportIfConfigured());

        return services
            .BuildServiceProvider(validateScopes: true)
            .GetRequiredService<IGrpcClientProvider>()
            .Create<StubClient>();
    }
}
