// REQ-HR-003, CTL-SARS-001, REQ-SEC-002: Payroll API endpoints.
// TASK-086: Run payroll, list runs, get run, finalize, mark filed, list results, adjustments.
// Director/HRManager roles only (no Manager, no Employee access) — REQ-SEC-002.

using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ZenoHR.Api.Auth;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Infrastructure.Services;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Calculation;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for the Payroll module.
/// REQ-HR-003: Payroll run lifecycle — create, calculate, finalize, file.
/// REQ-SEC-002: All endpoints restricted to Director or HRManager roles.
/// </summary>
public static class PayrollEndpoints
{
    public static IEndpointRouteBuilder MapPayrollEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payroll")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager"))
            .RequireRateLimiting("payroll")  // REQ-SEC-003: strict limit — payroll runs are heavy (closes VUL-007)
            .WithTags("Payroll");

        // GET /api/payroll/runs — list all runs (newest first)
        group.MapGet("/runs", ListRunsAsync)
            .WithName("ListPayrollRuns")
            .Produces<IReadOnlyList<PayrollRunSummaryDto>>(200);

        // GET /api/payroll/runs/{id} — get run detail
        group.MapGet("/runs/{id}", GetRunAsync)
            .WithName("GetPayrollRun")
            .Produces<PayrollRunDetailDto>(200)
            .Produces(404);

        // POST /api/payroll/runs — create + calculate a new run
        group.MapPost("/runs", CreateRunAsync)
            .WithName("CreatePayrollRun")
            .Produces<PayrollRunDetailDto>(201)
            .Produces<ProblemDetails>(400);

        // PUT /api/payroll/runs/{id}/finalize — finalize (lock) the run
        // REQ-SEC-004: MFA required — a stolen JWT alone cannot finalize payroll (closes VUL-003).
        group.MapPut("/runs/{id}/finalize", FinalizeRunAsync)
            .WithName("FinalizePayrollRun")
            .RequireAuthorization(ZenoHrPolicies.RequiresMfa)
            .Produces<PayrollRunDetailDto>(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        // PUT /api/payroll/runs/{id}/file — mark run as filed after EMP201 download
        // REQ-SEC-004: MFA required — filing is irreversible (closes VUL-003).
        group.MapPut("/runs/{id}/file", MarkFiledAsync)
            .WithName("MarkPayrollRunFiled")
            .RequireAuthorization(ZenoHrPolicies.RequiresMfa)
            .Produces(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        // GET /api/payroll/runs/{id}/results — per-employee payslip data
        group.MapGet("/runs/{id}/results", ListResultsAsync)
            .WithName("ListPayrollResults")
            .Produces<IReadOnlyList<PayrollResultDto>>(200)
            .Produces(404);

        // GET /api/payroll/runs/{runId}/results/{employeeId} — single employee result
        group.MapGet("/runs/{runId}/results/{employeeId}", GetResultAsync)
            .WithName("GetPayrollResult")
            .Produces<PayrollResultDto>(200)
            .Produces(404);

        // POST /api/payroll/adjustments — post-finalization adjustment
        group.MapPost("/adjustments", CreateAdjustmentAsync)
            .WithName("CreatePayrollAdjustment")
            .Produces<PayrollAdjustmentDto>(201)
            .Produces<ProblemDetails>(400);

        // GET /api/payroll/adjustments?runId= — list adjustments for a run
        group.MapGet("/adjustments", ListAdjustmentsAsync)
            .WithName("ListPayrollAdjustments")
            .Produces<IReadOnlyList<PayrollAdjustmentDto>>(200);

        // GET /api/payroll/runs/{id}/results/{employeeId}/payslip — own payslip
        // Employees can access their own payslip — self-access override
        app.MapGet("/api/payroll/runs/{runId}/results/{employeeId}/payslip",
                GetPayslipAsync)
            .RequireAuthorization()  // any authenticated user; handler enforces self-access
            .WithName("GetPayslip")
            .WithTags("Payroll")
            .Produces<PayrollResultDto>(200)
            .Produces(403)
            .Produces(404);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListRunsAsync(
        ClaimsPrincipal user,
        PayrollRunRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var runs = await repo.ListByTenantAsync(tenantId, ct);
        return Results.Ok(runs.Select(ToSummaryDto));
    }

    private static async Task<IResult> GetRunAsync(
        string id,
        ClaimsPrincipal user,
        PayrollRunRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var result = await repo.GetByRunIdAsync(tenantId, id, ct);
        if (result.IsFailure) return Results.NotFound(result.Error!.Message);
        return Results.Ok(ToDetailDto(result.Value!));
    }

    private static async Task<IResult> CreateRunAsync(
        [FromBody] CreatePayrollRunRequest req,
        ClaimsPrincipal user,
        PayrollOrchestrationService orchestrator,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (!Enum.TryParse<PayFrequency>(req.RunType, ignoreCase: true, out var runType)
            || runType == PayFrequency.Unknown)
            return Results.BadRequest($"Invalid run type: {req.RunType}. Must be Monthly or Weekly.");

        if (req.EmployeeIds.Count == 0)
            return Results.BadRequest("EmployeeIds must not be empty.");

        var idempotencyKey = req.IdempotencyKey ?? Guid.CreateVersion7().ToString();
        var now = DateTimeOffset.UtcNow;

        var result = await orchestrator.RunPayrollAsync(
            tenantId: tenantId,
            period: req.Period,
            runType: runType,
            employeeIds: req.EmployeeIds,
            ruleSetVersion: req.RuleSetVersion,
            initiatedBy: actorId,
            idempotencyKey: idempotencyKey,
            isSdlExempt: req.IsSdlExempt,
            now: now,
            ct: ct);

        if (result.IsFailure) return Results.BadRequest(result.Error!.Message);

        return Results.Created($"/api/payroll/runs/{result.Value!.Id}", ToDetailDto(result.Value));
    }

    private static async Task<IResult> FinalizeRunAsync(
        string id,
        ClaimsPrincipal user,
        PayrollOrchestrationService orchestrator,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var result = await orchestrator.FinalizeRunAsync(
            tenantId, id, actorId, DateTimeOffset.UtcNow, ct);

        if (result.IsFailure)
            return result.Error!.Code == ZenoHrErrorCode.PayrollRunNotFound
                ? Results.NotFound(result.Error.Message)
                : Results.BadRequest(result.Error.Message);

        return Results.Ok(ToDetailDto(result.Value!));
    }

    private static async Task<IResult> MarkFiledAsync(
        string id,
        ClaimsPrincipal user,
        PayrollRunRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var getResult = await repo.GetByRunIdAsync(tenantId, id, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error!.Message);

        var fileResult = getResult.Value!.MarkFiled(actorId, DateTimeOffset.UtcNow);
        if (fileResult.IsFailure) return Results.BadRequest(fileResult.Error!.Message);

        var saveResult = await repo.SaveAsync(getResult.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Ok(new { message = "Payroll run marked as filed." });
    }

    private static async Task<IResult> ListResultsAsync(
        string id,
        ClaimsPrincipal user,
        PayrollRunRepository runRepo,
        PayrollResultRepository resultRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var runResult = await runRepo.GetByRunIdAsync(tenantId, id, ct);
        if (runResult.IsFailure) return Results.NotFound(runResult.Error!.Message);

        var results = await resultRepo.ListByRunAsync(id, ct);
        return Results.Ok(results.Select(ToResultDto));
    }

    private static async Task<IResult> GetResultAsync(
        string runId,
        string employeeId,
        ClaimsPrincipal user,
        PayrollRunRepository runRepo,
        PayrollResultRepository resultRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var runResult = await runRepo.GetByRunIdAsync(tenantId, runId, ct);
        if (runResult.IsFailure) return Results.NotFound(runResult.Error!.Message);

        var result = await resultRepo.GetByEmployeeIdAsync(runId, employeeId, ct);
        if (result.IsFailure) return Results.NotFound(result.Error!.Message);

        return Results.Ok(ToResultDto(result.Value!));
    }

    private static async Task<IResult> GetPayslipAsync(
        string runId,
        string employeeId,
        ClaimsPrincipal user,
        PayrollRunRepository runRepo,
        PayrollResultRepository resultRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId) ?? "";
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        // Self-access guarantee: employee can always view own payslip
        if (employeeId != ownEmpId
            && systemRole is not ("Director" or "HRManager" or "Manager"))
            return Results.Forbid();

        var runResult = await runRepo.GetByRunIdAsync(tenantId, runId, ct);
        if (runResult.IsFailure) return Results.NotFound(runResult.Error!.Message);

        var result = await resultRepo.GetByEmployeeIdAsync(runId, employeeId, ct);
        if (result.IsFailure) return Results.NotFound(result.Error!.Message);

        return Results.Ok(ToResultDto(result.Value!));
    }

    private static async Task<IResult> CreateAdjustmentAsync(
        [FromBody] CreateAdjustmentRequest req,
        ClaimsPrincipal user,
        PayrollRunRepository runRepo,
        PayrollAdjustmentRepository adjRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        // Verify the run exists and is finalized
        var runResult = await runRepo.GetByRunIdAsync(tenantId, req.PayrollRunId, ct);
        if (runResult.IsFailure) return Results.NotFound(runResult.Error!.Message);
        if (!runResult.Value!.IsImmutable)
            return Results.BadRequest("Adjustments can only be posted to Finalized or Filed runs.");

        if (!Enum.TryParse<PayrollAdjustmentType>(req.AdjustmentType, ignoreCase: true, out var adjType)
            || adjType == PayrollAdjustmentType.Unknown)
            return Results.BadRequest($"Invalid adjustment type: {req.AdjustmentType}.");

        var adjustmentResult = PayrollAdjustment.Create(
            adjustmentId: $"adj_{Guid.CreateVersion7()}",
            tenantId: tenantId,
            payrollRunId: req.PayrollRunId,
            employeeId: req.EmployeeId,
            adjustmentType: adjType,
            reason: req.Reason,
            amount: new MoneyZAR(req.AmountZar),
            affectedFields: req.AffectedFields,
            createdBy: actorId,
            approvedBy: null,
            now: DateTimeOffset.UtcNow);

        if (adjustmentResult.IsFailure) return Results.BadRequest(adjustmentResult.Error!.Message);

        var saveResult = await adjRepo.AppendAsync(adjustmentResult.Value!, ct);
        if (saveResult.IsFailure) return Results.Problem(saveResult.Error!.Message);

        return Results.Created(
            $"/api/payroll/adjustments/{adjustmentResult.Value!.AdjustmentId}",
            ToAdjustmentDto(adjustmentResult.Value));
    }

    private static async Task<IResult> ListAdjustmentsAsync(
        string? runId,
        string? employeeId,
        ClaimsPrincipal user,
        PayrollAdjustmentRepository adjRepo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;

        IReadOnlyList<PayrollAdjustment> adjustments;
        if (!string.IsNullOrWhiteSpace(runId))
            adjustments = await adjRepo.ListByRunAsync(tenantId, runId, ct);
        else if (!string.IsNullOrWhiteSpace(employeeId))
            adjustments = await adjRepo.ListByEmployeeAsync(tenantId, employeeId, ct);
        else
            return Results.BadRequest("Provide either runId or employeeId query parameter.");

        return Results.Ok(adjustments.Select(ToAdjustmentDto));
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private static PayrollRunSummaryDto ToSummaryDto(PayrollRun r) => new(
        r.Id, r.Period, r.RunType.ToString(), r.Status.ToString(),
        r.EmployeeCount,
        r.GrossTotal.ToFirestoreString(),
        r.NetTotal.ToFirestoreString(),
        r.CreatedAt);

    private static PayrollRunDetailDto ToDetailDto(PayrollRun r) => new(
        r.Id, r.TenantId, r.Period, r.RunType.ToString(), r.Status.ToString(),
        r.TaxYear.EndingYear, r.RuleSetVersion,
        r.EmployeeCount, r.EmployeeIds,
        r.GrossTotal.ToFirestoreString(),
        r.PayeTotal.ToFirestoreString(),
        r.UifTotal.ToFirestoreString(),
        r.SdlTotal.ToFirestoreString(),
        r.EtiTotal.ToFirestoreString(),
        r.DeductionTotal.ToFirestoreString(),
        r.NetTotal.ToFirestoreString(),
        r.ComplianceFlags,
        r.Checksum,
        r.InitiatedBy, r.CreatedAt,
        r.CalculatedAt, r.FinalizedBy, r.FinalizedAt, r.FiledAt);

    private static PayrollResultDto ToResultDto(PayrollResult r) => new(
        r.EmployeeId, r.PayrollRunId,
        r.BasicSalary.ToFirestoreString(),
        r.OvertimePay.ToFirestoreString(),
        r.Allowances.ToFirestoreString(),
        r.GrossPay.ToFirestoreString(),
        r.Paye.ToFirestoreString(),
        r.UifEmployee.ToFirestoreString(),
        r.UifEmployer.ToFirestoreString(),
        r.Sdl.ToFirestoreString(),
        r.PensionEmployee.ToFirestoreString(),
        r.MedicalEmployee.ToFirestoreString(),
        r.EtiAmount.ToFirestoreString(),
        r.EtiEligible,
        r.DeductionTotal.ToFirestoreString(),
        r.NetPay.ToFirestoreString(),
        r.HoursOrdinary, r.HoursOvertime,
        r.TaxTableVersion,
        r.ComplianceFlags,
        r.CalculationTimestamp);

    private static PayrollAdjustmentDto ToAdjustmentDto(PayrollAdjustment a) => new(
        a.AdjustmentId, a.TenantId, a.PayrollRunId, a.EmployeeId,
        a.AdjustmentType.ToString(), a.Reason,
        a.Amount.ToFirestoreString(), a.AffectedFields,
        a.CreatedBy, a.ApprovedBy, a.CreatedAt);
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

public sealed record PayrollRunSummaryDto(
    string RunId, string Period, string RunType, string Status,
    int EmployeeCount, string GrossTotalZar, string NetTotalZar,
    DateTimeOffset CreatedAt);

public sealed record PayrollRunDetailDto(
    string RunId, string TenantId, string Period, string RunType, string Status,
    int TaxYear, string RuleSetVersion,
    int EmployeeCount, IReadOnlyList<string> EmployeeIds,
    string GrossTotalZar, string PayeTotalZar, string UifTotalZar,
    string SdlTotalZar, string EtiTotalZar,
    string DeductionTotalZar, string NetTotalZar,
    IReadOnlyList<string> ComplianceFlags,
    string? Checksum,
    string InitiatedBy, DateTimeOffset CreatedAt,
    DateTimeOffset? CalculatedAt, string? FinalizedBy,
    DateTimeOffset? FinalizedAt, DateTimeOffset? FiledAt);

public sealed record PayrollResultDto(
    string EmployeeId, string RunId,
    string BasicSalaryZar, string OvertimePayZar, string AllowancesZar, string GrossPayZar,
    string PayeZar, string UifEmployeeZar, string UifEmployerZar, string SdlZar,
    string PensionEmployeeZar, string MedicalEmployeeZar,
    string EtiAmountZar, bool EtiEligible,
    string DeductionTotalZar, string NetPayZar,
    decimal HoursOrdinary, decimal HoursOvertime,
    string TaxTableVersion,
    IReadOnlyList<string> ComplianceFlags,
    DateTimeOffset CalculationTimestamp);

public sealed record PayrollAdjustmentDto(
    string AdjustmentId, string TenantId, string PayrollRunId, string EmployeeId,
    string AdjustmentType, string Reason, string AmountZar,
    IReadOnlyList<string> AffectedFields,
    string CreatedBy, string? ApprovedBy, DateTimeOffset CreatedAt);

public sealed record CreatePayrollRunRequest(
    string Period,
    string RunType,
    IReadOnlyList<string> EmployeeIds,
    string RuleSetVersion,
    bool IsSdlExempt,
    string? IdempotencyKey);

public sealed record CreateAdjustmentRequest(
    string PayrollRunId,
    string EmployeeId,
    string AdjustmentType,
    string Reason,
    decimal AmountZar,
    IReadOnlyList<string> AffectedFields);
