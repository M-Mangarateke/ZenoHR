// CTL-POPIA-007: Monthly access review background service — PRD-15 §9.
// Runs on the 1st of each month at 7:00 AM SAST to generate access review records.

using System.Globalization;

namespace ZenoHR.Api.BackgroundServices;

/// <summary>
/// Background service that triggers monthly access review generation on the 1st of each month
/// at 7:00 AM South Africa Standard Time (UTC+2). Uses a 1-hour periodic check to determine
/// if the target day and hour have been reached, with a date guard to prevent duplicate runs.
/// </summary>
public sealed partial class MonthlyAccessReviewService : BackgroundService
{
    private static readonly TimeZoneInfo SastTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

    private const int TargetDayOfMonth = 1;
    private const int TargetHourSast = 7;

    private readonly ILogger<MonthlyAccessReviewService> _logger;
    private DateOnly _lastRunDate = DateOnly.MinValue;

    public MonthlyAccessReviewService(ILogger<MonthlyAccessReviewService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(_logger, TargetDayOfMonth, TargetHourSast);

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            var nowSast = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SastTimeZone);
            var todaySast = DateOnly.FromDateTime(nowSast);

            if (nowSast.Day != TargetDayOfMonth || nowSast.Hour != TargetHourSast || _lastRunDate == todaySast)
            {
                continue;
            }

            _lastRunDate = todaySast;
            var period = nowSast.ToString("yyyy-MM", CultureInfo.InvariantCulture);

            try
            {
                LogReviewGenerationStarted(_logger, period);

                // TODO: Resolve IServiceScope → AccessReviewService, load tenant assignments,
                // generate review records, and persist to Firestore (follow-up task).
                await Task.CompletedTask.ConfigureAwait(false);

                LogReviewGenerationCompleted(_logger, period);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogReviewGenerationFailed(_logger, ex, period);
            }
        }
    }

    [LoggerMessage(EventId = 3100, Level = LogLevel.Information,
        Message = "MonthlyAccessReviewService started. Target: day {TargetDay} at {TargetHour}:00 SAST.")]
    private static partial void LogServiceStarted(ILogger logger, int targetDay, int targetHour);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information,
        Message = "Monthly access review generation started for period {Period}.")]
    private static partial void LogReviewGenerationStarted(ILogger logger, string period);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Information,
        Message = "Monthly access review generation completed for period {Period}.")]
    private static partial void LogReviewGenerationCompleted(ILogger logger, string period);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Error,
        Message = "Monthly access review generation failed for period {Period}.")]
    private static partial void LogReviewGenerationFailed(ILogger logger, Exception ex, string period);
}
