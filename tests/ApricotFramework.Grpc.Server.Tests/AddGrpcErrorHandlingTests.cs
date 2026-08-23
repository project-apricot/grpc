using ApricotFramework.ErrorDefinitions.AspNetCore;
using ApricotFramework.Grpc.ErrorDefinitions;
using ApricotFramework.Grpc.Server.Extensions;
using ApricotFramework.Grpc.Server.Interceptors;
using Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Server.Tests;

/// <summary>
/// Covers what registration puts in the container, that resolving it is not a lifetime mistake, and
/// that it does not care when the host registers gRPC itself.
/// </summary>
public class AddGrpcErrorHandlingTests
{
    [Fact]
    public void AddGrpcErrorHandling_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ((IServiceCollection)null!).AddGrpcErrorHandling());
    }

    [Fact]
    public void AddGrpcErrorHandling_NullConfigure_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddGrpcErrorHandling(null!));
    }

    [Fact]
    public void AddGrpcErrorHandling_Always_ResolvesTheInterceptorFromTheRoot()
    {
        using var provider = Registered();

        // gRPC resolves a server interceptor from the root, so a scoped registration would throw here.
        Assert.NotNull(provider.GetRequiredService<ServerErrorInterceptor>());
        Assert.Same(
            provider.GetRequiredService<ServerErrorInterceptor>(),
            provider.GetRequiredService<ServerErrorInterceptor>());
    }

    [Fact]
    public void AddGrpcErrorHandling_Always_BringsTheBuiltInExceptionMappers()
    {
        using var provider = Registered();

        Assert.NotEmpty(provider.GetServices<IExceptionErrorMapper>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddGrpcErrorHandling_EitherSideOfAddGrpc_StillIntercepts(bool grpcFirst)
    {
        var services = new ServiceCollection();

        if (grpcFirst)
        {
            services.AddGrpc();
            services.AddGrpcErrorHandling();
        }
        else
        {
            services.AddGrpcErrorHandling();
            services.AddGrpc();
        }

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Contains(Interceptors(provider), registration => registration.Type == typeof(ServerErrorInterceptor));
    }

    [Fact]
    public void AddGrpcErrorHandling_WithoutAddGrpc_StillRegistersTheInterceptor()
    {
        // Registering gRPC is the host's business; ours only says what happens when a call fails.
        using var provider = Registered();

        Assert.Single(Interceptors(provider), registration => registration.Type == typeof(ServerErrorInterceptor));
    }

    [Fact]
    public void AddGrpcErrorHandling_CalledTwice_InterceptsOnce()
    {
        var services = new ServiceCollection();
        services.AddGrpcErrorHandling();
        services.AddGrpcErrorHandling();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Single(Interceptors(provider), registration => registration.Type == typeof(ServerErrorInterceptor));
    }

    [Fact]
    public void AddGrpcErrorHandling_NoBudgetGiven_UsesTheDefault()
    {
        using var provider = Registered();

        Assert.Equal(
            GrpcErrorOptions.DefaultMaxDetailsBytes,
            provider.GetRequiredService<IOptionsMonitor<GrpcErrorOptions>>().CurrentValue.MaxDetailsBytes);
    }

    [Fact]
    public void AddGrpcErrorHandling_BudgetGiven_UsesIt()
    {
        var services = new ServiceCollection();
        services.AddGrpcErrorHandling(options => options.MaxDetailsBytes = 8192);

        using var provider = services.BuildServiceProvider(validateScopes: true);

        Assert.Equal(8192, provider.GetRequiredService<IOptionsMonitor<GrpcErrorOptions>>().CurrentValue.MaxDetailsBytes);
    }

    /// <summary>
    /// Reads back the interceptors gRPC would run.
    /// </summary>
    /// <param name="provider">The container to read from.</param>
    /// <returns>The registered interceptors.</returns>
    private static InterceptorCollection Interceptors(IServiceProvider provider)
    {
        return provider.GetRequiredService<IOptions<GrpcServiceOptions>>().Value.Interceptors;
    }

    /// <summary>
    /// Registers error handling and builds a provider that fails on a lifetime mistake.
    /// </summary>
    /// <returns>The provider.</returns>
    private static ServiceProvider Registered()
    {
        var services = new ServiceCollection();
        services.AddGrpcErrorHandling();

        return services.BuildServiceProvider(validateScopes: true);
    }
}
