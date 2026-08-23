using ApricotFramework.ErrorDefinitions;
using ApricotFramework.ErrorDefinitions.AspNetCore;
using ApricotFramework.Grpc.ErrorDefinitions;
using ApricotFramework.Grpc.Server.Interceptors;
using ApricotFramework.Grpc.Server.Extensions;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Server.Tests;

/// <summary>
/// Covers what a caller is told for each kind of failure, and what it is deliberately not told.
/// </summary>
public class ServerErrorInterceptorTests
{
    [Fact]
    public async Task UnaryServerHandler_Succeeded_AnswersTheResponse()
    {
        var answer = await Interceptor().UnaryServerHandler<string, string>(
            "request",
            new ServerCalls(),
            (_, _) => Task.FromResult("response"));

        Assert.Equal("response", answer);
    }

    [Fact]
    public async Task UnaryServerHandler_ClassifiedFailure_ReportsTheErrorsItCarried()
    {
        var thrown = await Failed(Err.NotFound("ORDER_NOT_FOUND", "no such order").AsException());

        Assert.Equal(StatusCode.NotFound, thrown.StatusCode);

        var reported = Assert.Single(GrpcErrorStatus.ToErrors(thrown));
        Assert.Equal(ErrorKinds.NotFound, reported.Kind);
        Assert.Equal("ORDER_NOT_FOUND", reported.Code);
        Assert.Equal("no such order", reported.Message);
    }

    [Fact]
    public async Task UnaryServerHandler_ExceptionAMapperKnows_ReportsWhatTheMapperSaid()
    {
        var thrown = await Failed(
            new InvalidOperationException("tenant is missing"),
            new StubMapper(typeof(InvalidOperationException), Err.PreconditionFailed("TENANT_MISSING")));

        Assert.Equal(StatusCode.FailedPrecondition, thrown.StatusCode);
        Assert.Equal("TENANT_MISSING", GrpcErrorStatus.ToErrors(thrown)[0].Code);
    }

    [Fact]
    public async Task UnaryServerHandler_UnrecognisedException_SaysNothingAboutIt()
    {
        var thrown = await Failed(new InvalidOperationException("connection string: Password=hunter2"));

        Assert.Equal(StatusCode.Internal, thrown.StatusCode);

        var reported = Assert.Single(GrpcErrorStatus.ToErrors(thrown));
        Assert.Equal(ErrorKinds.Internal, reported.Kind);
        Assert.DoesNotContain("hunter2", thrown.Status.Detail, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", reported.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnaryServerHandler_ServiceThrewAnRpcException_LeavesItAlone()
    {
        var chosen = new RpcException(new Status(StatusCode.ResourceExhausted, "slow down"));

        var thrown = await Assert.ThrowsAsync<RpcException>(() => Interceptor().UnaryServerHandler<string, string>(
            "request",
            new ServerCalls(),
            (_, _) => throw chosen));

        Assert.Same(chosen, thrown);
    }

    [Fact]
    public async Task UnaryServerHandler_CallerWentAway_IsReportedAsCancelledRatherThanAServerFault()
    {
        // The mapper that decides this comes from the HTTP side, unchanged: that is the point of reusing it.
        var httpContext = new DefaultHttpContext { RequestAborted = new CancellationToken(canceled: true) };

        var thrown = await Assert.ThrowsAsync<RpcException>(() => Interceptor().UnaryServerHandler<string, string>(
            "request",
            new ServerCalls(httpContext),
            (_, _) => throw new OperationCanceledException()));

        Assert.Equal(ErrorKinds.Cancelled, GrpcErrorStatus.ToErrors(thrown)[0].Kind);
    }

    [Fact]
    public async Task UnaryServerHandler_MapperThatAnswersNothing_LeavesTheNextMapperToAnswer()
    {
        var thrown = await Failed(
            new InvalidOperationException("nope"),
            new StubMapper(typeof(InvalidOperationException), null),
            new StubMapper(typeof(InvalidOperationException), Err.Aborted("GAVE_UP")));

        Assert.Equal("GAVE_UP", GrpcErrorStatus.ToErrors(thrown)[0].Code);
    }

    [Fact]
    public async Task UnaryServerHandler_FirstMapperThatAnswers_Wins()
    {
        var thrown = await Failed(
            new InvalidOperationException("nope"),
            new StubMapper(typeof(InvalidOperationException), Err.Aborted("FIRST")),
            new StubMapper(typeof(InvalidOperationException), Err.Aborted("SECOND")));

        Assert.Equal("FIRST", GrpcErrorStatus.ToErrors(thrown)[0].Code);
    }

    [Fact]
    public async Task ClientStreamingServerHandler_Failed_ReportsTheErrors()
    {
        var thrown = await Assert.ThrowsAsync<RpcException>(() =>
            Interceptor().ClientStreamingServerHandler<string, string>(
                new EmptyReader(),
                new ServerCalls(),
                (_, _) => throw Err.Validation("BAD").AsException()));

        Assert.Equal(StatusCode.InvalidArgument, thrown.StatusCode);
    }

    [Fact]
    public async Task ServerStreamingServerHandler_Failed_ReportsTheErrors()
    {
        var thrown = await Assert.ThrowsAsync<RpcException>(() =>
            Interceptor().ServerStreamingServerHandler<string, string>(
                "request",
                new DiscardingWriter(),
                new ServerCalls(),
                (_, _, _) => throw Err.Validation("BAD").AsException()));

        Assert.Equal(StatusCode.InvalidArgument, thrown.StatusCode);
    }

    [Fact]
    public async Task DuplexStreamingServerHandler_Failed_ReportsTheErrors()
    {
        var thrown = await Assert.ThrowsAsync<RpcException>(() =>
            Interceptor().DuplexStreamingServerHandler<string, string>(
                new EmptyReader(),
                new DiscardingWriter(),
                new ServerCalls(),
                (_, _, _) => throw Err.Validation("BAD").AsException()));

        Assert.Equal(StatusCode.InvalidArgument, thrown.StatusCode);
    }

    [Fact]
    public async Task UnaryServerHandler_ManyErrors_ReportsThemAllWithinTheBudget()
    {
        var errors = Enumerable.Range(0, 5)
            .Select(index => Err.Validation($"FIELD_{index}", $"field {index} is wrong"))
            .ToList();

        var thrown = await Failed(new ErrorDefinitionException(errors, "invalid"));

        Assert.Equal(errors.Select(error => error.Code), GrpcErrorStatus.ToErrors(thrown).Select(error => error.Code));
    }

    /// <summary>
    /// Runs a failing call and returns what the caller would receive.
    /// </summary>
    /// <param name="failure">What the service throws.</param>
    /// <param name="mappers">The mappers to consult, beyond the built-in ones.</param>
    /// <returns>The exception the caller sees.</returns>
    private static async Task<RpcException> Failed(Exception failure, params IExceptionErrorMapper[] mappers)
    {
        return await Assert.ThrowsAsync<RpcException>(() => Interceptor(mappers).UnaryServerHandler<string, string>(
            "request",
            new ServerCalls(),
            (_, _) => throw failure));
    }

    /// <summary>
    /// Builds the interceptor with the built-in mappers and any others a test adds.
    /// </summary>
    /// <param name="mappers">The extra mappers, consulted before the built-in ones.</param>
    /// <returns>The interceptor.</returns>
    private static ServerErrorInterceptor Interceptor(params IExceptionErrorMapper[] mappers)
    {
        var services = new ServiceCollection();
        services.AddGrpcErrorHandling();

        using var provider = services.BuildServiceProvider(validateScopes: true);

        return new ServerErrorInterceptor(
            [.. mappers, .. provider.GetServices<IExceptionErrorMapper>()],
            new OptionsMonitorStub(new GrpcErrorOptions()),
            NullLogger<ServerErrorInterceptor>.Instance);
    }

    /// <summary>
    /// A mapper that recognises one exception type and answers one error.
    /// </summary>
    /// <param name="recognises">The exception type it answers for.</param>
    /// <param name="answer">The error to answer with, or null to answer nothing.</param>
    private sealed class StubMapper(Type recognises, ErrorDefinition? answer) : IExceptionErrorMapper
    {
        /// <inheritdoc />
        public IReadOnlyList<ErrorDefinition>? Map(HttpContext httpContext, Exception exception)
        {
            return exception.GetType() == recognises && answer is not null ? [answer] : null;
        }
    }

    /// <summary>
    /// Options that never change.
    /// </summary>
    /// <param name="value">The options to answer with.</param>
    private sealed class OptionsMonitorStub(GrpcErrorOptions value) : IOptionsMonitor<GrpcErrorOptions>
    {
        /// <inheritdoc />
        public GrpcErrorOptions CurrentValue => value;

        /// <inheritdoc />
        public GrpcErrorOptions Get(string? name) => value;

        /// <inheritdoc />
        public IDisposable? OnChange(Action<GrpcErrorOptions, string?> listener) => null;
    }

    /// <summary>
    /// A request stream with nothing in it.
    /// </summary>
    private sealed class EmptyReader : IAsyncStreamReader<string>
    {
        /// <inheritdoc />
        public string Current => throw new InvalidOperationException("Nothing was read.");

        /// <inheritdoc />
        public Task<bool> MoveNext(CancellationToken cancellationToken) => Task.FromResult(false);
    }

    /// <summary>
    /// A response stream that keeps nothing.
    /// </summary>
    private sealed class DiscardingWriter : IServerStreamWriter<string>
    {
        /// <inheritdoc />
        public WriteOptions? WriteOptions { get; set; }

        /// <inheritdoc />
        public Task WriteAsync(string message) => Task.CompletedTask;

        /// <inheritdoc />
        public Task WriteAsync(string message, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
