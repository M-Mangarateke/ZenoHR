// REQ-COMP-001, REQ-COMP-002, CTL-SARS-006: Compliance API endpoints.
// TASK-091: List submissions, generate EMP201/EMP501, download filing file.
// Director/HRManager roles only (REQ-SEC-002).

using System.Security.Claims;
using ZenoHR.Api.Auth;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Infrastructure.Services.Filing;
using ZenoHR.Module.Compliance.Entities;
using ZenoHR.Module.Compliance.Enums;

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
            .WithTags("Compliance");

        // GET /api/compliance/submissions?period={period} — list submissions for tenant
        group.MapGet("/submissions", ListSubmissionsAsync)
            .WithName("ListComplianceSubmissions")
            .Produces<IReadOnlyList<ComplianceSubmissionDto>>(200);

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

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListSubmissionsAsync(
        string? period,
        ClaimsPrincipal user,
        ComplianceSubmissionRepository repo,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId)!;

        if (!string.IsNullOrWhiteSpace(period))
        {
            var byPeriod = await repo.ListByPeriodAsync(tenantId, period, ct);
            if (byPeriod.IsFailure) return Results.Problem(byPeriod.Error.Message);
            return Results.Ok(byPeriod.Value.Select(ToDto));
        }

        var all = await repo.ListByTenantAsync(tenantId, ct: ct);
        if (all.IsFailure) return Results.Problem(all.Error.Message);
        return Results.Ok(all.Value.Select(ToDto));
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
