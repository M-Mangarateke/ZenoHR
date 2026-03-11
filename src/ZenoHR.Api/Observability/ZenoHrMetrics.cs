// REQ-OPS-003: Custom business metrics for ZenoHR observability.
// REQ-OPS-005: OpenTelemetry-compatible meters exported to Azure Monitor.
// TC-OPS-004: Verified via unit tests in ZenoHR.Module.Compliance.Tests.

using System.Diagnostics.Metrics;

namespace ZenoHR.Api.Observability;

/// <summary>
/// Exposes custom OpenTelemetry-compatible business metrics for ZenoHR.
/// Instruments are created once at startup and recorded throughout the application lifetime.
/// Exported to Azure Monitor via the OpenTelemetry meter pipeline configured in
/// <see cref="ObservabilityExtensions.AddZenoHrTelemetry"/>.
/// </summary>
public sealed class ZenoHrMetrics
{
    /// <summary>
    /// The meter name registered with OpenTelemetry. Must match
    /// <see cref="ObservabilityExtensions.MeterName"/>.
    /// </summary>
    public const string MeterName = "ZenoHR";

    private readonly Meter _meter;

    /// <summary>
    /// Total number of payroll runs executed (partitioned by status tag).
    /// </summary>
    public Counter<long> PayrollRunsTotal { get; }

    /// <summary>
    /// Duration of payroll run processing in seconds.
    /// </summary>
    public Histogram<double> PayrollRunDurationSeconds { get; }

    /// <summary>
    /// Total number of compliance checks executed (partitioned by type tag).
    /// </summary>
    public Counter<long> ComplianceChecksTotal { get; }

    /// <summary>
    /// Total number of API errors (partitioned by status_code and endpoint tags).
    /// </summary>
    public Counter<long> ApiErrorsTotal { get; }

    // REQ-OPS-003: Constructor creates all instruments eagerly so they are
    // discoverable by OpenTelemetry exporters from the first scrape.
    public ZenoHrMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MeterName);

        PayrollRunsTotal = _meter.CreateCounter<long>(
            name: "zenohr.payroll_runs_total",
            unit: "{run}",
            description: "Total number of payroll runs executed");

        PayrollRunDurationSeconds = _meter.CreateHistogram<double>(
            name: "zenohr.payroll_run_duration_seconds",
            unit: "s",
            description: "Duration of payroll run processing in seconds");

        ComplianceChecksTotal = _meter.CreateCounter<long>(
            name: "zenohr.compliance_checks_total",
            unit: "{check}",
            description: "Total number of compliance checks executed");

        ApiErrorsTotal = _meter.CreateCounter<long>(
            name: "zenohr.api_errors_total",
            unit: "{error}",
            description: "Total number of API errors");
    }
}
