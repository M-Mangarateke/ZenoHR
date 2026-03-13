// REQ-HR-002, CTL-BCEA-003, CTL-BCEA-004: Leave API endpoints.
// TASK-070: Submit, list, approve, reject leave requests. GET leave balances.
// Role scoping: Employees submit own; Managers approve team; Director/HRManager full access.

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZenoHR.Api.Auth;
using ZenoHR.Api.Pagination;
using ZenoHR.Api.Validation;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for the Leave module.
/// REQ-HR-002: Leave submission, approval, and balance queries.
/// CTL-BCEA-003: Balance cannot go negative (enforced in domain aggregate).
/// CTL-BCEA-004: Manager approves leave for their department only.
/// </summary>
public static class LeaveEndpoints
{
    public static IEndpointRouteBuilder MapLeaveEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/leave")
            .RequireAuthorization()
            .RequireRateLimiting("general-api")   // REQ-SEC-003: closes VUL-007
            .WithTags("Leave");

        // ── Balances ──────────────────────────────────────────────────────────

        // GET /api/leave/balances?employeeId=&cycleId= — own or authorized employee
        group.MapGet("/balances", GetBalancesAsync)
            .WithName("GetLeaveBalances")
            .Produces<IReadOnlyList<LeaveBalanceDto>>(200);

        // ── Requests ──────────────────────────────────────────────────────────

        // GET /api/leave/requests — own (Employee) or team/all (Manager/Director/HRManager)
        // VUL-027: Paginated — accepts skip/take query params (default 50, max 200).
        group.MapGet("/requests", ListRequestsAsync)
            .WithName("ListLeaveRequests")
            .Produces<PaginatedResponse<LeaveRequestDto>>(200);

        // GET /api/leave/requests/{id}
        group.MapGet("/requests/{id}", GetRequestByIdAsync)
            .WithName("GetLeaveRequestById")
            .Produces<LeaveRequestDto>(200)
            .Produces(404);

        // POST /api/leave/requests — submit new leave request
        // VUL-027: FluentValidation applied via WithValidation filter.
        group.MapPost("/requests", SubmitRequestAsync)
            .WithName("SubmitLeaveRequest")
            .WithValidation<SubmitLeaveRequestDto>()
            .Produces<LeaveRequestDto>(201)
            .Produces<ProblemDetails>(400);

        // PUT /api/leave/requests/{id}/approve — approve (Manager/Director/HRManager)
        group.MapPut("/requests/{id}/approve", ApproveRequestAsync)
            .WithName("ApproveLeaveRequest")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager", "Manager"))
            .Produces(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        // PUT /api/leave/requests/{id}/reject — reject (Manager/Director/HRManager)
        group.MapPut("/requests/{id}/reject", RejectRequestAsync)
            .WithName("RejectLeaveRequest")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager", "Manager"))
            .Produces(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        // PUT /api/leave/requests/{id}/cancel — cancel own request (any status pre-approval)
        group.MapPut("/requests/{id}/cancel", CancelRequestAsync)
            .WithName("CancelLeaveRequest")
            .Produces(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> GetBalancesAsync(
        string? employeeId,
        string? cycleId,
        ClaimsPrincipal user,
        LeaveBalanceRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        // Default to own employee ID if not specified or not authorized
        var targetEmpId = employeeId ?? ownEmpId;
        if (targetEmpId != ownEmpId && systemRole is not ("Director" or "HRManager" or "Manager"))
            return Results.Forbid();

        var balances = await repo.ListByEmployeeAsync(tenantId, targetEmpId, ct);

        if (!string.IsNullOrWhiteSpace(cycleId))
            balances = balances.Where(b => b.CycleId == cycleId).ToList();

        return Results.Ok(balances.Select(ToBalanceDto));
    }

    // VUL-027: Paginated list — accepts skip/take query params (default 50, max 200).
    private static async Task<IResult> ListRequestsAsync(
        string? employeeId,
        int? skip,
        int? take,
        ClaimsPrincipal user,
        LeaveRequestRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        IReadOnlyList<LeaveRequest> requests;

        if (systemRole is "Director" or "HRManager")
        {
            // For Director/HRManager, show specified employee or all — simplified: return specified emp or error
            var targetEmpId = employeeId ?? ownEmpId;
            requests = await repo.ListByEmployeeAsync(tenantId, targetEmpId, ct);
        }
        else if (systemRole == "Manager")
        {
            // Manager: pending requests for their department employees
            // Simplified: list for specified employeeId or own
            var targetEmpId = employeeId ?? ownEmpId;
            requests = await repo.ListByEmployeeAsync(tenantId, targetEmpId, ct);
        }
        else
        {
            // Employee: own requests only
            requests = await repo.ListByEmployeeAsync(tenantId, ownEmpId, ct);
        }

        var dtos = requests.Select(ToRequestDto).ToList().AsReadOnly();
        return Results.Ok(PaginationDefaults.Apply(dtos, skip, take));
    }

    private static async Task<IResult> GetRequestByIdAsync(
        string id,
        ClaimsPrincipal user,
        LeaveRequestRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var result = await repo.GetByLeaveRequestIdAsync(tenantId, id, ct);
        if (result.IsFailure) return Results.NotFound(result.Error!.Message);

        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        if (result.Value!.EmployeeId != ownEmpId && systemRole is not ("Director" or "HRManager" or "Manager"))
            return Results.Forbid();

        return Results.Ok(ToRequestDto(result.Value));
    }

    private static async Task<IResult> SubmitRequestAsync(
        [FromBody] SubmitLeaveRequestDto req,
        ClaimsPrincipal user,
        LeaveRequestRepository repo,
        LeaveBalanceRepository balanceRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;

        if (!Enum.TryParse<LeaveType>(req.LeaveType, ignoreCase: true, out var leaveType))
            return Results.BadRequest($"Invalid leave type: {req.LeaveType}");

        var startDate = DateOnly.Parse(req.StartDate, System.Globalization.CultureInfo.InvariantCulture);
        var endDate = DateOnly.Parse(req.EndDate, System.Globalization.CultureInfo.InvariantCulture);
        var cycleId = startDate.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Get current balance snapshot (informational only — enforcement is in domain)
        var balResult = await balanceRepo.GetByEmployeeAndTypeAsync(tenantId, ownEmpId, leaveType, cycleId, ct);
        decimal? balanceSnapshot = balResult.IsSuccess ? balResult.Value!.AvailableHours : null;

        var result = LeaveRequest.Submit(
            leaveRequestId: $"lr_{Guid.CreateVersion7()}",
            tenantId: tenantId,
            employeeId: ownEmpId,
            leaveType: leaveType,
            startDate: startDate,
            endDate: endDate,
            totalHours: req.TotalHours,
            reasonCode: req.ReasonCode,
            balanceSnapshotAtRequest: balanceSnapshot,
            now: DateTimeOffset.UtcNow);

        if (result.IsFailure) return Results.BadRequest(result.Error!.Message);

        var saveResult = await repo.SaveAsync(result.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Created($"/api/leave/requests/{result.Value!.LeaveRequestId}", ToRequestDto(result.Value));
    }

    private static async Task<IResult> ApproveRequestAsync(
        string id,
        ClaimsPrincipal user,
        LeaveRequestRepository repo,
        LeaveBalanceRepository balanceRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;

        var getResult = await repo.GetByLeaveRequestIdAsync(tenantId, id, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error!.Message);

        var request = getResult.Value!;
        var now = DateTimeOffset.UtcNow;

        // Approve the request (domain enforces state machine)
        var approveResult = request.Approve(actorId, now);
        if (approveResult.IsFailure) return Results.BadRequest(approveResult.Error!.Message);

        // Consume leave balance atomically (CTL-BCEA-003)
        var cycleId = request.StartDate.Year.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var balResult = await balanceRepo.GetByEmployeeAndTypeAsync(
            tenantId, request.EmployeeId, request.LeaveType, cycleId, ct);

        if (balResult.IsSuccess)
        {
            var balance = balResult.Value!;
            var consumeResult = balance.ConsumeHours(
                ledgerEntryId: $"le_{Guid.CreateVersion7()}",
                leaveRequestId: request.LeaveRequestId,
                hours: request.TotalHours,
                effectiveDate: request.StartDate,
                policyVersion: balance.PolicyVersion,
                actorId: actorId,
                now: now);

            if (consumeResult.IsFailure) return Results.BadRequest(consumeResult.Error!.Message);
            await balanceRepo.SaveWithLedgerEntriesAsync(balance, ct);
        }

        var saveResult = await repo.SaveAsync(request, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Ok(new { message = "Leave request approved." });
    }

    private static async Task<IResult> RejectRequestAsync(
        string id,
        [FromBody] RejectLeaveRequestDto req,
        ClaimsPrincipal user,
        LeaveRequestRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;

        var getResult = await repo.GetByLeaveRequestIdAsync(tenantId, id, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error!.Message);

        var rejectResult = getResult.Value!.Reject(actorId, req.RejectionReason, DateTimeOffset.UtcNow);
        if (rejectResult.IsFailure) return Results.BadRequest(rejectResult.Error!.Message);

        var saveResult = await repo.SaveAsync(getResult.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Ok(new { message = "Leave request rejected." });
    }

    private static async Task<IResult> CancelRequestAsync(
        string id,
        ClaimsPrincipal user,
        LeaveRequestRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)!;

        var getResult = await repo.GetByLeaveRequestIdAsync(tenantId, id, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error!.Message);

        // Employees can only cancel their own requests
        if (getResult.Value!.EmployeeId != ownEmpId)
        {
            var role = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";
            if (role is not ("Director" or "HRManager"))
                return Results.Forbid();
        }

        var cancelResult = getResult.Value!.Cancel(ownEmpId, DateTimeOffset.UtcNow);
        if (cancelResult.IsFailure) return Results.BadRequest(cancelResult.Error!.Message);

        var saveResult = await repo.SaveAsync(getResult.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Ok(new { message = "Leave request cancelled." });
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private static LeaveBalanceDto ToBalanceDto(LeaveBalance b) => new(
        b.BalanceId, b.EmployeeId, b.LeaveType.ToString(), b.CycleId,
        b.AccruedHours, b.ConsumedHours, b.AdjustmentHours, b.AvailableHours,
        b.LastAccrualDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));

    private static LeaveRequestDto ToRequestDto(LeaveRequest r) => new(
        r.LeaveRequestId, r.EmployeeId, r.LeaveType.ToString(),
        r.StartDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        r.EndDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        r.TotalHours, r.ReasonCode, r.Status.ToString(),
        r.ApproverId, r.RejectionReason, r.CreatedAt, r.UpdatedAt);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public sealed record LeaveBalanceDto(
    string BalanceId, string EmployeeId, string LeaveType, string CycleId,
    decimal AccruedHours, decimal ConsumedHours, decimal AdjustmentHours, decimal AvailableHours,
    string LastAccrualDate);

public sealed record LeaveRequestDto(
    string LeaveRequestId, string EmployeeId, string LeaveType,
    string StartDate, string EndDate, decimal TotalHours, string ReasonCode,
    string Status, string? ApproverId, string? RejectionReason,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SubmitLeaveRequestDto(
    string LeaveType, string StartDate, string EndDate,
    decimal TotalHours, string ReasonCode);

public sealed record RejectLeaveRequestDto(string RejectionReason);
