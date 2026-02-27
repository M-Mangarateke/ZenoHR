// REQ-HR-003: TaxYear — SA SARS tax year value object.
// CTL-SARS-001: Tax year runs 1 March (Y-1) to last day of February (Y).
// Example: TaxYear(2026) = 1 March 2025 to 28 February 2026 (the "2025/2026 tax year").

namespace ZenoHR.Domain.Common;

/// <summary>
/// Represents a South African SARS tax year.
/// The tax year runs from 1 March of the prior calendar year to the last day of February
/// of the ending year (28 or 29 February depending on leap year).
/// <para>
/// Convention: <see cref="EndingYear"/> is the calendar year the tax year ends in.
/// TaxYear(2026) = 2025/2026 tax year (1 Mar 2025 – 28 Feb 2026).
/// </para>
/// </summary>
public sealed class TaxYear : IEquatable<TaxYear>, IComparable<TaxYear>
{
    /// <summary>The calendar year the tax year ends in (e.g., 2026 for 2025/2026).</summary>
    public int EndingYear { get; }

    /// <summary>1 March of EndingYear - 1.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Last day of February of EndingYear (28 or 29).</summary>
    public DateOnly EndDate { get; }

    public TaxYear(int endingYear)
    {
        if (endingYear < 2000 || endingYear > 2100)
            throw new ArgumentOutOfRangeException(nameof(endingYear),
                $"Tax year must be between 2000 and 2100. Got: {endingYear}");

        EndingYear = endingYear;
        StartDate = new DateOnly(endingYear - 1, 3, 1);

        // Last day of February — DateOnly handles leap years via DateTime.DaysInMonth
        var daysInFeb = DateTime.DaysInMonth(endingYear, 2);
        EndDate = new DateOnly(endingYear, 2, daysInFeb);
    }

    // ── Convenience factory ──────────────────────────────────────────────────

    /// <summary>Returns the tax year that contains the given date.</summary>
    public static TaxYear ForDate(DateOnly date)
    {
        // If the month is March or later, the tax year ends in the following calendar year.
        // If the month is Jan/Feb, the tax year ends in this calendar year.
        return date.Month >= 3
            ? new TaxYear(date.Year + 1)
            : new TaxYear(date.Year);
    }

    /// <summary>Returns the current tax year based on today's date.</summary>
    public static TaxYear Current => ForDate(DateOnly.FromDateTime(DateTime.UtcNow));

    // ── Membership ───────────────────────────────────────────────────────────

    /// <summary>Returns true if <paramref name="date"/> falls within this tax year.</summary>
    public bool Contains(DateOnly date) => date >= StartDate && date <= EndDate;

    // ── Equality and comparison ──────────────────────────────────────────────

    public bool Equals(TaxYear? other) => other is not null && EndingYear == other.EndingYear;
    public override bool Equals(object? obj) => obj is TaxYear other && Equals(other);
    public override int GetHashCode() => EndingYear.GetHashCode();
    public int CompareTo(TaxYear? other) => other is null ? 1 : EndingYear.CompareTo(other.EndingYear);

    public static bool operator ==(TaxYear? left, TaxYear? right) =>
        left is null ? right is null : left.Equals(right);
    public static bool operator !=(TaxYear? left, TaxYear? right) => !(left == right);
    public static bool operator <(TaxYear left, TaxYear right) => left.CompareTo(right) < 0;
    public static bool operator >(TaxYear left, TaxYear right) => left.CompareTo(right) > 0;
    public static bool operator <=(TaxYear left, TaxYear right) => left.CompareTo(right) <= 0;
    public static bool operator >=(TaxYear left, TaxYear right) => left.CompareTo(right) >= 0;

    // ── Display ──────────────────────────────────────────────────────────────

    /// <summary>Returns the canonical SARS display format, e.g. "2025/2026".</summary>
    public override string ToString() => $"{EndingYear - 1}/{EndingYear}";
}
