// REQ-COMP-005: Compliance score data for evidence pack PDF generation.

namespace ZenoHR.Infrastructure.Services.Pdf.EvidencePack;

/// <summary>
/// Represents a compliance domain score included in an evidence pack.
/// </summary>
public sealed record EvidenceComplianceScore
{
    public required string Domain { get; init; }
    public required decimal ScorePercentage { get; init; }
    public required string Status { get; init; }
    public required string[] Findings { get; init; }
}
