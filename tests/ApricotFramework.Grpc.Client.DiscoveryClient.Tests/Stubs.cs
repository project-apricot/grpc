using ApricotFramework.DiscoveryClient;
using Grpc.Core;

namespace ApricotFramework.Grpc.Client.DiscoveryClient.Tests;

/// <summary>
/// A catalogue that knows one answer.
/// </summary>
/// <param name="grpcUrl">What it answers for a gRPC endpoint, or null for a service it does not know.</param>
internal sealed class StubDiscoveryClient(string? grpcUrl) : IDiscoveryClient
{
    /// <inheritdoc />
    public ValueTask<string?> GetApiBase(string service) => ValueTask.FromResult<string?>(null);

    /// <inheritdoc />
    public ValueTask<string?> GetApiUrl(string service, string api) => ValueTask.FromResult<string?>(null);

    /// <inheritdoc />
    public ValueTask<string?> GetGrpcUrl(string service) => ValueTask.FromResult(grpcUrl);

    /// <inheritdoc />
    public ValueTask<string?> GetBaseUrl(string service, string role) => ValueTask.FromResult<string?>(null);

    /// <inheritdoc />
    public ValueTask<string?> GetUrl(string service, string role, string path) => ValueTask.FromResult<string?>(null);
}

/// <summary>
/// A generated client stands in as this: the factory only needs the invoker constructor.
/// </summary>
/// <param name="invoker">The invoker the factory builds.</param>
internal sealed class StubClient(CallInvoker invoker)
{
    /// <summary>
    /// Gets the invoker, so the parameter is used.
    /// </summary>
    internal CallInvoker Invoker => invoker;
}
