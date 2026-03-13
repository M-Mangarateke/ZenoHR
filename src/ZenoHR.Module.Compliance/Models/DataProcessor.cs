// CTL-POPIA-013, CTL-POPIA-014, VUL-018: Data processor record for POPIA operator inventory.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Represents a third-party data processor (operator) that processes personal information
/// on behalf of Zenowethu (Pty) Ltd. POPIA Section 21 requires written agreements with
/// all such operators, and Section 72 governs cross-border transfers.
/// </summary>
public sealed record DataProcessor
{
    /// <summary>Unique identifier for this processor entry.</summary>
    public required string ProcessorId { get; init; }

    /// <summary>Legal name of the data processor / operator.</summary>
    public required string Name { get; init; }

    /// <summary>Description of the service provided by this processor.</summary>
    public required string ServiceDescription { get; init; }

    /// <summary>Categories of personal information processed (e.g., "Email", "Salary Data").</summary>
    public required IReadOnlyList<string> DataTypesProcessed { get; init; }

    /// <summary>Data residency region (e.g., "africa-south1", "South Africa North", "Multi-region").</summary>
    public required string Region { get; init; }

    /// <summary>Current status of the Data Processing Agreement with this processor.</summary>
    public required DpaStatus DpaStatus { get; init; }

    /// <summary>Whether this processor acts as a sub-processor under another processor's DPA.</summary>
    public required bool IsSubProcessor { get; init; }

    /// <summary>Date this processor entry was last reviewed for accuracy.</summary>
    public required DateTimeOffset LastReviewedAt { get; init; }
}
