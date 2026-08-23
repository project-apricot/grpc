using ApricotFramework.Grpc.Client.Interceptors;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Grpc.Client.Impl;

/// <summary>
/// Gives every gRPC client the deadline the settings ask for.
/// </summary>
internal sealed class DeadlineConfigurator : GrpcClientConfigurator
{
    /// <inheritdoc />
    protected override Interceptor Resolve(IServiceProvider provider) => provider.GetRequiredService<DeadlineInterceptor>();
}
