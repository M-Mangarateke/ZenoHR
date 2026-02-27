// REQ-HR-001: EffectivePeriod — date-range value object used for employment contracts,
//             leave balances, statutory rule validity, payroll periods.
// REQ-HR-003: Payroll periods use EffectivePeriod for month/week boundary calculations.

namespace ZenoHR.Domain.Common;

/// <summary>
/// Represents an inclusive date range with a known start and an optional end.
/// A null <see cref="End"/> means the period is open-ended (still active).
/// </summary>
public sealed class EffectivePeriod : IEquatable<EffectivePeriod>
{
    public DateOnly Start { get; }

    /// <summary>Inclusive end date. Null means open-ended (no expiry).</summary>
    public DateOnly? End { get; }

    public EffectivePeriod(DateOnly start, DateOnly? end = null)
    {
        if (end.HasValue && end.Value < start)
            throw new ArgumentException(
                $"EffectivePeriod end ({end:yyyy-MM-dd}) cannot be before start ({start:yyyy-MM-dd}).",
                nameof(end));

        Start = start;
        End = end;
    }

    // ── Factory helpers ──────────────────────────────────────────────────────

    public static EffectivePeriod OpenEndedFrom(DateOnly start) => new(start);

    public static EffectivePeriod ForMonth(int year, int month)
    {
        var start = new DateOnly(year, month, 1);
        var end = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        return new EffectivePeriod(start, end);
    }

    // ── Membership ───────────────────────────────────────────────────────────

    /// <summary>Returns true if <paramref name="date"/> falls within this period (inclusive).</summary>
    public bool Contains(DateOnly date) =>
        date >= Start && (!End.HasValue || date <= End.Value);

    /// <summary>True if this period is still active as of today (UTC).</summary>
    public bool IsActive => Contains(DateOnly.FromDateTime(DateTime.UtcNow));

    /// <summary>True if this period has a defined end date that has passed.</summary>
    public bool IsExpired => End.HasValue && End.Value < DateOnly.FromDateTime(DateTime.UtcNow);

    // ── Overlap ──────────────────────────────────────────────────────────────

    /// <summary>Returns true if this period overlaps with <paramref name="other"/>.</summary>
    public bool Overlaps(EffectivePeriod other)
    {
        // Two periods overlap if neither ends before the other starts.
        var thisEnd = End ?? DateOnly.MaxValue;
        var otherEnd = other.End ?? DateOnly.MaxValue;
        return Start <= otherEnd && thisEnd >= other.Start;
    }

    // ── Equality ─────────────────────────────────────────────────────────────

    public bool Equals(EffectivePeriod? other) =>
        other is not null && Start == other.Start && End == other.End;

    public override bool Equals(object? obj) => obj is EffectivePeriod other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Start, End);

    public override string ToString() =>
        End.HasValue ? $"{Start:yyyy-MM-dd} to {End:yyyy-MM-dd}" : $"{Start:yyyy-MM-dd} (open-ended)";
}
