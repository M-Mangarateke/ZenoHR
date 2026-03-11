// REQ-OPS-003: Nightly analytics background service.
// Runs at 2:00 AM SAST (UTC+2) daily to compute analytics snapshots.

using System.Globalization;

namespace ZenoHR.Api.BackgroundServices;

/// <summary>
/// Computes daily analytics snapshots at 2:00 AM South Africa Standard Time.
/// Uses a 1-hour periodic check to determine if the target hour has been reached
/// and guards against duplicate runs via <see cref="_lastRunDate"/>.
/// </summary>
public sealed partial class NightlyAnalyticsService : BackgroundService
{
    private static readonly TimeZoneInfo SastTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

    private const int TargetHourSast = 2;

    private readonly ILogger<NightlyAnalyticsService> _logger;
    private DateOnly _lastRunDate = DateOnly.MinValue;

    public NightlyAnalyticsService(ILogger<NightlyAnalyticsService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(_logger, TargetHourSast);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var nowSast = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SastTimeZone);
            var todaySast = DateOnly.FromDateTime(nowSast);

            if (nowSast.Hour != TargetHourSast || _lastRunDate == todaySast)
            {
                continue;
            }

            _lastRunDate = todaySast;
            var dateString = todaySast.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

            try
            {
                LogAnalyticsStarted(_logger, dateString);

                // TODO: Invoke analytics aggregation pipeline (TASK-134 follow-up).
                await Task.CompletedTask.ConfigureAwait(false);

                LogAnalyticsCompleted(_logger, dateString);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogAnalyticsFailed(_logger, ex, dateString);
            }
        }
    }

    [LoggerMessage(EventId = 3000, Level = LogLevel.Information,
        Message = "NightlyAnalyticsService started. Target hour: {TargetHour}:00 SAST.")]
    private static partial void LogServiceStarted(ILogger logger, int targetHour);

    [LoggerMessage(EventId = 3001, Level = LogLevel.Information,
        Message = "Nightly analytics started for {Date}.")]
    private static partial void LogAnalyticsStarted(ILogger logger, string date);

    [LoggerMessage(EventId = 3002, Level = LogLevel.Information,
        Message = "Nightly analytics completed for {Date}.")]
    private static partial void LogAnalyticsCompleted(ILogger logger, string date);

    [LoggerMessage(EventId = 3003, Level = LogLevel.Error,
        Message = "Nightly analytics failed for {Date}.")]
    private static partial void LogAnalyticsFailed(ILogger logger, Exception ex, string date);
}
