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

    /// <summary>
    /// Gets a client registered under a name of its own.
    /// </summary>
    /// <typeparam name="TClient">The generated client to get.</typeparam>
    /// <param name="name">The name it was registered under.</param>
    /// <returns>The client.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client was registered under that name.</exception>
    /// <remarks>
    /// For one client type reaching several services, each registered under its own name.
    /// </remarks>
    TClient Create<TClient>(string name) where TClient : class;
}
