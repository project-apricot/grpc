using ApricotFramework.DiscoveryClient;
using ApricotFramework.Grpc.Client.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Grpc.Client.DiscoveryClient.Extensions;

/// <summary>
/// Registers gRPC clients that find their peer in the discovery catalogue.
/// </summary>
public static class GrpcDiscoveryServiceCollectionExtensions
{
    /// <summary>
    /// Adds a client addressed by service name, resolved when its channel is created.
    /// </summary>
    /// <typeparam name="TClient">The generated client to register.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="service">The service to call, as the catalogue knows it.</param>
    /// <returns>The client builder, for adding credentials or configuring the channel further.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <remarks>
    /// The address is resolved once, when the channel is created, and the resolution blocks — a
    /// catalogue read from configuration answers without waiting, so in practice nothing is blocked,
    /// but one that reaches the network would be.
    /// </remarks>
    public static IHttpClientBuilder AddDiscoveredGrpcClient<TClient>(this IServiceCollection services, string service)
        where TClient : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(service);

        return services
            .AddGrpcClient<TClient>((provider, client) => client.Address = Resolve(provider, service))
            .AllowInsecureTransportIfConfigured();
    }

    /// <summary>
    /// Asks the catalogue where a service is.
    /// </summary>
    /// <param name="provider">Where to get the discovery client.</param>
    /// <param name="service">The service to find.</param>
    /// <returns>The address to call.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the catalogue does not know the service or gives an address that cannot be called.
    /// </exception>
    private static Uri Resolve(IServiceProvider provider, string service)
    {
        var discovery = provider.GetRequiredService<IDiscoveryClient>();
        var pending = discovery.GetGrpcUrl(service);

        // A catalogue read from configuration answers without waiting; only one that does not need this.
        var url = pending.IsCompletedSuccessfully ? pending.Result : pending.AsTask().GetAwaiter().GetResult();

        if (string.IsNullOrWhiteSpace(url))
        {
            throw new InvalidOperationException(
                $"The discovery catalogue has no gRPC endpoint for '{service}', so no client can be built for it.");
        }

        // A bare "host:port" parses as an absolute URI whose scheme is the host, so the scheme is checked
        // here rather than left to fail obscurely inside the channel.
        if (!Uri.TryCreate(url, UriKind.Absolute, out var address) || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"The discovery catalogue gave '{url}' as the gRPC endpoint of '{service}', which is not an http or https URL.");
        }

        return address;
    }
}
