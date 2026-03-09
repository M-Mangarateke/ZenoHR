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
using ZenoHR.Infrastructure.Services.Filing.Emp201;
using ZenoHR.Infrastructure.Services.Payslip;
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

        // CTL-SARS-006: EMP201 CSV download (TASK-088)
        // GET /api/payroll/runs/{runId}/emp201/csv — semicolon CSV for SARS eFiling upload
        group.MapGet("/runs/{runId}/emp201/csv", GetEmp201CsvAsync)
            .WithName("GetEmp201Csv")
            .Produces(200)
            .Produces(404);

        // CTL-SARS-006: EMP201 summary report (TASK-088)
        // GET /api/payroll/runs/{runId}/emp201/report — human-readable summary for HR Manager review
        group.MapGet("/runs/{runId}/emp201/report", GetEmp201ReportAsync)
            .WithName("GetEmp201Report")
            .Produces(200)
            .Produces(404);

        // GET /api/payroll/runs/{id}/results/{employeeId}/payslip — own payslip (JSON)
        // Employees can access their own payslip — self-access override
        app.MapGet("/api/payroll/runs/{runId}/results/{employeeId}/payslip",
                GetPayslipAsync)
            .RequireAuthorization()  // any authenticated user; handler enforces self-access
            .WithName("GetPayslip")
            .WithTags("Payroll")
            .Produces<PayrollResultDto>(200)
            .Produces(403)
            .Produces(404);

        // GET /api/payroll/runs/{runId}/payslips/{employeeId}/pdf — PDF download
        // REQ-HR-004, CTL-SARS-005: BCEA §33 compliant payslip PDF
        // REQ-SEC-002: Employee can only download own payslips (self-access guarantee)
        app.MapGet("/api/payroll/runs/{runId}/payslips/{employeeId}/pdf",
                GetPayslipPdfAsync)
            .RequireAuthorization()
            .WithName("GetPayslipPdf")
            .WithTags("Payroll")
            .Produces(200)
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

    // REQ-HR-004, CTL-SARS-005: PDF payslip download endpoint.
    // REQ-SEC-002: Director/HRManager can download any employee payslip;
    //              Employee and Manager can only download own payslip (self-access guarantee).
    private static async Task<IResult> GetPayslipPdfAsync(
        string runId,
        string employeeId,
        ClaimsPrincipal user,
        PayrollRunRepository runRepo,
        PayrollResultRepository resultRepo,
        IPayslipPdfGenerator pdfGenerator,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var ownEmpId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId) ?? "";
        var systemRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        // REQ-SEC-002: self-access guarantee — employee can always download own payslip
        if (employeeId != ownEmpId
            && systemRole is not ("Director" or "HRManager"))
            return Results.Forbid();

        var runResult = await runRepo.GetByRunIdAsync(tenantId, runId, ct);
        if (runResult.IsFailure) return Results.NotFound(runResult.Error!.Message);

        var resultGet = await resultRepo.GetByEmployeeIdAsync(runId, employeeId, ct);
        if (resultGet.IsFailure) return Results.NotFound(resultGet.Error!.Message);

        var r = resultGet.Value!;
        var run = runResult.Value!;

        // Build PayslipData from the PayrollResult + PayrollRun (stub values for fields
        // not yet stored in the run/result — full implementation requires employee/contract lookup)
        var payslipData = new ZenoHR.Infrastructure.Services.Payslip.PayslipData
        {
            // ── Employer (from company settings — using known Zenowethu values)
            EmployerName = "Zenowethu (Pty) Ltd",
            EmployerRegistrationNumber = "2018/123456/07",
            EmployerAddress = "23 Innovation Drive, Sandton, Gauteng, 2196",
            EmployerTaxReferenceNumber = "9123456789",
            EmployerPayeReference = "7234567890",
            EmployerUifReferenceNumber = "U0987654321",

            // ── Employee (from result)
            EmployeeId = r.EmployeeId,
            EmployeeFullName = r.EmployeeId,  // Full resolution requires employee lookup
            JobTitle = "—",
            Department = "—",
            TaxReferenceNumber = "—",
            UifNumber = "—",
            IdOrPassportMasked = "—",
            HireDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentMethod = "EFT",

            // ── Pay period
            PayPeriodLabel = run.Period,
            PeriodStart = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30)),
            PeriodEnd = DateOnly.FromDateTime(DateTime.UtcNow),
            PaymentDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PayrollRunReference = run.Id,

            // ── Hours
            HoursOrdinary = r.HoursOrdinary,
            HoursOvertime = r.HoursOvertime,

            // ── Earnings
            BasicSalary = r.BasicSalary.Amount,
            Overtime = r.OvertimePay.Amount,
            TravelAllowance = r.Allowances.Amount,
            MedicalAidEmployerContribution = r.MedicalEmployer.Amount,
            PensionEmployerContribution = r.PensionEmployer.Amount,
            GrossSalary = r.GrossPay.Amount,

            // ── Deductions
            PayeAmount = r.Paye.Amount,
            UifEmployee = r.UifEmployee.Amount,
            PensionEmployee = r.PensionEmployee.Amount,
            MedicalAidEmployee = r.MedicalEmployee.Amount,
            TotalDeductions = r.DeductionTotal.Amount,

            // ── Net pay
            NetPay = r.NetPay.Amount,

            // ── Employer-side contributions
            UifEmployer = r.UifEmployer.Amount,
            Sdl = r.Sdl.Amount,
            EtiAmount = r.EtiAmount.Amount,

            // ── Tax summary (YTD = this period for first run)
            AnnualisedIncome = r.GrossPay.Amount * 12m,
            AnnualTaxLiability = r.Paye.Amount * 12m,
            PrimaryRebate = 0m,
            YtdPaye = r.Paye.Amount,
            YtdUifEmployee = r.UifEmployee.Amount,
            YtdGross = r.GrossPay.Amount,

            // ── Leave balances (not yet on PayrollResult — zero until leave module integration)
            AnnualLeaveBalance = 0m,
            AnnualLeaveEntitlement = 21m,
            SickLeaveBalance = 0m,
            SickLeaveEntitlement = 30m,
            FamilyResponsibilityBalance = 0m,
            FamilyResponsibilityEntitlement = 3m,

            // ── Metadata
            TaxYear = $"{run.TaxYear.EndingYear - 1}/{run.TaxYear.EndingYear}",
            PayFrequency = run.RunType.ToString(),
            GeneratedByUserId = user.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier) ?? "system",
            GeneratedAt = DateTimeOffset.UtcNow,
            PayrollRunId = run.Id,
            PayrollResultId = r.EmployeeId,
        };

        var pdfBytes = pdfGenerator.Generate(payslipData);
        var fileName = $"payslip-{r.EmployeeId}-{run.Id}.pdf";
        return Results.File(pdfBytes, "application/pdf", fileName);
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

    // CTL-SARS-006: EMP201 CSV download handler (TASK-088).
    // TODO(TASK-088-wire): Replace mock Emp201Data with real data loaded from PayrollRun +
    //   PayrollResult subcollection + EmployeeRepository (company settings for PAYE/UIF/SDL refs).
    private static IResult GetEmp201CsvAsync(
        string runId,
        ClaimsPrincipal user,
        IEmp201Generator generator)
    {
        // Stub: mock data matching RUN-2026-02 until real wiring is implemented (see TODO above)
        var data = BuildMockEmp201Data(runId, user);
        var csv = generator.GenerateCsv(data);
        var fileName = $"EMP201-{data.TaxPeriod}-{runId}.csv";
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        return Results.File(bytes, "text/csv", fileName);
    }

    // CTL-SARS-006: EMP201 summary report handler (TASK-088).
    // TODO(TASK-088-wire): Replace mock Emp201Data with real data (same as GetEmp201CsvAsync).
    private static IResult GetEmp201ReportAsync(
        string runId,
        ClaimsPrincipal user,
        IEmp201Generator generator)
    {
        var data = BuildMockEmp201Data(runId, user);
        var report = generator.GenerateSummaryReport(data);
        return Results.Content(report, "text/plain");
    }

    // CTL-SARS-006: Builds a mock Emp201Data for stub endpoints — RUN-2026-02 sample.
    // Replace with real data loading once PayrollRun.ToEmp201Data() or equivalent is implemented.
    private static Emp201Data BuildMockEmp201Data(string runId, ClaimsPrincipal user)
    {
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? "system";

        var lines = new List<Emp201EmployeeLine>
        {
            new()
            {
                EmployeeId = "EMP-001",
                EmployeeFullName = "Zanele Dlamini",
                TaxReferenceNumber = "9876543210",
                IdOrPassportNumber = "8001015009087",
                GrossRemuneration = 45_000.00m,
                PayeDeducted = 8_250.00m,
                UifEmployee = 177.12m,
                UifEmployer = 177.12m,
                SdlEmployer = 450.00m,
                PaymentMethod = "EFT",
            },
            new()
            {
                EmployeeId = "EMP-002",
                EmployeeFullName = "Sipho Nkosi",
                TaxReferenceNumber = "1234567890",
                IdOrPassportNumber = "9203025008086",
                GrossRemuneration = 28_500.00m,
                PayeDeducted = 3_100.00m,
                UifEmployee = 177.12m,
                UifEmployer = 177.12m,
                SdlEmployer = 285.00m,
                PaymentMethod = "EFT",
            },
        };

        var generator = new Emp201Generator();
        return new Emp201Data
        {
            EmployerPAYEReference = "7234567890",
            EmployerUifReference = "U0987654321",
            EmployerSdlReference = "SDL0987654321",
            EmployerTradingName = "Zenowethu (Pty) Ltd",
            TaxPeriod = "202602",
            PeriodLabel = "February 2026",
            PayrollRunId = runId,
            TotalPayeDeducted = lines.Sum(l => l.PayeDeducted),
            TotalUifEmployee = lines.Sum(l => l.UifEmployee),
            TotalUifEmployer = lines.Sum(l => l.UifEmployer),
            TotalSdl = lines.Sum(l => l.SdlEmployer),
            TotalGrossRemuneration = lines.Sum(l => l.GrossRemuneration),
            EmployeeCount = lines.Count,
            DueDate = generator.CalculateDueDate(2026, 2),
            EmployeeLines = lines.AsReadOnly(),
            GeneratedByUserId = actorId,
            GeneratedAt = DateTimeOffset.UtcNow,
        };
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
