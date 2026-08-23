using ApricotFramework.DiscoveryClient;
using ApricotFramework.Grpc.Client.DiscoveryClient.Extensions;
using ApricotFramework.Grpc.Client.Extensions;
using ApricotFramework.Grpc.Client.Options;
using Grpc.Net.Client;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.DiscoveryClient.Tests;

/// <summary>
/// Covers what the catalogue is asked, and what happens when it cannot answer.
/// </summary>
public class AddDiscoveredGrpcClientTests
{
    [Fact]
    public void AddDiscoveredGrpcClient_KnownService_UsesTheDiscoveredAddress()
    {
        Assert.Equal(new Uri("http://localhost:5001"), Options("http://localhost:5001").Address);
    }

    [Fact]
    public void AddDiscoveredGrpcClient_UnknownService_SaysWhichServiceAndWhy()
    {
        var thrown = Assert.Throws<InvalidOperationException>(() => Options(null));

        Assert.Contains("orders", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("gRPC endpoint", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddDiscoveredGrpcClient_AddressThatIsNotAnHttpUrl_SaysSo()
    {
        // Parses as an absolute URI with scheme "orders", which the channel would reject far from here.
        var thrown = Assert.Throws<InvalidOperationException>(() => Options("orders:5001"));

        Assert.Contains("http or https URL", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddDiscoveredGrpcClient_NullService_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ServiceCollection().AddDiscoveredGrpcClient<StubClient>(null!));
    }

    [Fact]
    public void AddDiscoveredGrpcClient_InsecureNotAllowed_KeepsCredentialsOffAPlaintextChannel()
    {
        Assert.False(Channel("http://localhost:5001").UnsafeUseInsecureChannelCallCredentials);
    }

    [Fact]
    public void AddDiscoveredGrpcClient_InsecureAllowed_LetsCredentialsOntoAPlaintextChannel()
    {
        // The helper asks for insecure transport on the caller's behalf; the setting decides.
        var channel = Channel("http://localhost:5001", settings => settings.AllowInsecure = true);

        Assert.True(channel.UnsafeUseInsecureChannelCallCredentials);
    }

    /// <summary>
    /// Registers a discovered client and reads back the options the factory would use.
    /// </summary>
    /// <param name="grpcUrl">What the catalogue answers.</param>
    /// <param name="configure">The shared settings.</param>
    /// <returns>The options for the registered client.</returns>
    private static GrpcClientFactoryOptions Options(string? grpcUrl, Action<GrpcClientsOptions>? configure = null)
    {
        using var provider = Registered(grpcUrl, configure);

        return provider.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>().Get(nameof(StubClient));
    }

    /// <summary>
    /// Applies the registered channel configuration.
    /// </summary>
    /// <param name="grpcUrl">What the catalogue answers.</param>
    /// <param name="configure">The shared settings.</param>
    /// <returns>The channel the factory would build.</returns>
    private static GrpcChannelOptions Channel(string? grpcUrl, Action<GrpcClientsOptions>? configure = null)
    {
        // The channel actions close over the provider, so it has to outlive them.
        using var provider = Registered(grpcUrl, configure);

        var options = provider.GetRequiredService<IOptionsMonitor<GrpcClientFactoryOptions>>().Get(nameof(StubClient));
        var channel = new GrpcChannelOptions();

        foreach (var configureChannel in options.ChannelOptionsActions)
        {
            configureChannel(channel);
        }

        return channel;
    }

    /// <summary>
    /// Builds a container with a discovered client registered in it.
    /// </summary>
    /// <param name="grpcUrl">What the catalogue answers.</param>
    /// <param name="configure">The shared settings.</param>
    /// <returns>The container.</returns>
    private static ServiceProvider Registered(string? grpcUrl, Action<GrpcClientsOptions>? configure)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IDiscoveryClient>(new StubDiscoveryClient(grpcUrl));
        services.AddGrpcClientsCore(configure ?? (_ => { }));
        services.AddDiscoveredGrpcClient<StubClient>("orders");

        return services.BuildServiceProvider(validateScopes: true);
    }
}
