// CTL-POPIA-009, CTL-POPIA-015, CTL-BCEA-004: Nightly data archival background service.
// Identifies terminated employee records past the 5-year retention period and marks them as archived.
// Runs at 3:00 AM SAST (UTC+2) daily via PeriodicTimer.

using System.Collections.Immutable;
using System.Globalization;
using ZenoHR.Module.Compliance.Services.DataArchival;

namespace ZenoHR.Api.BackgroundServices;

/// <summary>
/// Background service that runs nightly at 3:00 AM South Africa Standard Time to identify
/// terminated employee records that have exceeded the 5-year retention period
/// (POPIA §14 + BCEA §31) and marks them as archived (soft archive — no deletion).
/// </summary>
public sealed partial class DataArchivalService : BackgroundService
{
    private static readonly TimeZoneInfo SastTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");

    private const int TargetHourSast = 3;

    private readonly ILogger<DataArchivalService> _logger;
    private DateOnly _lastRunDate = DateOnly.MinValue;

    public DataArchivalService(ILogger<DataArchivalService> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogServiceStarted(_logger, TargetHourSast, ArchivalPolicy.RetentionYears);

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
                LogArchivalStarted(_logger, dateString);

                // CTL-POPIA-015: Identify terminated employees past retention period.
                // TODO: Query Firestore for employees where employment_status == "Terminated"
                // and ArchivalPolicy.IsEligibleForArchival(termination_date, todaySast) == true.
                // TODO: For each eligible employee, check for legal hold (skip if held).
                // TODO: Mark eligible employee records as archived (soft archive — status change only).
                // TODO: Create AuditEvent for each archival action (hash-chained).

                var totalEligible = 0;
                var totalArchived = 0;
                var totalSkipped = 0;

                var report = new ArchivalReport(
                    TenantId: "system", // TODO: Iterate per tenant when multi-tenancy query is wired.
                    RunDate: todaySast,
                    TotalEligible: totalEligible,
                    TotalArchived: totalArchived,
                    TotalSkipped: totalSkipped,
                    Entries: ImmutableList<ArchivalRecord>.Empty);

                LogArchivalCompleted(_logger, dateString, report.TotalEligible, report.TotalArchived, report.TotalSkipped);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogArchivalFailed(_logger, ex, dateString);
            }
        }
    }

    // ── LoggerMessage source-generated methods ──────────────────────────

    [LoggerMessage(EventId = 3300, Level = LogLevel.Information,
        Message = "DataArchivalService started. Target hour: {TargetHour}:00 SAST. Retention period: {RetentionYears} years.")]
    private static partial void LogServiceStarted(ILogger logger, int targetHour, int retentionYears);

    [LoggerMessage(EventId = 3301, Level = LogLevel.Information,
        Message = "Data archival scan started for {Date}.")]
    private static partial void LogArchivalStarted(ILogger logger, string date);

    [LoggerMessage(EventId = 3302, Level = LogLevel.Information,
        Message = "Data archival completed for {Date}. Eligible: {TotalEligible}, Archived: {TotalArchived}, Skipped: {TotalSkipped}.")]
    private static partial void LogArchivalCompleted(ILogger logger, string date, int totalEligible, int totalArchived, int totalSkipped);

    [LoggerMessage(EventId = 3303, Level = LogLevel.Error,
        Message = "Data archival failed for {Date}.")]
    private static partial void LogArchivalFailed(ILogger logger, Exception ex, string date);
}
