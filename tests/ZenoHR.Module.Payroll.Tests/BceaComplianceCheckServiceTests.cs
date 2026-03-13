// VUL-024, VUL-025: Tests for BCEA pre-payroll compliance checks.
// CTL-BCEA-001, CTL-BCEA-003

using FluentAssertions;
using ZenoHR.Module.Payroll.Models;
using ZenoHR.Module.Payroll.Services;

namespace ZenoHR.Module.Payroll.Tests;

public sealed class BceaComplianceCheckServiceTests
{
    private readonly BceaComplianceCheckService _sut = new(new BceaComplianceOptions());

    // ── Overtime compliance ────────────────────────────────────────────────

    [Fact]
    public void CheckOvertimeCompliance_40HoursNoOvertime_Compliant()
    {
        var result = _sut.CheckOvertimeCompliance(40m, isOvertimeAgreed: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
    }

    [Fact]
    public void CheckOvertimeCompliance_45HoursNoOvertime_Compliant()
    {
        // Boundary: exactly at ordinary hours limit
        var result = _sut.CheckOvertimeCompliance(45m, isOvertimeAgreed: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
    }

    [Fact]
    public void CheckOvertimeCompliance_46HoursNoAgreement_ViolationNoOvertimeAgreement()
    {
        var result = _sut.CheckOvertimeCompliance(46m, isOvertimeAgreed: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeFalse();
        result.Value.Violations.Should().ContainSingle()
            .Which.Should().Contain("No overtime agreement");
    }

    [Fact]
    public void CheckOvertimeCompliance_50HoursWithAgreement_Compliant()
    {
        var result = _sut.CheckOvertimeCompliance(50m, isOvertimeAgreed: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
    }

    [Fact]
    public void CheckOvertimeCompliance_55HoursWithAgreement_Compliant()
    {
        // Boundary: exactly at max total (45 ordinary + 10 overtime)
        var result = _sut.CheckOvertimeCompliance(55m, isOvertimeAgreed: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
    }

    [Fact]
    public void CheckOvertimeCompliance_56HoursWithAgreement_ViolationOvertimeExceeded()
    {
        var result = _sut.CheckOvertimeCompliance(56m, isOvertimeAgreed: true);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeFalse();
        result.Value.Violations.Should().ContainSingle()
            .Which.Should().Contain("exceed BCEA maximum");
    }

    [Fact]
    public void CheckOvertimeCompliance_50HoursNoAgreement_ViolationNoOvertimeAgreement()
    {
        var result = _sut.CheckOvertimeCompliance(50m, isOvertimeAgreed: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeFalse();
        result.Value.Violations.Should().ContainSingle()
            .Which.Should().Contain("No overtime agreement");
    }

    [Fact]
    public void CheckOvertimeCompliance_NegativeHours_Violation()
    {
        var result = _sut.CheckOvertimeCompliance(-1m, isOvertimeAgreed: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeFalse();
        result.Value.Violations.Should().ContainSingle()
            .Which.Should().Contain("negative");
    }

    [Fact]
    public void CheckOvertimeCompliance_ZeroHours_Compliant()
    {
        var result = _sut.CheckOvertimeCompliance(0m, isOvertimeAgreed: false);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
    }

    // ── Leave compliance ───────────────────────────────────────────────────

    [Fact]
    public void CheckLeaveCompliance_15Days12Months_Compliant()
    {
        var result = _sut.CheckLeaveCompliance(15m, employmentMonths: 12);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void CheckLeaveCompliance_10Days12Months_WarningBelowMinimum()
    {
        // Pro-rated min for 12 months = 15 days; 10 < 15 -> warning
        var result = _sut.CheckLeaveCompliance(10m, employmentMonths: 12);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue(); // Warnings don't make it non-compliant
        result.Value.Warnings.Should().ContainSingle()
            .Which.Should().Contain("below the BCEA pro-rated minimum");
    }

    [Fact]
    public void CheckLeaveCompliance_5Days6Months_WarningBelowMinimum()
    {
        // Pro-rated min for 6 months = 6 x 1.25 = 7.5 days; 5 < 7.5 -> warning
        var result = _sut.CheckLeaveCompliance(5m, employmentMonths: 6);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue(); // Still compliant (warning only)
        result.Value.Warnings.Should().ContainSingle()
            .Which.Should().Contain("below the BCEA pro-rated minimum");
    }

    [Fact]
    public void CheckLeaveCompliance_NewEmployee1Month0Days_WarningBelowMinimum()
    {
        // Pro-rated min for 1 month = 1.25 days; 0 < 1.25 -> warning
        var result = _sut.CheckLeaveCompliance(0m, employmentMonths: 1);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue(); // Warning only
        result.Value.Warnings.Should().ContainSingle()
            .Which.Should().Contain("below the BCEA pro-rated minimum");
    }

    [Fact]
    public void CheckLeaveCompliance_NegativeEmploymentMonths_Failure()
    {
        var result = _sut.CheckLeaveCompliance(10m, employmentMonths: -1);

        result.IsFailure.Should().BeTrue();
        result.Error.Message.Should().Contain("negative");
    }

    [Fact]
    public void CheckLeaveCompliance_ZeroMonthsZeroDays_Compliant()
    {
        // Pro-rated min for 0 months = 0 days; 0 >= 0 -> no warning
        var result = _sut.CheckLeaveCompliance(0m, employmentMonths: 0);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Warnings.Should().BeEmpty();
    }

    // ── Combined pre-payroll validation ────────────────────────────────────

    [Fact]
    public void ValidatePrePayroll_OvertimeViolationAndLeaveWarning_NotCompliant()
    {
        // Overtime violation (56h with agreement) + leave warning (10 days for 12 months)
        var result = _sut.ValidatePrePayroll(56m, isOvertimeAgreed: true, 10m, employmentMonths: 12);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeFalse(); // Violation wins
        result.Value.Violations.Should().HaveCount(1);
        result.Value.Warnings.Should().HaveCount(1);
    }

    [Fact]
    public void ValidatePrePayroll_NoViolationsNoWarnings_Compliant()
    {
        var result = _sut.ValidatePrePayroll(40m, isOvertimeAgreed: false, 15m, employmentMonths: 12);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
        result.Value.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void ValidatePrePayroll_WarningsOnly_StillCompliant()
    {
        // No overtime violation, but leave below minimum -> warning only
        var result = _sut.ValidatePrePayroll(40m, isOvertimeAgreed: false, 5m, employmentMonths: 12);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeTrue();
        result.Value.Violations.Should().BeEmpty();
        result.Value.Warnings.Should().HaveCount(1);
    }

    [Fact]
    public void ValidatePrePayroll_NegativeHours_NotCompliant()
    {
        var result = _sut.ValidatePrePayroll(-5m, isOvertimeAgreed: false, 15m, employmentMonths: 12);

        result.IsSuccess.Should().BeTrue();
        result.Value.IsCompliant.Should().BeFalse();
        result.Value.Violations.Should().ContainSingle()
            .Which.Should().Contain("negative");
    }
}
