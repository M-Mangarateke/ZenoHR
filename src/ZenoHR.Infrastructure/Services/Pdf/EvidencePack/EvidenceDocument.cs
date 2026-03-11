// REQ-COMP-005: Supporting document metadata for evidence pack PDF generation.

namespace ZenoHR.Infrastructure.Services.Pdf.EvidencePack;

/// <summary>
/// Represents a supporting document reference included in an evidence pack.
/// </summary>
public sealed record EvidenceDocument
{
    public required string DocumentTitle { get; init; }
    public required string DocumentType { get; init; }
    public required DateOnly DocumentDate { get; init; }
    public required string Reference { get; init; }
}
