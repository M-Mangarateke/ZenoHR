// REQ-HR-003: MoneyZAR — South African Rand value object.
// Critical Rule: All monetary values MUST use decimal. float/double is a Sev-1 defect.
// CTL-SARS-001: Rounding rules per docs/schemas/monetary-precision.md
// Firestore storage: string (to preserve exact decimal; Firestore number is IEEE 754 double).

namespace ZenoHR.Domain.Common;

/// <summary>
/// Immutable value object representing a South African Rand (ZAR) monetary amount.
/// Always backed by <see cref="decimal"/>. Stored as a string in Firestore to preserve
/// exact decimal representation. See docs/schemas/monetary-precision.md.
/// </summary>
public readonly record struct MoneyZAR : IComparable<MoneyZAR>
{
    public decimal Amount { get; }

    public MoneyZAR(decimal amount) => Amount = amount;

    // ── Well-known values ────────────────────────────────────────────────────

    public static readonly MoneyZAR Zero = new(0m);

    // ── Arithmetic operators ─────────────────────────────────────────────────

    public static MoneyZAR operator +(MoneyZAR a, MoneyZAR b) => new(a.Amount + b.Amount);
    public static MoneyZAR operator -(MoneyZAR a, MoneyZAR b) => new(a.Amount - b.Amount);
    public static MoneyZAR operator -(MoneyZAR a) => new(-a.Amount);

    /// <summary>Scale by a scalar multiplier (e.g., hours, rate, percentage).</summary>
    public static MoneyZAR operator *(MoneyZAR money, decimal scalar) => new(money.Amount * scalar);
    public static MoneyZAR operator *(decimal scalar, MoneyZAR money) => new(scalar * money.Amount);

    /// <summary>Divide by a scalar (e.g., ÷12 for monthly, ÷52 for weekly).</summary>
    public static MoneyZAR operator /(MoneyZAR money, decimal divisor)
    {
        if (divisor == 0m) throw new DivideByZeroException("Cannot divide MoneyZAR by zero.");
        return new(money.Amount / divisor);
    }

    // ── Comparison operators ─────────────────────────────────────────────────

    public static bool operator <(MoneyZAR a, MoneyZAR b) => a.Amount < b.Amount;
    public static bool operator >(MoneyZAR a, MoneyZAR b) => a.Amount > b.Amount;
    public static bool operator <=(MoneyZAR a, MoneyZAR b) => a.Amount <= b.Amount;
    public static bool operator >=(MoneyZAR a, MoneyZAR b) => a.Amount >= b.Amount;

    public int CompareTo(MoneyZAR other) => Amount.CompareTo(other.Amount);

    // ── Rounding — per monetary-precision.md ────────────────────────────────

    /// <summary>
    /// Round to nearest rand (0 decimal places, AwayFromZero).
    /// Used for: annual PAYE tax calculation step 5.
    /// </summary>
    public MoneyZAR RoundToRand() =>
        new(Math.Round(Amount, 0, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Round to nearest cent (2 decimal places, AwayFromZero).
    /// Used for: period PAYE, UIF, SDL, ETI after de-annualisation.
    /// </summary>
    public MoneyZAR RoundToCent() =>
        new(Math.Round(Amount, 2, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Clamp to at least zero. Applies floor of R0.00.
    /// Required after PAYE rebate subtraction (income below threshold → R0).
    /// </summary>
    public MoneyZAR FloorAtZero() => Amount < 0m ? Zero : this;

    // ── Min / Max helpers ────────────────────────────────────────────────────

    public MoneyZAR Max(MoneyZAR other) => Amount >= other.Amount ? this : other;
    public MoneyZAR Min(MoneyZAR other) => Amount <= other.Amount ? this : other;

    public static MoneyZAR Max(MoneyZAR a, MoneyZAR b) => a.Amount >= b.Amount ? a : b;
    public static MoneyZAR Min(MoneyZAR a, MoneyZAR b) => a.Amount <= b.Amount ? a : b;

    // ── State predicates ─────────────────────────────────────────────────────

    public bool IsNegative => Amount < 0m;
    public bool IsZeroOrNegative => Amount <= 0m;
    public bool IsZero => Amount == 0m;
    public bool IsPositive => Amount > 0m;

    // ── Conversions ──────────────────────────────────────────────────────────

    /// <summary>Implicit conversion from decimal — no data loss possible.</summary>
    public static implicit operator MoneyZAR(decimal amount) => new(amount);

    /// <summary>Explicit conversion to decimal — caller takes responsibility for precision.</summary>
    public static explicit operator decimal(MoneyZAR money) => money.Amount;

    // ── Firestore serialisation ──────────────────────────────────────────────

    /// <summary>Serialise to Firestore string field. Always 2 decimal places, invariant culture.</summary>
    public string ToFirestoreString() =>
        Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Deserialise from Firestore string field. Throws <see cref="FormatException"/> on invalid input.</summary>
    public static MoneyZAR FromFirestoreString(string value) =>
        decimal.TryParse(value,
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var amount)
            ? new(amount)
            : throw new FormatException($"Cannot parse '{value}' as MoneyZAR. Expected decimal string.");

    // ── Display ──────────────────────────────────────────────────────────────

    public override string ToString() => $"R {Amount:N2}";
}
