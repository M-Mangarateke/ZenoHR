// CTL-SARS-001: Statutory seed data loader — writes PAYE, UIF/SDL, ETI, BCEA, holidays to Firestore.
// REQ-HR-003: All payroll calculation rules originate from these seeded documents.
// Critical rule: NEVER hardcode statutory values — this loader is the sole source of truth.

using Google.Cloud.Firestore;
using System.Reflection;
using System.Text.Json;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Infrastructure.Seeding;

/// <summary>
/// Seeds statutory rule set documents into the Firestore statutory_rule_sets collection.
/// Rule sets are cross-tenant (tenant_id = "SYSTEM") and are read by all payroll engines.
///
/// Seed files are embedded resources in ZenoHR.Infrastructure:
///   - sars-paye.json         (SARS_PAYE)
///   - sars-uif-sdl.json      (SARS_UIF_SDL)
///   - sars-eti.json          (SARS_ETI)
///   - bcea-leave.json        (BCEA_LEAVE)
///   - bcea-working-time.json (BCEA_WORKING_TIME)
///   - bcea-notice-severance.json (BCEA_NOTICE_SEVERANCE)
///   - sa-public-holidays.json (SA_PUBLIC_HOLIDAYS)
///
/// CTL-SARS-001, REQ-HR-003
/// </summary>
public sealed class StatutoryRuleSetLoader
{
    private static readonly Assembly _assembly = typeof(StatutoryRuleSetLoader).Assembly;

    // Logical resource names embedded in ZenoHR.Infrastructure.csproj
    private static readonly string[] _resourceNames =
    [
        "ZenoHR.Infrastructure.SeedData.sars-paye.json",
        "ZenoHR.Infrastructure.SeedData.sars-uif-sdl.json",
        "ZenoHR.Infrastructure.SeedData.sars-eti.json",
        "ZenoHR.Infrastructure.SeedData.bcea-leave.json",
        "ZenoHR.Infrastructure.SeedData.bcea-working-time.json",
        "ZenoHR.Infrastructure.SeedData.bcea-notice-severance.json",
        "ZenoHR.Infrastructure.SeedData.sa-public-holidays.json",
    ];

    private readonly FirestoreDb _db;

    public StatutoryRuleSetLoader(FirestoreDb db)
    {
        ArgumentNullException.ThrowIfNull(db);
        _db = db;
    }

    /// <summary>
    /// Seeds all statutory rule sets into Firestore. Idempotent — existing documents are overwritten
    /// (SetAsync upsert). Safe to run on every startup or during tenant provisioning.
    /// Returns the count of documents written.
    /// CTL-SARS-001
    /// </summary>
    public async Task<Result<int>> LoadAllAsync(CancellationToken ct = default)
    {
        var written = 0;
        var errors = new List<string>();

        foreach (var resourceName in _resourceNames)
        {
            var result = await LoadResourceAsync(resourceName, ct);
            if (result.IsSuccess)
                written++;
            else
                errors.Add($"{resourceName}: {result.Error.Message}");
        }

        if (errors.Count > 0)
            return Result<int>.Failure(ZenoHrErrorCode.FirestoreUnavailable,
                $"Seeding completed with {errors.Count} error(s): {string.Join("; ", errors)}");

        return Result<int>.Success(written);
    }

    /// <summary>
    /// Seeds a single embedded resource. Exposed for testing individual rule sets.
    /// CTL-SARS-001
    /// </summary>
    public async Task<Result> LoadResourceAsync(string resourceName, CancellationToken ct = default)
    {
        try
        {
            var json = ReadEmbeddedResource(resourceName);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var ruleDomain = root.TryGetProperty("rule_domain", out var rd)
                ? rd.GetString() ?? ""
                : throw new InvalidOperationException($"Missing rule_domain in {resourceName}");

            var version = root.TryGetProperty("version", out var v)
                ? v.GetString() ?? ""
                : throw new InvalidOperationException($"Missing version in {resourceName}");

            var docId = BuildDocumentId(ruleDomain, version);
            var firestoreMap = BuildFirestoreDocument(root, ruleDomain, version);

            var docRef = _db.Collection("statutory_rule_sets").Document(docId);
            await docRef.SetAsync(firestoreMap, cancellationToken: ct);

            return Result.Success();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result.Failure(ZenoHrErrorCode.FirestoreUnavailable,
                $"Failed to seed {resourceName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the document IDs that would be seeded by LoadAllAsync.
    /// Useful for test assertions.
    /// </summary>
    public static IReadOnlyList<string> GetExpectedDocumentIds()
    {
        var ids = new List<string>();
        foreach (var resourceName in _resourceNames)
        {
            var json = ReadEmbeddedResource(resourceName);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var ruleDomain = root.GetProperty("rule_domain").GetString() ?? "";
            var version = root.GetProperty("version").GetString() ?? "";
            ids.Add(BuildDocumentId(ruleDomain, version));
        }
        return ids;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Derives a deterministic Firestore document ID from rule_domain + version year.</summary>
    public static string BuildDocumentId(string ruleDomain, string version)
    {
        // version format: "2026.1.0" → year = "2026"
        var year = version.Split('.')[0];
        return $"{ruleDomain}_{year}";
    }

    private static Dictionary<string, object?> BuildFirestoreDocument(
        JsonElement root, string ruleDomain, string version)
    {
        // Start with the full JSON converted to a Firestore map
        var fullMap = FirestoreJsonConverter.ToFirestoreMap(root);

        // Remove the $schema key (JSON Schema validator — not stored in Firestore)
        fullMap.Remove("$schema");

        // Extract top-level metadata fields; put remaining fields under "rule_data"
        var metaKeys = new HashSet<string>
        {
            "rule_domain", "version", "effective_from", "effective_to",
            "tax_year", "source", "source_url"
        };

        var ruleData = new Dictionary<string, object?>();
        foreach (var kv in fullMap)
        {
            if (!metaKeys.Contains(kv.Key))
                ruleData[kv.Key] = kv.Value;
        }

        var now = Timestamp.GetCurrentTimestamp();
        return new Dictionary<string, object?>
        {
            ["tenant_id"] = "SYSTEM",         // Cross-tenant rule sets
            ["rule_domain"] = ruleDomain,
            ["version"] = version,
            ["effective_from"] = fullMap.TryGetValue("effective_from", out var ef) ? ef : null,
            ["effective_to"] = fullMap.TryGetValue("effective_to", out var et) ? et : null,
            ["tax_year"] = fullMap.TryGetValue("tax_year", out var ty) ? ty : "",
            ["source"] = fullMap.TryGetValue("source", out var src) ? src : "",
            ["source_url"] = fullMap.TryGetValue("source_url", out var url) ? url : "",
            ["rule_data"] = ruleData,
            ["created_at"] = now,
            ["seeded_at"] = now,
            ["seeded_by"] = "StatutoryRuleSetLoader",
            ["schema_version"] = "1.0",
        };
    }

    private static string ReadEmbeddedResource(string resourceName)
    {
        using var stream = _assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource not found: {resourceName}. " +
                $"Available resources: {string.Join(", ", _assembly.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
