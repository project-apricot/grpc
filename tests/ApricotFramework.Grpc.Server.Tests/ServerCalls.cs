using Grpc.Core;
using Microsoft.AspNetCore.Http;

namespace ApricotFramework.Grpc.Server.Tests;

/// <summary>
/// A call context standing in for one gRPC would build, carrying the HTTP context the mappers are given.
/// </summary>
/// <remarks>
/// The key is the one <c>ServerCallContext.GetHttpContext</c> reads, which is how the interceptor reaches
/// the request without a running server.
/// </remarks>
internal sealed class ServerCalls : ServerCallContext
{
    /// <summary>
    /// Where the extension method looks for the request.
    /// </summary>
    private const string HttpContextKey = "__HttpContext";

    /// <summary>
    /// What the extension method finds there.
    /// </summary>
    private readonly Dictionary<object, object> state;

    /// <summary>
    /// Initializes a new instance of the <see cref="ServerCalls"/> class.
    /// </summary>
    /// <param name="httpContext">The request being answered, or null to leave the state empty.</param>
    internal ServerCalls(HttpContext? httpContext = null)
    {
        this.HttpContext = httpContext ?? new DefaultHttpContext();
        this.state = new Dictionary<object, object> { [HttpContextKey] = this.HttpContext };
    }

    /// <summary>
    /// Gets the request being answered.
    /// </summary>
    internal HttpContext HttpContext { get; }

    /// <inheritdoc />
    protected override string MethodCore => "/apricot.Example/Call";

    /// <inheritdoc />
    protected override string HostCore => "localhost";

    /// <inheritdoc />
    protected override string PeerCore => "ipv4:127.0.0.1:0";

    /// <inheritdoc />
    protected override DateTime DeadlineCore => DateTime.MaxValue;

    /// <inheritdoc />
    protected override Metadata RequestHeadersCore => [];

    /// <inheritdoc />
    protected override CancellationToken CancellationTokenCore => CancellationToken.None;

    /// <inheritdoc />
    protected override Metadata ResponseTrailersCore => [];

    /// <inheritdoc />
    protected override Status StatusCore { get; set; }

    /// <inheritdoc />
    protected override WriteOptions? WriteOptionsCore { get; set; }

    /// <inheritdoc />
    protected override AuthContext AuthContextCore => new(null, new Dictionary<string, List<AuthProperty>>());

    /// <inheritdoc />
    protected override IDictionary<object, object> UserStateCore => this.state;

    /// <inheritdoc />
    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders) => Task.CompletedTask;
}
