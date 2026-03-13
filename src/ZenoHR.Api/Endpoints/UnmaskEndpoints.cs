// CTL-POPIA-002, VUL-020, REQ-SEC-001: PII unmask endpoint with purpose code enforcement.
// POST /api/employees/{employeeId}/unmask — validates purpose, returns unmasked field, logs audit.
// Director and HRManager roles only. 422 for invalid purpose, 403 for unauthorized, 404 if not found.

using System.Security.Claims;
using ZenoHR.Api.Auth;
using ZenoHR.Api.DTOs;
using ZenoHR.Infrastructure.Audit;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.Audit.Domain;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoint for unmasking sensitive PII fields on employee records.
/// CTL-POPIA-002: Purpose limitation — every unmask requires an approved purpose code.
/// VUL-020: Closes the unmask-without-purpose vulnerability.
/// </summary>
public static class UnmaskEndpoints
{
    // CTL-POPIA-002: Allowed field names for unmask operations.
    private static readonly HashSet<string> AllowedFields = ["national_id", "tax_reference", "bank_account"];

    public static IEndpointRouteBuilder MapUnmaskEndpoints(this IEndpointRouteBuilder app)
    {
        // REQ-SEC-002: Director and HRManager only — no Manager or Employee access.
        app.MapPost("/api/employees/{employeeId}/unmask", UnmaskFieldAsync)
            .RequireAuthorization(policy => policy.RequireRole("Director", "HRManager"))
            .RequireRateLimiting("api")   // REQ-SEC-003: closes VUL-007
            .WithTags("Employees")
            .WithName("UnmaskEmployeeField")
            .Produces<UnmaskResponse>(200)
            .Produces(403)
            .Produces(404)
            .ProducesValidationProblem(422);

        return app;
    }

    // CTL-POPIA-002: Unmask handler — validates purpose code, fetches raw value, records audit.
    private static async Task<IResult> UnmaskFieldAsync(
        string employeeId,
        UnmaskRequest request,
        ClaimsPrincipal user,
        EmployeeRepository repo,
        UnmaskAuditService auditService,
        AuditEventWriter auditWriter,
        CancellationToken ct)
    {
        // ── Validate request ──────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.FieldName))
            return Results.UnprocessableEntity(new { error = "FieldName is required." });

        if (!AllowedFields.Contains(request.FieldName))
            return Results.UnprocessableEntity(new { error = $"Invalid field name: '{request.FieldName}'. Allowed: national_id, tax_reference, bank_account." });

        if (!UnmaskRequest.ApprovedPurposeCodes.Contains(request.PurposeCode))
            return Results.UnprocessableEntity(new { error = $"Invalid purpose code: '{request.PurposeCode}'. Must be one of the POPIA-approved purpose codes." });

        // CTL-POPIA-002: AUDIT_REVIEW and HR_INVESTIGATION require justification text.
        if (request.PurposeCode is "AUDIT_REVIEW" or "HR_INVESTIGATION"
            && string.IsNullOrWhiteSpace(request.Justification))
        {
            return Results.UnprocessableEntity(new { error = $"Justification is required for purpose code '{request.PurposeCode}'." });
        }

        // ── Resolve actor from JWT ────────────────────────────────────────────
        var tenantId = user.FindFirstValue(ZenoHrClaimNames.TenantId);
        if (string.IsNullOrWhiteSpace(tenantId))
            return Results.Forbid();

        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                     ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var actorRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "";

        // ── Fetch employee (tenant-scoped) ────────────────────────────────────
        var result = await repo.GetByEmployeeIdAsync(tenantId, employeeId, ct);
        if (result.IsFailure)
            return Results.NotFound(new { error = $"Employee '{employeeId}' not found." });

        var employee = result.Value;

        // ── Extract raw (unmasked) field value ────────────────────────────────
        var rawValue = request.FieldName switch
        {
            "national_id" => employee.NationalIdOrPassport,
            "tax_reference" => employee.TaxReference,
            "bank_account" => employee.BankAccountRef,
            _ => null // Already validated above — should never happen.
        };

        if (string.IsNullOrEmpty(rawValue))
            return Results.NotFound(new { error = $"Field '{request.FieldName}' has no value for employee '{employeeId}'." });

        // ── Record audit (CTL-POPIA-002) ──────────────────────────────────────
        var now = DateTimeOffset.UtcNow;
        var auditRecord = auditService.CreateUnmaskAuditRecord(
            tenantId: tenantId,
            actorId: actorId,
            actorRole: actorRole,
            employeeId: employeeId,
            fieldName: request.FieldName,
            purposeCode: request.PurposeCode,
            justification: request.Justification,
            occurredAt: now);

        // Persist the audit event via AuditEventWriter — atomically maintains the SHA-256 hash chain.
        // AuditEventWriter reads the chain head inside a Firestore transaction, so previousEventHash
        // is always correct (not null except for genesis). This closes the Sev-1 hash chain gap.
        var auditResult = await auditWriter.WriteAsync(new WriteAuditEventRequest
        {
            TenantId     = auditRecord.TenantId,
            ActorId      = auditRecord.ActorId,
            ActorRole    = auditRecord.ActorRole,
            Action       = AuditAction.Read,
            ResourceType = AuditResourceType.Employee,
            ResourceId   = auditRecord.EmployeeId,
            Metadata     = auditRecord.Metadata,
            OccurredAt   = auditRecord.OccurredAt,
        }, ct);

        // AuditEventWriter returns Result<AuditEvent> — extract the event ID for the response.
        var auditEventId = auditResult.IsSuccess ? auditResult.Value.EventId : "audit-write-failed";

        return Results.Ok(new UnmaskResponse(
            EmployeeId: employeeId,
            FieldName: request.FieldName,
            Value: rawValue,
            PurposeCode: request.PurposeCode,
            AuditEventId: auditEventId));
    }
}

/// <summary>
/// Response DTO for a successful unmask operation.
/// CTL-POPIA-002: Includes the audit event ID for traceability.
/// </summary>
public sealed record UnmaskResponse(
    string EmployeeId,
    string FieldName,
    string Value,
    string PurposeCode,
    string AuditEventId);
