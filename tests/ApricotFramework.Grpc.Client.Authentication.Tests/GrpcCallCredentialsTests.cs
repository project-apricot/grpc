using System.Net.Http.Headers;
using ApricotFramework.Authentication;
using ApricotFramework.Grpc.Client.Authentication.Extensions;
using ApricotFramework.Grpc.Client.Authentication.Options;
using ApricotFramework.Grpc.Client.Extensions;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Grpc.Client.Authentication.Tests;

/// <summary>
/// Covers what a call carries, and whose fault it is when no token can be had.
/// </summary>
public class GrpcCallCredentialsTests
{
    [Fact]
    public async Task AddGrpcCallCredentials_TokenObtained_PresentsItUnderTheSchemeTheProviderNamed()
    {
        var sent = await Called();

        Assert.Equal("Bearer a-token", Assert.Single(sent.GetValues(GrpcCredentialsOptions.DefaultAuthorizationHeader)));
    }

    [Fact]
    public async Task AddGrpcCallCredentials_HeaderConfigured_PresentsTheTokenUnderThatNameAlone()
    {
        var sent = await Called(configureShared: shared => shared.AuthorizationHeader = "x-service-authorization");

        Assert.Equal("Bearer a-token", Assert.Single(sent.GetValues("x-service-authorization")));
        Assert.False(sent.Contains(GrpcCredentialsOptions.DefaultAuthorizationHeader));
    }

    [Fact]
    public async Task AddGrpcCallCredentials_HeaderFromConfiguration_IsBoundFromTheGrpcClientsSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GrpcClients:AuthorizationHeader"] = "x-from-config",
            })
            .Build();

        var sent = await Called(configuration: configuration);

        Assert.Equal("Bearer a-token", Assert.Single(sent.GetValues("x-from-config")));
    }

    [Fact]
    public async Task AddGrpcCallCredentials_Always_AsksForWhatThisClientNamed()
    {
        var authenticator = new StubAuthenticator();

        await Assert.ThrowsAnyAsync<Exception>(async () => await Client(authenticator).Call());

        Assert.Equal(["orders.read"], authenticator.Requested?.Scopes);
        Assert.Equal(["urn:svc:orders"], authenticator.Requested?.Resources);
    }

    [Fact]
    public async Task AddGrpcCallCredentials_TokenProviderDown_SaysWaitingMayHelp()
    {
        var thrown = await Failing(new ClientAuthenticationException(
            ClientAuthenticationFailure.Unavailable,
            "the provider at https://login.example.com refused: client_secret is wrong"));

        Assert.Equal(StatusCode.Unavailable, thrown.StatusCode);
        Assert.DoesNotContain("client_secret", thrown.Status.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddGrpcCallCredentials_TokenMisconfigured_BlamesThisServiceAsInternal()
    {
        var thrown = await Failing(new ClientAuthenticationException(
            ClientAuthenticationFailure.InvalidCredentials,
            "the provider rejected the client identifier"));

        // Never Unauthenticated: the caller's own credential was fine, ours is not.
        Assert.Equal(StatusCode.Internal, thrown.StatusCode);
        Assert.Contains("StubClient", thrown.Status.Detail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Makes a call and returns the headers it carried.
    /// </summary>
    /// <param name="configureShared">How tokens are presented.</param>
    /// <param name="configuration">Configuration to bind the shared settings from.</param>
    /// <returns>The headers the transport was given.</returns>
    private static async Task<HttpRequestHeaders> Called(
        Action<GrpcCredentialsOptions>? configureShared = null,
        IConfiguration? configuration = null)
    {
        var handler = new CapturingHandler();

        await Assert.ThrowsAnyAsync<Exception>(
            async () => await Client(new StubAuthenticator(), configureShared, configuration, handler).Call());

        Assert.NotNull(handler.Sent);

        return handler.Sent;
    }

    /// <summary>
    /// Makes a call with an authenticator that fails, and returns what the call failed with.
    /// </summary>
    /// <param name="failure">How the authenticator fails.</param>
    /// <returns>The exception the call fails with.</returns>
    private static async Task<RpcException> Failing(ClientAuthenticationException failure)
    {
        return await Assert.ThrowsAsync<RpcException>(
            async () => await Client(new StubAuthenticator(failure)).Call());
    }

    /// <summary>
    /// Builds a client that presents credentials, pointed at an address nothing is listening on.
    /// </summary>
    /// <param name="authenticator">The authenticator the credentials use.</param>
    /// <param name="configureShared">How tokens are presented.</param>
    /// <param name="configuration">Configuration to bind the shared settings from.</param>
    /// <param name="handler">The transport to use, or null for the real one.</param>
    /// <returns>The client.</returns>
    private static StubClient Client(
        IClientAuthenticator authenticator,
        Action<GrpcCredentialsOptions>? configureShared = null,
        IConfiguration? configuration = null,
        HttpMessageHandler? handler = null)
    {
        var services = new ServiceCollection();

        services.AddSingleton(authenticator);
        services.AddGrpcClientsCore(settings => settings.AllowInsecure = true);

        if (configuration is not null)
        {
            services.ConfigureGrpcCallCredentials(configuration);
        }

        if (configureShared is not null)
        {
            services.ConfigureGrpcCallCredentials(configureShared);
        }

        var builder = services
            .AddGrpcClient<StubClient>(o => o.Address = new Uri("http://localhost:1"))
            .AllowInsecureTransportIfConfigured()
            .AddGrpcCallCredentials(credentials =>
            {
                credentials.Resource = "urn:svc:orders";
                credentials.Scopes = ["orders.read"];
            });

        if (handler is not null)
        {
            builder.ConfigurePrimaryHttpMessageHandler(() => handler);
        }

        return services
            .BuildServiceProvider(validateScopes: true)
            .GetRequiredService<IGrpcClientProvider>()
            .Create<StubClient>();
    }
}
