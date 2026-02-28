// REQ-OPS-003: Clock-in/Clock-out API endpoints — employee self-service time tracking.
// TASK-071: Clock-in, clock-out, today's status, manager team panel.

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZenoHR.Api.Auth;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.TimeAttendance;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for the Time &amp; Attendance module.
/// REQ-OPS-003: Employee self-service clock-in/out from the 14-clock-in.html screen.
/// Managers can view team status and create flags for absent employees.
/// </summary>
public static class ClockEndpoints
{
    public static IEndpointRouteBuilder MapClockEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/clock")
            .RequireAuthorization()
            .WithTags("TimeAttendance");

        // POST /api/clock/in — employee self-service clock-in
        group.MapPost("/in", ClockInAsync)
            .WithName("ClockIn")
            .Produces<ClockEntryDto>(201)
            .Produces<ProblemDetails>(400);

        // POST /api/clock/out/{entryId} — clock out
        group.MapPost("/out/{entryId}", ClockOutAsync)
            .WithName("ClockOut")
            .Produces<ClockEntryDto>(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        // GET /api/clock/today — own today status
        group.MapGet("/today", GetTodayStatusAsync)
            .WithName("GetTodayClockStatus")
            .Produces<ClockEntryDto?>(200);

        // GET /api/clock/team — manager team status panel (Manager/Director/HRManager)
        group.MapGet("/team", GetTeamStatusAsync)
            .WithName("GetTeamClockStatus")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager", "Manager"))
            .Produces<IReadOnlyList<ClockEntryDto>>(200);

        // POST /api/clock/flags — create timesheet flag (Manager only)
        group.MapPost("/flags", CreateFlagAsync)
            .WithName("CreateTimesheetFlag")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager", "Manager"))
            .Produces<TimesheetFlagDto>(201)
            .Produces<ProblemDetails>(400);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ClockInAsync(
        ClaimsPrincipal user,
        ClockEntryRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var empId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        // REQ-OPS-003: At most one open entry per day
        var existing = await repo.GetOpenEntryAsync(tenantId, empId, today, ct);
        if (existing is not null)
            return Results.BadRequest("Already clocked in today. Clock out first.");

        var result = ClockEntry.ClockIn(
            entryId: $"ce_{Guid.CreateVersion7()}",
            tenantId: tenantId,
            employeeId: empId,
            source: ClockEntrySource.EmployeeSelf,
            now: now);

        if (result.IsFailure) return Results.BadRequest(result.Error!.Message);

        var saveResult = await repo.SaveAsync(result.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Created($"/api/clock/today", ToDto(result.Value!));
    }

    private static async Task<IResult> ClockOutAsync(
        string entryId,
        ClaimsPrincipal user,
        ClockEntryRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var empId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;
        var now = DateTimeOffset.UtcNow;

        var getResult = await repo.GetByEntryIdAsync(tenantId, entryId, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error!.Message);

        var entry = getResult.Value!;
        if (entry.EmployeeId != empId)
            return Results.Forbid();

        var clockOutResult = entry.ClockOut(now, now);
        if (clockOutResult.IsFailure) return Results.BadRequest(clockOutResult.Error!.Message);

        var saveResult = await repo.SaveAsync(entry, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Ok(ToDto(entry));
    }

    private static async Task<IResult> GetTodayStatusAsync(
        ClaimsPrincipal user,
        ClockEntryRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var empId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        var entry = await repo.GetOpenEntryAsync(tenantId, empId, today, ct);
        return Results.Ok(entry is not null ? ToDto(entry) : null);
    }

    private static async Task<IResult> GetTeamStatusAsync(
        ClaimsPrincipal user,
        EmployeeRepository empRepo,
        ClockEntryRepository clockRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        IReadOnlyList<ZenoHR.Module.Employee.Aggregates.Employee> teamEmployees;
        if (systemRole is "Director" or "HRManager")
            teamEmployees = await empRepo.ListActiveAsync(tenantId, ct);
        else
        {
            var deptId = user.FindFirstValue(ZenoHrClaimNames.DeptId) ?? "";
            teamEmployees = string.IsNullOrWhiteSpace(deptId)
                ? []
                : await empRepo.ListByDepartmentAsync(tenantId, deptId, ct);
        }

        if (teamEmployees.Count == 0) return Results.Ok(Array.Empty<ClockEntryDto>());

        var empIds = teamEmployees.Select(e => e.EmployeeId).ToList();
        var entries = await clockRepo.ListOpenForEmployeesAsync(tenantId, empIds, today, ct);

        return Results.Ok(entries.Select(ToDto));
    }

    private static async Task<IResult> CreateFlagAsync(
        [FromBody] CreateFlagDto req,
        ClaimsPrincipal user,
        TimesheetFlagRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var flaggedBy = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;

        if (!Enum.TryParse<TimesheetFlagReason>(req.Reason, ignoreCase: true, out var reason))
            return Results.BadRequest($"Invalid flag reason: {req.Reason}");

        var result = TimesheetFlag.Create(
            flagId: $"tf_{Guid.CreateVersion7()}",
            tenantId: tenantId,
            employeeId: req.EmployeeId,
            flaggedBy: flaggedBy,
            flagDate: DateOnly.Parse(req.FlagDate, System.Globalization.CultureInfo.InvariantCulture),
            reason: reason,
            notes: req.Notes,
            now: DateTimeOffset.UtcNow);

        if (result.IsFailure) return Results.BadRequest(result.Error!.Message);

        var saveResult = await repo.SaveAsync(result.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Created($"/api/clock/flags/{result.Value!.FlagId}", ToFlagDto(result.Value));
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private static ClockEntryDto ToDto(ClockEntry e) => new(
        e.EntryId, e.EmployeeId, e.ClockInAt, e.ClockOutAt,
        e.CalculatedHours, e.Status.ToString(),
        e.Date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private static TimesheetFlagDto ToFlagDto(TimesheetFlag f) => new(
        f.FlagId, f.EmployeeId, f.FlaggedBy,
        f.FlagDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        f.Reason.ToString(), f.Notes, f.Status.ToString());
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record ClockEntryDto(
    string EntryId, string EmployeeId, DateTimeOffset ClockInAt,
    DateTimeOffset? ClockOutAt, decimal? CalculatedHours, string Status, string Date);

public sealed record TimesheetFlagDto(
    string FlagId, string EmployeeId, string FlaggedBy, string FlagDate,
    string Reason, string? Notes, string Status);

public sealed record CreateFlagDto(string EmployeeId, string FlagDate, string Reason, string? Notes);
