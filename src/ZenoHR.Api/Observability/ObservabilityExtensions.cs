// REQ-OPS-005: Structured telemetry via OpenTelemetry SDK — traces, metrics, and logs.
// REQ-OPS-006: Azure Monitor (Application Insights) integration for production observability.
// TC-OPS-004: Health endpoint and telemetry pipeline verified in integration tests.

using Azure.Monitor.OpenTelemetry.AspNetCore;
using OpenTelemetry.Trace;

namespace ZenoHR.Api.Observability;

/// <summary>
/// Registers OpenTelemetry tracing, metrics, and Azure Monitor export for the ZenoHR API host.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Azure.Monitor.OpenTelemetry.AspNetCore</c> meta-package automatically instruments:
/// <list type="bullet">
///   <item>ASP.NET Core HTTP requests — traces + latency/error-rate metrics</item>
///   <item>Outbound HTTP via <see cref="System.Net.Http.HttpClient"/> — traces + metrics</item>
///   <item>.NET runtime metrics — GC, thread pool, memory pressure</item>
/// </list>
/// </para>
/// <para>
/// <strong>Connection string resolution order</strong> (follows Azure Monitor SDK convention):
/// <list type="number">
///   <item><c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> environment variable (set in Azure Container Apps)</item>
///   <item><c>AzureMonitor:ConnectionString</c> in appsettings / Key Vault</item>
/// </list>
/// If neither is configured, the exporter is silently disabled — telemetry is still
/// captured locally and visible in development via debug output.
/// </para>
/// </remarks>
public static class ObservabilityExtensions
{
    /// <summary>
    /// Name of the custom <see cref="System.Diagnostics.ActivitySource"/> for ZenoHR operations.
    /// Use this constant when creating spans in application code:
    /// <code>using var activity = ZenoHrActivitySource.StartActivity("CalculatePAYE");</code>
    /// </summary>
    public const string ActivitySourceName = "ZenoHR";

    /// <summary>
    /// Name of the custom OpenTelemetry meter for ZenoHR business metrics.
    /// </summary>
    public const string MeterName = "ZenoHR";

    /// <summary>
    /// Adds OpenTelemetry tracing + metrics + Azure Monitor export to the API host.
    /// Call this from <c>Program.cs</c> before <c>builder.Build()</c>.
    /// </summary>
    public static WebApplicationBuilder AddZenoHrTelemetry(this WebApplicationBuilder builder)
    {
        // JSON console formatter in non-Development environments.
        // Azure Container Apps collects stdout/stderr — JSON lines are parsed by Log Analytics.
        if (!builder.Environment.IsDevelopment())
        {
            builder.Logging.AddJsonConsole(options =>
            {
                // Single-line JSON — easier for log aggregators (Azure Monitor, Seq, Splunk)
                options.JsonWriterOptions = new System.Text.Json.JsonWriterOptions { Indented = false };
            });
        }

        // UseAzureMonitor() reads APPLICATIONINSIGHTS_CONNECTION_STRING from env vars.
        // Falls back to AzureMonitor:ConnectionString in configuration (appsettings / Key Vault).
        // If neither is set, the exporter is disabled silently — safe for development.
        builder.Services.AddOpenTelemetry()
            .UseAzureMonitor()
            .WithTracing(tracing =>
                // Register ZenoHR's custom ActivitySource so custom spans are exported.
                // Payroll calculations, audit writes, RBAC resolution will emit spans here.
                // VUL-022: LogRedactionProcessor strips PII fields before export to Azure Monitor.
                // CTL-POPIA-001: national_id, tax_reference, bank_account redacted from all spans.
                tracing
                    .AddSource(ActivitySourceName)
                    .AddProcessor(new LogRedactionProcessor()))
            .WithMetrics(metrics =>
                // Register ZenoHR's custom meter for business metrics
                // (payroll runs processed, leave requests, ETI calculations, etc.)
                metrics.AddMeter(MeterName));

        return builder;
    }
}
