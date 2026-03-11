// REQ-OPS-003: OpenTelemetry observability + health checks for ZenoHR.
// REQ-OPS-005: Structured telemetry via OpenTelemetry SDK — traces, metrics, and logs.
// REQ-OPS-006: Azure Monitor (Application Insights) integration for production observability.
// REQ-OPS-007: Health check endpoints for Azure Container Apps liveness/readiness probes.
// TC-OPS-004: Health endpoint and telemetry pipeline verified in integration tests.

using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Trace;
using ZenoHR.Api.Observability.HealthChecks;

namespace ZenoHR.Api.Observability;

/// <summary>
/// Registers OpenTelemetry tracing, metrics, Azure Monitor export, health checks,
/// and custom business metrics for the ZenoHR API host.
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
    // REQ-OPS-005, REQ-OPS-006: Telemetry pipeline with Azure Monitor exporter.
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

    /// <summary>
    /// Registers custom business metrics (<see cref="ZenoHrMetrics"/>) and health checks
    /// (liveness + Firestore readiness) for the ZenoHR API host.
    /// Call this from <c>Program.cs</c> before <c>builder.Build()</c>.
    /// </summary>
    // REQ-OPS-003: Custom metrics + health check registration.
    // REQ-OPS-007: Health checks for Azure Container Apps probes.
    public static IServiceCollection AddZenoHrObservability(
        this IServiceCollection services, IConfiguration configuration)
    {
        // REQ-OPS-003: Register custom business metrics as a singleton.
        // ZenoHrMetrics uses IMeterFactory (provided by OTel SDK) to create instruments.
        services.AddSingleton<ZenoHrMetrics>();

        // REQ-OPS-007: Health checks — liveness (default) + readiness (Firestore connectivity).
        services.AddHealthChecks()
            .AddCheck<FirestoreHealthCheck>(
                name: "firestore",
                failureStatus: Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
                tags: ["ready"]);

        return services;
    }

    /// <summary>
    /// Maps health check endpoints for Azure Container Apps probes.
    /// <list type="bullet">
    ///   <item><c>/health</c> — liveness probe (is the process alive?)</item>
    ///   <item><c>/health/ready</c> — readiness probe (can the app serve traffic? checks Firestore)</item>
    /// </list>
    /// </summary>
    // REQ-OPS-007: Liveness + readiness probes for Azure Container Apps (TASK-148).
    public static WebApplication MapZenoHrHealthChecks(this WebApplication app)
    {
        // /health — liveness: returns 200 if the process is alive.
        // Excludes all tagged checks — just confirms the host is running.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => false // No dependency checks — pure liveness
        }).AllowAnonymous();

        // /health/ready — readiness: runs checks tagged "ready" (Firestore connectivity).
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready")
        }).AllowAnonymous();

        return app;
    }
}
