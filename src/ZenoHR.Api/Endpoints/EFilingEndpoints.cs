// CTL-SARS-010: eFiling API endpoints — EMP201 submission, status query, submission history.
// TASK-131: EMP201 eFiling integration endpoints.
// REQ-SEC-002: All endpoints restricted to Director or HRManager roles.

using System.Security.Claims;
using ZenoHR.Api.Auth;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Services.EFiling;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for SARS eFiling integration.
/// CTL-SARS-010: Submit EMP201, query submission status, list submission history.
/// REQ-SEC-002: Director/HRManager roles only.
/// </summary>
public static class EFilingEndpoints
{
    public static IEndpointRouteBuilder MapEFilingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/efiling")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager"))
            .WithTags("EFiling");

        // POST /api/efiling/emp201/submit — submit EMP201 to SARS eFiling
        // CTL-SARS-010: Accepts EMP201 content (base64-encoded) and metadata for eFiling submission
        group.MapPost("/emp201/submit", SubmitEmp201Async)
            .WithName("SubmitEmp201")
            .Produces<EFilingSubmissionResultDto>(201)
            .Produces(400);

        // GET /api/efiling/submissions/{submissionId}/status — query submission status
        // CTL-SARS-010: Check current lifecycle status of a previously submitted return
        group.MapGet("/submissions/{submissionId}/status", GetSubmissionStatusAsync)
            .WithName("GetEFilingSubmissionStatus")
            .Produces<EFilingSubmissionResultDto>(200)
            .Produces(400)
            .Produces(404);

        // GET /api/efiling/submissions — list submission history for a tenant and tax year
        // CTL-SARS-010: Retrieve all eFiling submissions for a given tenant and tax year
        group.MapGet("/submissions", ListSubmissionsAsync)
            .WithName("ListEFilingSubmissions")
            .Produces<IReadOnlyList<EFilingSubmissionResultDto>>(200)
            .Produces(400);

        return app;
    }

    // ── Handlers ────────────────────────────────────────────────────────────────

    // CTL-SARS-010: Submit EMP201 to SARS eFiling
    private static async Task<IResult> SubmitEmp201Async(
        Emp201SubmitRequest request,
        ClaimsPrincipal user,
        Emp201SubmissionService submissionService,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId);
        if (string.IsNullOrWhiteSpace(tenantId))
            return Results.BadRequest("TenantId claim is missing from the authenticated user.");

        var submittedBy = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                       ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
                       ?? "unknown";

        byte[] content;
        try
        {
            content = Convert.FromBase64String(request.Emp201ContentBase64);
        }
        catch (FormatException)
        {
            return Results.BadRequest("Emp201ContentBase64 is not valid Base64.");
        }

        if (content.Length == 0)
            return Results.BadRequest("EMP201 content must not be empty.");

        var result = await submissionService.SubmitEmp201Async(
            tenantId, request.TaxYear, request.TaxPeriod, content, submittedBy, ct);

        if (result.IsFailure)
            return Results.BadRequest(result.Error.Message);

        return Results.Created(
            $"/api/efiling/submissions/{result.Value.SubmissionId}/status",
            ToDto(result.Value));
    }

    // CTL-SARS-010: Query submission status
    private static async Task<IResult> GetSubmissionStatusAsync(
        string submissionId,
        ClaimsPrincipal user,
        Emp201SubmissionService submissionService,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId);
        if (string.IsNullOrWhiteSpace(tenantId))
            return Results.BadRequest("TenantId claim is missing from the authenticated user.");

        var result = await submissionService.GetSubmissionStatusAsync(submissionId, tenantId, ct);

        if (result.IsFailure)
        {
            return result.Error.Code == ZenoHrErrorCode.ComplianceSubmissionNotFound
                ? Results.NotFound(result.Error.Message)
                : Results.BadRequest(result.Error.Message);
        }

        return Results.Ok(ToDto(result.Value));
    }

    // CTL-SARS-010: List submission history
    private static async Task<IResult> ListSubmissionsAsync(
        int? taxYear,
        ClaimsPrincipal user,
        IEFilingClient eFilingClient,
        CancellationToken ct)
    {
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId);
        if (string.IsNullOrWhiteSpace(tenantId))
            return Results.BadRequest("TenantId claim is missing from the authenticated user.");

        var year = taxYear ?? DateTimeOffset.UtcNow.Year;

        var result = await eFilingClient.GetSubmissionHistoryAsync(tenantId, year, ct);

        if (result.IsFailure)
            return Results.BadRequest(result.Error.Message);

        return Results.Ok(result.Value.Select(ToDto).ToList());
    }

    // ── DTO mapping ─────────────────────────────────────────────────────────────

    private static EFilingSubmissionResultDto ToDto(EFilingSubmissionResult r) => new(
        r.SubmissionId,
        r.Status.ToString(),
        r.SubmittedAt,
        r.SarsReferenceNumber,
        r.ErrorMessage,
        r.RetryCount);
}

// ── Request / Response DTOs ─────────────────────────────────────────────────────

/// <summary>
/// Request body for submitting an EMP201 to SARS eFiling.
/// CTL-SARS-010: Content is Base64-encoded CSV generated by the EMP201 generator.
/// </summary>
public sealed record Emp201SubmitRequest(
    int TaxYear,
    int TaxPeriod,
    string Emp201ContentBase64);

/// <summary>
/// eFiling submission result DTO returned by the API.
/// CTL-SARS-010: Maps <see cref="EFilingSubmissionResult"/> to an API-safe shape.
/// </summary>
public sealed record EFilingSubmissionResultDto(
    string SubmissionId,
    string Status,
    DateTimeOffset SubmittedAt,
    string? SarsReferenceNumber,
    string? ErrorMessage,
    int RetryCount);
