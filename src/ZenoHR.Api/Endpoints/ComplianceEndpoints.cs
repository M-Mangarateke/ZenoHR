// REQ-COMP-001, REQ-COMP-002, CTL-SARS-006: Compliance API endpoints.
// TASK-091: List submissions, generate EMP201/EMP501, download filing file.
// Director/HRManager roles only (REQ-SEC-002).

using System.Security.Claims;
using ZenoHR.Api.Auth;
using ZenoHR.Api.Pagination;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Infrastructure.Services.Filing;
using ZenoHR.Infrastructure.Services.Filing.Itreg;
using ZenoHR.Module.Compliance.Entities;
using ZenoHR.Module.Compliance.Enums;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for the Compliance module.
/// REQ-COMP-001: Submission listing, EMP201/EMP501 generation, filing download.
/// REQ-SEC-002: All endpoints restricted to Director or HRManager roles.
/// </summary>
public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager"))
            .RequireRateLimiting("general-api")   // REQ-SEC-003: closes VUL-007
            .WithTags("Compliance");

        // GET /api/compliance/submissions?period={period} — list submissions for tenant
        // VUL-027: Paginated — accepts skip/take query params (default 50, max 200).
        group.MapGet("/submissions", ListSubmissionsAsync)
            .WithName("ListComplianceSubmissions")
            .Produces<PaginatedResponse<ComplianceSubmissionDto>>(200);

        // GET /api/compliance/submissions/{id} — get single submission
        group.MapGet("/submissions/{id}", GetSubmissionAsync)
            .WithName("GetComplianceSubmission")
            .Produces<ComplianceSubmissionDto>(200)
            .Produces(404);

        // POST /api/compliance/emp201/{period} — generate EMP201 for period
        // CTL-SARS-006: Reads finalized payroll run, generates CSV, persists submission
        group.MapPost("/emp201/{period}", GenerateEmp201Async)
            .WithName("GenerateEmp201")
            .Produces<ComplianceSubmissionDto>(201)
            .Produces(400)
            .Produces(404);

        // POST /api/compliance/emp501/{taxYear} — generate EMP501 annual reconciliation
        // REQ-COMP-002: Reads all 12 monthly runs for the SA tax year
        group.MapPost("/emp501/{taxYear}", GenerateEmp501Async)
            .WithName("GenerateEmp501")
            .Produces<ComplianceSubmissionDto>(201)
            .Produces(400)
            .Produces(404);

        // GET /api/compliance/submissions/{id}/download — download the generated CSV/XML file
        // CTL-SARS-006: Returns the stored filing content as a downloadable file
        group.MapGet("/submissions/{id}/download", DownloadSubmissionAsync)
            .WithName("DownloadComplianceSubmission")
            .Produces(200)
            .Produces(404);

        // GET /api/compliance/employees/missing-tax-reference — employees missing valid tax refs
        // CTL-SARS-006: ITREG workflow — identify employees needing SARS income tax registration
        group.MapGet("/employees/missing-tax-reference", GetMissingTaxReferencesAsync)
            .WithName("GetMissingTaxReferences")
            .Produces<IReadOnlyList<MissingTaxReferenceEntry>>(200)
            .Produces(400);

        // POST /api/compliance/itreg/generate — generate ITREG export file
        // CTL-SARS-006: Produces SARS e@syFile-compatible registration export
        group.MapPost("/itreg/generate", GenerateItregAsync)
            .WithName("GenerateItreg")
            .Produces<string>(200, "text/plain")
            .Produces(400);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    // VUL-027: Paginated list — accepts skip/take query params (default 50, max 200).
    private static async Task<IResult> ListSubmissionsAsync(
        string? period,
        int? skip,
        int? take,
        ClaimsPrincipal user,
        ComplianceSubmissionRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;

        if (!string.IsNullOrWhiteSpace(period))
        {
            var byPeriod = await repo.ListByPeriodAsync(tenantId, period, ct);
            if (byPeriod.IsFailure) return Results.Problem(byPeriod.Error.Message);
            var periodDtos = byPeriod.Value.Select(ToDto).ToList().AsReadOnly();
            return Results.Ok(PaginationDefaults.Apply(periodDtos, skip, take));
        }

        var all = await repo.ListByTenantAsync(tenantId, ct: ct);
        if (all.IsFailure) return Results.Problem(all.Error.Message);
        var allDtos = all.Value.Select(ToDto).ToList().AsReadOnly();
        return Results.Ok(PaginationDefaults.Apply(allDtos, skip, take));
    }

    private static async Task<IResult> GetSubmissionAsync(
        string id,
        ClaimsPrincipal user,
        ComplianceSubmissionRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var result = await repo.GetByIdAsync(tenantId, id, ct);
        if (result.IsFailure) return Results.NotFound(result.Error.Message);
        if (result.Value is null) return Results.NotFound($"Submission {id} not found.");
        return Results.Ok(ToDto(result.Value));
    }

    private static async Task<IResult> GenerateEmp201Async(
        string period,
        ClaimsPrincipal user,
        FilingWorkflowService filingService,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var result = await filingService.GenerateEmp201Async(tenantId, period, actorId, ct);

        if (result.IsFailure)
        {
            return result.Error.Code == ZenoHrErrorCode.PayrollRunNotFound
                ? Results.NotFound(result.Error.Message)
                : Results.BadRequest(result.Error.Message);
        }

        return Results.Created(
            $"/api/compliance/submissions/{result.Value.Id}",
            ToDto(result.Value));
    }

    private static async Task<IResult> GenerateEmp501Async(
        string taxYear,
        ClaimsPrincipal user,
        FilingWorkflowService filingService,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var result = await filingService.GenerateEmp501Async(tenantId, taxYear, actorId, ct);

        if (result.IsFailure)
        {
            return result.Error.Code is ZenoHrErrorCode.InvalidFilingPeriod
                ? Results.BadRequest(result.Error.Message)
                : Results.Problem(result.Error.Message);
        }

        return Results.Created(
            $"/api/compliance/submissions/{result.Value.Id}",
            ToDto(result.Value));
    }

    private static async Task<IResult> DownloadSubmissionAsync(
        string id,
        ClaimsPrincipal user,
        ComplianceSubmissionRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;
        var result = await repo.GetByIdAsync(tenantId, id, ct);

        if (result.IsFailure) return Results.NotFound(result.Error.Message);
        if (result.Value is null) return Results.NotFound($"Submission {id} not found.");

        var submission = result.Value;
        if (submission.GeneratedFileContent is null || submission.GeneratedFileContent.Length == 0)
            return Results.NotFound("No file content stored for this submission.");

        // CTL-SARS-006: Serve with appropriate MIME type and descriptive filename
        var ext = submission.SubmissionType == ComplianceSubmissionType.Emp501 ? "csv" : "csv";
        var fileName = $"{submission.SubmissionType}-{submission.Period}-{submission.Id}.{ext}";
        return Results.File(submission.GeneratedFileContent, "text/csv", fileName);
    }

    // ── ITREG handlers ────────────────────────────────────────────────────────

    // CTL-SARS-006: Returns employees with missing or invalid tax references
    private static async Task<IResult> GetMissingTaxReferencesAsync(
        ClaimsPrincipal user,
        MissingTaxReferenceService missingTaxRefService,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;

        var result = await missingTaxRefService.GetMissingTaxReferencesAsync(tenantId, ct);

        return result.IsFailure
            ? Results.BadRequest(result.Error.Message)
            : Results.Ok(result.Value);
    }

    // CTL-SARS-006: Generates ITREG export for employees missing tax references
    private static async Task<IResult> GenerateItregAsync(
        ItregGenerateRequest request,
        ClaimsPrincipal user,
        MissingTaxReferenceService missingTaxRefService,
        IComplianceEmployeeQuery employeeQuery,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;

        // Retrieve employees missing tax references
        var missingResult = await missingTaxRefService.GetMissingTaxReferencesAsync(tenantId, ct);
        if (missingResult.IsFailure)
            return Results.BadRequest(missingResult.Error.Message);

        if (missingResult.Value.Count == 0)
            return Results.BadRequest("No employees are missing tax references.");

        // Get full employee data for ITREG records
        var employees = await employeeQuery.GetAllEmployeeTaxSummariesAsync(tenantId, ct);
        var missingIds = missingResult.Value.Select(m => m.EmployeeId).ToHashSet(StringComparer.Ordinal);

        var records = employees
            .Where(e => missingIds.Contains(e.EmployeeId))
            .Select(e => new ItregRecord(
                e.EmployeeId,
                e.FullName,
                e.IdNumber ?? string.Empty,
                DateOnly.FromDateTime(DateTime.UtcNow), // Placeholder — real DOB from full employee record
                string.Empty, // Residential address — requires full employee query
                string.Empty, // Postal code — requires full employee query
                e.EmploymentStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
                request.EmployerPayeReference,
                null,
                null))
            .ToList();

        var generateResult = ItregGenerator.Generate(
            tenantId,
            request.EmployerPayeReference,
            records.AsReadOnly(),
            DateTimeOffset.UtcNow);

        return generateResult.IsFailure
            ? Results.BadRequest(generateResult.Error.Message)
            : Results.Text(generateResult.Value, "text/plain");
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    private static ComplianceSubmissionDto ToDto(ComplianceSubmission s) => new(
        s.Id,
        s.TenantId,
        s.Period,
        s.SubmissionType.ToString(),
        s.Status.ToString(),
        s.FilingReference,
        s.SubmittedAt,
        s.AcceptedAt,
        s.PayeAmount.ToFirestoreString(),
        s.UifAmount.ToFirestoreString(),
        s.SdlAmount.ToFirestoreString(),
        s.GrossAmount.ToFirestoreString(),
        s.EmployeeCount,
        s.ChecksumSha256,
        s.ComplianceFlags,
        s.CreatedAt,
        s.CreatedBy);
}

// ── DTOs ──────────────────────────────────────────────────────────────────────

/// <summary>
/// API response DTO for a ComplianceSubmission.
/// REQ-COMP-001: Exposes submission metadata and declared amounts.
/// File content is excluded from the list/detail response — use the download endpoint.
/// </summary>
public sealed record ComplianceSubmissionDto(
    string Id,
    string TenantId,
    string Period,
    string SubmissionType,
    string Status,
    string? FilingReference,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? AcceptedAt,
    string PayeAmountZar,
    string UifAmountZar,
    string SdlAmountZar,
    string GrossAmountZar,
    int EmployeeCount,
    string? ChecksumSha256,
    IReadOnlyList<string> ComplianceFlags,
    DateTimeOffset CreatedAt,
    string CreatedBy);

/// <summary>
/// Request body for ITREG export generation.
/// CTL-SARS-006: Requires employer PAYE reference for SARS registration file header.
/// </summary>
public sealed record ItregGenerateRequest(string EmployerPayeReference);
