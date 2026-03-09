// CTL-SARS-006: EMP201 filing generator interface
// Supports CSV (SARS eFiling upload) and human-readable summary report.

namespace ZenoHR.Infrastructure.Services.Filing.Emp201;

/// <summary>
/// Generates EMP201 monthly declaration outputs for SARS submission.
/// CTL-SARS-006: EMP201 must be filed and paid by the 7th of each following month.
/// </summary>
public interface IEmp201Generator
{
    /// <summary>
    /// Generates a semicolon-delimited CSV string for SARS eFiling upload.
    /// Throws <see cref="InvalidOperationException"/> if PAYE invariant is violated.
    /// </summary>
    string GenerateCsv(Emp201Data data);

    /// <summary>
    /// Generates a human-readable summary report for HR Manager review before submission.
    /// </summary>
    string GenerateSummaryReport(Emp201Data data);

    /// <summary>
    /// Returns the SARS filing due date for a given payroll month.
    /// Rule: 7th of the following calendar month. If the 7th falls on Saturday or Sunday,
    /// the due date advances to the following Monday.
    /// </summary>
    DateOnly CalculateDueDate(int year, int month);
}
