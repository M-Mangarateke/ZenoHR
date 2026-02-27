// TC-PAY-001: MoneyZAR unit tests — verifies precision, rounding, and operator correctness.
// See docs/schemas/monetary-precision.md for the rounding rules under test.

using FluentAssertions;
using ZenoHR.Domain.Common;

namespace ZenoHR.Domain.Tests.Common;

public sealed class MoneyZARTests
{
    // ── Construction ─────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_StoresExactDecimalValue()
    {
        var money = new MoneyZAR(1234.56m);
        money.Amount.Should().Be(1234.56m);
    }

    [Fact]
    public void Zero_HasAmountOfZero()
    {
        MoneyZAR.Zero.Amount.Should().Be(0m);
    }

    // ── Arithmetic ───────────────────────────────────────────────────────────

    [Fact]
    public void Addition_ReturnsSumOfAmounts()
    {
        var a = new MoneyZAR(1000m);
        var b = new MoneyZAR(234.56m);
        (a + b).Amount.Should().Be(1234.56m);
    }

    [Fact]
    public void Subtraction_ReturnsDifferenceOfAmounts()
    {
        var a = new MoneyZAR(1234.56m);
        var b = new MoneyZAR(234.56m);
        (a - b).Amount.Should().Be(1000m);
    }

    [Fact]
    public void Multiplication_ScalesAmount()
    {
        var money = new MoneyZAR(1000m);
        (money * 1.5m).Amount.Should().Be(1500m);
        (1.5m * money).Amount.Should().Be(1500m);
    }

    [Fact]
    public void Division_ScalesDownAmount()
    {
        var money = new MoneyZAR(12000m);
        (money / 12m).Amount.Should().Be(1000m);
    }

    [Fact]
    public void Division_ByZero_ThrowsDivideByZeroException()
    {
        var money = new MoneyZAR(1000m);
        var act = () => _ = money / 0m;
        act.Should().Throw<DivideByZeroException>();
    }

    [Fact]
    public void UnaryMinus_NegatesAmount()
    {
        var money = new MoneyZAR(500m);
        (-money).Amount.Should().Be(-500m);
    }

    // ── Rounding — core contract per monetary-precision.md ──────────────────

    [Theory]
    [InlineData(1234.50, 1235)]   // exactly half → rounds up (AwayFromZero)
    [InlineData(1234.49, 1234)]
    [InlineData(1234.51, 1235)]
    [InlineData(-1234.50, -1235)] // negative half → rounds away from zero (−1235)
    public void RoundToRand_RoundsToNearestRandAwayFromZero(double input, double expected)
    {
        var money = new MoneyZAR((decimal)input);
        money.RoundToRand().Amount.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(1234.555, 1234.56)]  // half → rounds up (AwayFromZero)
    [InlineData(1234.554, 1234.55)]
    [InlineData(1234.545, 1234.55)]
    [InlineData(-1234.555, -1234.56)] // negative half → rounds away from zero
    public void RoundToCent_RoundsToNearestCentAwayFromZero(double input, double expected)
    {
        var money = new MoneyZAR((decimal)input);
        money.RoundToCent().Amount.Should().Be((decimal)expected);
    }

    [Fact]
    public void FloorAtZero_NegativeAmount_ReturnsZero()
    {
        new MoneyZAR(-100m).FloorAtZero().Amount.Should().Be(0m);
    }

    [Fact]
    public void FloorAtZero_PositiveAmount_ReturnsUnchanged()
    {
        new MoneyZAR(100m).FloorAtZero().Amount.Should().Be(100m);
    }

    [Fact]
    public void FloorAtZero_Zero_ReturnsZero()
    {
        MoneyZAR.Zero.FloorAtZero().Amount.Should().Be(0m);
    }

    // ── PAYE annual tax rounding scenario (from monetary-precision.md) ───────

    [Fact]
    public void RoundToRand_ThenDivideBy12_ProducesExpectedMonthlyPAYE()
    {
        // Annual income = R600,000 → annual PAYE bracket calc ≈ R141,067 (2025/2026)
        // Monthly = R141,067 / 12 = R11,755.5833... → R11,755.58 (RoundToCent AwayFromZero)
        var annualPaye = new MoneyZAR(141_067m);
        var monthly = (annualPaye / 12m).RoundToCent();
        monthly.Amount.Should().Be(11_755.58m);
    }

    // ── Min / Max helpers ────────────────────────────────────────────────────

    [Fact]
    public void Max_ReturnsLargerAmount()
    {
        var a = new MoneyZAR(500m);
        var b = new MoneyZAR(200m);
        MoneyZAR.Max(a, b).Amount.Should().Be(500m);
    }

    [Fact]
    public void Min_ReturnsSmallerAmount()
    {
        var a = new MoneyZAR(500m);
        var b = new MoneyZAR(200m);
        MoneyZAR.Min(a, b).Amount.Should().Be(200m);
    }

    // ── UIF ceiling scenario ─────────────────────────────────────────────────

    [Theory]
    [InlineData(10_000, 100.00)]    // 1% of R10,000 = R100 (below ceiling)
    [InlineData(17_712, 177.12)]    // 1% of R17,712 = R177.12 (at ceiling)
    [InlineData(20_000, 177.12)]    // 1% of R20,000 = R200 → capped at R177.12
    [InlineData(50_000, 177.12)]    // high salary → always capped
    public void UifCeiling_CapAt177_12(double grossPay, double expectedUif)
    {
        var uifCeiling = new MoneyZAR(177.12m);
        var gross = new MoneyZAR((decimal)grossPay);
        var calculated = (gross * 0.01m).RoundToCent();
        var actual = MoneyZAR.Min(calculated, uifCeiling);
        actual.Amount.Should().Be((decimal)expectedUif);
    }

    // ── Equality (record struct) ─────────────────────────────────────────────

    [Fact]
    public void Equality_SameAmount_AreEqual()
    {
        new MoneyZAR(100m).Should().Be(new MoneyZAR(100m));
    }

    [Fact]
    public void Equality_DifferentAmount_AreNotEqual()
    {
        new MoneyZAR(100m).Should().NotBe(new MoneyZAR(101m));
    }

    // ── Firestore serialisation ──────────────────────────────────────────────

    [Theory]
    [InlineData(1234.56, "1234.56")]
    [InlineData(0, "0.00")]
    [InlineData(999999.99, "999999.99")]
    public void ToFirestoreString_ProducesExact2dpString(double amount, string expected)
    {
        new MoneyZAR((decimal)amount).ToFirestoreString().Should().Be(expected);
    }

    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("0.00", 0)]
    [InlineData("177.12", 177.12)]
    public void FromFirestoreString_ParsesExactly(string input, double expected)
    {
        MoneyZAR.FromFirestoreString(input).Amount.Should().Be((decimal)expected);
    }

    [Fact]
    public void FromFirestoreString_InvalidInput_ThrowsFormatException()
    {
        var act = () => MoneyZAR.FromFirestoreString("not-a-number");
        act.Should().Throw<FormatException>();
    }

    // ── Implicit conversion ──────────────────────────────────────────────────

    [Fact]
    public void ImplicitConversion_FromDecimal_Works()
    {
        MoneyZAR money = 500m;
        money.Amount.Should().Be(500m);
    }
}
