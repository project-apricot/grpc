using ApricotFramework.Grpc.Client.Impl;
using ApricotFramework.Grpc.Client.Interceptors;
using ApricotFramework.Grpc.Client.Options;
using ApricotFramework.Grpc.Client.Validation;
using ApricotFramework.Grpc.ErrorDefinitions.Interceptors;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Extensions;

/// <summary>
/// Registers how this service's gRPC clients behave.
/// </summary>
/// <remarks>
/// Registering the clients themselves stays with the host: <c>AddGrpcClient</c> is gRPC's own and this
/// library has nothing to add to it. What is here is what happens to every call one makes.
/// </remarks>
public static class GrpcClientServiceCollectionExtensions
{
    /// <summary>
    /// The configuration section the shared settings are bound from unless another is named.
    /// </summary>
    public const string ConfigurationSectionName = "GrpcClients";

    /// <summary>
    /// Adds what every gRPC client needs, reading the shared settings from the configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the <c>GrpcClients</c> section.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public static IServiceCollection AddGrpcClientsCore(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.AddGrpcClientsCore(configuration, ConfigurationSectionName);
    }

    /// <summary>
    /// Adds what every gRPC client needs, reading the shared settings from a section of your choosing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to read from.</param>
    /// <param name="sectionName">The section holding the settings.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <remarks>
    /// A host that renames the section must name the same one to every package that reads it —
    /// the credentials package binds its own settings out of it too.
    /// </remarks>
    public static IServiceCollection AddGrpcClientsCore(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(sectionName);

        services.AddOptions<GrpcClientsOptions>().Bind(configuration.GetSection(sectionName));

        return RegisterSharedServices(services);
    }

    /// <summary>
    /// Adds what every gRPC client needs, with the shared settings written in code.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the shared settings.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public static IServiceCollection AddGrpcClientsCore(this IServiceCollection services, Action<GrpcClientsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<GrpcClientsOptions>().Configure(configure);

        return RegisterSharedServices(services);
    }

    /// <summary>
    /// Reports a failed call as the classified errors the callee sent, on every gRPC client.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is null.</exception>
    /// <remarks>
    /// Every gRPC client in the container, including ones this library did not register. Use the
    /// <see cref="AddGrpcErrorMapping(IHttpClientBuilder)">builder overload</see> to translate for one
    /// client instead.
    /// </remarks>
    public static IServiceCollection AddGrpcErrorMapping(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ClientErrorInterceptor>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<GrpcClientFactoryOptions>, ErrorMappingConfigurator>());

        return services;
    }

    /// <summary>
    /// Reports a failed call as the classified errors the callee sent, on this client.
    /// </summary>
    /// <param name="builder">The client builder.</param>
    /// <returns>The client builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// Harmless alongside the container-wide call: the second interceptor sees an
    /// <see cref="global::ApricotFramework.ErrorDefinitions.ErrorDefinitionException"/> rather than an <c>RpcException</c> and
    /// leaves it alone.
    /// </remarks>
    public static IHttpClientBuilder AddGrpcErrorMapping(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services.TryAddSingleton<ClientErrorInterceptor>();

        return builder.AddInterceptor<ClientErrorInterceptor>();
    }

    /// <summary>
    /// Lets this client talk to a peer it cannot verify, but only if the settings say so.
    /// </summary>
    /// <param name="builder">The client builder.</param>
    /// <returns>The client builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// Does nothing unless <see cref="GrpcClientsOptions.AllowInsecure"/> is on — hence the name. Calling
    /// it costs nothing in an environment that does not need it, and grepping for it does not tell you
    /// a client is insecure, only that it would be if the deployment said so.
    /// <para>
    /// Per client rather than container-wide, because relaxing certificate validation happens on the
    /// <c>HttpClient</c> handler and a gRPC client is not distinguishable from any other named client
    /// there — doing it globally would quietly stop verifying every outbound call this service makes.
    /// </para>
    /// </remarks>
    public static IHttpClientBuilder AllowInsecureTransportIfConfigured(this IHttpClientBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureChannel((provider, channel) => channel.UnsafeUseInsecureChannelCallCredentials = Settings(provider).AllowInsecure);

        builder.ConfigurePrimaryHttpMessageHandler((handler, provider) =>
        {
            if (!Settings(provider).AllowInsecure || handler is not SocketsHttpHandler sockets)
            {
                return;
            }

#pragma warning disable CA5359 // Accepting any certificate is what AllowInsecure asks for, and is warned about at startup.
            sockets.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;
#pragma warning restore CA5359
        });

        return builder;
    }

    /// <summary>
    /// Reads the settings every client shares.
    /// </summary>
    /// <param name="provider">Where to get them.</param>
    /// <returns>The settings.</returns>
    internal static GrpcClientsOptions Settings(IServiceProvider provider)
    {
        return provider.GetRequiredService<IOptions<GrpcClientsOptions>>().Value;
    }

    /// <summary>
    /// Registers the services every client shares.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    private static IServiceCollection RegisterSharedServices(IServiceCollection services)
    {
        services.AddLogging();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<GrpcClientsOptions>, GrpcClientsOptionsValidator>());
        services.AddOptions<GrpcClientsOptions>().ValidateOnStart();

        // Singleton: the interceptor holds nothing per call, and a channel-wide one is resolved from the root.
        services.TryAddSingleton<DeadlineInterceptor>();
        services.TryAddSingleton<IGrpcClientProvider, DefaultGrpcClientProvider>();

        // The deadline applies to every gRPC client; it is inert until one is configured.
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<GrpcClientFactoryOptions>, DeadlineConfigurator>());

        return services;
    }
}
