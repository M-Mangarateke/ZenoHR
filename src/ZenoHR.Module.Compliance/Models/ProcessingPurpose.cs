// CTL-POPIA-001: Processing purpose record — links a data processing activity
// to its POPIA §11 lawful basis and the categories of personal data involved.

namespace ZenoHR.Module.Compliance.Models;

/// <summary>
/// Represents a registered data processing purpose with its lawful basis,
/// data categories, and lifecycle state. Stored in Firestore and validated
/// before any personal data access is permitted.
/// </summary>
public sealed record ProcessingPurpose
{
    /// <summary>Unique identifier for this purpose (e.g., "PUR-000001").</summary>
    public required string PurposeId { get; init; }

    /// <summary>Tenant owning this purpose registration.</summary>
    public required string TenantId { get; init; }

    /// <summary>Human-readable description of the processing activity.</summary>
    public required string Description { get; init; }

    /// <summary>POPIA §11 lawful basis category.</summary>
    public required LawfulBasis LawfulBasis { get; init; }

    /// <summary>Categories of personal data processed (e.g., "salary", "id_number", "banking").</summary>
    public required IReadOnlyList<string> DataCategories { get; init; }

    /// <summary>User who registered this purpose.</summary>
    public required string CreatedBy { get; init; }

    /// <summary>UTC timestamp of registration.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Whether this purpose is currently active. Revoked purposes are set to false.</summary>
    public required bool IsActive { get; init; }

    /// <summary>User who revoked this purpose (null if still active).</summary>
    public string? RevokedBy { get; init; }

    /// <summary>UTC timestamp of revocation (null if still active).</summary>
    public DateTimeOffset? RevokedAt { get; init; }
}
