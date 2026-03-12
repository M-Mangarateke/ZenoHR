// CTL-POPIA-010: POPIA §24 — correction of personal information request record.
// Immutable record; corrections create new documents (write-once principle preserved).

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record representing a data subject's request to correct personal information.
/// POPIA Act §24 requires responsible parties to correct or delete inaccurate,
/// irrelevant, excessive, out of date, incomplete, misleading, or unlawfully obtained information.
/// </summary>
public sealed record CorrectionRequest
{
    public required string RequestId { get; init; }
    public required string TenantId { get; init; }
    public required string EmployeeId { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public required string RequestedBy { get; init; }
    public required string FieldName { get; init; }
    public required string CurrentValue { get; init; }
    public required string ProposedValue { get; init; }
    public required string Reason { get; init; }
    public required CorrectionStatus Status { get; init; }
    public string? ReviewedBy { get; init; }
    public DateTimeOffset? ReviewedAt { get; init; }
    public DateTimeOffset? AppliedAt { get; init; }
    public string? RejectionReason { get; init; }
}
