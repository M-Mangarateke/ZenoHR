// REQ-COMP-005: Evidence pack request model for PDF generation.

namespace ZenoHR.Infrastructure.Services.Pdf.EvidencePack;

/// <summary>
/// Request model containing all data needed to generate an evidence pack PDF.
/// </summary>
public sealed record EvidencePackRequest
{
    public required string TenantId { get; init; }
    public required string CompanyName { get; init; }
    public required string GeneratedBy { get; init; }
    public required DateOnly PeriodStart { get; init; }
    public required DateOnly PeriodEnd { get; init; }

    /// <summary>
    /// Pack type: SARS_AUDIT, POPIA_REVIEW, INTERNAL_AUDIT, or BCEA_INSPECTION.
    /// </summary>
    public required string PackType { get; init; }

    public required IReadOnlyList<EvidenceAuditEntry> AuditEntries { get; init; }
    public required IReadOnlyList<EvidenceComplianceScore> ComplianceScores { get; init; }
    public required IReadOnlyList<EvidenceDocument> SupportingDocuments { get; init; }
}
