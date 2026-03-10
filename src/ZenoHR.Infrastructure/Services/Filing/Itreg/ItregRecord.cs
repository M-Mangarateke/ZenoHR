// CTL-SARS-006: ITREG employee income tax registration record.
// REQ-HR-001: Employee data elements required for SARS income tax registration.

namespace ZenoHR.Infrastructure.Services.Filing.Itreg;

/// <summary>
/// Represents a single employee record for the SARS ITREG (income tax registration) process.
/// CTL-SARS-006: Contains all fields required by SARS for registering an employee
/// who does not yet have a tax reference number.
/// </summary>
public sealed record ItregRecord(
    string EmployeeId,
    string FullName,
    string IdNumber,
    DateOnly DateOfBirth,
    string ResidentialAddress,
    string PostalCode,
    DateOnly EmploymentStartDate,
    string EmployerPayeReference,
    string? ContactNumber,
    string? EmailAddress);
