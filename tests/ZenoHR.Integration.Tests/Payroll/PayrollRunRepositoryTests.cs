// TC-PAY-010: PayrollRunRepository integration tests against Firestore emulator.
// REQ-HR-003: Payroll run persistence, lifecycle state transitions.
// CTL-SARS-001: Immutability guard — Filed runs reject non-Filed overwrites.
// REQ-SEC-005: Tenant isolation enforced at repository layer.

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using ZenoHR.Domain.Common;
using ZenoHR.Infrastructure.Firestore;
using ZenoHR.Integration.Tests.Infrastructure;
using ZenoHR.Module.Payroll.Aggregates;
using ZenoHR.Module.Payroll.Calculation;

namespace ZenoHR.Integration.Tests.Payroll;

/// <summary>
/// Integration tests for <see cref="PayrollRunRepository"/> against the Firestore emulator.
/// TC-PAY-010-A: SaveAsync then GetByRunId returns Draft run with correct period.
/// TC-PAY-010-B: Tenant isolation — different tenant cannot read another tenant's run.
/// TC-PAY-010-C: ListByTenantAsync returns runs in descending created_at order.
/// TC-PAY-010-D: ListByStatusAsync returns only runs matching the given status.
/// TC-PAY-010-E: SaveAsync after MarkCalculated persists aggregate totals.
/// TC-PAY-010-F: SaveAsync after Finalize persists Finalized status and checksum.
/// TC-PAY-010-G: SaveAsync rejects Filed → non-Filed write (immutability guard).
/// TC-PAY-010-H: ListByPeriodAsync returns only runs for the matching period.
/// </summary>
[Collection(EmulatorCollection.Name)]
public sealed class PayrollRunRepositoryTests : IntegrationTestBase
{
    // TC-PAY-010
    private readonly PayrollRunRepository _repo;

    public PayrollRunRepositoryTests(FirestoreEmulatorFixture fixture) : base(fixture)
    {
        _repo = new PayrollRunRepository(fixture.Db, NullLogger<PayrollRunRepository>.Instance);
    }

    // ── TC-PAY-010-A: Save and retrieve Draft run ─────────────────────────────

    [Fact]
    public async Task SaveAsync_ThenGetByRunId_ReturnsDraftRunWithCorrectPeriodAndStatus()
    {
        // TC-PAY-010-A: Arrange
        var runId = $"pr_{Guid.NewGuid():N}";
        var run = CreateDraftRun(runId, TenantId, period: "2026-03");

        // Act
        var saveResult = await _repo.SaveAsync(run);
        var getResult = await _repo.GetByRunIdAsync(TenantId, runId);

        // Assert
        saveResult.IsSuccess.Should().BeTrue();
        getResult.IsSuccess.Should().BeTrue();
        getResult.Value!.Id.Should().Be(runId);
        getResult.Value.TenantId.Should().Be(TenantId);
        getResult.Value.Period.Should().Be("2026-03");
        getResult.Value.Status.Should().Be(PayrollRunStatus.Draft);
        getResult.Value.RunType.Should().Be(PayFrequency.Monthly);
    }

    // ── TC-PAY-010-B: Tenant isolation ────────────────────────────────────────

    [Fact]
    public async Task GetByRunIdAsync_WrongTenant_ReturnsFailure()
    {
        // TC-PAY-010-B: Arrange
        var runId = $"pr_{Guid.NewGuid():N}";
        var run = CreateDraftRun(runId, TenantId);
        await _repo.SaveAsync(run);

        // Act — query using a different tenant
        var result = await _repo.GetByRunIdAsync("other-tenant-xyz", runId);

        // Assert
        result.IsFailure.Should().BeTrue(because: "cross-tenant access must be rejected at the repository layer");
    }

    // ── TC-PAY-010-C: ListByTenantAsync descending order ─────────────────────

    [Fact]
    public async Task ListByTenantAsync_ReturnsRunsInDescendingCreatedAtOrder()
    {
        // TC-PAY-010-C: Arrange — create three runs with slight timestamp separation
        var run1 = CreateDraftRun($"pr_{Guid.NewGuid():N}", TenantId, period: "2026-01",
            createdAt: DateTimeOffset.UtcNow.AddSeconds(-10));
        var run2 = CreateDraftRun($"pr_{Guid.NewGuid():N}", TenantId, period: "2026-02",
            createdAt: DateTimeOffset.UtcNow.AddSeconds(-5));
        var run3 = CreateDraftRun($"pr_{Guid.NewGuid():N}", TenantId, period: "2026-03",
            createdAt: DateTimeOffset.UtcNow);

        await _repo.SaveAsync(run1);
        await _repo.SaveAsync(run2);
        await _repo.SaveAsync(run3);

        // Act
        var runs = await _repo.ListByTenantAsync(TenantId);

        // Assert — most recently created run is first
        runs.Should().HaveCountGreaterThanOrEqualTo(3);
        var tenantRuns = runs.Where(r => new[] { run1.Id, run2.Id, run3.Id }.Contains(r.Id)).ToList();
        tenantRuns.Should().HaveCount(3);

        // Verify descending order by checking run3 (newest) comes before run1 (oldest)
        var run3Index = tenantRuns.FindIndex(r => r.Id == run3.Id);
        var run1Index = tenantRuns.FindIndex(r => r.Id == run1.Id);
        run3Index.Should().BeLessThan(run1Index,
            because: "runs should be returned newest-first (descending created_at)");
    }

    // ── TC-PAY-010-D: ListByStatusAsync filters by status ────────────────────

    [Fact]
    public async Task ListByStatusAsync_ReturnsOnlyRunsMatchingGivenStatus()
    {
        // TC-PAY-010-D: Arrange
        var draftRun = CreateDraftRun($"pr_{Guid.NewGuid():N}", TenantId);
        var calculatedRun = CreateDraftRun($"pr_{Guid.NewGuid():N}", TenantId);

        // Transition calculatedRun to Calculated status
        calculatedRun.MarkCalculated(
            grossTotal: new MoneyZAR(50_000m),
            payeTotal: new MoneyZAR(10_000m),
            uifTotal: new MoneyZAR(354.24m),
            sdlTotal: new MoneyZAR(500m),
            etiTotal: MoneyZAR.Zero,
            deductionTotal: new MoneyZAR(10_354.24m),
            netTotal: new MoneyZAR(39_645.76m),
            complianceFlags: ["CTL-SARS-001:PASS"],
            actorId: "uid-sarah-hr",
            now: DateTimeOffset.UtcNow);

        await _repo.SaveAsync(draftRun);
        await _repo.SaveAsync(calculatedRun);

        // Act
        var draftRuns = await _repo.ListByStatusAsync(TenantId, PayrollRunStatus.Draft);
        var calculatedRuns = await _repo.ListByStatusAsync(TenantId, PayrollRunStatus.Calculated);

        // Assert
        draftRuns.Should().Contain(r => r.Id == draftRun.Id);
        draftRuns.Should().NotContain(r => r.Id == calculatedRun.Id);
        calculatedRuns.Should().Contain(r => r.Id == calculatedRun.Id);
        calculatedRuns.Should().NotContain(r => r.Id == draftRun.Id);
    }

    // ── TC-PAY-010-E: Persist aggregate totals after MarkCalculated ───────────

    [Fact]
    public async Task SaveAsync_AfterMarkCalculated_PersistsAggregateTotals()
    {
        // TC-PAY-010-E: Arrange
        var runId = $"pr_{Guid.NewGuid():N}";
        var run = CreateDraftRun(runId, TenantId);

        var expectedGross = new MoneyZAR(80_000m);
        var expectedPaye = new MoneyZAR(15_000m);
        var expectedUif = new MoneyZAR(354.24m);
        var expectedSdl = new MoneyZAR(800m);
        var expectedNet = new MoneyZAR(63_845.76m);

        run.MarkCalculated(
            grossTotal: expectedGross,
            payeTotal: expectedPaye,
            uifTotal: expectedUif,
            sdlTotal: expectedSdl,
            etiTotal: MoneyZAR.Zero,
            deductionTotal: new MoneyZAR(16_154.24m),
            netTotal: expectedNet,
            complianceFlags: ["CTL-SARS-001:PASS", "CTL-SARS-005:PASS"],
            actorId: "uid-sarah-hr",
            now: DateTimeOffset.UtcNow);

        // Act
        await _repo.SaveAsync(run);
        var getResult = await _repo.GetByRunIdAsync(TenantId, runId);

        // Assert
        getResult.IsSuccess.Should().BeTrue();
        var retrieved = getResult.Value!;
        retrieved.Status.Should().Be(PayrollRunStatus.Calculated);
        retrieved.GrossTotal.Amount.Should().Be(expectedGross.Amount);
        retrieved.PayeTotal.Amount.Should().Be(expectedPaye.Amount);
        retrieved.UifTotal.Amount.Should().Be(expectedUif.Amount);
        retrieved.SdlTotal.Amount.Should().Be(expectedSdl.Amount);
        retrieved.NetTotal.Amount.Should().Be(expectedNet.Amount);
        retrieved.CalculatedAt.Should().NotBeNull();
        retrieved.ComplianceFlags.Should().Contain("CTL-SARS-001:PASS");
    }

    // ── TC-PAY-010-F: Persist Finalized status and checksum ──────────────────

    [Fact]
    public async Task SaveAsync_AfterFinalize_PersistsFinalizedStatusAndChecksum()
    {
        // TC-PAY-010-F: Arrange
        var runId = $"pr_{Guid.NewGuid():N}";
        var run = CreateDraftRun(runId, TenantId);
        var now = DateTimeOffset.UtcNow;

        run.MarkCalculated(
            grossTotal: new MoneyZAR(50_000m),
            payeTotal: new MoneyZAR(10_000m),
            uifTotal: new MoneyZAR(354.24m),
            sdlTotal: new MoneyZAR(500m),
            etiTotal: MoneyZAR.Zero,
            deductionTotal: new MoneyZAR(10_354.24m),
            netTotal: new MoneyZAR(39_645.76m),
            complianceFlags: ["CTL-SARS-001:PASS"],
            actorId: "uid-sarah-hr",
            now: now);

        var checksum = "sha256-abc123def456";
        run.Finalize(checksum, finalizedBy: "uid-sarah-hr", now: now.AddSeconds(1));

        // Act
        await _repo.SaveAsync(run);
        var getResult = await _repo.GetByRunIdAsync(TenantId, runId);

        // Assert
        getResult.IsSuccess.Should().BeTrue();
        var retrieved = getResult.Value!;
        retrieved.Status.Should().Be(PayrollRunStatus.Finalized);
        retrieved.Checksum.Should().Be(checksum);
        retrieved.FinalizedBy.Should().Be("uid-sarah-hr");
        retrieved.FinalizedAt.Should().NotBeNull();
    }

    // ── TC-PAY-010-G: Immutability guard — Filed → non-Filed rejected ─────────

    [Fact]
    public async Task SaveAsync_WhenRunIsFiled_RejectsNonFiledOverwrite()
    {
        // TC-PAY-010-G: Arrange — build a Filed run
        var runId = $"pr_{Guid.NewGuid():N}";
        var run = CreateDraftRun(runId, TenantId);
        var now = DateTimeOffset.UtcNow;

        run.MarkCalculated(
            grossTotal: new MoneyZAR(50_000m),
            payeTotal: new MoneyZAR(10_000m),
            uifTotal: new MoneyZAR(354.24m),
            sdlTotal: new MoneyZAR(500m),
            etiTotal: MoneyZAR.Zero,
            deductionTotal: new MoneyZAR(10_354.24m),
            netTotal: new MoneyZAR(39_645.76m),
            complianceFlags: ["CTL-SARS-001:PASS"],
            actorId: "uid-sarah-hr",
            now: now);

        run.Finalize("sha256-checksum-xyz", "uid-sarah-hr", now.AddSeconds(1));
        run.MarkFiled("uid-sarah-hr", now.AddSeconds(2));

        // Persist Filed state
        await _repo.SaveAsync(run);

        // Create a Draft run with the same ID — simulates illegal overwrite
        var impostor = CreateDraftRun(runId, TenantId);

        // Act — attempt to save Draft over Filed
        var result = await _repo.SaveAsync(impostor);

        // Assert — immutability guard must reject this
        result.IsFailure.Should().BeTrue(because: "Filed runs are terminal — no non-Filed write may overwrite them (CTL-SARS-001)");
    }

    // ── TC-PAY-010-H: ListByPeriodAsync returns only matching period ──────────

    [Fact]
    public async Task ListByPeriodAsync_ReturnsOnlyRunsForMatchingPeriod()
    {
        // TC-PAY-010-H: Arrange
        var marchRun = CreateDraftRun($"pr_{Guid.NewGuid():N}", TenantId, period: "2026-03");
        var aprilRun = CreateDraftRun($"pr_{Guid.NewGuid():N}", TenantId, period: "2026-04");

        await _repo.SaveAsync(marchRun);
        await _repo.SaveAsync(aprilRun);

        // Act
        var marchResults = await _repo.ListByPeriodAsync(TenantId, "2026-03");
        var aprilResults = await _repo.ListByPeriodAsync(TenantId, "2026-04");

        // Assert
        marchResults.Should().Contain(r => r.Id == marchRun.Id);
        marchResults.Should().NotContain(r => r.Id == aprilRun.Id);
        aprilResults.Should().Contain(r => r.Id == aprilRun.Id);
        aprilResults.Should().NotContain(r => r.Id == marchRun.Id);
    }

    // ── Helper factory ────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a PayrollRun in Draft status for use in tests.
    /// TC-PAY-010: Factory ensures consistent test data.
    /// </summary>
    private static PayrollRun CreateDraftRun(
        string runId,
        string tenantId,
        string period = "2026-03",
        DateTimeOffset? createdAt = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        var result = PayrollRun.Create(
            id: runId,
            tenantId: tenantId,
            period: period,
            runType: PayFrequency.Monthly,
            employeeIds: ["emp-001", "emp-002"],
            ruleSetVersion: "SARS_PAYE_2026",
            initiatedBy: "uid-sarah-hr",
            idempotencyKey: Guid.NewGuid().ToString("N"),
            now: now);

        result.IsSuccess.Should().BeTrue("CreateDraftRun factory should always succeed");
        return result.Value!;
    }
}
