// REQ-COMP-005: Audit entry data for evidence pack PDF generation.

namespace ZenoHR.Infrastructure.Services.Pdf.EvidencePack;

/// <summary>
/// Represents a single audit trail entry included in an evidence pack.
/// </summary>
public sealed record EvidenceAuditEntry
{
    public required string EventId { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public required string Action { get; init; }
    public required string PerformedBy { get; init; }
    public required string Description { get; init; }
    public string? EntityId { get; init; }
    public required string EventHash { get; init; }
}
