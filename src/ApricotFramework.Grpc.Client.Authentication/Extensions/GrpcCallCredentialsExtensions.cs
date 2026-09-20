using ApricotFramework.Authentication;
using ApricotFramework.Authentication.ClientCredentials;
using ApricotFramework.Authentication.TokenExchange;
using ApricotFramework.Grpc.Client.Authentication.Options;
using ApricotFramework.Grpc.Client.Authentication.Validation;
using ApricotFramework.Grpc.Client.Extensions;
using Grpc.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Authentication.Extensions;

/// <summary>
/// Puts an access token on the calls this service makes.
/// </summary>
/// <remarks>
/// <para>
/// Two verbs, deliberately. <c>Configure</c> on the services sets how a token is presented and is only
/// needed to change it; <c>Add</c> on a client builder is what actually presents one.
/// </para>
/// <para>
/// Which <em>whose</em> token it is, is the choice between the <c>Add</c> methods, and it is made per
/// client because it is a property of the callee: some mainstreams want this service, some want the
/// person this service is serving, and one host may talk to both.
/// </para>
/// </remarks>
public static class GrpcCallCredentialsExtensions
{
    /// <summary>
    /// Reads how tokens are presented from the <c>GrpcClients</c> section.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the section.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <remarks>
    /// Only needed to change the header from configuration; attaching credentials to a client does not
    /// require it.
    /// </remarks>
    public static IServiceCollection ConfigureGrpcCallCredentials(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services.ConfigureGrpcCallCredentials(configuration, GrpcClientServiceCollectionExtensions.ConfigurationSectionName);
    }

    /// <summary>
    /// Reads how tokens are presented from a section of your choosing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration to read from.</param>
    /// <param name="sectionName">The section holding the settings.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    /// <remarks>
    /// Name the same section the client settings were bound from; the two share it.
    /// </remarks>
    public static IServiceCollection ConfigureGrpcCallCredentials(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(sectionName);

        services.AddOptions<GrpcCredentialsOptions>().Bind(configuration.GetSection(sectionName));

        return RegisterCredentials(services);
    }

    /// <summary>
    /// Sets how tokens are presented, in code.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures the shared credential settings.</param>
    /// <returns>The service collection, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public static IServiceCollection ConfigureGrpcCallCredentials(this IServiceCollection services, Action<GrpcCredentialsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        services.AddOptions<GrpcCredentialsOptions>().Configure(configure);

        return RegisterCredentials(services);
    }

    /// <summary>
    /// Presents this service's own access token on every call this client makes.
    /// </summary>
    /// <param name="builder">The client builder.</param>
    /// <param name="configure">What this client asks for where it differs from the service default.</param>
    /// <returns>The client builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// The default, and the right one wherever the callee is answering this service rather than a
    /// person: a background job, a cache warm, anything with nobody waiting on it.
    /// </remarks>
    public static IHttpClientBuilder AddGrpcCallCredentials(this IHttpClientBuilder builder, Action<CallCredentialsOptions>? configure = null)
    {
        return builder.AddGrpcCallCredentials<IClientCredentialsAuthenticator>(configure);
    }

    /// <summary>
    /// Presents a token obtained on behalf of whoever this service is serving, on every call this
    /// client makes.
    /// </summary>
    /// <param name="builder">The client builder.</param>
    /// <param name="configure">What this client asks for where it differs from the service default.</param>
    /// <returns>The client builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// <para>
    /// For a callee that should apply the caller's authority rather than this service's — so that a
    /// gateway or an agent surface can reach no more than the person behind it could. Requires
    /// <c>AddTokenExchangeAuthentication</c>, which is what registers the authenticator this resolves.
    /// </para>
    /// <para>
    /// A call with nobody to act for fails rather than going out as this service. That is the point:
    /// the alternative is a background job quietly reaching what only a signed-in person should.
    /// </para>
    /// </remarks>
    public static IHttpClientBuilder AddGrpcExchangedCallCredentials(this IHttpClientBuilder builder, Action<CallCredentialsOptions>? configure = null)
    {
        return builder.AddGrpcCallCredentials<ITokenExchangeAuthenticator>(configure);
    }

    /// <summary>
    /// Presents a token from the named authenticator on every call this client makes.
    /// </summary>
    /// <typeparam name="TAuthenticator">The registration to get the token from.</typeparam>
    /// <param name="builder">The client builder.</param>
    /// <param name="configure">What this client asks for where it differs from the service default.</param>
    /// <returns>The client builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is null.</exception>
    /// <remarks>
    /// The open form for a grant this package does not name. A token this service cannot get fails
    /// the call as <c>Unavailable</c> when waiting may help and <c>Internal</c> when it will not — never
    /// <c>Unauthenticated</c>, which would blame the caller for a credential of ours. The
    /// authenticator's own message is not repeated: it names the provider and can quote what it refused.
    /// </remarks>
    public static IHttpClientBuilder AddGrpcCallCredentials<TAuthenticator>(this IHttpClientBuilder builder, Action<CallCredentialsOptions>? configure = null)
        where TAuthenticator : ITokenAuthenticator
    {
        ArgumentNullException.ThrowIfNull(builder);

        RegisterCredentials(builder.Services);

        var options = new CallCredentialsOptions();
        configure?.Invoke(options);

        var callee = builder.Name;

        return builder.AddCallCredentials((context, metadata, provider) => PresentToken<TAuthenticator>(provider, options, callee, metadata, context.CancellationToken));
    }

    /// <summary>
    /// Registers what presenting a token needs.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection, for chaining.</returns>
    private static IServiceCollection RegisterCredentials(IServiceCollection services)
    {
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<GrpcCredentialsOptions>, GrpcCredentialsOptionsValidator>());
        services.AddOptions<GrpcCredentialsOptions>().ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Puts the token on a call.
    /// </summary>
    /// <typeparam name="TAuthenticator">The registration to get the token from.</typeparam>
    /// <param name="provider">Where to get the authenticator and the settings.</param>
    /// <param name="options">What this client asks for.</param>
    /// <param name="callee">The client being called for the message if no token can be had.</param>
    /// <param name="metadata">The call's metadata.</param>
    /// <param name="cancellationToken">The token to cancel with.</param>
    /// <returns>A task that completes once the credential is on the call.</returns>
    /// <exception cref="RpcException">Thrown when no token could be obtained.</exception>
    /// <remarks>
    /// One body for every grant, because everything here — what the callee is called, how the header is
    /// spelled, how a refusal is reported — is the same question whoever the token belongs to. Only the
    /// authenticator differs, and it differs by resolution rather than by branch.
    /// </remarks>
    private static async Task PresentToken<TAuthenticator>(
        IServiceProvider provider,
        CallCredentialsOptions options,
        string callee,
        Metadata metadata,
        CancellationToken cancellationToken)
        where TAuthenticator : ITokenAuthenticator
    {
        var authenticator = provider.GetRequiredService<TAuthenticator>();

        var parameters = new TokenRequestParameters
        {
            Resources = options.Resource is null ? null : [options.Resource],
            Scopes = options.Scopes,
            Audiences = options.Audiences,
        };

        try
        {
            var token = await authenticator.AuthenticateAsync(parameters, cancellationToken).ConfigureAwait(false);
            var header = provider.GetRequiredService<IOptions<GrpcCredentialsOptions>>().Value.AuthorizationHeader;

            metadata.Add(header, $"{token.TokenType} {token.Value}");
        }
        catch (TokenRequestException failure)
        {
            var code = failure.Reason == TokenRequestFailure.Unavailable
                ? StatusCode.Unavailable
                : StatusCode.Internal;

            // Classified from the status code by the error interceptor if one is installed.
            throw new RpcException(new Status(
                code,
                $"This service could not obtain an access token to call '{callee}'."));
        }
    }
}
