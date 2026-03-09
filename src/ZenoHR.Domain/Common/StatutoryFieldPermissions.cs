// CTL-SARS-001, REQ-HR-003, REQ-SEC-002
// Defines which fields within each statutory rule set's rule_data may be edited via the Settings UI.
// All annually-changing SARS and statutory figures are now editable by HR Manager / Director.
// Structurally fixed fields (rule_domain, source, irp5_code, policy descriptions) are excluded.

namespace ZenoHR.Domain.Common;

/// <summary>
/// Identifies how a statutory field should be rendered in the Settings UI editor.
/// </summary>
public enum FieldEditType
{
    /// <summary>A single numeric or text value — rendered as one <c>&lt;input&gt;</c>.</summary>
    Scalar,

    /// <summary>A flat dictionary of key-value pairs — rendered as a group of labeled inputs.</summary>
    NestedObject,

    /// <summary>A list of flat dictionaries — rendered as an editable HTML table (one row per item).</summary>
    ArrayOfObjects,
}

/// <summary>
/// Describes a single editable field within a statutory rule set's <c>rule_data</c> map.
/// </summary>
/// <param name="Key">The top-level key within <c>rule_data</c> (e.g. "tax_brackets", "rebates").</param>
/// <param name="Label">Human-readable label shown in the Settings UI.</param>
/// <param name="EditType">How the field should be rendered.</param>
public sealed record FieldSpec(string Key, string Label, FieldEditType EditType);

/// <summary>
/// Defines the whitelist of <c>rule_data</c> fields that HR Manager / Director may edit
/// via the Statutory Rates settings page for each rule domain, together with rendering metadata.
///
/// Design intent:
/// - All annually-published statutory figures are editable (SARS Budget Speech, DoEL gazette, NMW).
/// - Structurally fixed metadata (source URLs, policy descriptions, IRP5 codes) are excluded.
/// - ETI tiers are excluded — the ETI Act (valid to 2029) rarely changes and requires legal review.
/// - Every edit is audited (REQ-OPS-005). The whitelist prevents accidental corruption of
///   read-only metadata, but does not restrict edits to confirmed gazette values.
/// CTL-SARS-001, REQ-HR-003
/// </summary>
public static class StatutoryFieldPermissions
{
    // ── Allowed field whitelists per domain ───────────────────────────────────

    private static readonly Dictionary<string, IReadOnlySet<string>> _domainFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [RuleDomains.SarsPaye] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "tax_brackets",               // array of 7 objects — updated every Budget Speech
                "rebates",                    // object — primary / secondary / tertiary rebates
                "tax_thresholds",             // object — below_65 / 65_to_74 / 75_and_over
                "retirement_fund_deduction",  // object — annual_cap + rate_of_taxable_income
                "data_status"
            },
            [RuleDomains.SarsMsftc] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "monthly_credits",  // object — principal / first_dependant / each_additional
                "annual_credits",   // object — same keys, = monthly × 12
                "data_status"
            },
            [RuleDomains.SarsTravel] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "reimbursive_rate_rands_per_km",  // scalar decimal (e.g. 4.95)
                "reimbursive_rate_cents_per_km",  // scalar int (derived: × 100)
                "fixed_cost_table",               // array of 9 objects — vehicle cost bands
                "subsistence_allowance",          // nested object — domestic per-diem rates
                "data_status"
            },
            [RuleDomains.SarsUifSdl] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "uif",  // object — rates, ceiling, maximums
                "sdl",  // object — rate, exemption threshold
                "data_status"
            },
            [RuleDomains.Nmw] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "general_workers",
                "domestic_workers",
                "farm_workers",
                "expanded_public_works_programme",
                "eti_relevance",
                "data_status"
            },
            [RuleDomains.BceaEarningsThreshold] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "annual_threshold",
                "monthly_threshold",
                "weekly_threshold",
                "daily_threshold",
                "data_status"
            },
        };

    /// <summary>Field allowed for every rule domain (promote PROVISIONAL → CONFIRMED).</summary>
    private static readonly IReadOnlySet<string> _universal =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "data_status" };

    // ── Field type / rendering metadata per domain ────────────────────────────

    private static readonly Dictionary<string, IReadOnlyList<FieldSpec>> _fieldSpecs =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [RuleDomains.SarsPaye] =
            [
                new("tax_brackets",              "Tax Brackets (7 brackets)",           FieldEditType.ArrayOfObjects),
                new("rebates",                   "Rebates",                             FieldEditType.NestedObject),
                new("tax_thresholds",            "Tax Thresholds",                      FieldEditType.NestedObject),
                new("retirement_fund_deduction", "Retirement Fund Deduction",           FieldEditType.NestedObject),
            ],
            [RuleDomains.SarsMsftc] =
            [
                new("monthly_credits", "Monthly Medical Credits (ZAR/month)", FieldEditType.NestedObject),
                new("annual_credits",  "Annual Medical Credits (ZAR/year)",   FieldEditType.NestedObject),
            ],
            [RuleDomains.SarsTravel] =
            [
                new("reimbursive_rate_rands_per_km", "Reimbursive Rate (R/km)",     FieldEditType.Scalar),
                new("reimbursive_rate_cents_per_km", "Reimbursive Rate (cents/km)", FieldEditType.Scalar),
                new("fixed_cost_table",              "Fixed Cost Table (9 bands)",   FieldEditType.ArrayOfObjects),
                new("subsistence_allowance",         "Subsistence Allowance",        FieldEditType.NestedObject),
            ],
            [RuleDomains.SarsUifSdl] =
            [
                new("uif", "UIF",  FieldEditType.NestedObject),
                new("sdl", "SDL",  FieldEditType.NestedObject),
            ],
            [RuleDomains.Nmw] =
            [
                new("general_workers",                 "General Workers",              FieldEditType.NestedObject),
                new("domestic_workers",                "Domestic Workers",             FieldEditType.NestedObject),
                new("farm_workers",                    "Farm Workers",                 FieldEditType.NestedObject),
                new("expanded_public_works_programme", "EPWP (Expanded Public Works)", FieldEditType.NestedObject),
                new("eti_relevance",                   "ETI Relevance",                FieldEditType.NestedObject),
            ],
            [RuleDomains.BceaEarningsThreshold] =
            [
                new("annual_threshold",  "Annual Threshold (ZAR)",  FieldEditType.Scalar),
                new("monthly_threshold", "Monthly Threshold (ZAR)", FieldEditType.Scalar),
                new("weekly_threshold",  "Weekly Threshold (ZAR)",  FieldEditType.Scalar),
                new("daily_threshold",   "Daily Threshold (ZAR)",   FieldEditType.Scalar),
            ],
        };

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the set of field names that may be updated for a given rule domain.
    /// </summary>
    public static IReadOnlySet<string> GetAllowedFields(string ruleDomain) =>
        _domainFields.TryGetValue(ruleDomain, out var fields) ? fields : _universal;

    /// <summary>
    /// Returns ordered field specs (rendering metadata) for the given rule domain.
    /// Returns an empty list for domains with no spec (e.g. ETI, public holidays — data_status only).
    /// </summary>
    public static IReadOnlyList<FieldSpec> GetFieldSpecs(string ruleDomain) =>
        _fieldSpecs.TryGetValue(ruleDomain, out var specs) ? specs : [];

    /// <summary>
    /// Checks whether a field update request is entirely within the allowed-fields whitelist.
    /// Returns the names of any disallowed fields, or an empty list if all fields are permitted.
    /// </summary>
    public static IReadOnlyList<string> GetDisallowedFields(
        string ruleDomain, IEnumerable<string> requestedFields)
    {
        var allowed = GetAllowedFields(ruleDomain);
        return requestedFields.Where(f => !allowed.Contains(f)).ToList();
    }

    /// <summary>Returns true if the field name is permitted for the given rule domain.</summary>
    public static bool IsFieldAllowed(string ruleDomain, string fieldName) =>
        GetAllowedFields(ruleDomain).Contains(fieldName);
}
