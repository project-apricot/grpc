using ApricotFramework.ErrorDefinitions;
using ApricotFramework.Grpc.Client.Extensions;
using Grpc.Core;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Tests;

/// <summary>
/// Covers that translating errors is something a host asks for, and that asking for it works whether
/// the client was registered by this library or by gRPC's own API.
/// </summary>
public class GrpcErrorMappingTests
{
    [Fact]
    public async Task AddGrpcClient_WithoutErrorMapping_FailsAsATransportError()
    {
        // The client package has no opinion about errors until the host says so.
        await Assert.ThrowsAsync<RpcException>(async () => await Client(map: Mapping.None).Call());
    }

    [Fact]
    public async Task AddGrpcErrorMapping_OnTheContainer_TranslatesAClientGrpcRegisteredItself()
    {
        var thrown = await Assert.ThrowsAsync<ErrorDefinitionException>(
            async () => await Client(map: Mapping.Container).Call());

        Assert.Equal(ErrorKinds.Unavailable, thrown.FirstError().Kind);
    }

    [Fact]
    public async Task AddGrpcErrorMapping_OnOneBuilder_TranslatesThatClient()
    {
        var thrown = await Assert.ThrowsAsync<ErrorDefinitionException>(
            async () => await Client(map: Mapping.Builder).Call());

        Assert.Equal(ErrorKinds.Unavailable, thrown.FirstError().Kind);
    }

    [Fact]
    public async Task AddGrpcErrorMapping_BothWays_TranslatesOnce()
    {
        // The outer interceptor sees an ErrorDefinitionException, not an RpcException, so it passes.
        var thrown = await Assert.ThrowsAsync<ErrorDefinitionException>(
            async () => await Client(map: Mapping.Both).Call());

        Assert.IsType<RpcException>(thrown.InnerException);
    }

    [Fact]
    public void AddGrpcErrorMapping_CalledTwice_RegistersOneConfigurator()
    {
        var services = new ServiceCollection();
        services.AddGrpcClientsCore(_ => { });
        services.AddGrpcErrorMapping();
        services.AddGrpcErrorMapping();

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var configurators = provider
            .GetServices<IConfigureOptions<GrpcClientFactoryOptions>>()
            .Count(c => c.GetType().Name.Contains("ErrorMapping", StringComparison.Ordinal));

        Assert.Equal(1, configurators);
    }

    [Fact]
    public void AddGrpcClients_Always_GivesEveryClientTheDeadlineInterceptor()
    {
        var services = new ServiceCollection();
        services.AddGrpcClientsCore(settings => settings.DefaultDeadline = TimeSpan.FromSeconds(5));
        services.AddGrpcClient<StubClient>(o => o.Address = new Uri("http://localhost:1"));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var options = provider
            .GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>()
            .Get(nameof(StubClient));

        Assert.Single(options.InterceptorRegistrations);
    }

    [Fact]
    public void AddGrpcClients_CalledTwice_StillGivesEveryClientOneDeadlineInterceptor()
    {
        // A ConfigureAll lambda would attach one per call; a configurator type deduplicates.
        var services = new ServiceCollection();
        services.AddGrpcClientsCore(_ => { });
        services.AddGrpcClientsCore(_ => { });
        services.AddGrpcClient<StubClient>(o => o.Address = new Uri("http://localhost:1"));

        using var provider = services.BuildServiceProvider(validateScopes: true);
        var options = provider.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>().Get(nameof(StubClient));

        Assert.Single(options.InterceptorRegistrations);
    }

    /// <summary>
    /// Which way error mapping was asked for.
    /// </summary>
    private enum Mapping
    {
        /// <summary>Not asked for at all.</summary>
        None,

        /// <summary>Asked for on the container, covering every client.</summary>
        Container,

        /// <summary>Asked for on one client builder.</summary>
        Builder,

        /// <summary>Asked for both ways.</summary>
        Both,
    }

    /// <summary>
    /// Builds a client pointed at an address nothing is listening on, so every call fails.
    /// </summary>
    /// <param name="map">Which way to ask for error mapping.</param>
    /// <returns>The client.</returns>
    private static StubClient Client(Mapping map)
    {
        var services = new ServiceCollection();
        services.AddGrpcClientsCore(_ => { });

        if (map is Mapping.Container or Mapping.Both)
        {
            services.AddGrpcErrorMapping();
        }

        var builder = services.AddGrpcClient<StubClient>(o => o.Address = new Uri("http://localhost:1"));

        if (map is Mapping.Builder or Mapping.Both)
        {
            builder.AddGrpcErrorMapping();
        }

        return services
            .BuildServiceProvider(validateScopes: true)
            .GetRequiredService<IGrpcClientProvider>()
            .Create<StubClient>();
    }
}
