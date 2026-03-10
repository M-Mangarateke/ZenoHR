// TC-COMP-TAXREF: Tax reference number validation tests.
// CTL-SARS-006: SARS income tax reference format — 10 digits, starts with 0/1/2/3.

using FluentAssertions;
using ZenoHR.Module.Compliance.Services;

namespace ZenoHR.Module.Compliance.Tests;

/// <summary>
/// Tests for <see cref="TaxReferenceValidator"/> covering:
/// - Valid references (starts with 0, 1, 2, 3)
/// - Null/empty input
/// - Length violations (too short, too long)
/// - Invalid first digit (4–9)
/// - Non-digit characters
/// </summary>
public sealed class TaxReferenceValidatorTests
{
    // ── TC-COMP-TAXREF-001: Valid tax reference → success ────────────────────

    [Fact]
    public void Validate_ValidTaxReference_ReturnsSuccess()
    {
        // TC-COMP-TAXREF-001: "0123456789" is valid — 10 digits, starts with 0
        var result = TaxReferenceValidator.Validate("0123456789");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("0123456789");
    }

    // ── TC-COMP-TAXREF-002: Starts with 0 → success ─────────────────────────

    [Fact]
    public void Validate_StartsWithZero_ReturnsSuccess()
    {
        // TC-COMP-TAXREF-002: First digit 0 is valid
        var result = TaxReferenceValidator.Validate("0999999999");

        result.IsSuccess.Should().BeTrue();
    }

    // ── TC-COMP-TAXREF-003: Starts with 3 → success ─────────────────────────

    [Fact]
    public void Validate_StartsWithThree_ReturnsSuccess()
    {
        // TC-COMP-TAXREF-003: First digit 3 is the highest valid starting digit
        var result = TaxReferenceValidator.Validate("3999999999");

        result.IsSuccess.Should().BeTrue();
    }

    // ── TC-COMP-TAXREF-004: Null → failure ───────────────────────────────────

    [Fact]
    public void Validate_Null_ReturnsFailure()
    {
        // TC-COMP-TAXREF-004: Null tax reference must fail
        var result = TaxReferenceValidator.Validate(null);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("required");
    }

    // ── TC-COMP-TAXREF-005: Empty → failure ──────────────────────────────────

    [Fact]
    public void Validate_Empty_ReturnsFailure()
    {
        // TC-COMP-TAXREF-005: Empty string must fail
        var result = TaxReferenceValidator.Validate("");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("required");
    }

    // ── TC-COMP-TAXREF-006: Too short → failure ─────────────────────────────

    [Fact]
    public void Validate_TooShort_ReturnsFailure()
    {
        // TC-COMP-TAXREF-006: 6 digits is too short
        var result = TaxReferenceValidator.Validate("012345");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("exactly 10 digits");
    }

    // ── TC-COMP-TAXREF-007: Too long → failure ──────────────────────────────

    [Fact]
    public void Validate_TooLong_ReturnsFailure()
    {
        // TC-COMP-TAXREF-007: 11 digits is too long
        var result = TaxReferenceValidator.Validate("01234567890");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("exactly 10 digits");
    }

    // ── TC-COMP-TAXREF-008: Starts with 4 → failure ─────────────────────────

    [Fact]
    public void Validate_StartsWithFour_ReturnsFailure()
    {
        // TC-COMP-TAXREF-008: First digit 4 is invalid
        var result = TaxReferenceValidator.Validate("4123456789");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("start with 0, 1, 2, or 3");
    }

    // ── TC-COMP-TAXREF-009: Non-digit characters → failure ──────────────────

    [Fact]
    public void Validate_NonDigits_ReturnsFailure()
    {
        // TC-COMP-TAXREF-009: Letters are not valid
        var result = TaxReferenceValidator.Validate("012345678A");

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("exactly 10 digits");
    }

    // ── TC-COMP-TAXREF-010: Starts with 1 → success ─────────────────────────

    [Fact]
    public void Validate_StartsWithOne_ReturnsSuccess()
    {
        // TC-COMP-TAXREF-010: First digit 1 is valid
        var result = TaxReferenceValidator.Validate("1234567890");

        result.IsSuccess.Should().BeTrue();
    }

    // ── TC-COMP-TAXREF-011: Starts with 2 → success ─────────────────────────

    [Fact]
    public void Validate_StartsWithTwo_ReturnsSuccess()
    {
        // TC-COMP-TAXREF-011: First digit 2 is valid
        var result = TaxReferenceValidator.Validate("2345678901");

        result.IsSuccess.Should().BeTrue();
    }
}
