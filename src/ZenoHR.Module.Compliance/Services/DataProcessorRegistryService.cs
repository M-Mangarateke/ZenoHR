// CTL-POPIA-013, CTL-POPIA-014, VUL-018: Data processor registry service.
// Manages processor inventory, DPA compliance checks, and cross-border transfer identification.

using System.Globalization;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Compliance.Models;

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Service for managing the data processor inventory required by POPIA Section 21 (operator agreements)
/// and Section 72 (cross-border transfer governance). Validates DPA coverage and identifies
/// processors that transfer personal information outside South Africa.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Instance methods for DI compatibility")]
public sealed class DataProcessorRegistryService
{
    /// <summary>
    /// Validates and registers a new data processor in the inventory.
    /// </summary>
    /// <param name="processor">The data processor to register.</param>
    /// <returns>The validated processor record, or a failure result if validation fails.</returns>
    public Result<DataProcessor> RegisterProcessor(DataProcessor processor)
    {
        ArgumentNullException.ThrowIfNull(processor);

        if (string.IsNullOrWhiteSpace(processor.ProcessorId))
            return Result<DataProcessor>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ProcessorId is required.");

        if (string.IsNullOrWhiteSpace(processor.Name))
            return Result<DataProcessor>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Name is required.");

        if (string.IsNullOrWhiteSpace(processor.ServiceDescription))
            return Result<DataProcessor>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "ServiceDescription is required.");

        if (processor.DataTypesProcessed is null || processor.DataTypesProcessed.Count == 0)
            return Result<DataProcessor>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "At least one data type must be specified.");

        if (string.IsNullOrWhiteSpace(processor.Region))
            return Result<DataProcessor>.Failure(ZenoHrErrorCode.RequiredFieldMissing, "Region is required.");

        if (processor.DpaStatus == DpaStatus.Unknown)
            return Result<DataProcessor>.Failure(ZenoHrErrorCode.ValidationFailed, "DpaStatus must be specified (not Unknown).");

        return Result<DataProcessor>.Success(processor);
    }

    /// <summary>
    /// Returns all processors that require a DPA but have not yet obtained one.
    /// POPIA Section 21 mandates written agreements with all operators.
    /// </summary>
    /// <param name="processors">The full list of registered processors.</param>
    /// <returns>Processors where DpaStatus is <see cref="DpaStatus.Required"/>.</returns>
    public IReadOnlyList<DataProcessor> GetProcessorsRequiringDpa(IReadOnlyList<DataProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);
        return processors.Where(p => p.DpaStatus == DpaStatus.Required).ToList();
    }

    /// <summary>
    /// Identifies processors whose data residency region is NOT within South Africa.
    /// These processors represent potential cross-border transfers under POPIA Section 72.
    /// </summary>
    /// <param name="processors">The full list of registered processors.</param>
    /// <param name="saRegions">
    /// Set of region identifiers considered to be within South Africa
    /// (e.g., "africa-south1", "South Africa North").
    /// </param>
    /// <returns>Processors with regions not matching any SA region identifier.</returns>
    public IReadOnlyList<DataProcessor> GetCrossBorderProcessors(
        IReadOnlyList<DataProcessor> processors,
        IReadOnlySet<string> saRegions)
    {
        ArgumentNullException.ThrowIfNull(processors);
        ArgumentNullException.ThrowIfNull(saRegions);

        return processors
            .Where(p => !string.Equals(p.Region, "N/A", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(p.Region, "N/A (in-process)", StringComparison.OrdinalIgnoreCase)
                     && !string.Equals(p.Region, "N/A (in-process, no data transfer)", StringComparison.OrdinalIgnoreCase)
                     && !saRegions.Contains(p.Region, StringComparer.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Validates that all processors requiring a DPA have obtained one.
    /// A processor with <see cref="DpaStatus.Expired"/> is treated as non-compliant.
    /// Processors with <see cref="DpaStatus.NotRequired"/> are excluded from validation.
    /// </summary>
    /// <param name="processors">The full list of registered processors.</param>
    /// <returns>
    /// Success with <c>true</c> if all DPA-requiring processors have obtained agreements;
    /// Success with <c>false</c> if any processor is still Required or Expired.
    /// </returns>
    public Result<bool> ValidateAllDpasObtained(IReadOnlyList<DataProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);

        var nonCompliant = processors
            .Where(p => p.DpaStatus is DpaStatus.Required or DpaStatus.Expired)
            .ToList();

        if (nonCompliant.Count > 0)
        {
            var names = string.Join(", ", nonCompliant.Select(p =>
                string.Format(CultureInfo.InvariantCulture, "{0} ({1})", p.Name, p.DpaStatus)));

            return Result<bool>.Failure(
                ZenoHrErrorCode.ComplianceCheckFailed,
                string.Format(CultureInfo.InvariantCulture,
                    "DPA compliance check failed. {0} processor(s) non-compliant: {1}",
                    nonCompliant.Count, names));
        }

        return Result<bool>.Success(true);
    }
}
