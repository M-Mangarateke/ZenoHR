// REQ-SEC-008: Break-glass emergency access request record.
// VUL-006: Implements ticketed break-glass procedure with time-limited tokens
// and mandatory post-event audit review.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Immutable record representing a break-glass emergency access request.
/// Break-glass access bypasses normal authentication when production is unavailable.
/// Requires Director + SaasAdmin dual approval, is time-limited (default 4 hours),
/// and mandates a post-event audit review before closure.
/// </summary>
public sealed record BreakGlassRequest
{
    /// <summary>Default expiry window in hours from approval time.</summary>
    public const int DefaultExpiryHours = 4;

    public required string RequestId { get; init; }
    public required string TenantId { get; init; }
    public required string RequestedBy { get; init; }
    public required DateTimeOffset RequestedAt { get; init; }
    public required string Reason { get; init; }
    public required BreakGlassUrgency Urgency { get; init; }
    public required BreakGlassStatus Status { get; init; }
    public string? ApprovedBy { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public string? RevokedBy { get; init; }
    public DateTimeOffset? RevokedAt { get; init; }
    public string? PostReviewCompletedBy { get; init; }
    public DateTimeOffset? PostReviewCompletedAt { get; init; }

    /// <summary>True if the access window has passed its expiry time.</summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;

    /// <summary>True if the request is approved and the access window has not expired.</summary>
    public bool IsActive => Status == BreakGlassStatus.Approved && !IsExpired;
}
