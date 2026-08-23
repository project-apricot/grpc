using System.Net.Http.Headers;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace ApricotFramework.Grpc.Client.Tests;



/// <summary>
/// A generated client stands in as this: the factory only needs the invoker constructor.
/// </summary>
/// <param name="invoker">The invoker the factory builds.</param>
internal sealed class StubClient(CallInvoker invoker)
{
    /// <summary>
    /// Makes a call, so the channel, its credentials and its interceptors all run.
    /// </summary>
    /// <returns>The call.</returns>
    internal AsyncUnaryCall<string> Call() =>
        invoker.AsyncUnaryCall(Calls.Method, null, new CallOptions(), "request");
}

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
    /// <returns>The call.</returns>
    internal static AsyncUnaryCall<string> Unary(Task<string> response)
    {
        return new AsyncUnaryCall<string>(
            response,
            Task.FromResult(new Metadata()),
            () => Status.DefaultSuccess,
            () => [],
            () => { });
    }
}

/// <summary>
/// Answers every request with a failure, having recorded the headers the call carried.
/// </summary>
/// <remarks>
/// The call credentials write into the request headers, so this is where the token becomes visible
/// without standing up a server.
/// </remarks>
internal sealed class CapturingHandler : HttpMessageHandler
{
    /// <summary>
    /// Gets the headers of the last request, or null if none was sent.
    /// </summary>
    internal HttpRequestHeaders? Sent { get; private set; }

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        this.Sent = request.Headers;

        throw new HttpRequestException("nothing is listening");
    }
}

/// <summary>
/// Keeps every log record written through it, so a test can say what was warned about.
/// </summary>
internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    /// <summary>
    /// Gets what was written, in order.
    /// </summary>
    internal List<(LogLevel Level, string Message)> Records { get; } = [];

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName) => new Recorder(this.Records);

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    /// The logger handed to every category.
    /// </summary>
    /// <param name="records">Where to keep what is written.</param>
    private sealed class Recorder(List<(LogLevel Level, string Message)> records) : ILogger
    {
        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            records.Add((logLevel, formatter(state, exception)));
        }
    }
}
