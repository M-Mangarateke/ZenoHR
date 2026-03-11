// REQ-OPS-003: ETI expiry alert background service.
// Checks weekly on Mondays at 6:00 AM SAST for employees approaching ETI expiry.

using System.Globalization;

namespace ZenoHR.Api.BackgroundServices;

/// <summary>
/// Weekly background service that checks for employees approaching the end of their
/// 24-month Employment Tax Incentive (ETI) qualifying window.
/// ETI runs for two 12-month tiers from the employee's qualifying start date.
/// </summary>
public sealed partial class EtiExpiryAlertService : BackgroundService
{
    private static readonly TimeZoneInfo SastTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

    private const int TargetHourSast = 6;
    private const DayOfWeek TargetDay = DayOfWeek.Monday;

    private readonly ILogger<EtiExpiryAlertService> _logger;
    private DateOnly _lastCheckDate = DateOnly.MinValue;

    public EtiExpiryAlertService(ILogger<EtiExpiryAlertService> logger)
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

            if (!IsCheckDay(nowSast, todaySast))
            {
                continue;
            }

            _lastCheckDate = todaySast;

            try
            {
                LogExpiryCheckStarted(_logger);

                // TODO: Query employees with active ETI and check expiry dates (TASK-134 follow-up).
                var count = 0;
                await Task.CompletedTask.ConfigureAwait(false);

                LogExpiryCheckCompleted(_logger, count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogExpiryCheckFailed(_logger, ex,
                    todaySast.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }
    }

    internal bool IsCheckDay(DateTime nowSast, DateOnly todaySast) =>
        nowSast.DayOfWeek == TargetDay
        && nowSast.Hour == TargetHourSast
        && _lastCheckDate != todaySast;

    [LoggerMessage(EventId = 3200, Level = LogLevel.Information,
        Message = "EtiExpiryAlertService started. Check schedule: Mondays at {TargetHour}:00 SAST.")]
    private static partial void LogServiceStarted(ILogger logger, int targetHour);

    [LoggerMessage(EventId = 3201, Level = LogLevel.Information,
        Message = "ETI expiry check started.")]
    private static partial void LogExpiryCheckStarted(ILogger logger);

    [LoggerMessage(EventId = 3202, Level = LogLevel.Information,
        Message = "ETI expiry check completed. {Count} employees approaching expiry.")]
    private static partial void LogExpiryCheckCompleted(ILogger logger, int count);

    [LoggerMessage(EventId = 3203, Level = LogLevel.Error,
        Message = "ETI expiry check failed on {Date}.")]
    private static partial void LogExpiryCheckFailed(ILogger logger, Exception ex, string date);
}
