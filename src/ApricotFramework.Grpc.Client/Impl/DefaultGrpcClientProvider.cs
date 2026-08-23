using Grpc.Net.ClientFactory;

namespace ApricotFramework.Grpc.Client.Impl;

/// <summary>
/// Hands out clients from the gRPC client factory.
/// </summary>
/// <param name="factory">The factory to ask.</param>
internal sealed class DefaultGrpcClientProvider(GrpcClientFactory factory) : IGrpcClientProvider
{
    /// <inheritdoc />
    public TClient Create<TClient>() where TClient : class
    {
        return factory.CreateClient<TClient>(typeof(TClient).Name);
    }
}
