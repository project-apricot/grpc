using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ApricotFramework.Grpc.ErrorDefinitions.Tests;

/// <summary>
/// The pieces a client interceptor needs to be called without a server.
/// </summary>
internal static class Calls
{
    /// <summary>
    /// The marshaller for the stand-in request and response type.
    /// </summary>
    private static readonly Marshaller<string> Text =
        Marshallers.Create(Encoding.UTF8.GetBytes, Encoding.UTF8.GetString);

    /// <summary>
    /// Gets the method every test calls.
    /// </summary>
    internal static Method<string, string> Method { get; } =
        new(MethodType.Unary, "apricot.Example", "Call", Text, Text);

    /// <summary>
    /// Builds the context of a call.
    /// </summary>
    /// <param name="options">The options the caller chose.</param>
    /// <returns>The context.</returns>
    internal static ClientInterceptorContext<string, string> Context(CallOptions options = default)
    {
        return new ClientInterceptorContext<string, string>(Method, null, options);
    }

    /// <summary>
    /// Builds a unary call in flight.
    /// </summary>
    /// <param name="response">What the call answers.</param>
    /// <param name="headers">What the call answers with as headers.</param>
    /// <returns>The call.</returns>
    internal static AsyncUnaryCall<string> Unary(Task<string> response, Task<Metadata>? headers = null)
    {
        return new AsyncUnaryCall<string>(
            response,
            headers ?? Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }

    /// <summary>
    /// A stream that fails when it is read.
    /// </summary>
    /// <param name="failure">What it fails with.</param>
    internal sealed class FailingReader(RpcException failure) : IAsyncStreamReader<string>
    {
        /// <inheritdoc />
        public string Current => throw failure;

        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromException<bool>(failure);
    }

    /// <summary>
    /// A stream that fails when it is written to.
    /// </summary>
    /// <param name="failure">What it fails with.</param>
    internal sealed class FailingWriter(RpcException failure) : IClientStreamWriter<string>
    {
        /// <inheritdoc />
        public WriteOptions? WriteOptions { get; set; }

        /// <inheritdoc />
        public Task CompleteAsync() => Task.FromException(failure);

        /// <inheritdoc />
        public Task WriteAsync(string message) => Task.FromException(failure);

        /// <inheritdoc />
        public Task WriteAsync(string message, CancellationToken cancellationToken) => Task.FromException(failure);
    }
}
