using ApricotFramework.Grpc.ErrorDefinitions.Interceptors;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.DependencyInjection;

namespace ApricotFramework.Grpc.Client.Impl;

/// <summary>
/// Gives every gRPC client the error translation.
/// </summary>
internal sealed class ErrorMappingConfigurator : GrpcClientConfigurator
{
    /// <inheritdoc />
    protected override Interceptor Resolve(IServiceProvider provider) => provider.GetRequiredService<ClientErrorInterceptor>();
}
