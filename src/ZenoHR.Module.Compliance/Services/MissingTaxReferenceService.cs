// CTL-SARS-006: Service to identify employees missing valid tax references.
// REQ-HR-001: Supports ITREG workflow by finding employees needing SARS registration.

using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Identifies employees with missing or invalid SARS tax reference numbers.
/// CTL-SARS-006: Core service for the ITREG (income tax registration) assistance workflow.
/// </summary>
public sealed class MissingTaxReferenceService
{
    private readonly IComplianceEmployeeQuery _employeeQuery;

    public MissingTaxReferenceService(IComplianceEmployeeQuery employeeQuery)
    {
        ArgumentNullException.ThrowIfNull(employeeQuery);
        _employeeQuery = employeeQuery;
    }

    /// <summary>
    /// Returns employee summaries where TaxReference is null, empty, or invalid format.
    /// CTL-SARS-006: Each entry includes the specific validation issue (MISSING or INVALID_FORMAT).
    /// </summary>
    /// <param name="tenantId">The tenant identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of employees with missing/invalid tax references.</returns>
    public async Task<Result<IReadOnlyList<MissingTaxReferenceEntry>>> GetMissingTaxReferencesAsync(
        string tenantId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result<IReadOnlyList<MissingTaxReferenceEntry>>.Failure(
                ZenoHrErrorCode.RequiredFieldMissing,
                "Tenant ID is required.");
        }

        var employees = await _employeeQuery.GetAllEmployeeTaxSummariesAsync(tenantId, ct);

        var entries = new List<MissingTaxReferenceEntry>();

        foreach (var emp in employees)
        {
            if (string.IsNullOrWhiteSpace(emp.TaxReference))
            {
                entries.Add(new MissingTaxReferenceEntry(
                    emp.EmployeeId,
                    emp.FullName,
                    emp.IdNumber,
                    emp.TaxReference,
                    "MISSING",
                    emp.EmploymentStartDate));
                continue;
            }

            var validationResult = TaxReferenceValidator.Validate(emp.TaxReference);
            if (validationResult.IsFailure)
            {
                entries.Add(new MissingTaxReferenceEntry(
                    emp.EmployeeId,
                    emp.FullName,
                    emp.IdNumber,
                    emp.TaxReference,
                    "INVALID_FORMAT",
                    emp.EmploymentStartDate));
            }
        }

        return Result<IReadOnlyList<MissingTaxReferenceEntry>>.Success(entries.AsReadOnly());
    }
}
