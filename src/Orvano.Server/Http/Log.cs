using Microsoft.Extensions.Logging;

namespace Orvano.Server.Http;

/// <summary>The SDK's log lines, at Debug only: method, path, status, and duration, never bodies or the key (LIB-8).</summary>
internal static partial class Log
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "{Method} {Path} answered {Status} in {DurationMs:0} ms")]
    public static partial void Call(ILogger logger, string method, string path, int status, double durationMs);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "{Method} {Path} timed out after {TimeoutMs:0} ms")]
    public static partial void TimedOut(ILogger logger, string method, string path, double timeoutMs);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "{Method} {Path} failed to connect")]
    public static partial void Failed(ILogger logger, string method, string path);
}
