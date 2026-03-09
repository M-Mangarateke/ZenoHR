// REQ-HR-002, CTL-BCEA-003, CTL-BCEA-004: Leave request Firestore repository.
// Collection: leave_requests (root — queried cross-employee by managers).
// State machine enforcement: Submitted → ManagerReview → Approved | Rejected | Cancelled.

using Microsoft.Extensions.Logging;
using Google.Cloud.Firestore;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Leave.Aggregates;

namespace ZenoHR.Infrastructure.Firestore;

/// <summary>
/// Firestore repository for the <c>leave_requests</c> root collection.
/// Root collection (not subcollection) to support manager cross-employee queries.
/// CTL-BCEA-004: Managers approve leave for their department only.
/// </summary>
public sealed class LeaveRequestRepository : BaseFirestoreRepository<LeaveRequest>
{
    public LeaveRequestRepository(FirestoreDb db, ILogger<LeaveRequestRepository> logger) : base(db, logger) { }

    protected override string CollectionName => "leave_requests";
    protected override ZenoHrErrorCode NotFoundErrorCode => ZenoHrErrorCode.LeaveRequestNotFound;

    // ── Hydration ────────────────────────────────────────────────────────────

    protected override LeaveRequest FromSnapshot(DocumentSnapshot snapshot)
    {
        var leaveType = ParseLeaveType(snapshot.GetValue<string>("leave_type"));
        var status = ParseStatus(snapshot.GetValue<string>("status"));

        string? approverId = null;
        snapshot.TryGetValue("approver_id", out approverId);

        DateTimeOffset? approvedAt = null;
        if (snapshot.TryGetValue<Timestamp>("approved_at", out var approvedAtTs))
            approvedAt = approvedAtTs.ToDateTimeOffset();

        string? rejectionReason = null;
        snapshot.TryGetValue("rejection_reason", out rejectionReason);

        double? balanceSnapshot = null;
        if (snapshot.TryGetValue<double>("balance_snapshot_at_request", out var bsRaw))
            balanceSnapshot = bsRaw;
        decimal? balanceSnapshotDecimal = balanceSnapshot.HasValue ? (decimal)balanceSnapshot.Value : null;

        return LeaveRequest.Reconstitute(
            leaveRequestId: snapshot.Id,
            tenantId: snapshot.GetValue<string>("tenant_id"),
            employeeId: snapshot.GetValue<string>("employee_id"),
            leaveType: leaveType,
            startDate: DateOnly.FromDateTime(snapshot.GetValue<Timestamp>("start_date").ToDateTime()),
            endDate: DateOnly.FromDateTime(snapshot.GetValue<Timestamp>("end_date").ToDateTime()),
            totalHours: ToDecimal(snapshot, "total_hours"),
            reasonCode: snapshot.GetValue<string>("reason_code"),
            balanceSnapshotAtRequest: balanceSnapshotDecimal,
            status: status,
            approverId: approverId,
            approvedAt: approvedAt,
            rejectionReason: rejectionReason,
            createdAt: snapshot.GetValue<Timestamp>("created_at").ToDateTimeOffset(),
            updatedAt: snapshot.GetValue<Timestamp>("updated_at").ToDateTimeOffset(),
            createdBy: snapshot.GetValue<string>("created_by"));
    }

    // ── Serialisation ────────────────────────────────────────────────────────

    protected override Dictionary<string, object?> ToDocument(LeaveRequest r) => new()
    {
        ["tenant_id"] = r.TenantId,
        ["leave_request_id"] = r.LeaveRequestId,
        ["employee_id"] = r.EmployeeId,
        ["leave_type"] = ToLeaveTypeString(r.LeaveType),
        ["start_date"] = Timestamp.FromDateTime(r.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["end_date"] = Timestamp.FromDateTime(r.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)),
        ["total_hours"] = (double)r.TotalHours,
        ["reason_code"] = r.ReasonCode,
        ["status"] = ToStatusString(r.Status),
        ["approver_id"] = r.ApproverId,
        ["approved_at"] = r.ApprovedAt.HasValue
            ? Timestamp.FromDateTimeOffset(r.ApprovedAt.Value)
            : (object?)null,
        ["rejection_reason"] = r.RejectionReason,
        ["balance_snapshot_at_request"] = r.BalanceSnapshotAtRequest.HasValue
            ? (double)r.BalanceSnapshotAtRequest.Value
            : (object?)null,
        ["created_at"] = Timestamp.FromDateTimeOffset(r.CreatedAt),
        ["updated_at"] = Timestamp.FromDateTimeOffset(r.UpdatedAt),
        ["created_by"] = r.CreatedBy,
        ["schema_version"] = r.SchemaVersion,
    };

    // ── Public reads ─────────────────────────────────────────────────────────

    /// <summary>Gets a leave request by ID, verifying tenant ownership.</summary>
    public Task<Result<LeaveRequest>> GetByLeaveRequestIdAsync(
        string tenantId, string leaveRequestId, CancellationToken ct = default)
        => GetByIdAsync(tenantId, leaveRequestId, ct);

    /// <summary>
    /// Lists all leave requests for a specific employee, ordered newest-first.
    /// Used for the employee self-service leave history view.
    /// </summary>
    public Task<IReadOnlyList<LeaveRequest>> ListByEmployeeAsync(
        string tenantId, string employeeId, CancellationToken ct = default)
    {
        var query = TenantQuery(tenantId)
            .WhereEqualTo("employee_id", employeeId)
            .OrderByDescending("start_date");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Lists pending (Submitted or ManagerReview) leave requests for a set of employees.
    /// Used by the manager approval queue for their department. CTL-BCEA-004.
    /// </summary>
    private static readonly string[] PendingStatuses = ["submitted", "manager_review"];

    public Task<IReadOnlyList<LeaveRequest>> ListPendingForEmployeesAsync(
        string tenantId, IReadOnlyList<string> employeeIds, CancellationToken ct = default)
    {
        // Firestore WhereIn supports up to 30 values per query
        var query = TenantQuery(tenantId)
            .WhereIn("employee_id", employeeIds)
            .WhereIn("status", PendingStatuses)
            .OrderBy("start_date");
        return ExecuteQueryAsync(query, ct);
    }

    /// <summary>
    /// Lists approved leave requests overlapping a date range.
    /// Used for payroll period conflict checks and leave calendar display.
    /// </summary>
    public Task<IReadOnlyList<LeaveRequest>> ListApprovedByDateRangeAsync(
        string tenantId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var fromTs = Timestamp.FromDateTime(from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var toTs = Timestamp.FromDateTime(to.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        var query = TenantQuery(tenantId)
            .WhereEqualTo("status", "approved")
            .WhereGreaterThanOrEqualTo("start_date", fromTs)
            .WhereLessThanOrEqualTo("start_date", toTs);
        return ExecuteQueryAsync(query, ct);
    }

    // ── Writes ───────────────────────────────────────────────────────────────

    /// <summary>Upserts a leave request (create or update after state transitions).</summary>
    public Task<Result> SaveAsync(LeaveRequest request, CancellationToken ct = default)
        => SetDocumentAsync(request.LeaveRequestId, request, ct);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static decimal ToDecimal(DocumentSnapshot snapshot, string field)
    {
        if (snapshot.TryGetValue<double>(field, out var d)) return (decimal)d;
        if (snapshot.TryGetValue<long>(field, out var l)) return l;
        return 0m;
    }

    private static string ToLeaveTypeString(LeaveType leaveType) => leaveType switch
    {
        LeaveType.Annual => "annual",
        LeaveType.Sick => "sick",
        LeaveType.FamilyResponsibility => "family_responsibility",
        LeaveType.Maternity => "maternity",
        LeaveType.Parental => "parental",
        _ => leaveType.ToString().ToLowerInvariant(),
    };

    private static LeaveType ParseLeaveType(string value) => value switch
    {
        "annual" => LeaveType.Annual,
        "sick" => LeaveType.Sick,
        "family_responsibility" => LeaveType.FamilyResponsibility,
        "maternity" => LeaveType.Maternity,
        "parental" => LeaveType.Parental,
        _ => LeaveType.Unknown,
    };

    private static string ToStatusString(LeaveRequestStatus status) => status switch
    {
        LeaveRequestStatus.Submitted => "submitted",
        LeaveRequestStatus.ManagerReview => "manager_review",
        LeaveRequestStatus.Approved => "approved",
        LeaveRequestStatus.Rejected => "rejected",
        LeaveRequestStatus.Cancelled => "cancelled",
        _ => "submitted",
    };

    private static LeaveRequestStatus ParseStatus(string value) => value switch
    {
        "submitted" => LeaveRequestStatus.Submitted,
        "manager_review" => LeaveRequestStatus.ManagerReview,
        "approved" => LeaveRequestStatus.Approved,
        "rejected" => LeaveRequestStatus.Rejected,
        "cancelled" => LeaveRequestStatus.Cancelled,
        _ => LeaveRequestStatus.Unknown,
    };
}
