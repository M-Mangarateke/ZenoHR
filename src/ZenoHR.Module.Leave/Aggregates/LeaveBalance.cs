// REQ-HR-002, CTL-BCEA-003, CTL-BCEA-004: Leave balance aggregate root.
// Firestore schema: docs/schemas/firestore-collections.md §7.1.
// One balance per employee per leave type per cycle.
// Accruals, consumptions, and adjustments post to the append-only accrual_ledger subcollection.
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Leave.Events;

namespace ZenoHR.Module.Leave.Aggregates;

/// <summary>
/// Aggregate root tracking an employee's leave balance for one leave type in one cycle.
/// Mutations always produce a corresponding <see cref="AccrualLedgerEntry"/> to maintain the
/// append-only audit trail required by POPIA and BCEA.
/// </summary>
public sealed class LeaveBalance
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Deterministic ID: <c>lb_&lt;emp_short&gt;_&lt;type&gt;_&lt;cycle&gt;</c>.</summary>
    public string BalanceId { get; }

    /// <summary>Tenant isolation key.</summary>
    public string TenantId { get; }

    /// <summary>FK to employees collection.</summary>
    public string EmployeeId { get; }

    /// <summary>Leave type (Annual, Sick, FamilyResponsibility, Maternity, Parental).</summary>
    public LeaveType LeaveType { get; }

    /// <summary>Leave cycle identifier (e.g. "2026" for annual cycle).</summary>
    public string CycleId { get; }

    // ── Balance fields ────────────────────────────────────────────────────────

    /// <summary>Total accrued hours. Non-negative. Incremented by monthly accrual jobs.</summary>
    public decimal AccruedHours { get; private set; }

    /// <summary>Total consumed hours. Non-negative. Incremented when leave is approved.</summary>
    public decimal ConsumedHours { get; private set; }

    /// <summary>Net adjustment hours (can be negative for clawbacks).</summary>
    public decimal AdjustmentHours { get; private set; }

    /// <summary>Derived available hours: AccruedHours - ConsumedHours + AdjustmentHours.</summary>
    public decimal AvailableHours => AccruedHours - ConsumedHours + AdjustmentHours;

    // ── Policy ────────────────────────────────────────────────────────────────

    public string PolicyVersion { get; }
    public DateOnly LastAccrualDate { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public string SchemaVersion { get; } = "1.0";

    // ── Pending ledger entries (populated during mutations, persisted by infrastructure) ─

    private readonly List<AccrualLedgerEntry> _pendingEntries = [];

    /// <summary>Pops pending ledger entries for persistence. Called by infrastructure after saving the balance.</summary>
    public IReadOnlyList<AccrualLedgerEntry> PopPendingEntries()
    {
        var entries = _pendingEntries.ToList();
        _pendingEntries.Clear();
        return entries;
    }

    // ── Domain events ─────────────────────────────────────────────────────────

    private readonly List<object> _domainEvents = [];

    public IReadOnlyList<object> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    private LeaveBalance(
        string balanceId, string tenantId, string employeeId,
        LeaveType leaveType, string cycleId, string policyVersion, DateTimeOffset now)
    {
        BalanceId = balanceId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        LeaveType = leaveType;
        CycleId = cycleId;
        PolicyVersion = policyVersion;
        AccruedHours = 0;
        ConsumedHours = 0;
        AdjustmentHours = 0;
        LastAccrualDate = DateOnly.FromDateTime(now.UtcDateTime);
        CreatedAt = now;
        UpdatedAt = now;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new zero-balance leave balance for an employee/type/cycle.
    /// Called during employee onboarding and at cycle rollover.
    /// </summary>
    public static Result<LeaveBalance> Create(
        string balanceId, string tenantId, string employeeId,
        LeaveType leaveType, string cycleId, string policyVersion, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(balanceId))
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValidationFailed, "BalanceId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (leaveType == LeaveType.Unknown)
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.InvalidLeaveType, "LeaveType must not be Unknown.");
        if (string.IsNullOrWhiteSpace(cycleId))
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValidationFailed, "CycleId is required.");
        if (string.IsNullOrWhiteSpace(policyVersion))
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValidationFailed, "PolicyVersion is required.");

        return Result<LeaveBalance>.Success(new LeaveBalance(balanceId, tenantId, employeeId, leaveType, cycleId, policyVersion, now));
    }

    // ── Accrual ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Posts a monthly accrual to the balance.
    /// Called by the background accrual job. Hours come from StatutoryRuleSet — never hardcoded.
    /// </summary>
    public Result<LeaveBalance> PostAccrual(
        string ledgerEntryId,
        decimal hours,
        DateOnly accrualDate,
        string reasonCode,
        string policyVersion,
        DateTimeOffset now)
    {
        if (hours <= 0)
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValueOutOfRange, "Accrual hours must be positive.");

        var entryResult = AccrualLedgerEntry.Create(
            ledgerEntryId, BalanceId, TenantId, EmployeeId,
            AccrualEntryType.Accrual, hours, accrualDate, reasonCode, null, policyVersion, "system", now);

        if (entryResult.IsFailure) return Result<LeaveBalance>.Failure(entryResult.Error!);

        AccruedHours += hours;
        LastAccrualDate = accrualDate;
        UpdatedAt = now;

        _pendingEntries.Add(entryResult.Value!);
        return Result<LeaveBalance>.Success(this);
    }

    // ── Consumption (on leave approval) ──────────────────────────────────────

    /// <summary>
    /// Consumes leave hours when a leave request is approved.
    /// BCEA §20: Annual leave balance cannot become negative without a policy exception.
    /// </summary>
    public Result<LeaveBalance> ConsumeHours(
        string ledgerEntryId,
        string leaveRequestId,
        decimal hours,
        DateOnly effectiveDate,
        string policyVersion,
        string actorId,
        DateTimeOffset now,
        bool allowNegativeBalance = false)
    {
        if (hours <= 0)
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValueOutOfRange, "Hours to consume must be positive.");
        if (string.IsNullOrWhiteSpace(leaveRequestId))
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValidationFailed, "LeaveRequestId is required.");

        // CTL-BCEA-003: Block negative balance unless policy exception granted.
        if (!allowNegativeBalance && AvailableHours < hours)
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.InsufficientLeaveBalance,
                $"Insufficient leave balance. Available: {AvailableHours:F2}h, Requested: {hours:F2}h.");

        var entryResult = AccrualLedgerEntry.Create(
            ledgerEntryId, BalanceId, TenantId, EmployeeId,
            AccrualEntryType.Consumption, -hours, effectiveDate,
            "leave_taken", leaveRequestId, policyVersion, actorId, now);

        if (entryResult.IsFailure) return Result<LeaveBalance>.Failure(entryResult.Error!);

        ConsumedHours += hours;
        UpdatedAt = now;

        _pendingEntries.Add(entryResult.Value!);
        return Result<LeaveBalance>.Success(this);
    }

    // ── Reversal (on leave rejection/cancellation) ────────────────────────────

    /// <summary>
    /// Reverses a prior consumption when leave is rejected or cancelled.
    /// </summary>
    public Result<LeaveBalance> ReverseConsumption(
        string ledgerEntryId,
        string leaveRequestId,
        decimal hours,
        DateOnly effectiveDate,
        string policyVersion,
        string actorId,
        DateTimeOffset now)
    {
        if (hours <= 0)
            return Result<LeaveBalance>.Failure(ZenoHrErrorCode.ValueOutOfRange, "Hours to reverse must be positive.");

        var entryResult = AccrualLedgerEntry.Create(
            ledgerEntryId, BalanceId, TenantId, EmployeeId,
            AccrualEntryType.Adjustment, hours, effectiveDate,
            "leave_reversal", leaveRequestId, policyVersion, actorId, now);

        if (entryResult.IsFailure) return Result<LeaveBalance>.Failure(entryResult.Error!);

        ConsumedHours = Math.Max(0, ConsumedHours - hours);
        AdjustmentHours += hours;
        UpdatedAt = now;

        _pendingEntries.Add(entryResult.Value!);
        return Result<LeaveBalance>.Success(this);
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    /// <summary>Reconstitutes a LeaveBalance from Firestore. No domain events raised.</summary>
    public static LeaveBalance Reconstitute(
        string balanceId, string tenantId, string employeeId, LeaveType leaveType,
        string cycleId, decimal accruedHours, decimal consumedHours, decimal adjustmentHours,
        string policyVersion, DateOnly lastAccrualDate, DateTimeOffset createdAt, DateTimeOffset updatedAt)
    {
        var b = new LeaveBalance(balanceId, tenantId, employeeId, leaveType, cycleId, policyVersion, createdAt)
        {
            AccruedHours = accruedHours,
            ConsumedHours = consumedHours,
            AdjustmentHours = adjustmentHours,
            LastAccrualDate = lastAccrualDate,
            UpdatedAt = updatedAt,
        };
        return b;
    }
}
