using ApricotFramework.ErrorDefinitions.AspNetCore.Extensions;
using ApricotFramework.Grpc.ErrorDefinitions;
using ApricotFramework.Grpc.Server.Interceptors;
using Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ApricotFramework.Grpc.Server.Extensions;

/// <summary>
/// Adds classified error reporting to a gRPC server.
/// </summary>
/// <remarks>
/// Registering gRPC itself stays with the host — <c>AddGrpc</c>, <c>AddGrpcReflection</c> and
/// <c>MapGrpcService</c> are gRPC's own and this library has nothing to add to them. The interceptor is
/// attached through <see cref="GrpcServiceOptions"/>, so it does not matter whether <c>AddGrpc</c> is
/// called before or after this.
/// </remarks>
public static class GrpcServerServiceCollectionExtensions
{
    /// <summary>
    /// Reports a failed call as the classified errors that describe it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    public static IServiceCollection AddGrpcErrorHandling(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return RegisterErrorHandling(services);
    }

    /// <summary>
    /// Reports a failed call as the classified errors that describe it, with a size budget of your own.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures how much room the errors may take on the wire.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configure"/> is null.
    /// </exception>
    /// <remarks>
    /// Not bound from configuration: what a service answers when it fails is part of its contract, and a
    /// contract an environment variable can change is not one.
    /// </remarks>
    public static IServiceCollection AddGrpcErrorHandling(this IServiceCollection services, Action<GrpcErrorOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<GrpcErrorOptions>().Configure(configure);

        return RegisterErrorHandling(services);
    }

    /// <summary>
    /// Registers the interceptor and the mappers it consults.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    private static IServiceCollection RegisterErrorHandling(IServiceCollection services)
    {
        // Idempotent, so a host that registers error reporting itself is unaffected by this.
        services.AddErrorDefinitions();

        // Singleton: the interceptor holds nothing per call, and gRPC resolves it once per channel.
        services.TryAddSingleton<ServerErrorInterceptor>();

        services.Configure<GrpcServiceOptions>(options =>
        {
            // Configure actions accumulate, so without this a second call would intercept twice.
            if (options.Interceptors.All(interceptor => interceptor.Type != typeof(ServerErrorInterceptor)))
            {
                options.Interceptors.Add<ServerErrorInterceptor>();
            }
        });

        return services;
    }
}
