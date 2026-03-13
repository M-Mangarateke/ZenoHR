// CTL-POPIA-001: Lawful Basis Service — validates that every data processing operation
// has a documented POPIA §11 lawful basis before personal data may be accessed.

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Domain service for managing POPIA §11 processing purposes. Validates purpose registration,
/// checks whether a data category is covered by an active purpose, and handles revocation.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class LawfulBasisService
{
    /// <summary>
    /// Register a new processing purpose. Validates all required fields and lawful basis.
    /// </summary>
    // CTL-POPIA-001
    public Result<ProcessingPurpose> RegisterPurpose(
        string tenantId,
        string description,
        LawfulBasis lawfulBasis,
        IReadOnlyList<string> dataCategories,
        string createdBy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(description))
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Description is required.");

        if (lawfulBasis == LawfulBasis.Unknown)
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.ValidationFailed, "LawfulBasis must not be Unknown. A valid POPIA §11 basis is required.");

        if (dataCategories is null || dataCategories.Count == 0)
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "At least one data category is required.");

        if (string.IsNullOrWhiteSpace(createdBy))
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "CreatedBy is required.");

        // Tenant-safe ID: GUID avoids cross-tenant collision from static counters
        var purposeId = string.Format(CultureInfo.InvariantCulture, "PUR-{0}", Guid.NewGuid().ToString("N")[..8]);

        var purpose = new ProcessingPurpose
        {
            PurposeId = purposeId,
            TenantId = tenantId,
            Description = description,
            LawfulBasis = lawfulBasis,
            DataCategories = dataCategories,
            CreatedBy = createdBy,
            CreatedAt = DateTimeOffset.UtcNow,
            IsActive = true,
        };

        return Result<ProcessingPurpose>.Success(purpose);
    }

    /// <summary>
    /// Validate whether processing is allowed for a specific data category.
    /// Returns the first active purpose that covers the requested category, or failure.
    /// </summary>
    // CTL-POPIA-001
    public Result<ProcessingPurpose> ValidateProcessingAllowed(
        string tenantId,
        string dataCategory,
        IReadOnlyList<ProcessingPurpose> purposes)
    {
        ArgumentNullException.ThrowIfNull(purposes);

        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(dataCategory))
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "DataCategory is required.");

        var matchingPurpose = purposes.FirstOrDefault(p =>
            p.IsActive &&
            string.Equals(p.TenantId, tenantId, StringComparison.Ordinal) &&
            p.DataCategories.Any(dc => string.Equals(dc, dataCategory, StringComparison.OrdinalIgnoreCase)));

        if (matchingPurpose is null)
        {
            return Result<ProcessingPurpose>.Failure(
                ZenoHrErrorCode.ComplianceCheckFailed,
                $"No active processing purpose covers data category '{dataCategory}' for tenant '{tenantId}'. POPIA §11 lawful basis required.");
        }

        return Result<ProcessingPurpose>.Success(matchingPurpose);
    }

    /// <summary>
    /// Get all active processing purposes for a tenant.
    /// </summary>
    // CTL-POPIA-001
    public IReadOnlyList<ProcessingPurpose> GetActivePurposes(
        string tenantId,
        IReadOnlyList<ProcessingPurpose> purposes)
    {
        ArgumentNullException.ThrowIfNull(purposes);

        return purposes
            .Where(p => p.IsActive && string.Equals(p.TenantId, tenantId, StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Revoke a processing purpose, marking it inactive with revocation metadata.
    /// Returns a new record with IsActive=false and revocation details.
    /// </summary>
    // CTL-POPIA-001
    public Result<ProcessingPurpose> RevokePurpose(ProcessingPurpose purpose, string revokedBy)
    {
        ArgumentNullException.ThrowIfNull(purpose);

        if (string.IsNullOrWhiteSpace(revokedBy))
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "RevokedBy is required.");

        if (!purpose.IsActive)
            return Result<ProcessingPurpose>.Failure(ZenoHrErrorCode.ValidationFailed, "Purpose is already revoked.");

        var revoked = purpose with
        {
            IsActive = false,
            RevokedBy = revokedBy,
            RevokedAt = DateTimeOffset.UtcNow,
        };

        return Result<ProcessingPurpose>.Success(revoked);
    }
}
