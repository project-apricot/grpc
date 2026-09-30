using ApricotFramework.Grpc.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Grpc.Client.Tests;

/// <summary>
/// Covers reaching a client by its type's name and by a name of its own.
/// </summary>
public class GrpcClientProviderTests
{
    [Fact]
    public void Create_ByType_ReachesTheClientNamedAfterIt()
    {
        var services = Services();

        services.AddGrpcClient<StubClient>(o => o.Address = new Uri("http://localhost:1"));

        Assert.NotNull(Provider(services).Create<StubClient>());
    }

    [Fact]
    public void Create_ByName_ReachesEachRegistrationOfOneType()
    {
        var services = Services();

        services.AddGrpcClient<StubClient>("orders", o => o.Address = new Uri("http://localhost:1"));
        services.AddGrpcClient<StubClient>("billing", o => o.Address = new Uri("http://localhost:2"));

        var clients = Provider(services);

        Assert.NotNull(clients.Create<StubClient>("orders"));
        Assert.NotNull(clients.Create<StubClient>("billing"));
    }

    [Fact]
    public void Create_ByAName_NeverRegistered_SaysWhichName()
    {
        var services = Services();

        services.AddGrpcClient<StubClient>("orders", o => o.Address = new Uri("http://localhost:1"));

        var thrown = Assert.Throws<InvalidOperationException>(() => Provider(services).Create<StubClient>("ordres"));

        Assert.Contains("ordres", thrown.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Create_ByABlankName_Throws(string name)
    {
        var services = Services();

        services.AddGrpcClient<StubClient>("orders", o => o.Address = new Uri("http://localhost:1"));

        Assert.Throws<ArgumentException>(() => Provider(services).Create<StubClient>(name));
    }

    /// <summary>
    /// Builds a collection with the shared client services in it.
    /// </summary>
    /// <returns>The collection.</returns>
    private static ServiceCollection Services()
    {
        var services = new ServiceCollection();

        services.AddGrpcClientsCore(_ => { });

        return services;
    }

    /// <summary>
    /// Builds the container and gets the provider from it.
    /// </summary>
    /// <param name="services">The collection.</param>
    /// <returns>The provider.</returns>
    private static IGrpcClientProvider Provider(ServiceCollection services) =>
        services.BuildServiceProvider(validateScopes: true).GetRequiredService<IGrpcClientProvider>();
}
