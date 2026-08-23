using ApricotFramework.Grpc.Client.Options;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Interceptors;

/// <summary>
/// Gives a unary call the configured deadline when the caller set none.
/// </summary>
/// <remarks>
/// A call with no deadline waits for as long as the connection lasts, which is how one slow dependency
/// becomes an exhausted caller. A call that runs out of time fails as
/// <see cref="global::ApricotFramework.ErrorDefinitions.ErrorKinds.Timeout"/>, so it is handled like any other error.
/// <para>
/// Unary calls only: a streaming call is often meant to stay open, and a deadline would end it.
/// </para>
/// </remarks>
/// <param name="options">The settings holding the deadline, read per call so a reload takes effect.</param>
public sealed class DeadlineInterceptor(IOptionsMonitor<GrpcClientsOptions> options) : Interceptor
{
    /// <inheritdoc />
    public override TResponse BlockingUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        BlockingUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        return continuation(request, this.WithDeadline(context));
    }

    /// <inheritdoc />
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        return continuation(request, this.WithDeadline(context));
    }

    /// <summary>
    /// Adds the deadline, leaving one the caller already chose alone.
    /// </summary>
    /// <typeparam name="TRequest">What the call sends.</typeparam>
    /// <typeparam name="TResponse">What the call answers.</typeparam>
    /// <param name="context">The call to amend.</param>
    /// <returns>The call to make.</returns>
    private ClientInterceptorContext<TRequest, TResponse> WithDeadline<TRequest, TResponse>(
        ClientInterceptorContext<TRequest, TResponse> context)
        where TRequest : class
        where TResponse : class
    {
        var deadline = options.CurrentValue.DefaultDeadline;

        if (deadline is null || context.Options.Deadline is not null)
        {
            return context;
        }

        return new ClientInterceptorContext<TRequest, TResponse>(
            context.Method,
            context.Host,
            context.Options.WithDeadline(DateTime.UtcNow.Add(deadline.Value)));
    }
}
