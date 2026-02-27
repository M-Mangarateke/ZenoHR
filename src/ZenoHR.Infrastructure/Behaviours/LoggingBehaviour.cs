// REQ-OPS-005: LoggingBehaviour â€” structured request/response logging for all MediatR commands/queries.
// Logs request type, tenant, duration, and success/failure outcome.
// Sensitive field values are NOT logged (DataClassification enforcement).

using MediatR;
using Microsoft.Extensions.Logging;

namespace ZenoHR.Infrastructure.Behaviours;

/// <summary>
/// MediatR pipeline behaviour that logs all requests and their outcomes.
/// Runs for every IRequest&lt;TResponse&gt; passing through the MediatR pipeline.
/// </summary>
internal sealed partial class LoggingBehaviour<TRequest, TResponse>(
    ILogger<LoggingBehaviour<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        LogHandling(logger, requestName);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await next();
            sw.Stop();
            LogHandled(logger, requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            LogError(logger, ex, requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }

    [LoggerMessage(EventId = 1000, Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private static partial void LogHandling(ILogger logger, string requestName);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "Handled {RequestName} in {ElapsedMs}ms")]
    private static partial void LogHandled(ILogger logger, string requestName, long elapsedMs);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Error, Message = "Error handling {RequestName} after {ElapsedMs}ms")]
    private static partial void LogError(ILogger logger, Exception ex, string requestName, long elapsedMs);
}