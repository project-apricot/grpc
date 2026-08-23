using ApricotFramework.Grpc.Server.Extensions;

namespace ApricotFramework.Grpc.Examples.Server;

/// <summary>
/// This service's own composition root for its gRPC server.
/// </summary>
/// <remarks>
/// The library ships no <c>AddGrpcServer</c>, because registering gRPC and exposing reflection are
/// gRPC's own calls and wrapping them would only hide them. Gathering the choices a service has made
/// belongs here instead — including whether its schema is reachable, which is not something a library
/// should decide.
/// </remarks>
public static class GrpcServerExtensions
{
    /// <summary>
    /// Registers gRPC the way this service wants it.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGrpcServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddGrpc();
        services.AddGrpcReflection();
        services.AddGrpcErrorHandling();

        return services;
    }
}
