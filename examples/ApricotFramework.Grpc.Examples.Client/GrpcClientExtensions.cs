using ApricotFramework.Grpc.Client.Authentication.Extensions;
using ApricotFramework.Grpc.Client.DiscoveryClient.Extensions;
using ApricotFramework.Grpc.Client.Extensions;
using ApricotFramework.Grpc.Examples.Contracts.Orders;

namespace ApricotFramework.Grpc.Examples.Client;

/// <summary>
/// This service's own composition root for gRPC clients.
/// </summary>
/// <remarks>
/// The library deliberately leaves the name <c>AddGrpcClients</c> free, exposing
/// <c>AddGrpcClientsCore</c> instead, so that a service can gather its own choices here — which
/// capabilities it wants, and every client it calls — and its <c>Program.cs</c> stays one line.
/// </remarks>
public static class GrpcClientExtensions
{
    /// <summary>
    /// Registers every gRPC client this service calls, and how they behave.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to read settings from.</param>
    /// <returns>The service collection, for chaining.</returns>
    public static IServiceCollection AddGrpcClients(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);

        // The capabilities this service wants. Drop any line and the rest still works.
        services.AddGrpcClientsCore(configuration);
        services.AddGrpcErrorMapping();

        // Nothing here binds GrpcCredentialsOptions, because this service presents its token under the
        // default Authorization header. ConfigureGrpcCallCredentials(configuration) is only for changing that.

        services
            .AddDiscoveredGrpcClient<Orders.OrdersClient>("orders")
            .AddGrpcCallCredentials(credentials =>
            {
                credentials.Resource = "urn:svc:orders";
                credentials.Scopes = ["orders.read", "orders.write"];
            });

        return services;
    }
}
