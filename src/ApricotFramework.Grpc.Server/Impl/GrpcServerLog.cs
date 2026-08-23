using Microsoft.Extensions.Logging;

namespace ApricotFramework.Grpc.Server.Impl;

/// <summary>
/// The log messages the interceptor writes.
/// </summary>
/// <remarks>
/// The interceptor answers every failure, so an unrecognised one is logged here at error level or
/// nowhere at all. A classified error is a service doing its job, so those are debug.
/// </remarks>
internal static partial class GrpcServerLog
{
    /// <summary>
    /// Records a failure the service classified deliberately.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="method">The method that failed.</param>
    /// <param name="kind">The kind reported.</param>
    /// <param name="code">The code reported.</param>
    /// <param name="exception">The exception behind it.</param>
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Reported {Kind}/{Code} from {Method}.")]
    public static partial void Reported(ILogger logger, string method, string kind, string code, Exception exception);

    /// <summary>
    /// Records a failure nothing recognized, which the caller is told nothing about.
    /// </summary>
    /// <param name="logger">The logger to write to.</param>
    /// <param name="method">The method that failed.</param>
    /// <param name="exception">The exception the caller is not told about.</param>
    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Error,
        Message = "An unrecognised exception in {Method} was reported to the caller as an internal error with no detail. This log is the only record of it.")]
    public static partial void Unrecognised(ILogger logger, string method, Exception exception);
}
