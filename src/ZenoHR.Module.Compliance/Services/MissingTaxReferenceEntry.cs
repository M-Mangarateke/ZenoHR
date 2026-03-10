// CTL-SARS-006: DTO for employees with missing or invalid tax references.
// REQ-HR-001: Identifies employees needing SARS income tax registration assistance.

namespace ZenoHR.Module.Compliance.Services;

/// <summary>
/// Represents an employee with a missing or invalid SARS tax reference number.
/// CTL-SARS-006: Used by the ITREG workflow to identify employees requiring registration.
/// </summary>
public sealed record MissingTaxReferenceEntry(
    string EmployeeId,
    string FullName,
    string? IdNumber,
    string? CurrentTaxReference,
    string ValidationIssue,
    DateOnly? EmploymentStartDate);
