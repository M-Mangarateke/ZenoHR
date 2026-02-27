// CTL-SARS-001: RuleVersion — tracks which version of a statutory rule set applies.
// Statutory rule sets (PAYE brackets, UIF ceiling, ETI rates) change annually.
// A RuleVersion tells the system exactly which seed data governs a calculation.

namespace ZenoHR.Domain.Common;

/// <summary>
/// Identifies a specific version of a statutory rule set (e.g., PAYE 2025/2026 v1).
/// Rule sets are seeded from docs/seed-data/*.json and stored in Firestore
/// under the <c>statutory_rule_sets</c> collection.
/// </summary>
public sealed class RuleVersion : IEquatable<RuleVersion>, IComparable<RuleVersion>
{
    /// <summary>
    /// Identifies the rule set family (e.g., "paye", "uif-sdl", "eti", "bcea-leave").
    /// Matches the <c>rule_type</c> field in Firestore <c>statutory_rule_sets</c>.
    /// </summary>
    public string RuleSetId { get; }

    /// <summary>Monotonically increasing version number within the rule set family.</summary>
    public int Version { get; }

    /// <summary>The date from which this rule version takes effect.</summary>
    public DateOnly EffectiveFrom { get; }

    /// <summary>The date after which this rule version no longer applies. Null = currently active.</summary>
    public DateOnly? ObsoletedOn { get; }

    public RuleVersion(string ruleSetId, int version, DateOnly effectiveFrom, DateOnly? obsoletedOn = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleSetId);
        if (version < 1)
            throw new ArgumentOutOfRangeException(nameof(version), "Rule version must be >= 1.");
        if (obsoletedOn.HasValue && obsoletedOn.Value <= effectiveFrom)
            throw new ArgumentException(
                "ObsoletedOn must be after EffectiveFrom.", nameof(obsoletedOn));

        RuleSetId = ruleSetId;
        Version = version;
        EffectiveFrom = effectiveFrom;
        ObsoletedOn = obsoletedOn;
    }

    /// <summary>True if this version is the currently active one (not yet obsoleted).</summary>
    public bool IsCurrent => !ObsoletedOn.HasValue;

    /// <summary>True if this rule version was active on the given date.</summary>
    public bool WasActiveOn(DateOnly date) =>
        date >= EffectiveFrom && (!ObsoletedOn.HasValue || date < ObsoletedOn.Value);

    // ── Equality and comparison ──────────────────────────────────────────────

    public bool Equals(RuleVersion? other) =>
        other is not null &&
        RuleSetId == other.RuleSetId &&
        Version == other.Version;

    public override bool Equals(object? obj) => obj is RuleVersion other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(RuleSetId, Version);

    public int CompareTo(RuleVersion? other)
    {
        if (other is null) return 1;
        var idComp = string.Compare(RuleSetId, other.RuleSetId, StringComparison.Ordinal);
        return idComp != 0 ? idComp : Version.CompareTo(other.Version);
    }

    public static bool operator ==(RuleVersion? left, RuleVersion? right) =>
        left is null ? right is null : left.Equals(right);
    public static bool operator !=(RuleVersion? left, RuleVersion? right) => !(left == right);
    public static bool operator <(RuleVersion left, RuleVersion right) => left.CompareTo(right) < 0;
    public static bool operator >(RuleVersion left, RuleVersion right) => left.CompareTo(right) > 0;
    public static bool operator <=(RuleVersion left, RuleVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >=(RuleVersion left, RuleVersion right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{RuleSetId} v{Version} (effective {EffectiveFrom:yyyy-MM-dd})";
}
