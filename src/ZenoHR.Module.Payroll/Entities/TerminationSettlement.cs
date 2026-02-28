// REQ-HR-001, CTL-BCEA-006, CTL-BCEA-007, PRD-16 Section 12: Termination settlement entity.
// Firestore schema: docs/schemas/firestore-collections.md §8.4.
// Calculates notice pay, severance pay, and accrued leave payout on termination.
// Amounts from StatutoryRuleSet — never hardcoded.
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Payroll.Entities;

/// <summary>
/// Lifecycle status of a termination settlement.
/// </summary>
public enum TerminationSettlementStatus
{
    Unknown = 0,
    Draft = 1,
    Calculated = 2,
    Approved = 3,
    Finalized = 4,
}

/// <summary>
/// Entity representing a termination settlement for an employee.
/// Aggregates notice pay, severance, and leave payout.
/// Must pass legal check before finalization (CTL-BCEA-006).
/// </summary>
public sealed class TerminationSettlement
{
    // ── Identity ──────────────────────────────────────────────────────────────

    public string SettlementId { get; }
    public string TenantId { get; }
    public string EmployeeId { get; }

    /// <summary>FK reference to termination_cases collection.</summary>
    public string TerminationCaseId { get; }

    // ── Settlement components ─────────────────────────────────────────────────

    /// <summary>Notice period pay. Calculated from contracted salary and notice period days.</summary>
    public MoneyZAR NoticePay { get; private set; }

    /// <summary>Severance pay (CTL-BCEA-006). Zero if not applicable (e.g. voluntary resignation).</summary>
    public MoneyZAR SeverancePay { get; private set; }

    /// <summary>Accrued annual leave payout on termination (CTL-BCEA-007).</summary>
    public MoneyZAR LeavePayoutZar { get; private set; }

    /// <summary>Total settlement = notice_pay + severance_pay + leave_payout.</summary>
    public MoneyZAR TotalSettlement => NoticePay + SeverancePay + LeavePayoutZar;

    // ── Policy and versioning ─────────────────────────────────────────────────

    public string PolicyVersion { get; }
    public string RuleSetVersion { get; }

    // ── Legal check ───────────────────────────────────────────────────────────

    /// <summary>Legal check status. Must be "passed" before finalization.</summary>
    public string LegalCheckStatus { get; private set; }

    /// <summary>Compliance control results (e.g. CTL-BCEA-006: PASS).</summary>
    public IReadOnlyList<string> ComplianceControlResults { get; private set; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public TerminationSettlementStatus Status { get; private set; }
    public string? ApprovedBy { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string CreatedBy { get; }
    public string SchemaVersion { get; } = "1.0";

    // ── Constructor ───────────────────────────────────────────────────────────

    private TerminationSettlement(
        string settlementId, string tenantId, string employeeId, string terminationCaseId,
        string policyVersion, string ruleSetVersion, string createdBy, DateTimeOffset now)
    {
        SettlementId = settlementId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        TerminationCaseId = terminationCaseId;
        PolicyVersion = policyVersion;
        RuleSetVersion = ruleSetVersion;
        NoticePay = MoneyZAR.Zero;
        SeverancePay = MoneyZAR.Zero;
        LeavePayoutZar = MoneyZAR.Zero;
        LegalCheckStatus = "pending";
        ComplianceControlResults = [];
        Status = TerminationSettlementStatus.Draft;
        CreatedAt = now;
        UpdatedAt = now;
        CreatedBy = createdBy;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>Creates a new termination settlement in Draft status.</summary>
    public static Result<TerminationSettlement> Create(
        string settlementId, string tenantId, string employeeId, string terminationCaseId,
        string policyVersion, string ruleSetVersion, string createdBy, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(settlementId))
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValidationFailed, "SettlementId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (string.IsNullOrWhiteSpace(terminationCaseId))
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValidationFailed, "TerminationCaseId is required.");
        if (string.IsNullOrWhiteSpace(policyVersion))
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValidationFailed, "PolicyVersion is required.");
        if (string.IsNullOrWhiteSpace(ruleSetVersion))
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValidationFailed, "RuleSetVersion is required.");

        return Result<TerminationSettlement>.Success(
            new TerminationSettlement(settlementId, tenantId, employeeId, terminationCaseId,
                policyVersion, ruleSetVersion, createdBy, now));
    }

    // ── Calculation ───────────────────────────────────────────────────────────

    /// <summary>
    /// Sets calculated settlement amounts. Transitions to Calculated status.
    /// All amounts must come from the statutory rule set — never hardcoded.
    /// </summary>
    /// <param name="noticePay">Notice period pay per contracted daily rate × notice days.</param>
    /// <param name="severancePay">
    /// Severance: 1 week's pay per completed year of service (CTL-BCEA-006).
    /// Zero for voluntary resignation or misconduct dismissal.
    /// </param>
    /// <param name="leavePayoutZar">Accrued leave balance × daily rate at termination (CTL-BCEA-007).</param>
    /// <param name="complianceResults">Compliance control result strings (e.g. "CTL-BCEA-006:PASS").</param>
    public Result<TerminationSettlement> SetCalculated(
        MoneyZAR noticePay,
        MoneyZAR severancePay,
        MoneyZAR leavePayoutZar,
        IReadOnlyList<string> complianceResults,
        string actorId,
        DateTimeOffset now)
    {
        if (Status != TerminationSettlementStatus.Draft)
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.InvalidEmployeeState,
                $"Cannot calculate: settlement is in {Status} status (expected Draft).");
        if (noticePay < MoneyZAR.Zero)
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValueOutOfRange, "NoticePay cannot be negative.");
        if (severancePay < MoneyZAR.Zero)
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValueOutOfRange, "SeverancePay cannot be negative.");
        if (leavePayoutZar < MoneyZAR.Zero)
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ValueOutOfRange, "LeavePayoutZar cannot be negative.");

        NoticePay = noticePay;
        SeverancePay = severancePay;
        LeavePayoutZar = leavePayoutZar;
        ComplianceControlResults = complianceResults;

        // Derive legal check status from compliance results.
        var hasFail = complianceResults.Any(r => r.EndsWith(":FAIL", StringComparison.OrdinalIgnoreCase));
        LegalCheckStatus = hasFail ? "failed" : "passed";

        Status = TerminationSettlementStatus.Calculated;
        UpdatedAt = now;

        return Result<TerminationSettlement>.Success(this);
    }

    /// <summary>
    /// Approves the settlement. Requires legal check to have passed.
    /// </summary>
    public Result<TerminationSettlement> Approve(string approverId, DateTimeOffset now)
    {
        if (Status != TerminationSettlementStatus.Calculated)
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.InvalidEmployeeState,
                $"Cannot approve: settlement is in {Status} status (expected Calculated).");
        if (LegalCheckStatus != "passed")
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.ComplianceCheckFailed,
                "Cannot approve: legal check has not passed (CTL-BCEA-006).");

        ApprovedBy = approverId;
        Status = TerminationSettlementStatus.Approved;
        UpdatedAt = now;

        return Result<TerminationSettlement>.Success(this);
    }

    /// <summary>
    /// Finalizes the settlement. Immutable after this point.
    /// </summary>
    public Result<TerminationSettlement> Finalize(string actorId, DateTimeOffset now)
    {
        if (Status != TerminationSettlementStatus.Approved)
            return Result<TerminationSettlement>.Failure(ZenoHrErrorCode.InvalidEmployeeState,
                $"Cannot finalize: settlement is in {Status} status (expected Approved).");

        Status = TerminationSettlementStatus.Finalized;
        UpdatedAt = now;

        return Result<TerminationSettlement>.Success(this);
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    public static TerminationSettlement Reconstitute(
        string settlementId, string tenantId, string employeeId, string terminationCaseId,
        MoneyZAR noticePay, MoneyZAR severancePay, MoneyZAR leavePayoutZar,
        string policyVersion, string ruleSetVersion, string legalCheckStatus,
        IReadOnlyList<string> complianceControlResults,
        TerminationSettlementStatus status, string? approvedBy,
        DateTimeOffset createdAt, DateTimeOffset updatedAt, string createdBy)
    {
        var s = new TerminationSettlement(settlementId, tenantId, employeeId, terminationCaseId,
            policyVersion, ruleSetVersion, createdBy, createdAt)
        {
            NoticePay = noticePay,
            SeverancePay = severancePay,
            LeavePayoutZar = leavePayoutZar,
            LegalCheckStatus = legalCheckStatus,
            ComplianceControlResults = complianceControlResults,
            Status = status,
            ApprovedBy = approvedBy,
            UpdatedAt = updatedAt,
        };
        return s;
    }
}
