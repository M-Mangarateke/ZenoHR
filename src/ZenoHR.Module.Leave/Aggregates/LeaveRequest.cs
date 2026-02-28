// REQ-HR-002, CTL-BCEA-003, CTL-BCEA-004: Leave request aggregate.
// Firestore schema: docs/schemas/firestore-collections.md §7.3.
// State machine: Submitted → ManagerReview → Approved | Rejected | Cancelled.
// Director/HRManager self-approve leave (PRD-15 §1.5).
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Leave.Events;

namespace ZenoHR.Module.Leave.Aggregates;

/// <summary>
/// Aggregate representing a single leave request from an employee.
/// Approval consumes leave balance atomically via the <see cref="LeaveBalance"/> aggregate.
/// </summary>
public sealed class LeaveRequest
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>Firestore document ID. Pattern: <c>lr_&lt;uuid7&gt;</c>.</summary>
    public string LeaveRequestId { get; }

    /// <summary>Tenant isolation key.</summary>
    public string TenantId { get; }

    /// <summary>FK to employees collection.</summary>
    public string EmployeeId { get; }

    // ── Request details ───────────────────────────────────────────────────────

    public LeaveType LeaveType { get; }
    public DateOnly StartDate { get; }
    public DateOnly EndDate { get; }

    /// <summary>Total leave hours requested (calendar days × ordinary hours per day).</summary>
    public decimal TotalHours { get; }

    public string ReasonCode { get; }

    /// <summary>Available balance snapshot at submission time. Informational only.</summary>
    public decimal? BalanceSnapshotAtRequest { get; }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public LeaveRequestStatus Status { get; private set; }

    /// <summary>Actor ID of the approving manager. Null until approved/rejected.</summary>
    public string? ApproverId { get; private set; }

    public DateTimeOffset? ApprovedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    // ── Audit ─────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Actor ID of the requesting employee.</summary>
    public string CreatedBy { get; }

    public string SchemaVersion { get; } = "1.0";

    // ── Domain events ─────────────────────────────────────────────────────────

    private readonly List<object> _domainEvents = [];

    public IReadOnlyList<object> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }

    // ── Constructor ───────────────────────────────────────────────────────────

    private LeaveRequest(
        string leaveRequestId, string tenantId, string employeeId,
        LeaveType leaveType, DateOnly startDate, DateOnly endDate,
        decimal totalHours, string reasonCode, decimal? balanceSnapshotAtRequest,
        string createdBy, DateTimeOffset now)
    {
        LeaveRequestId = leaveRequestId;
        TenantId = tenantId;
        EmployeeId = employeeId;
        LeaveType = leaveType;
        StartDate = startDate;
        EndDate = endDate;
        TotalHours = totalHours;
        ReasonCode = reasonCode;
        BalanceSnapshotAtRequest = balanceSnapshotAtRequest;
        Status = LeaveRequestStatus.Submitted;
        CreatedBy = createdBy;
        CreatedAt = now;
        UpdatedAt = now;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a new <see cref="LeaveRequest"/> in <see cref="LeaveRequestStatus.Submitted"/> status.
    /// Raises <see cref="LeaveRequestSubmittedEvent"/>.
    /// </summary>
    public static Result<LeaveRequest> Submit(
        string leaveRequestId,
        string tenantId,
        string employeeId,
        LeaveType leaveType,
        DateOnly startDate,
        DateOnly endDate,
        decimal totalHours,
        string reasonCode,
        decimal? balanceSnapshotAtRequest,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(leaveRequestId))
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.ValidationFailed, "LeaveRequestId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (leaveType == LeaveType.Unknown)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.InvalidLeaveType, "LeaveType must not be Unknown.");
        if (endDate < startDate)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.ValueOutOfRange, "EndDate must be on or after StartDate.");
        if (totalHours <= 0)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.ValueOutOfRange, "TotalHours must be positive.");
        if (string.IsNullOrWhiteSpace(reasonCode))
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.ValidationFailed, "ReasonCode is required.");

        var request = new LeaveRequest(
            leaveRequestId, tenantId, employeeId, leaveType, startDate, endDate,
            totalHours, reasonCode, balanceSnapshotAtRequest, employeeId, now);

        request._domainEvents.Add(new LeaveRequestSubmittedEvent(
            leaveRequestId, employeeId, leaveType, startDate, endDate, totalHours)
            { TenantId = tenantId, ActorId = employeeId });

        return Result<LeaveRequest>.Success(request);
    }

    // ── State transitions ─────────────────────────────────────────────────────

    /// <summary>
    /// Moves request to ManagerReview for complex cases requiring additional scrutiny.
    /// </summary>
    public Result<LeaveRequest> SendToManagerReview(string approverId, DateTimeOffset now)
    {
        if (Status != LeaveRequestStatus.Submitted)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.LeaveRequestAlreadyProcessed,
                $"Cannot move to review: request is in {Status} status.");

        ApproverId = approverId;
        Status = LeaveRequestStatus.ManagerReview;
        UpdatedAt = now;
        return Result<LeaveRequest>.Success(this);
    }

    /// <summary>
    /// Approves the leave request. Raises <see cref="LeaveRequestApprovedEvent"/>.
    /// Director/HRManager may self-approve per PRD-15 §1.5.
    /// </summary>
    public Result<LeaveRequest> Approve(string approverId, DateTimeOffset now)
    {
        if (Status != LeaveRequestStatus.Submitted && Status != LeaveRequestStatus.ManagerReview)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.LeaveRequestAlreadyProcessed,
                $"Cannot approve: request is in {Status} status.");

        ApproverId = approverId;
        ApprovedAt = now;
        Status = LeaveRequestStatus.Approved;
        UpdatedAt = now;

        _domainEvents.Add(new LeaveRequestApprovedEvent(LeaveRequestId, EmployeeId, approverId, LeaveType, TotalHours)
            { TenantId = TenantId, ActorId = approverId });

        return Result<LeaveRequest>.Success(this);
    }

    /// <summary>
    /// Rejects the leave request. Raises <see cref="LeaveRequestRejectedEvent"/>.
    /// </summary>
    public Result<LeaveRequest> Reject(string approverId, string rejectionReason, DateTimeOffset now)
    {
        if (Status != LeaveRequestStatus.Submitted && Status != LeaveRequestStatus.ManagerReview)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.LeaveRequestAlreadyProcessed,
                $"Cannot reject: request is in {Status} status.");
        if (string.IsNullOrWhiteSpace(rejectionReason))
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.ValidationFailed, "RejectionReason is required.");

        ApproverId = approverId;
        RejectionReason = rejectionReason;
        Status = LeaveRequestStatus.Rejected;
        UpdatedAt = now;

        _domainEvents.Add(new LeaveRequestRejectedEvent(LeaveRequestId, EmployeeId, approverId, LeaveType, rejectionReason)
            { TenantId = TenantId, ActorId = approverId });

        return Result<LeaveRequest>.Success(this);
    }

    /// <summary>
    /// Cancels the leave request. Only permitted before approval.
    /// </summary>
    public Result<LeaveRequest> Cancel(string actorId, DateTimeOffset now)
    {
        if (Status == LeaveRequestStatus.Approved)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.LeaveRequestAlreadyProcessed,
                "Cannot cancel an already approved request. Use the withdrawal workflow.");
        if (Status == LeaveRequestStatus.Cancelled || Status == LeaveRequestStatus.Rejected)
            return Result<LeaveRequest>.Failure(ZenoHrErrorCode.LeaveRequestAlreadyProcessed,
                $"Request is already in terminal status: {Status}.");

        Status = LeaveRequestStatus.Cancelled;
        UpdatedAt = now;
        return Result<LeaveRequest>.Success(this);
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    public static LeaveRequest Reconstitute(
        string leaveRequestId, string tenantId, string employeeId,
        LeaveType leaveType, DateOnly startDate, DateOnly endDate,
        decimal totalHours, string reasonCode, decimal? balanceSnapshotAtRequest,
        LeaveRequestStatus status, string? approverId, DateTimeOffset? approvedAt,
        string? rejectionReason, DateTimeOffset createdAt, DateTimeOffset updatedAt, string createdBy)
    {
        var r = new LeaveRequest(leaveRequestId, tenantId, employeeId, leaveType, startDate, endDate,
            totalHours, reasonCode, balanceSnapshotAtRequest, createdBy, createdAt)
        {
            Status = status,
            ApproverId = approverId,
            ApprovedAt = approvedAt,
            RejectionReason = rejectionReason,
            UpdatedAt = updatedAt,
        };
        return r;
    }
}
