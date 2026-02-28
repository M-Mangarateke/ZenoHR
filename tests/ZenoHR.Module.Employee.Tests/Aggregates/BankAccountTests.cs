// TC-HR-002: BankAccount entity unit tests.
// REQ-HR-001, CTL-POPIA-005: Bank account creation, validation, primary flag management.

using FluentAssertions;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Module.Employee.Tests.Aggregates;

/// <summary>
/// Unit tests for the <see cref="BankAccount"/> entity.
/// TC-HR-002-A: Create_ValidInput_Succeeds
/// TC-HR-002-B: Create_BlankAccountNumber_Fails
/// TC-HR-002-C: Create_InvalidBranchCode_Fails
/// TC-HR-002-D: Create_UnknownAccountType_Fails
/// TC-HR-002-E: Deactivate_SetsEffectiveTo
/// TC-HR-002-F: ClearPrimary_SetsPrimaryFalse
/// </summary>
public sealed class BankAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    // ── TC-HR-002-A: Create valid ─────────────────────────────────────────────

    [Fact]
    public void Create_ValidInput_Succeeds()
    {
        var result = MakeBankAccount("ba-001", isPrimary: true);

        result.IsSuccess.Should().BeTrue();
        result.Value!.BankAccountId.Should().Be("ba-001");
        result.Value.AccountType.Should().Be(BankAccountType.Cheque);
        result.Value.IsPrimary.Should().BeTrue();
        result.Value.EffectiveTo.Should().BeNull();
    }

    // ── TC-HR-002-B: Blank account number rejected ───────────────────────────

    [Fact]
    public void Create_BlankAccountNumber_ReturnsFailure()
    {
        var result = BankAccount.Create(
            bankAccountId: "ba-001", tenantId: "tenant-001", employeeId: "emp-001",
            accountHolderName: "Jane Doe", bankName: "FNB", accountNumber: "",
            branchCode: "250655", accountType: BankAccountType.Cheque,
            isPrimary: true, effectiveFrom: new DateOnly(2026, 1, 1), now: Now, createdBy: "actor-001");

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("AccountNumber");
    }

    // ── TC-HR-002-C: Invalid branch code rejected ─────────────────────────────

    [Theory]
    [InlineData("25065")]   // 5 digits (too short)
    [InlineData("2506558")] // 7 digits (too long)
    [InlineData("")]
    public void Create_InvalidBranchCode_ReturnsFailure(string branchCode)
    {
        var result = BankAccount.Create(
            bankAccountId: "ba-001", tenantId: "tenant-001", employeeId: "emp-001",
            accountHolderName: "Jane Doe", bankName: "FNB", accountNumber: "62123456789",
            branchCode: branchCode, accountType: BankAccountType.Cheque,
            isPrimary: true, effectiveFrom: new DateOnly(2026, 1, 1), now: Now, createdBy: "actor-001");

        result.IsFailure.Should().BeTrue();
        result.Error!.Message.Should().Contain("BranchCode");
    }

    // ── TC-HR-002-D: Unknown account type rejected ────────────────────────────

    [Fact]
    public void Create_UnknownAccountType_ReturnsFailure()
    {
        var result = BankAccount.Create(
            bankAccountId: "ba-001", tenantId: "tenant-001", employeeId: "emp-001",
            accountHolderName: "Jane Doe", bankName: "FNB", accountNumber: "62123456789",
            branchCode: "250655", accountType: BankAccountType.Unknown,
            isPrimary: true, effectiveFrom: new DateOnly(2026, 1, 1), now: Now, createdBy: "actor-001");

        result.IsFailure.Should().BeTrue();
    }

    // ── TC-HR-002-E: Deactivate sets EffectiveTo ──────────────────────────────

    [Fact]
    public void Deactivate_SetsEffectiveTo()
    {
        var ba = MakeBankAccount("ba-001", isPrimary: true).Value!;
        var deactivateDate = new DateOnly(2026, 6, 30);

        ba.Deactivate(deactivateDate);

        ba.EffectiveTo.Should().Be(deactivateDate);
    }

    // ── TC-HR-002-F: ClearPrimary sets IsPrimary to false ─────────────────────

    [Fact]
    public void ClearPrimary_SetsPrimaryFalse()
    {
        var ba = MakeBankAccount("ba-001", isPrimary: true).Value!;
        ba.IsPrimary.Should().BeTrue();

        ba.ClearPrimary();

        ba.IsPrimary.Should().BeFalse();
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static Result<BankAccount> MakeBankAccount(
        string id, bool isPrimary) =>
        BankAccount.Create(
            bankAccountId: id,
            tenantId: "tenant-001",
            employeeId: "emp-001",
            accountHolderName: "Jane Doe",
            bankName: "FNB",
            accountNumber: "62123456789",
            branchCode: "250655",
            accountType: BankAccountType.Cheque,
            isPrimary: isPrimary,
            effectiveFrom: new DateOnly(2026, 1, 1),
            now: Now,
            createdBy: "actor-001");
}
