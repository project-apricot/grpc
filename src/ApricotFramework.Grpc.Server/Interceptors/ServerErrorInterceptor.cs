using ApricotFramework.ErrorDefinitions;
using ApricotFramework.ErrorDefinitions.AspNetCore;
using ApricotFramework.Grpc.ErrorDefinitions;
using ApricotFramework.Grpc.Server.Impl;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Server.Interceptors;

/// <summary>
/// Answers a failed call with the classified errors that describe it.
/// </summary>
/// <remarks>
/// The exception is put to the same <see cref="IExceptionErrorMapper"/> registry that answers this
/// service's HTTP requests, so a failure means the same thing whichever way a caller reached it and a
/// service teaches the pipeline about its exceptions once.
/// <para>
/// An exception no mapper recognizes is reported as <see cref="ErrorKinds.Internal"/> with nothing of the
/// exception in it — its text routinely holds credentials and SQL — and logged instead.
/// </para>
/// </remarks>
public sealed class ServerErrorInterceptor : Interceptor
{
    /// <summary>
    /// What an exception no mapper recognized is reported as.
    /// </summary>
    private static readonly IReadOnlyList<ErrorDefinition> UnrecognisedErrors = [Err.Internal()];

    /// <summary>
    /// The mappers to consult, in registration order.
    /// </summary>
    private readonly IReadOnlyList<IExceptionErrorMapper> mappers;

    /// <summary>
    /// How much room the errors may take on the wire.
    /// </summary>
    private readonly IOptionsMonitor<GrpcErrorOptions> options;

    /// <summary>
    /// Where failures are recorded, since the response deliberately says little about them.
    /// </summary>
    private readonly ILogger<ServerErrorInterceptor> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerErrorInterceptor"/> class.
    /// </summary>
    /// <param name="mappers">The mappers to consult, in registration order.</param>
    /// <param name="options">The size budget to apply.</param>
    /// <param name="logger">Where to record failures.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public ServerErrorInterceptor(
        IEnumerable<IExceptionErrorMapper> mappers,
        IOptionsMonitor<GrpcErrorOptions> options,
        ILogger<ServerErrorInterceptor> logger)
    {
        ArgumentNullException.ThrowIfNull(mappers);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.mappers = [.. mappers];
        this.options = options;
        this.logger = logger;
    }

    /// <inheritdoc />
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        try
        {
            return await continuation(request, context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            throw this.Report(context, exception);
        }
    }

    /// <inheritdoc />
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        try
        {
            return await continuation(requestStream, context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            throw this.Report(context, exception);
        }
    }

    /// <inheritdoc />
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        try
        {
            await continuation(request, responseStream, context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            throw this.Report(context, exception);
        }
    }

    /// <inheritdoc />
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        try
        {
            await continuation(requestStream, responseStream, context).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not RpcException)
        {
            throw this.Report(context, exception);
        }
    }

    /// <summary>
    /// Turns a failure into the answer the caller receives, and records what the answer leaves out.
    /// </summary>
    /// <param name="context">The call that failed.</param>
    /// <param name="exception">The exception it failed with.</param>
    /// <returns>The exception to throw in its place.</returns>
    private RpcException Report(ServerCallContext context, Exception exception)
    {
        var errors = this.Map(context, exception);

        if (errors is null)
        {
            // The only record that will exist: nothing of the exception reaches the caller.
            GrpcServerLog.Unrecognised(this.logger, context.Method, exception);

            errors = UnrecognisedErrors;
        }
        else
        {
            GrpcServerLog.Reported(this.logger, context.Method, errors[0].Kind, errors[0].Code, exception);
        }

        return GrpcErrorStatus.ToRpcException(errors, this.options.CurrentValue);
    }

    /// <summary>
    /// Asks each mapper in turn what the exception was.
    /// </summary>
    /// <param name="context">The call that failed.</param>
    /// <param name="exception">The exception to map.</param>
    /// <returns>The first non-empty answer, or null when no mapper recognised the exception.</returns>
    private IReadOnlyList<ErrorDefinition>? Map(ServerCallContext context, Exception exception)
    {
        var httpContext = context.GetHttpContext();

        foreach (var mapper in this.mappers)
        {
            var mapped = mapper.Map(httpContext, exception);

            // An empty answer would report a failure with no errors in it, so it counts as unrecognised.
            if (mapped is { Count: > 0 })
            {
                return mapped;
            }
        }

        return null;
    }
}
