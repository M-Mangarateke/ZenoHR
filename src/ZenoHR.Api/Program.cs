// REQ-OPS-001: ZenoHR API entry point — ASP.NET Core 10 modular monolith host.
// REQ-SEC-001: Authentication middleware wired here (TASK-024).
// REQ-OPS-005: OpenTelemetry tracing + metrics wired here (TASK-032).
// REQ-OPS-006: Azure Monitor export configured via AddZenoHrTelemetry() (TASK-032).

using ZenoHR.Api.Auth;
using ZenoHR.Api.Endpoints;
using ZenoHR.Api.Observability;
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

// ── App pipeline ─────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();   // TASK-024: validate Firebase JWT on every request
app.UseAuthorization();    // TASK-025: enforce [Authorize(Roles = ...)] on endpoints

// Health check endpoint — anonymous, no auth required
// REQ-OPS-007: Health endpoint for Azure Container Apps liveness probe
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "ZenoHR.Api" }))
   .WithName("HealthCheck")
   .AllowAnonymous();

// ── Module API endpoints (TASK-067, TASK-070, TASK-071) ───────────────────────
app.MapEmployeeEndpoints();  // GET/POST/PUT /api/employees
app.MapLeaveEndpoints();     // GET/POST/PUT /api/leave/requests, /api/leave/balances
app.MapClockEndpoints();     // POST /api/clock/in|out, GET /api/clock/today|team

app.Run();
