namespace ApricotFramework.Grpc.Client;

/// <summary>
/// Hands out the gRPC clients a service registered.
/// </summary>
/// <remarks>
/// One dependency to inject however many services this one calls, instead of one generated client type
/// each. Resolving the client types directly works just as well where that reads better.
/// </remarks>
public interface IGrpcClientProvider
{
    /// <summary>
    /// Gets a client.
    /// </summary>
    /// <typeparam name="TClient">The generated client to get.</typeparam>
    /// <returns>The client.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no such client was registered.</exception>
    TClient Create<TClient>() where TClient : class;
}
