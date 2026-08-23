using ApricotFramework.ErrorDefinitions;
using ApricotFramework.Grpc.ErrorDefinitions.Interceptors;
using Grpc.Core;

namespace ApricotFramework.Grpc.ErrorDefinitions.Tests;

/// <summary>
/// Covers every call shape the interceptor claims to translate, including the streaming ones where the
/// failure surfaces long after the call was made.
/// </summary>
public class ClientErrorInterceptorTests
{
    /// <summary>
    /// The interceptor under test.
    /// </summary>
    private readonly ClientErrorInterceptor interceptor = new();

    [Fact]
    public void BlockingUnaryCall_CalleeFailed_ThrowsTheErrorsItSent()
    {
        var thrown = Assert.Throws<ErrorDefinitionException>(() =>
            this.interceptor.BlockingUnaryCall("request", Calls.Context(), (_, _) => throw NotFound()));

        Assert.Equal(ErrorKinds.NotFound, thrown.FirstError().Kind);
        Assert.Equal("ORDER_NOT_FOUND", thrown.FirstError().Code);
        Assert.Equal("no such order", thrown.Message);
        Assert.IsType<RpcException>(thrown.InnerException);
    }

    [Fact]
    public void BlockingUnaryCall_Succeeded_AnswersTheResponse()
    {
        Assert.Equal("response", this.interceptor.BlockingUnaryCall("request", Calls.Context(), (_, _) => "response"));
    }

    [Fact]
    public async Task AsyncUnaryCall_CalleeFailed_ThrowsTheErrorsItSent()
    {
        var call = this.interceptor.AsyncUnaryCall(
            "request",
            Calls.Context(),
            (_, _) => Calls.Unary(Task.FromException<string>(NotFound())));

        var thrown = await Assert.ThrowsAsync<ErrorDefinitionException>(async () => await call);

        Assert.Equal("ORDER_NOT_FOUND", thrown.FirstError().Code);
    }

    [Fact]
    public async Task AsyncUnaryCall_Succeeded_AnswersTheResponse()
    {
        var call = this.interceptor.AsyncUnaryCall(
            "request",
            Calls.Context(),
            (_, _) => Calls.Unary(Task.FromResult("response")));

        Assert.Equal("response", await call);
        Assert.Empty(await call.ResponseHeadersAsync);
    }

    [Fact]
    public async Task AsyncUnaryCall_HeadersFailed_ThrowsTheErrorsItSent()
    {
        var call = this.interceptor.AsyncUnaryCall(
            "request",
            Calls.Context(),
            (_, _) => Calls.Unary(Task.FromResult("response"), Task.FromException<Metadata>(NotFound())));

        await Assert.ThrowsAsync<ErrorDefinitionException>(() => call.ResponseHeadersAsync);
    }

    [Fact]
    public async Task AsyncServerStreamingCall_StreamFailed_ThrowsTheErrorsItSent()
    {
        var call = this.interceptor.AsyncServerStreamingCall(
            "request",
            Calls.Context(),
            (_, _) => new AsyncServerStreamingCall<string>(
                new Calls.FailingReader(NotFound()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => [],
                () => { }));

        var thrown = await Assert.ThrowsAsync<ErrorDefinitionException>(
            () => call.ResponseStream.MoveNext(TestContext.Current.CancellationToken));

        Assert.Equal("ORDER_NOT_FOUND", thrown.FirstError().Code);
    }

    [Fact]
    public async Task AsyncClientStreamingCall_WriteFailed_ThrowsTheErrorsItSent()
    {
        var call = this.interceptor.AsyncClientStreamingCall(
            Calls.Context(),
            _ => new AsyncClientStreamingCall<string, string>(
                new Calls.FailingWriter(NotFound()),
                Task.FromResult("response"),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => [],
                () => { }));

        // Both overloads: the one taking a token is a default interface method that refuses cancellation
        // unless the implementation forwards it, so a wrapper that ignores it silently removes support.
#pragma warning disable xUnit1051 // Testing the overload that takes no token is the point.
        await Assert.ThrowsAsync<ErrorDefinitionException>(() => call.RequestStream.WriteAsync("request"));
#pragma warning restore xUnit1051
        await Assert.ThrowsAsync<ErrorDefinitionException>(
            () => call.RequestStream.WriteAsync("request", TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<ErrorDefinitionException>(() => call.RequestStream.CompleteAsync());
    }

    [Fact]
    public async Task AsyncDuplexStreamingCall_ReadFailed_ThrowsTheErrorsItSent()
    {
        var call = this.interceptor.AsyncDuplexStreamingCall(
            Calls.Context(),
            _ => new AsyncDuplexStreamingCall<string, string>(
                new Calls.FailingWriter(NotFound()),
                new Calls.FailingReader(NotFound()),
                Task.FromResult(new Metadata()),
                () => Status.DefaultSuccess,
                () => [],
                () => { }));

        await Assert.ThrowsAsync<ErrorDefinitionException>(
            () => call.ResponseStream.MoveNext(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void BlockingUnaryCall_FailureFromAPeerThatSentNoErrors_IsStillClassified()
    {
        var thrown = Assert.Throws<ErrorDefinitionException>(() =>
            this.interceptor.BlockingUnaryCall(
                "request",
                Calls.Context(),
                (_, _) => throw new RpcException(new Status(StatusCode.Unavailable, "no route"))));

        Assert.Equal(ErrorKinds.Unavailable, thrown.FirstError().Kind);
        Assert.Equal("no route", thrown.Message);
    }

    /// <summary>
    /// The failure a callee running this library would send.
    /// </summary>
    /// <returns>The exception a call would throw.</returns>
    private static RpcException NotFound()
    {
        return GrpcErrorStatus.ToRpcException([Err.NotFound("ORDER_NOT_FOUND", "no such order")]);
    }
}
