// REQ-HR-002, CTL-BCEA-003: Append-only accrual ledger entry.
// Firestore schema: docs/schemas/firestore-collections.md §7.2.
// Every accrual, consumption, adjustment, forfeiture, or carryover is a separate entry.
// IMMUTABLE after creation — no updates or deletes permitted.
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Leave.Aggregates;

/// <summary>
/// Entry type for the append-only accrual ledger.
/// </summary>
public enum AccrualEntryType
{
    Unknown = 0,
    Accrual = 1,
    Consumption = 2,
    Adjustment = 3,
    Forfeiture = 4,
    Carryover = 5,
}

/// <summary>
/// Immutable append-only ledger entry for a single leave balance event.
/// Sum of all ledger entries must reconcile to parent <see cref="LeaveBalance"/> fields.
/// </summary>
public sealed class AccrualLedgerEntry
{
    public string LedgerEntryId { get; }
    public string BalanceId { get; }
    public string TenantId { get; }
    public string EmployeeId { get; }
    public AccrualEntryType EntryType { get; }

    /// <summary>Hours affected. Positive for accrual/carryover, negative for consumption/forfeiture.</summary>
    public decimal Hours { get; }

    public DateOnly EffectiveDate { get; }
    public string ReasonCode { get; }

    /// <summary>FK to leave_requests. Non-null for Consumption entries only.</summary>
    public string? LeaveRequestId { get; }

    public string PolicyVersion { get; }

    /// <summary>Actor ID or "system" for automated accruals.</summary>
    public string PostedBy { get; }

    public DateTimeOffset CreatedAt { get; }

    private AccrualLedgerEntry(
        string ledgerEntryId,
        string balanceId,
        string tenantId,
        string employeeId,
        AccrualEntryType entryType,
        decimal hours,
        DateOnly effectiveDate,
        string reasonCode,
        string? leaveRequestId,
        string policyVersion,
        string postedBy,
        DateTimeOffset createdAt)
    {
        LedgerEntryId = ledgerEntryId;
        BalanceId = balanceId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        EntryType = entryType;
        Hours = hours;
        EffectiveDate = effectiveDate;
        ReasonCode = reasonCode;
        LeaveRequestId = leaveRequestId;
        PolicyVersion = policyVersion;
        PostedBy = postedBy;
        CreatedAt = createdAt;
    }

    /// <summary>Creates an immutable accrual ledger entry.</summary>
    public static Result<AccrualLedgerEntry> Create(
        string ledgerEntryId,
        string balanceId,
        string tenantId,
        string employeeId,
        AccrualEntryType entryType,
        decimal hours,
        DateOnly effectiveDate,
        string reasonCode,
        string? leaveRequestId,
        string policyVersion,
        string postedBy,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(ledgerEntryId))
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "LedgerEntryId is required.");
        if (string.IsNullOrWhiteSpace(balanceId))
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "BalanceId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (entryType == AccrualEntryType.Unknown)
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "EntryType must not be Unknown.");
        if (hours == 0)
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "Hours must not be zero.");
        if (string.IsNullOrWhiteSpace(reasonCode))
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "ReasonCode is required.");
        if (string.IsNullOrWhiteSpace(policyVersion))
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "PolicyVersion is required.");
        if (string.IsNullOrWhiteSpace(postedBy))
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValidationFailed, "PostedBy is required.");

        // Consumption and forfeiture must be negative hours.
        if ((entryType == AccrualEntryType.Consumption || entryType == AccrualEntryType.Forfeiture) && hours > 0)
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValueOutOfRange,
                $"{entryType} entries must have negative hours.");

        // Accrual and carryover must be positive hours.
        if ((entryType == AccrualEntryType.Accrual || entryType == AccrualEntryType.Carryover) && hours < 0)
            return Result<AccrualLedgerEntry>.Failure(ZenoHrErrorCode.ValueOutOfRange,
                $"{entryType} entries must have positive hours.");

        return Result<AccrualLedgerEntry>.Success(new AccrualLedgerEntry(
            ledgerEntryId, balanceId, tenantId, employeeId, entryType, hours,
            effectiveDate, reasonCode, leaveRequestId, policyVersion, postedBy, now));
    }

    /// <summary>Reconstitutes a ledger entry from Firestore (read-path only).</summary>
    public static AccrualLedgerEntry Reconstitute(
        string ledgerEntryId, string balanceId, string tenantId, string employeeId,
        AccrualEntryType entryType, decimal hours, DateOnly effectiveDate, string reasonCode,
        string? leaveRequestId, string policyVersion, string postedBy, DateTimeOffset createdAt)
        => new(ledgerEntryId, balanceId, tenantId, employeeId, entryType, hours,
               effectiveDate, reasonCode, leaveRequestId, policyVersion, postedBy, createdAt);
}
