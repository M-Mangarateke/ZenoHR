// REQ-HR-003: Payroll calculations must use statutory rule sets from Firestore.
// CTL-SARS-001: PAYE, UIF, SDL, ETI rates sourced from StatutoryRuleSet documents.
// CTL-BCEA-001 to CTL-BCEA-008: BCEA working time and leave rules from StatutoryRuleSet.
// Critical rule: NEVER hardcode statutory values — always read from this type.

namespace ZenoHR.Domain.Common;

/// <summary>
/// Represents a statutory rule set document from Firestore (collection: statutory_rule_sets).
/// Each instance contains the rules for a specific domain (PAYE, UIF/SDL, ETI, BCEA leave, etc.)
/// for a specific effective period.
/// REQ-HR-003: All payroll calculations read from StatutoryRuleSet — no hardcoded rates.
/// </summary>
public sealed record StatutoryRuleSet
{
    /// <summary>Firestore document ID. Format: {RULE_DOMAIN}_{VERSION_YEAR} e.g. SARS_PAYE_2026</summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>e.g. "SARS_PAYE", "SARS_UIF_SDL", "SARS_ETI", "BCEA_LEAVE", "BCEA_WORKING_TIME"</summary>
    public string RuleDomain { get; init; } = string.Empty;

    /// <summary>e.g. "2026.1.0"</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>First date this rule set is effective (inclusive).</summary>
    public DateOnly EffectiveFrom { get; init; }

    /// <summary>Last date this rule set is effective (inclusive). Null = open-ended.</summary>
    public DateOnly? EffectiveTo { get; init; }

    /// <summary>Tax year label for SARS rules. e.g. "2026". Empty for BCEA rules.</summary>
    public string TaxYear { get; init; } = string.Empty;

    /// <summary>
    /// The complete rule data as a nested dictionary — matches the JSON structure from the seed file.
    /// Payroll engines read specific keys from this map (e.g. rule_data["tax_brackets"]).
    /// REQ-HR-003: Engine reads this at runtime — no hardcoded constants.
    /// </summary>
    public IReadOnlyDictionary<string, object?> RuleData { get; init; } =
        new Dictionary<string, object?>();

    /// <summary>Source reference (legislation URL, SARS publication, etc.)</summary>
    public string Source { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Returns true if this rule set covers the given date.</summary>
    public bool IsEffectiveOn(DateOnly date) =>
        date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo.Value);
}

/// <summary>
/// Well-known rule domain identifiers. Used to look up rule sets from the repository.
/// </summary>
public static class RuleDomains
{
    public const string SarsPaye = "SARS_PAYE";
    public const string SarsUifSdl = "SARS_UIF_SDL";
    public const string SarsEti = "SARS_ETI";
    public const string BceaLeave = "BCEA_LEAVE";
    public const string BceaWorkingTime = "BCEA_WORKING_TIME";
    public const string BceaNoticeSeverance = "BCEA_NOTICE_SEVERANCE";
    public const string SaPublicHolidays = "SA_PUBLIC_HOLIDAYS";
}
