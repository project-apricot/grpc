using ApricotFramework.ErrorDefinitions;
using Grpc.Core;
using Grpc.Core.Interceptors;

namespace ApricotFramework.Grpc.ErrorDefinitions.Interceptors;

/// <summary>
/// Reports a failed call as the classified errors the callee sent.
/// </summary>
/// <remarks>
/// A caller handles a remote failure the way it handles a local one, and the original
/// <see cref="RpcException"/> stays as the inner exception.
/// <para>
/// Every failure is translated, including one the callee never saw: a proxy's <c>unavailable</c>
/// arrives classified just the same.
/// </para>
/// </remarks>
public sealed class ClientErrorInterceptor : Interceptor
{
    /// <inheritdoc />
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        try
        {
            return continuation(request, context);
        }
        catch (RpcException exception)
        {
            throw Translate(exception);
        }
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var call = continuation(request, context);

        return new AsyncUnaryCall<TResponse>(
            Translating(call.ResponseAsync),
            Translating(call.ResponseHeadersAsync),
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    /// <inheritdoc />
    public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var call = continuation(request, context);

        return new AsyncServerStreamingCall<TResponse>(
            new TranslatingStreamReader<TResponse>(call.ResponseStream),
            Translating(call.ResponseHeadersAsync),
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    /// <inheritdoc />
    public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var call = continuation(context);

        return new AsyncClientStreamingCall<TRequest, TResponse>(
            new TranslatingStreamWriter<TRequest>(call.RequestStream),
            Translating(call.ResponseAsync),
            Translating(call.ResponseHeadersAsync),
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    /// <inheritdoc />
    public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        var call = continuation(context);

        return new AsyncDuplexStreamingCall<TRequest, TResponse>(
            new TranslatingStreamWriter<TRequest>(call.RequestStream),
            new TranslatingStreamReader<TResponse>(call.ResponseStream),
            Translating(call.ResponseHeadersAsync),
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    /// <summary>
    /// Turns a transport failure into the errors it carried.
    /// </summary>
    /// <param name="exception">The failure to translate.</param>
    /// <returns>The exception to throw in its place.</returns>
    private static ErrorDefinitionException Translate(RpcException exception)
    {
        var errors = GrpcErrorStatus.ToErrors(exception);
        var message = errors[0].Message;

        return new ErrorDefinitionException(
            errors,
            message.Length > 0 ? message : exception.Status.Detail,
            exception);
    }

    /// <summary>
    /// Awaits a call's result, translating whatever it fails with.
    /// </summary>
    /// <typeparam name="T">What the call answers.</typeparam>
    /// <param name="pending">The result to await.</param>
    /// <returns>The result.</returns>
    private static async Task<T> Translating<T>(Task<T> pending)
    {
        try
        {
            return await pending.ConfigureAwait(false);
        }
        catch (RpcException exception)
        {
            throw Translate(exception);
        }
    }

    /// <summary>
    /// Reads a response stream, translating whatever it fails with.
    /// </summary>
    /// <typeparam name="T">What the stream carries.</typeparam>
    /// <param name="inner">The stream to read.</param>
    private sealed class TranslatingStreamReader<T>(IAsyncStreamReader<T> inner) : IAsyncStreamReader<T>
    {
        /// <inheritdoc />
        public T Current => inner.Current;

        /// <inheritdoc />
        public async Task<bool> MoveNext(CancellationToken cancellationToken)
        {
            try
            {
                return await inner.MoveNext(cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException exception)
            {
                throw Translate(exception);
            }
        }
    }

    /// <summary>
    /// Writes a request stream, translating whatever it fails with.
    /// </summary>
    /// <typeparam name="T">What the stream carries.</typeparam>
    /// <param name="inner">The stream to write to.</param>
    private sealed class TranslatingStreamWriter<T>(IClientStreamWriter<T> inner) : IClientStreamWriter<T>
    {
        /// <inheritdoc />
        public WriteOptions? WriteOptions
        {
            get => inner.WriteOptions;
            set => inner.WriteOptions = value;
        }

        /// <inheritdoc />
        public async Task CompleteAsync()
        {
            try
            {
                await inner.CompleteAsync().ConfigureAwait(false);
            }
            catch (RpcException exception)
            {
                throw Translate(exception);
            }
        }

        /// <inheritdoc />
        public async Task WriteAsync(T message)
        {
            try
            {
                await inner.WriteAsync(message).ConfigureAwait(false);
            }
            catch (RpcException exception)
            {
                throw Translate(exception);
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Forwarded rather than inherited: the interface's own implementation refuses cancellation, so
        /// not overriding it would take that support away from a stream that has it.
        /// </remarks>
        public async Task WriteAsync(T message, CancellationToken cancellationToken)
        {
            try
            {
                await inner.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException exception)
            {
                throw Translate(exception);
            }
        }
    }
}
