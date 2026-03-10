// CTL-SARS-006: Query interface for compliance-relevant employee data.
// REQ-HR-001: Cross-module query (Compliance reads Employee data via defined interface).

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Query interface for retrieving employee tax summary data for compliance checks.
/// CTL-SARS-006: Used by <see cref="MissingTaxReferenceService"/> to identify employees
/// needing SARS income tax registration.
/// </summary>
public interface IComplianceEmployeeQuery
{
    /// <summary>
    /// Retrieves tax-relevant summary data for all employees in the specified tenant.
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of employee tax summaries.</returns>
    Task<IReadOnlyList<EmployeeTaxSummary>> GetAllEmployeeTaxSummariesAsync(
        string tenantId, CancellationToken ct);
}

/// <summary>
/// Lightweight projection of employee data needed for tax compliance checks.
/// CTL-SARS-006: Contains only the fields necessary to determine tax registration status.
/// </summary>
public sealed record EmployeeTaxSummary(
    string EmployeeId,
    string FullName,
    string? IdNumber,
    string? TaxReference,
    DateOnly? EmploymentStartDate);
