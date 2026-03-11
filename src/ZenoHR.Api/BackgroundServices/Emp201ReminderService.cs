// REQ-OPS-003: EMP201 filing deadline reminder service.
// Checks daily at 8:00 AM SAST for upcoming EMP201 filing deadlines.

using System.Globalization;

namespace ZenoHR.Api.BackgroundServices;

/// <summary>
/// Monitors EMP201 filing deadlines and logs warnings when the deadline
/// is within 5 days, or errors when the filing is overdue.
/// EMP201 is due by the 7th of each month for the previous month's PAYE/UIF/SDL.
/// </summary>
public sealed partial class Emp201ReminderService : BackgroundService
{
    private static readonly TimeZoneInfo SastTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

    private const int TargetHourSast = 8;
    private const int ReminderWindowDays = 5;

    private readonly ILogger<Emp201ReminderService> _logger;
    private DateOnly _lastCheckDate = DateOnly.MinValue;

    public Emp201ReminderService(ILogger<Emp201ReminderService> logger)
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

            if (nowSast.Hour != TargetHourSast || _lastCheckDate == todaySast)
            {
                continue;
            }

            _lastCheckDate = todaySast;

            try
            {
                CheckDeadline(todaySast);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCheckFailed(_logger, ex,
                    todaySast.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }
    }

    private void CheckDeadline(DateOnly today)
    {
        var daysUntil = Emp201DeadlineHelper.GetDaysUntilDeadline(today);
        var nextDeadline = Emp201DeadlineHelper.GetNextDeadline(today);
        var (month, year) = Emp201DeadlineHelper.GetFilingPeriod(nextDeadline);
        var periodString = Emp201DeadlineHelper.FormatFilingPeriod(month, year);

        // If today is past the 7th, the current month's filing might be overdue.
        if (today.Day > 7)
        {
            var overdueDeadline = new DateOnly(today.Year, today.Month, 7);
            var (overdueMonth, overdueYear) = Emp201DeadlineHelper.GetFilingPeriod(overdueDeadline);
            var overduePeriod = Emp201DeadlineHelper.FormatFilingPeriod(overdueMonth, overdueYear);

            LogFilingOverdue(_logger, overduePeriod);
        }

        if (daysUntil <= ReminderWindowDays)
        {
            LogFilingDueSoon(_logger, daysUntil, periodString);
        }
    }

    [LoggerMessage(EventId = 3100, Level = LogLevel.Information,
        Message = "Emp201ReminderService started. Check hour: {TargetHour}:00 SAST.")]
    private static partial void LogServiceStarted(ILogger logger, int targetHour);

    [LoggerMessage(EventId = 3101, Level = LogLevel.Error,
        Message = "EMP201 filing OVERDUE for period {Period}.")]
    private static partial void LogFilingOverdue(ILogger logger, string period);

    [LoggerMessage(EventId = 3102, Level = LogLevel.Warning,
        Message = "EMP201 filing due in {Days} days for period {Period}.")]
    private static partial void LogFilingDueSoon(ILogger logger, int days, string period);

    [LoggerMessage(EventId = 3103, Level = LogLevel.Error,
        Message = "EMP201 reminder check failed for {Date}.")]
    private static partial void LogCheckFailed(ILogger logger, Exception ex, string date);
}
