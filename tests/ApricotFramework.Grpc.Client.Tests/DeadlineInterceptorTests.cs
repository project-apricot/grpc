using ApricotFramework.Grpc.Client.Interceptors;
using ApricotFramework.Grpc.Client.Options;
using Grpc.Core;
using Microsoft.Extensions.Options;

namespace ApricotFramework.Grpc.Client.Tests;

/// <summary>
/// Covers when the deadline is applied and, as importantly, when it is not.
/// </summary>
public class DeadlineInterceptorTests
{
    [Fact]
    public void BlockingUnaryCall_NoDeadlineConfigured_LeavesTheCallAlone()
    {
        DateTime? seen = null;

        Interceptor(null).BlockingUnaryCall("request", Calls.Context(), (_, context) =>
        {
            seen = context.Options.Deadline;

            return "response";
        });

        Assert.Null(seen);
    }

    [Fact]
    public void BlockingUnaryCall_NoDeadline_GetsOne()
    {
        var interceptor = Interceptor(TimeSpan.FromSeconds(30));
        DateTime? seen = null;

        interceptor.BlockingUnaryCall("request", Calls.Context(), (_, context) =>
        {
            seen = context.Options.Deadline;

            return "response";
        });

        Assert.NotNull(seen);
        Assert.InRange(seen.Value, DateTime.UtcNow.AddSeconds(25), DateTime.UtcNow.AddSeconds(35));
    }

    [Fact]
    public async Task AsyncUnaryCall_NoDeadline_GetsOne()
    {
        var interceptor = Interceptor(TimeSpan.FromSeconds(30));
        DateTime? seen = null;

        var call = interceptor.AsyncUnaryCall("request", Calls.Context(), (_, context) =>
        {
            seen = context.Options.Deadline;

            return Calls.Unary(Task.FromResult("response"));
        });

        Assert.Equal("response", await call);
        Assert.NotNull(seen);
    }

    [Fact]
    public void BlockingUnaryCall_CallerChoseADeadline_KeepsIt()
    {
        var interceptor = Interceptor(TimeSpan.FromSeconds(30));
        var chosen = DateTime.UtcNow.AddHours(1);
        DateTime? seen = null;

        interceptor.BlockingUnaryCall("request", Calls.Context(new CallOptions(deadline: chosen)), (_, context) =>
        {
            seen = context.Options.Deadline;

            return "response";
        });

        Assert.Equal(chosen, seen);
    }

    /// <summary>
    /// Builds the interceptor with a deadline configured, or with none.
    /// </summary>
    /// <param name="deadline">The deadline to configure.</param>
    /// <returns>The interceptor.</returns>
    private static DeadlineInterceptor Interceptor(TimeSpan? deadline)
    {
        return new DeadlineInterceptor(new StaticOptions(new GrpcClientsOptions { DefaultDeadline = deadline }));
    }

    /// <summary>
    /// Settings that never change.
    /// </summary>
    /// <param name="value">The settings to answer with.</param>
    private sealed class StaticOptions(GrpcClientsOptions value) : IOptionsMonitor<GrpcClientsOptions>
    {
        /// <inheritdoc />
        public GrpcClientsOptions CurrentValue => value;

        /// <inheritdoc />
        public GrpcClientsOptions Get(string? name) => value;

        /// <inheritdoc />
        public IDisposable? OnChange(Action<GrpcClientsOptions, string?> listener) => null;
    }
}
