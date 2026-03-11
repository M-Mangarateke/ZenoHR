// TC-PAY-040: TaxDirectiveService — registration, validation, status transitions, active lookup.
// CTL-SARS-004: SARS IRP3 tax directive handling for all 4 types.
using FluentAssertions;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Models;
using ZenoHR.Module.Payroll.Services;

namespace ZenoHR.Module.Payroll.Tests.TaxDirective;

public sealed class TaxDirectiveServiceTests
{
    private readonly TaxDirectiveService _sut = new();

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static readonly DateOnly DefaultFrom = new(2026, 3, 1);
    private static readonly DateOnly DefaultTo = new(2027, 2, 28);
    private static readonly DateTimeOffset DefaultIssuedAt = new(2026, 2, 15, 10, 0, 0, TimeSpan.Zero);

    private Result<Models.TaxDirective> RegisterIRP3a(
        string? tenantId = "tenant_001",
        string? employeeId = "emp_001",
        string? directiveNumber = "1234567",
        decimal lumpSum = 500_000m,
        decimal taxOnLump = 90_000m) =>
        _sut.RegisterDirective(
            tenantId!, employeeId!, directiveNumber!,
            TaxDirectiveType.IRP3a, DefaultFrom, DefaultTo,
            directiveRate: null,
            lumpSumAmount: new MoneyZAR(lumpSum),
            taxOnLumpSum: new MoneyZAR(taxOnLump),
            issuedBy: "SARS Pretoria",
            issuedAt: DefaultIssuedAt);

    private Result<Models.TaxDirective> RegisterIRP3b(
        string? tenantId = "tenant_001",
        string? employeeId = "emp_001",
        string? directiveNumber = "9876543",
        decimal lumpSum = 300_000m,
        decimal taxOnLump = 45_000m) =>
        _sut.RegisterDirective(
            tenantId!, employeeId!, directiveNumber!,
            TaxDirectiveType.IRP3b, DefaultFrom, DefaultTo,
            directiveRate: null,
            lumpSumAmount: new MoneyZAR(lumpSum),
            taxOnLumpSum: new MoneyZAR(taxOnLump),
            issuedBy: "SARS Cape Town",
            issuedAt: DefaultIssuedAt);

    private Result<Models.TaxDirective> RegisterIRP3c(
        string? tenantId = "tenant_001",
        string? employeeId = "emp_001",
        string? directiveNumber = "7654321",
        decimal rate = 25m) =>
        _sut.RegisterDirective(
            tenantId!, employeeId!, directiveNumber!,
            TaxDirectiveType.IRP3c, DefaultFrom, DefaultTo,
            directiveRate: rate,
            lumpSumAmount: null,
            taxOnLumpSum: null,
            issuedBy: "SARS Johannesburg",
            issuedAt: DefaultIssuedAt);

    private Result<Models.TaxDirective> RegisterIRP3s(
        string? tenantId = "tenant_001",
        string? employeeId = "emp_001",
        string? directiveNumber = "1122334",
        decimal rate = 35m) =>
        _sut.RegisterDirective(
            tenantId!, employeeId!, directiveNumber!,
            TaxDirectiveType.IRP3s, DefaultFrom, DefaultTo,
            directiveRate: rate,
            lumpSumAmount: null,
            taxOnLumpSum: null,
            issuedBy: "SARS Durban",
            issuedAt: DefaultIssuedAt);

    // ── RegisterDirective — Success paths ──────────────────────────────────────

    [Fact]
    public void RegisterDirective_ValidIRP3a_Succeeds()
    {
        // TC-PAY-040-001
        var result = RegisterIRP3a();

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(TaxDirectiveType.IRP3a);
        result.Value.Status.Should().Be(TaxDirectiveStatus.Pending);
        result.Value.LumpSumAmount!.Value.Amount.Should().Be(500_000m);
        result.Value.TaxOnLumpSum!.Value.Amount.Should().Be(90_000m);
        result.Value.DirectiveRate.Should().BeNull();
        result.Value.DirectiveId.Should().StartWith("DIR-");
        result.Value.TenantId.Should().Be("tenant_001");
        result.Value.EmployeeId.Should().Be("emp_001");
    }

    [Fact]
    public void RegisterDirective_ValidIRP3b_Succeeds()
    {
        // TC-PAY-040-002
        var result = RegisterIRP3b();

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(TaxDirectiveType.IRP3b);
        result.Value.Status.Should().Be(TaxDirectiveStatus.Pending);
        result.Value.LumpSumAmount!.Value.Amount.Should().Be(300_000m);
        result.Value.TaxOnLumpSum!.Value.Amount.Should().Be(45_000m);
        result.Value.DirectiveRate.Should().BeNull();
    }

    [Fact]
    public void RegisterDirective_ValidIRP3c_Succeeds()
    {
        // TC-PAY-040-003
        var result = RegisterIRP3c();

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(TaxDirectiveType.IRP3c);
        result.Value.Status.Should().Be(TaxDirectiveStatus.Pending);
        result.Value.DirectiveRate.Should().Be(25m);
        result.Value.LumpSumAmount.Should().BeNull();
        result.Value.TaxOnLumpSum.Should().BeNull();
    }

    [Fact]
    public void RegisterDirective_ValidIRP3s_Succeeds()
    {
        // TC-PAY-040-004
        var result = RegisterIRP3s();

        result.IsSuccess.Should().BeTrue();
        result.Value.Type.Should().Be(TaxDirectiveType.IRP3s);
        result.Value.Status.Should().Be(TaxDirectiveStatus.Pending);
        result.Value.DirectiveRate.Should().Be(35m);
        result.Value.LumpSumAmount.Should().BeNull();
        result.Value.TaxOnLumpSum.Should().BeNull();
    }

    // ── RegisterDirective — Validation failures ────────────────────────────────

    [Fact]
    public void RegisterDirective_EmptyTenantId_ReturnsFailure()
    {
        // TC-PAY-040-010
        var result = RegisterIRP3a(tenantId: "");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        result.Error.Message.Should().Contain("TenantId");
    }

    [Fact]
    public void RegisterDirective_EmptyEmployeeId_ReturnsFailure()
    {
        // TC-PAY-040-011
        var result = RegisterIRP3a(employeeId: "");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        result.Error.Message.Should().Contain("EmployeeId");
    }

    [Theory]
    [InlineData("123456", "too short — 6 digits")]
    [InlineData("12345678901", "too long — 11 digits")]
    [InlineData("ABCDEFG", "non-numeric")]
    [InlineData("123-456", "contains non-digit characters")]
    [InlineData("", "empty string")]
    public void RegisterDirective_InvalidDirectiveNumber_ReturnsFailure(string directiveNumber, string _scenario)
    {
        // TC-PAY-040-012
        var result = _sut.RegisterDirective(
            "tenant_001", "emp_001", directiveNumber,
            TaxDirectiveType.IRP3a, DefaultFrom, DefaultTo,
            directiveRate: null,
            lumpSumAmount: new MoneyZAR(100_000m),
            taxOnLumpSum: new MoneyZAR(18_000m),
            issuedBy: "SARS",
            issuedAt: DefaultIssuedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        result.Error.Message.Should().Contain("DirectiveNumber");
    }

    [Fact]
    public void RegisterDirective_UnknownType_ReturnsFailure()
    {
        // TC-PAY-040-013
        var result = _sut.RegisterDirective(
            "tenant_001", "emp_001", "1234567",
            TaxDirectiveType.Unknown, DefaultFrom, DefaultTo,
            directiveRate: null,
            lumpSumAmount: null,
            taxOnLumpSum: null,
            issuedBy: "SARS",
            issuedAt: DefaultIssuedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        result.Error.Message.Should().Contain("Unknown");
    }

    [Fact]
    public void RegisterDirective_EffectiveToBeforeFrom_ReturnsFailure()
    {
        // TC-PAY-040-014
        var result = _sut.RegisterDirective(
            "tenant_001", "emp_001", "1234567",
            TaxDirectiveType.IRP3a,
            effectiveFrom: new DateOnly(2027, 1, 1),
            effectiveTo: new DateOnly(2026, 6, 30),
            directiveRate: null,
            lumpSumAmount: new MoneyZAR(100_000m),
            taxOnLumpSum: new MoneyZAR(18_000m),
            issuedBy: "SARS",
            issuedAt: DefaultIssuedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        result.Error.Message.Should().Contain("EffectiveTo");
    }

    [Fact]
    public void RegisterDirective_IRP3aWithoutLumpSum_ReturnsFailure()
    {
        // TC-PAY-040-015
        var result = _sut.RegisterDirective(
            "tenant_001", "emp_001", "1234567",
            TaxDirectiveType.IRP3a, DefaultFrom, DefaultTo,
            directiveRate: null,
            lumpSumAmount: null,
            taxOnLumpSum: new MoneyZAR(18_000m),
            issuedBy: "SARS",
            issuedAt: DefaultIssuedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        result.Error.Message.Should().Contain("LumpSumAmount");
    }

    [Fact]
    public void RegisterDirective_IRP3cWithoutRate_ReturnsFailure()
    {
        // TC-PAY-040-016
        var result = RegisterIRP3c(rate: 0m); // rate is provided but let's test null
        // Actually test null rate
        var resultNull = _sut.RegisterDirective(
            "tenant_001", "emp_001", "7654321",
            TaxDirectiveType.IRP3c, DefaultFrom, DefaultTo,
            directiveRate: null,
            lumpSumAmount: null,
            taxOnLumpSum: null,
            issuedBy: "SARS",
            issuedAt: DefaultIssuedAt);

        resultNull.IsFailure.Should().BeTrue();
        resultNull.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        resultNull.Error.Message.Should().Contain("DirectiveRate");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(100.01)]
    [InlineData(150)]
    public void RegisterDirective_IRP3sRateOutOfRange_ReturnsFailure(double rate)
    {
        // TC-PAY-040-017
        var result = _sut.RegisterDirective(
            "tenant_001", "emp_001", "1122334",
            TaxDirectiveType.IRP3s, DefaultFrom, DefaultTo,
            directiveRate: (decimal)rate,
            lumpSumAmount: null,
            taxOnLumpSum: null,
            issuedBy: "SARS",
            issuedAt: DefaultIssuedAt);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveValidationFailed);
        result.Error.Message.Should().Contain("DirectiveRate");
    }

    // ── UpdateStatus ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TaxDirectiveStatus.Pending, TaxDirectiveStatus.Active)]
    [InlineData(TaxDirectiveStatus.Pending, TaxDirectiveStatus.Revoked)]
    [InlineData(TaxDirectiveStatus.Active, TaxDirectiveStatus.Expired)]
    [InlineData(TaxDirectiveStatus.Active, TaxDirectiveStatus.Revoked)]
    [InlineData(TaxDirectiveStatus.Expired, TaxDirectiveStatus.Revoked)]
    public void UpdateStatus_ForwardTransition_Succeeds(
        TaxDirectiveStatus from, TaxDirectiveStatus to)
    {
        // TC-PAY-040-020
        var directive = RegisterIRP3a().Value with { Status = from };

        var result = TaxDirectiveService.UpdateStatus(directive, to);

        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be(to);
    }

    [Theory]
    [InlineData(TaxDirectiveStatus.Active, TaxDirectiveStatus.Pending)]
    [InlineData(TaxDirectiveStatus.Expired, TaxDirectiveStatus.Active)]
    [InlineData(TaxDirectiveStatus.Expired, TaxDirectiveStatus.Pending)]
    [InlineData(TaxDirectiveStatus.Revoked, TaxDirectiveStatus.Pending)]
    [InlineData(TaxDirectiveStatus.Revoked, TaxDirectiveStatus.Active)]
    [InlineData(TaxDirectiveStatus.Revoked, TaxDirectiveStatus.Expired)]
    public void UpdateStatus_BackwardTransition_ReturnsFailure(
        TaxDirectiveStatus from, TaxDirectiveStatus to)
    {
        // TC-PAY-040-021
        var directive = RegisterIRP3a().Value with { Status = from };

        var result = TaxDirectiveService.UpdateStatus(directive, to);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveInvalidStatusTransition);
    }

    // ── GetActiveDirective ─────────────────────────────────────────────────────

    [Fact]
    public void GetActiveDirective_WithActive_ReturnsDirective()
    {
        // TC-PAY-040-030
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var directive = RegisterIRP3c().Value with
        {
            Status = TaxDirectiveStatus.Active,
            EffectiveFrom = today.AddDays(-30),
            EffectiveTo = today.AddDays(30)
        };

        var directives = new List<Models.TaxDirective> { directive };

        var result = TaxDirectiveService.GetActiveDirective(directives, "emp_001");

        result.IsSuccess.Should().BeTrue();
        result.Value.DirectiveId.Should().Be(directive.DirectiveId);
    }

    [Fact]
    public void GetActiveDirective_NoneActive_ReturnsFailure()
    {
        // TC-PAY-040-031
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expiredDirective = RegisterIRP3c().Value with
        {
            Status = TaxDirectiveStatus.Active,
            EffectiveFrom = today.AddDays(-365),
            EffectiveTo = today.AddDays(-1) // expired yesterday
        };

        var pendingDirective = RegisterIRP3s().Value with
        {
            Status = TaxDirectiveStatus.Pending,
            EffectiveFrom = today,
            EffectiveTo = today.AddDays(365)
        };

        var directives = new List<Models.TaxDirective> { expiredDirective, pendingDirective };

        var result = TaxDirectiveService.GetActiveDirective(directives, "emp_001");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be(ZenoHrErrorCode.TaxDirectiveNotFound);
    }

    // ── GetExpiredDirectives ───────────────────────────────────────────────────

    [Fact]
    public void GetExpiredDirectives_ReturnsExpiredOnly()
    {
        // TC-PAY-040-040
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var expired1 = RegisterIRP3a().Value with
        {
            DirectiveId = "DIR-2026-0010",
            Status = TaxDirectiveStatus.Active,
            EffectiveFrom = today.AddDays(-365),
            EffectiveTo = today.AddDays(-30)
        };

        var expired2 = RegisterIRP3b().Value with
        {
            DirectiveId = "DIR-2026-0011",
            Status = TaxDirectiveStatus.Pending,
            EffectiveFrom = today.AddDays(-200),
            EffectiveTo = today.AddDays(-1)
        };

        var activeDirective = RegisterIRP3c().Value with
        {
            DirectiveId = "DIR-2026-0012",
            Status = TaxDirectiveStatus.Active,
            EffectiveFrom = today.AddDays(-30),
            EffectiveTo = today.AddDays(30)
        };

        var revokedDirective = RegisterIRP3s().Value with
        {
            DirectiveId = "DIR-2026-0013",
            Status = TaxDirectiveStatus.Revoked,
            EffectiveFrom = today.AddDays(-365),
            EffectiveTo = today.AddDays(-100) // past EffectiveTo but revoked — should NOT be returned
        };

        var directives = new List<Models.TaxDirective>
        {
            expired1, expired2, activeDirective, revokedDirective
        };

        var result = TaxDirectiveService.GetExpiredDirectives(directives);

        result.Should().HaveCount(2);
        result.Select(d => d.DirectiveId).Should().Contain("DIR-2026-0010");
        result.Select(d => d.DirectiveId).Should().Contain("DIR-2026-0011");
        result.Select(d => d.DirectiveId).Should().NotContain("DIR-2026-0012");
        result.Select(d => d.DirectiveId).Should().NotContain("DIR-2026-0013");
    }
}
