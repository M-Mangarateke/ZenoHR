// REQ-HR-003, CTL-SARS-001: Post-finalization payroll adjustment (compensating record).
// Firestore schema: docs/schemas/firestore-collections.md §8.3.
// Append-only — immutable once created.

using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Payroll.Entities;

/// <summary>
/// Post-finalization correction to a finalized payroll result.
/// Append-only — adjustments are never updated or deleted.
/// Firestore collection: <c>payroll_adjustments</c> (root, tenant-scoped).
/// </summary>
public sealed class PayrollAdjustment
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>UUID v7. Matches Firestore document ID.</summary>
    public string AdjustmentId { get; }

    /// <summary>Tenant isolation key.</summary>
    public string TenantId { get; }

    // ── References ────────────────────────────────────────────────────────────

    /// <summary>FK to the finalized <c>payroll_runs</c> document.</summary>
    public string PayrollRunId { get; }

    /// <summary>FK to the affected <c>employees</c> document.</summary>
    public string EmployeeId { get; }

    // ── Adjustment detail ─────────────────────────────────────────────────────

    /// <summary>Nature of the adjustment.</summary>
    public PayrollAdjustmentType AdjustmentType { get; }

    /// <summary>Human-readable explanation of the adjustment reason.</summary>
    public string Reason { get; }

    /// <summary>Adjustment amount. Can be negative (e.g., overpayment reversal).</summary>
    public MoneyZAR Amount { get; }

    /// <summary>Payroll result field names this adjustment affects (e.g. ["paye_zar", "net_pay_zar"]).</summary>
    public IReadOnlyList<string> AffectedFields { get; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    /// <summary>Actor ID who created this adjustment.</summary>
    public string CreatedBy { get; }

    /// <summary>Actor ID of the approver. Optional — depends on business policy.</summary>
    public string? ApprovedBy { get; }

    /// <summary>Server timestamp when created. Immutable.</summary>
    public DateTimeOffset CreatedAt { get; }

    public string SchemaVersion { get; } = "1.0";

    // ── Constructor (private — use factory) ───────────────────────────────────

    private PayrollAdjustment(
        string adjustmentId, string tenantId,
        string payrollRunId, string employeeId,
        PayrollAdjustmentType adjustmentType, string reason,
        MoneyZAR amount, IReadOnlyList<string> affectedFields,
        string createdBy, string? approvedBy, DateTimeOffset createdAt)
    {
        AdjustmentId = adjustmentId;
        TenantId = tenantId;
        PayrollRunId = payrollRunId;
        EmployeeId = employeeId;
        AdjustmentType = adjustmentType;
        Reason = reason;
        Amount = amount;
        AffectedFields = affectedFields;
        CreatedBy = createdBy;
        ApprovedBy = approvedBy;
        CreatedAt = createdAt;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an immutable <see cref="PayrollAdjustment"/>.
    /// REQ-HR-003: Must reference a finalized payroll run.
    /// </summary>
    public static Result<PayrollAdjustment> Create(
        string adjustmentId, string tenantId,
        string payrollRunId, string employeeId,
        PayrollAdjustmentType adjustmentType,
        string reason, MoneyZAR amount,
        IReadOnlyList<string> affectedFields,
        string createdBy, string? approvedBy,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(adjustmentId))
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "AdjustmentId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(payrollRunId))
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "PayrollRunId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (adjustmentType == PayrollAdjustmentType.Unknown)
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "AdjustmentType must not be Unknown.");
        if (string.IsNullOrWhiteSpace(reason))
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "Reason is required.");
        if (affectedFields.Count == 0)
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "AffectedFields must contain at least one entry.");
        if (string.IsNullOrWhiteSpace(createdBy))
            return Result<PayrollAdjustment>.Failure(ZenoHrErrorCode.ValidationFailed, "CreatedBy is required.");

        return Result<PayrollAdjustment>.Success(new PayrollAdjustment(
            adjustmentId, tenantId, payrollRunId, employeeId,
            adjustmentType, reason, amount, affectedFields,
            createdBy, approvedBy, now));
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    /// <summary>Reconstitutes a <see cref="PayrollAdjustment"/> from Firestore (read-path, no validation).</summary>
    public static PayrollAdjustment Reconstitute(
        string adjustmentId, string tenantId,
        string payrollRunId, string employeeId,
        PayrollAdjustmentType adjustmentType, string reason,
        MoneyZAR amount, IReadOnlyList<string> affectedFields,
        string createdBy, string? approvedBy, DateTimeOffset createdAt)
        => new(adjustmentId, tenantId, payrollRunId, employeeId,
               adjustmentType, reason, amount, affectedFields,
               createdBy, approvedBy, createdAt);
}

/// <summary>Types of post-finalization payroll adjustment.</summary>
public enum PayrollAdjustmentType
{
    Unknown = 0,

    /// <summary>Corrects an error in the original calculation (e.g. wrong salary captured).</summary>
    Correction = 1,

    /// <summary>Reverses a previously issued payment (e.g. erroneous duplicate payment).</summary>
    Reversal = 2,

    /// <summary>Additional payment in a later period (e.g. missed overtime).</summary>
    Supplementary = 3
}
