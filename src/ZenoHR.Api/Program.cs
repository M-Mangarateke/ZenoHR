// REQ-OPS-001: ZenoHR API entry point — ASP.NET Core 10 modular monolith host.
// REQ-SEC-001: Authentication middleware wired here (TASK-024).
// REQ-SEC-003, REQ-SEC-007: Security headers, CORS, HSTS, rate limiting wired here.
// REQ-OPS-005: OpenTelemetry tracing + metrics wired here (TASK-032).
// REQ-OPS-006: Azure Monitor export configured via AddZenoHrTelemetry() (TASK-032).

using ZenoHR.Api.Auth;
using ZenoHR.Api.Endpoints;
using ZenoHR.Api.Middleware;
using ZenoHR.Api.Observability;
using ZenoHR.Api.Security;
using ZenoHR.Infrastructure.Extensions;

var builder = WebApplication.CreateBuilder(args);

// ── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();

// OpenTelemetry tracing + metrics + Azure Monitor export (TASK-032)
// Reads APPLICATIONINSIGHTS_CONNECTION_STRING from env (set in Azure Container Apps / Key Vault).
// Safe to call in Development — exporter silently disabled when connection string is absent.
builder.AddZenoHrTelemetry();

// Firebase Auth / JWT validation (TASK-024)
// Firebase:ProjectId must be set via .NET User Secrets or Azure Key Vault (see TASK-036).
var firebaseProjectId = builder.Configuration["Firebase:ProjectId"]
    ?? throw new InvalidOperationException(
        "Firebase:ProjectId is required. " +
        "Set it via .NET User Secrets: dotnet user-secrets set Firebase:ProjectId <your-project-id>");

builder.Services.AddZenoHrFirebaseAuth(firebaseProjectId);

// Firestore connection (TASK-020)
builder.Services.AddZenoHrFirestore(builder.Configuration);

// MediatR + pipeline behaviours (TASK-048)
builder.Services.AddZenoHrMediatR();

// HSTS — 1 year max-age, include subdomains (REQ-SEC-003, closes VUL-023)
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = true;
});

// CORS — allowed origins from config; production value from Azure Key Vault (REQ-SEC-007)
// Closes VUL-002: no CORS policy previously configured.
builder.Services.AddZenoHrCors(builder.Configuration);

// Rate limiting — closes VUL-007: no rate limiting on API endpoints.
// REQ-SEC-003: protect against DoS and credential-stuffing at the API layer.
// Three named policies: general-api (sliding), auth-endpoints (fixed), payroll-ops (fixed).
builder.Services.AddZenoHrRateLimiting(); // VUL-007

// ── App pipeline ─────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseCorrelationId();          // 1st: assigns X-Correlation-Id before everything (REQ-OPS-008)
app.UseGlobalExceptionHandler(); // 2nd: outermost exception catch (REQ-OPS-008)
app.UseZenoHrSecurityHeaders();  // 3rd: CSP, X-Frame-Options, nosniff, Referrer-Policy (REQ-SEC-003)
if (!app.Environment.IsDevelopment())
{
    // 4th: HSTS — only in production (dev uses HTTP/localhost) — closes VUL-023
    app.UseHsts();
}
app.UseHttpsRedirection();       // 5th: redirect HTTP → HTTPS
app.UseCors("ZenoHrPolicy");     // 6th: CORS before auth (REQ-SEC-007)
app.UseAuthentication();         // 7th: validate Firebase JWT (TASK-024)
app.UseAuthorization();          // 8th: enforce policies (TASK-025)
app.UseRateLimiter();            // 9th: rate limiting (REQ-SEC-003)

// Health check endpoints — anonymous, no auth, no rate limit required
// REQ-OPS-007: Liveness + readiness probes for Azure Container Apps (TASK-148).
// /health      → liveness  (is the process alive? returns 200 if so)
// /health/ready → readiness (is the app ready to serve traffic? same check for now;
//                            extend to verify Firestore connectivity when startup probe is added)
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ZenoHR.Api" }))
   .WithName("HealthCheck")
   .AllowAnonymous();

app.MapGet("/health/ready", () => Results.Ok(new { status = "ready", service = "ZenoHR.Api" }))
   .WithName("ReadinessCheck")
   .AllowAnonymous();

// ── Module API endpoints (TASK-067, TASK-070, TASK-071, TASK-086) ─────────────
app.MapEmployeeEndpoints();   // GET/POST/PUT /api/employees
app.MapLeaveEndpoints();      // GET/POST/PUT /api/leave/requests, /api/leave/balances
app.MapClockEndpoints();      // POST /api/clock/in|out, GET /api/clock/today|team
app.MapPayrollEndpoints();    // GET/POST/PUT /api/payroll/runs, /api/payroll/adjustments
app.MapComplianceEndpoints(); // REQ-COMP-001: GET/POST /api/compliance/submissions, emp201, emp501
app.MapStatutoryEndpoints();  // CTL-SARS-001: GET/PUT /api/settings/statutory

app.Run();
