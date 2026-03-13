// CTL-POPIA-004: Tests for pre-payroll data quality validation.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Services;

namespace ZenoHR.Module.Payroll.Tests;

public sealed class DataQualityCheckServiceTests
{
    // ── ValidateSaIdNumber ──────────────────────────────────────────────────

    [Fact]
    public void ValidateSaIdNumber_ValidId_Passes()
    {
        // CTL-POPIA-004 — 8801015009080 is a known valid Luhn SA ID
        var result = DataQualityCheckService.ValidateSaIdNumber("8801015009080");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void ValidateSaIdNumber_WrongLength_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateSaIdNumber("880101500908");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("13 digits");
    }

    [Fact]
    public void ValidateSaIdNumber_NonNumeric_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateSaIdNumber("88010150090AB");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("only digits");
    }

    [Fact]
    public void ValidateSaIdNumber_InvalidMonth_Fails()
    {
        // CTL-POPIA-004 — month 13 is invalid
        var result = DataQualityCheckService.ValidateSaIdNumber("8813015009087");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("invalid month");
    }

    [Fact]
    public void ValidateSaIdNumber_InvalidDay_Fails()
    {
        // CTL-POPIA-004 — day 00 is invalid
        var result = DataQualityCheckService.ValidateSaIdNumber("8801005009087");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("invalid day");
    }

    [Fact]
    public void ValidateSaIdNumber_InvalidLuhn_Fails()
    {
        // CTL-POPIA-004 — last digit changed to break Luhn
        var result = DataQualityCheckService.ValidateSaIdNumber("8801015009087");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("Luhn");
    }

    [Fact]
    public void ValidateSaIdNumber_Empty_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateSaIdNumber("");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.RequiredFieldMissing);
    }

    // ── ValidateTaxReference ───────────────────────────────────────────────

    [Theory]
    [InlineData("0123456789")]
    [InlineData("1234567890")]
    [InlineData("2345678901")]
    [InlineData("3456789012")]
    [InlineData("9876543210")]
    public void ValidateTaxReference_ValidRef_Passes(string taxRef)
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateTaxReference(taxRef);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateTaxReference_WrongLength_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateTaxReference("012345678");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("10 digits");
    }

    [Theory]
    [InlineData("4123456789")]
    [InlineData("5123456789")]
    [InlineData("6123456789")]
    [InlineData("7123456789")]
    [InlineData("8123456789")]
    public void ValidateTaxReference_InvalidPrefix_Fails(string taxRef)
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateTaxReference(taxRef);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("start with");
    }

    [Fact]
    public void ValidateTaxReference_NonNumeric_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateTaxReference("012345678A");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
    }

    // ── ValidateBankAccountRef ─────────────────────────────────────────────

    [Theory]
    [InlineData("123456")]       // 6 digits — minimum
    [InlineData("1234567")]      // 7 digits
    [InlineData("12345678901")]  // 11 digits — maximum
    public void ValidateBankAccountRef_ValidLength_Passes(string accountRef)
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateBankAccountRef(accountRef);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateBankAccountRef_TooShort_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateBankAccountRef("12345");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("6–11 digits");
    }

    [Fact]
    public void ValidateBankAccountRef_TooLong_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateBankAccountRef("123456789012");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
        result.Error.Message.Should().Contain("6–11 digits");
    }

    [Fact]
    public void ValidateBankAccountRef_NonNumeric_Fails()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateBankAccountRef("12345A");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.InvalidFormat);
    }

    // ── ValidateEmployeeDataQuality (combined) ─────────────────────────────

    [Fact]
    public void ValidateEmployeeDataQuality_AllValid_Passes()
    {
        // CTL-POPIA-004
        var result = DataQualityCheckService.ValidateEmployeeDataQuality(
            "8801015009080", "0123456789", "1234567890");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ValidateEmployeeDataQuality_OneInvalid_ReturnsFailureList()
    {
        // CTL-POPIA-004 — invalid tax ref prefix, valid SA ID and bank account
        var result = DataQualityCheckService.ValidateEmployeeDataQuality(
            "8801015009080", "5123456789", "1234567890");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("1 error(s)");
        result.Error.Message.Should().Contain("start with");
    }

    [Fact]
    public void ValidateEmployeeDataQuality_MultipleInvalid_ReturnsAllFailures()
    {
        // CTL-POPIA-004 — invalid ID (too short) + invalid tax ref (bad prefix) + invalid bank (too short)
        var result = DataQualityCheckService.ValidateEmployeeDataQuality(
            "123", "5123456789", "12");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.ValidationFailed);
        result.Error.Message.Should().Contain("3 error(s)");
    }
}
