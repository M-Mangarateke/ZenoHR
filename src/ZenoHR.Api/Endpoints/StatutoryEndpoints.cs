// CTL-SARS-001, REQ-HR-003, REQ-SEC-002, REQ-OPS-005
// Statutory rule set settings endpoints — Director / HRManager only.
// GET  /api/settings/statutory              — list all seeded rule sets
// PUT  /api/settings/statutory/{id}/rule-data — partial field update with audit trail

using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using ZenoHR.Api.Auth;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Infrastructure.Audit;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Module.Audit.Domain;

namespace ZenoHR.Api.Endpoints;

/// <summary>
/// Minimal API endpoints for Statutory Rates management in the Settings section.
/// Allows HR Manager / Director to update PROVISIONAL statutory figures when the confirmed
/// gazette values are published — without requiring code changes or redeployment.
/// REQ-SEC-002: Director and HRManager only. REQ-OPS-005: every change is audited.
/// </summary>
public static class StatutoryEndpoints
{
    // Field whitelist is defined in ZenoHR.Domain.Common.StatutoryFieldPermissions (testable without Firestore)

    public static IEndpointRouteBuilder MapStatutoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/settings/statutory")
            .RequireAuthorization(p => p.RequireRole("Director", "HRManager"))
            .RequireRateLimiting("general-api")
            .WithTags("Settings");

        // GET /api/settings/statutory — list all seeded rule sets
        group.MapGet("/", ListAllAsync)
            .WithName("ListStatutoryRuleSets")
            .Produces<IReadOnlyList<StatutoryRuleSetDto>>(200);

        // PUT /api/settings/statutory/{id}/rule-data — partial field update
        group.MapPut("/{id}/rule-data", UpdateRuleDataAsync)
            .WithName("UpdateStatutoryRuleData")
            .Produces<StatutoryRuleSetDto>(200)
            .Produces<ProblemDetails>(400)
            .Produces(404);

        return app;
    }

    // ── Handlers ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ListAllAsync(
        StatutoryRuleSetRepository repo,
        CancellationToken ct)
    {
        var ruleSets = await repo.GetAllAsync(ct);
        return Results.Ok(ruleSets.Select(ToDto).OrderBy(r => r.RuleDomain));
    }

    private static async Task<IResult> UpdateRuleDataAsync(
        string id,
        [FromBody] UpdateRuleDataRequest req,
        ClaimsPrincipal user,
        StatutoryRuleSetRepository repo,
        AuditEventWriter auditWriter,
        CancellationToken ct)
    {
        // Resolve actor identity for audit trail
        var actorId = user.FindFirstValue(ZenoHrClaimNames.EmployeeId)
                   ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var actorRole = user.FindFirstValue(ZenoHrClaimNames.SystemRoleJwt) ?? "HRManager";

        // Fetch the existing rule set
        var getResult = await repo.GetByIdAsync(id, ct);
        if (getResult.IsFailure) return Results.NotFound(getResult.Error.Message);

        var ruleSet = getResult.Value!;

        // Validate submitted fields against the whitelist (StatutoryFieldPermissions — CTL-SARS-001)
        var disallowed = StatutoryFieldPermissions.GetDisallowedFields(ruleSet.RuleDomain, req.Fields.Keys);

        if (disallowed.Count > 0)
        {
            var allowed = StatutoryFieldPermissions.GetAllowedFields(ruleSet.RuleDomain);
            return Results.BadRequest(
                $"Field(s) not permitted for rule domain '{ruleSet.RuleDomain}': " +
                string.Join(", ", disallowed) +
                ". Only the following fields may be updated: " +
                string.Join(", ", allowed) + ".");
        }

        if (req.Fields.Count == 0)
            return Results.BadRequest("At least one field must be provided in 'fields'.");

        // Capture previous data_status for the audit metadata
        var previousStatus = ruleSet.RuleData.TryGetValue("data_status", out var ps)
            ? ps?.ToString() ?? ""
            : "";
        var newStatus = req.Fields.TryGetValue("data_status", out var ns)
            ? ns?.ToString() ?? previousStatus
            : previousStatus;

        // Perform partial update
        var updateResult = await repo.UpdateRuleDataAsync(id, req.Fields, actorId, ct);
        if (updateResult.IsFailure) return Results.Problem(updateResult.Error.Message);

        // Write audit event — statutory changes are always audited (REQ-OPS-005)
        var auditMetadata = JsonSerializer.Serialize(new
        {
            rule_domain = ruleSet.RuleDomain,
            fields_updated = string.Join(", ", req.Fields.Keys),
            previous_data_status = previousStatus,
            new_data_status = newStatus
        });

        await auditWriter.WriteAsync(new WriteAuditEventRequest
        {
            TenantId     = "SYSTEM",
            ActorId      = actorId,
            ActorRole    = actorRole,
            Action       = AuditAction.Update,
            ResourceType = AuditResourceType.StatutoryRuleSet,
            ResourceId   = id,
            Metadata     = auditMetadata,
            OccurredAt   = DateTimeOffset.UtcNow
        }, ct);

        // Re-fetch to return the updated state
        var refreshed = await repo.GetByIdAsync(id, ct);
        if (refreshed.IsFailure) return Results.Ok(new { message = "Updated successfully." });
        return Results.Ok(ToDto(refreshed.Value!));
    }

    // ── DTO ──────────────────────────────────────────────────────────────────

    private static StatutoryRuleSetDto ToDto(ZenoHR.Domain.Common.StatutoryRuleSet r) => new(
        r.Id,
        r.RuleDomain,
        r.Version,
        r.EffectiveFrom.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        r.EffectiveTo?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        r.TaxYear,
        r.Source,
        r.RuleData.TryGetValue("data_status", out var ds) ? ds?.ToString() ?? "" : "",
        r.RuleData.TryGetValue("source_url", out var su) ? su?.ToString() ?? "" : "",
        r.RuleData,
        r.CreatedAt,
        AllowedFieldsFor(r.RuleDomain));

    private static List<string> AllowedFieldsFor(string ruleDomain) =>
        StatutoryFieldPermissions.GetAllowedFields(ruleDomain).ToList();
}

// ── Request / Response DTOs ───────────────────────────────────────────────────

public sealed record UpdateRuleDataRequest(
    IReadOnlyDictionary<string, object?> Fields);

public sealed record StatutoryRuleSetDto(
    string Id,
    string RuleDomain,
    string Version,
    string EffectiveFrom,
    string? EffectiveTo,
    string TaxYear,
    string Source,
    string DataStatus,
    string SourceUrl,
    IReadOnlyDictionary<string, object?> RuleData,
    DateTimeOffset CreatedAt,
    IReadOnlyList<string> EditableFields);
