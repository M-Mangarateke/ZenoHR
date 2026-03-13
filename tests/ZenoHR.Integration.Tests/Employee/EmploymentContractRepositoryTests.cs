// TC-HR-010: EmploymentContractRepository integration tests against Firestore emulator.
// REQ-HR-001, REQ-HR-003: Employment contract persistence and active contract lookups.
// CTL-SARS-001: base_salary_zar stored as string for decimal precision.
// REQ-SEC-005: Tenant isolation enforced on contract documents.

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Employee.Aggregates;

namespace ZenoHR.Integration.Tests.Employee;

/// <summary>
/// Integration tests for <see cref="EmploymentContractRepository"/> against the Firestore emulator.
/// TC-HR-010-A: SaveAsync then GetByContractId returns correct contract with decimal salary precision.
/// TC-HR-010-B: GetActiveContractAsync returns the active contract for an employee.
/// TC-HR-010-C: ListByEmployeeAsync returns all contracts for an employee.
/// TC-HR-010-D: Decimal precision — base_salary_zar round-trips with exact cents.
/// TC-HR-010-E: Tenant isolation — contract from different tenant is not accessible.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class EmploymentContractRepositoryTests : IntegrationTestBase
{
    // TC-HR-010
    private readonly EmploymentContractRepository _repo;

    public EmploymentContractRepositoryTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _repo = new EmploymentContractRepository(fixture.Db, NullLogger<EmploymentContractRepository>.Instance);
    }

    // ── TC-HR-010-A: Save then GetByContractId round-trip ─────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByContractId_ReturnsCorrectContract()
    {
        // TC-HR-010-A: Arrange
        var contractId = $"con_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var salary = new MoneyZAR(45_000.75m);
        var now = DateTimeOffset.UtcNow;

        var contract = CreateContract(contractId, employeeId, salary, now);

        // Act
        var saveResult = await _repo.SaveAsync(contract);
        var getResult = await _repo.GetByContractIdAsync(TenantId, contractId);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.ContractId.Should().Be(contractId);
        getResult.Value.TenantId.Should().Be(TenantId);
        getResult.Value.EmployeeId.Should().Be(employeeId);
        getResult.Value.BaseSalary.Amount.Should().Be(45_000.75m,
            because: "base_salary_zar must round-trip with exact decimal precision (CTL-SARS-001)");
        getResult.Value.IsActive.Should().BeTrue();
        getResult.Value.SalaryBasis.Should().Be(SalaryBasis.Monthly);
    }

    // ── TC-HR-010-B: GetActiveContractAsync ───────────────────────────────────

    [Fact]
    public async Task GetActiveContractAsync_WithActiveContract_ReturnsIt()
    {
        // TC-HR-010-B: Arrange
        var contractId = $"con_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var contract = CreateContract(contractId, employeeId, new MoneyZAR(30_000m), now);
        await _repo.SaveAsync(contract);

        // Act
        var result = await _repo.GetActiveContractAsync(TenantId, employeeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ContractId.Should().Be(contractId);
        result.Value.IsActive.Should().BeTrue();
    }

    // ── TC-HR-010-C: ListByEmployeeAsync ──────────────────────────────────────

    [Fact]
    public async Task ListByEmployeeAsync_ReturnsAllContracts()
    {
        // TC-HR-010-C: Arrange — create two contracts for the same employee
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var contract1 = CreateContract($"con_{Guid.NewGuid():N}", employeeId, new MoneyZAR(25_000m), now,
            startDate: new DateOnly(2025, 1, 1));
        var contract2 = CreateContract($"con_{Guid.NewGuid():N}", employeeId, new MoneyZAR(30_000m), now,
            startDate: new DateOnly(2026, 1, 1));

        await _repo.SaveAsync(contract1);
        await _repo.SaveAsync(contract2);

        // Act
        var results = await _repo.ListByEmployeeAsync(TenantId, employeeId);

        // Assert
        results.Should().HaveCount(2);
        results.Select(c => c.EmployeeId).Should().OnlyContain(id => id == employeeId);
    }

    // ── TC-HR-010-D: Decimal precision for base_salary_zar ────────────────────

    [Theory]
    [InlineData("12345.67")]
    [InlineData("99999.99")]
    [InlineData("0.01")]
    [InlineData("1000000.00")]
    public async Task SaveAsync_DecimalPrecision_RoundTripsExactly(string salaryStr)
    {
        // TC-HR-010-D: Verify string storage preserves decimal precision
        var salary = new MoneyZAR(decimal.Parse(salaryStr, System.Globalization.CultureInfo.InvariantCulture));
        var contractId = $"con_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var contract = CreateContract(contractId, employeeId, salary, now);
        await _repo.SaveAsync(contract);

        // Act
        var getResult = await _repo.GetByContractIdAsync(TenantId, contractId);

        // Assert
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.BaseSalary.Amount.Should().Be(salary.Amount,
            because: $"base_salary_zar '{salaryStr}' must round-trip exactly via string storage");
    }

    // ── TC-HR-010-E: Tenant isolation ─────────────────────────────────────────

    [Fact]
    public async Task GetByContractIdAsync_WrongTenant_ReturnsFailure()
    {
        // TC-HR-010-E: Arrange
        var contractId = $"con_{Guid.NewGuid():N}";
        var employeeId = $"emp_{Guid.NewGuid():N}";
        var now = DateTimeOffset.UtcNow;

        var contract = CreateContract(contractId, employeeId, new MoneyZAR(40_000m), now);
        await _repo.SaveAsync(contract);

        // Act — attempt to read with a different tenant
        var otherTenant = $"test-tenant-{Guid.NewGuid():N}";
        var result = await _repo.GetByContractIdAsync(otherTenant, contractId);

        // Assert
        result.IsFailure.Should().BeTrue(
            because: "REQ-SEC-005: contract owned by a different tenant must not be accessible");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private EmploymentContract CreateContract(
        string contractId, string employeeId, MoneyZAR salary, DateTimeOffset now,
        DateOnly? startDate = null)
    {
        var result = EmploymentContract.Create(
            contractId: contractId,
            tenantId: TenantId,
            employeeId: employeeId,
            startDate: startDate ?? new DateOnly(2026, 1, 1),
            endDate: null,
            salaryBasis: SalaryBasis.Monthly,
            baseSalary: salary,
            ordinaryHoursPerWeek: 40m,
            ordinaryHoursPolicyVersion: "BCEA-2026-v1",
            occupationalLevel: "Junior",
            actorId: "uid-sarah-hr",
            now: now);

        result.IsSuccess.Should().BeTrue(
            because: $"CreateContract factory for {contractId} must succeed");
        return result.Value!;
    }
}
