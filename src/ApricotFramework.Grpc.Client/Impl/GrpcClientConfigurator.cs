using Grpc.Core.Interceptors;
using Grpc.Net.ClientFactory;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Impl;

/// <summary>
/// Attaches an interceptor to every gRPC client in the container.
/// </summary>
/// <remarks>
/// A container-wide hook rather than <c>IHttpClientBuilder.AddInterceptor</c>, because the calls that
/// use it run before any client is registered and must also reach clients registered afterward by
/// something else.
/// <para>
/// Registered with <c>TryAddEnumerable</c> on the concrete type, so adding it twice leaves one — which
/// is what makes those calls idempotent. Applies to gRPC clients only:
/// <see cref="GrpcClientFactoryOptions"/> is read for no other kind of client, so configuring every
/// name of it is safe in a way that configuring every <c>HttpClient</c> would not be.
/// </para>
/// </remarks>
internal abstract class GrpcClientConfigurator : IConfigureNamedOptions<GrpcClientFactoryOptions>
{
    /// <inheritdoc />
    public void Configure(GrpcClientFactoryOptions options) => this.Configure(name: null, options);

    /// <inheritdoc />
    public void Configure(string? name, GrpcClientFactoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.InterceptorRegistrations.Add(new InterceptorRegistration(InterceptorScope.Channel, this.Resolve));
    }

    /// <summary>
    /// Gets the interceptor to attach.
    /// </summary>
    /// <param name="provider">Where to resolve it from — the root, since the scope is the channel.</param>
    /// <returns>The interceptor.</returns>
    protected abstract Interceptor Resolve(IServiceProvider provider);
}
