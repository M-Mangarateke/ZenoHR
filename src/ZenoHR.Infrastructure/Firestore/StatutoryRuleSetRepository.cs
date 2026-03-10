// CTL-SARS-001: Statutory rule sets must be read from Firestore — no hardcoded rates.
// REQ-HR-003: Payroll engines depend on this repository to get current tax/levy rules.

using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Repository for reading statutory rule sets from the Firestore statutory_rule_sets collection.
/// Rule sets are cross-tenant (tenant_id = "SYSTEM") and are seeded by SaasAdmin.
/// CTL-SARS-001: PAYE, UIF, SDL, ETI rates must come from here.
/// REQ-HR-003: All payroll calculations use this repository.
/// </summary>
public sealed class StatutoryRuleSetRepository : BaseFirestoreRepository<StatutoryRuleSet>
{
    public StatutoryRuleSetRepository(FirestoreDb db, ILogger<StatutoryRuleSetRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "statutory_rule_sets";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.StatutoryRuleSetNotFound;

    protected override StatutoryRuleSet FromSnapshot(DocumentSnapshot snapshot)
    {
        // ToDictionary() returns Dictionary<string, object> from the Firestore SDK
        Dictionary<string, object> raw = snapshot.ToDictionary();
        // Wrap as nullable for consistent helper usage
        var data = raw.ToDictionary(kv => kv.Key, kv => (object?)kv.Value);

        return new StatutoryRuleSet
        {
            Id = snapshot.Id,
            RuleDomain = data.TryGetValue("rule_domain", out var rd) ? rd?.ToString() ?? "" : "",
            Version = data.TryGetValue("version", out var v) ? v?.ToString() ?? "" : "",
            EffectiveFrom = ParseDateOnly(data, "effective_from"),
            EffectiveTo = ParseDateOnlyNullable(data, "effective_to"),
            TaxYear = data.TryGetValue("tax_year", out var ty) ? ty?.ToString() ?? "" : "",
            Source = data.TryGetValue("source", out var src) ? src?.ToString() ?? "" : "",
            RuleData = ExtractRuleData(data),
            CreatedAt = data.TryGetValue("created_at", out var ca) && ca is Timestamp ts
                ? ts.ToDateTimeOffset()
                : DateTimeOffset.UtcNow,
        };
    }

    protected override Dictionary<string, object?> ToDocument(StatutoryRuleSet entity) =>
        throw new NotSupportedException("StatutoryRuleSet documents are written by SeedDataLoader only.");

    // ─── Public Query Methods ────────────────────────────────────────────────

    /// <summary>
    /// Returns all statutory rule sets (cross-tenant SYSTEM scope).
    /// Used by the Settings UI to display the full list for HR Manager / Director review.
    /// CTL-SARS-001, REQ-SEC-002
    /// </summary>
    public async Task<IReadOnlyList<StatutoryRuleSet>> GetAllAsync(CancellationToken ct = default)
    {
        var snapshot = await Db.Collection("statutory_rule_sets")
            .WhereEqualTo("tenant_id", "SYSTEM")
            .GetSnapshotAsync(ct);
        return snapshot.Documents.Select(FromSnapshot).ToList();
    }

    /// <summary>
    /// Partially updates named fields within the <c>rule_data</c> map of a statutory rule set.
    /// Only touches explicitly specified fields — does NOT overwrite metadata (rule_domain, version, etc.).
    /// Called exclusively from the Settings UI after Director / HRManager authorisation.
    /// CTL-SARS-001, REQ-HR-003, REQ-OPS-005
    /// </summary>
    public async Task<Result> UpdateRuleDataAsync(
        string documentId,
        IReadOnlyDictionary<string, object?> fieldUpdates,
        string updatedBy,
        CancellationToken ct = default)
    {
        var docRef = Db.Collection("statutory_rule_sets").Document(documentId);

        // Firestore UpdateAsync uses dot-notation strings for nested field paths.
        // "rule_data.{field}" targets only that sub-field without touching the rest of rule_data.
        var updates = new Dictionary<string, object?>(fieldUpdates.Count + 2);
        foreach (var (field, value) in fieldUpdates)
            updates[$"rule_data.{field}"] = value;

        // Stamp who last updated and when
        updates["last_updated_by"] = updatedBy;
        updates["last_updated_at"] = Timestamp.GetCurrentTimestamp();

        await docRef.UpdateAsync(updates, cancellationToken: ct);
        return Result.Success();
    }

    /// <summary>
    /// Gets the statutory rule set for a given domain that is effective on the specified date.
    /// Returns the most recently effective version if multiple overlap.
    /// CTL-SARS-001
    /// </summary>
    public async Task<Result<StatutoryRuleSet>> GetEffectiveAsync(
        string ruleDomain, DateOnly effectiveDate, CancellationToken ct = default)
    {
        // Query all rule sets for this domain, then filter by effective date in-process
        // (Firestore doesn't support date range overlap queries efficiently)
        var query = Collection
            .WhereEqualTo("rule_domain", ruleDomain)
            .WhereEqualTo("tenant_id", "SYSTEM");

        var ruleSets = await ExecuteQueryAsync(query, ct);

        var effective = ruleSets
            .Where(r => r.IsEffectiveOn(effectiveDate))
            .OrderByDescending(r => r.EffectiveFrom)
            .FirstOrDefault();

        return effective is null
            ? Result<StatutoryRuleSet>.Failure(ZenoHrErrorCode.StatutoryRuleSetNotFound,
                $"No effective rule set found for domain '{ruleDomain}' on {effectiveDate:yyyy-MM-dd}")
            : Result<StatutoryRuleSet>.Success(effective);
    }

    /// <summary>Gets a rule set by its exact document ID.</summary>
    public Task<Result<StatutoryRuleSet>> GetByIdAsync(string documentId, CancellationToken ct = default)
        => GetByIdAsync("SYSTEM", documentId, ct);

    /// <summary>
    /// Upserts a pending tax year rule set document.
    /// Called by TaxYearImportService during the import workflow.
    /// The document is written with status = "pending" — activation is a separate step.
    /// CTL-SARS-001, REQ-COMP-015
    /// </summary>
    public async Task<Result> UpsertPendingTaxYearAsync(
        string documentId,
        Dictionary<string, object?> fields,
        CancellationToken ct = default)
    {
        var docRef = Db.Collection("statutory_rule_sets").Document(documentId);
        await docRef.SetAsync(fields!, cancellationToken: ct);
        return Result.Success();
    }

    /// <summary>
    /// Updates an existing pending tax year document to status = "active".
    /// Fails if the document does not exist.
    /// Called by TaxYearImportService.ActivateAsync after regression passes.
    /// CTL-SARS-001, REQ-COMP-015
    /// </summary>
    public async Task<Result> SetStatusAsync(
        string documentId,
        string status,
        string actorUid,
        CancellationToken ct = default)
    {
        var docRef = Db.Collection("statutory_rule_sets").Document(documentId);
        var snapshot = await docRef.GetSnapshotAsync(ct);
        if (!snapshot.Exists)
            return Result.Failure(ZenoHrErrorCode.StatutoryRuleSetNotFound,
                $"Statutory rule set '{documentId}' not found — cannot set status.");

        var updates = new Dictionary<string, object?>
        {
            ["status"] = status,
            [$"{status}_at"] = Timestamp.GetCurrentTimestamp(),
            [$"{status}_by"] = actorUid,
        };
        await docRef.UpdateAsync(updates!, cancellationToken: ct);
        return Result.Success();
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static DateOnly ParseDateOnly(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var val) || val is null) return DateOnly.MinValue;

        // Firestore stores dates as Timestamp; seed data JSON stores as "yyyy-MM-dd" string
        if (val is Timestamp ts) return DateOnly.FromDateTime(ts.ToDateTime());
        if (val is string s && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d)) return d;
        return DateOnly.MinValue;
    }

    private static DateOnly? ParseDateOnlyNullable(Dictionary<string, object?> data, string key)
    {
        if (!data.TryGetValue(key, out var val) || val is null) return null;
        if (val is Timestamp ts) return DateOnly.FromDateTime(ts.ToDateTime());
        if (val is string s && DateOnly.TryParseExact(s, "yyyy-MM-dd", out var d)) return d;
        return null;
    }

    private static Dictionary<string, object?> ExtractRuleData(Dictionary<string, object?> data)
    {
        if (!data.TryGetValue("rule_data", out var rd)) return [];
        return rd is IDictionary<string, object> nested
            ? nested.ToDictionary(kv => kv.Key, kv => (object?)kv.Value)
            : [];
    }
}
