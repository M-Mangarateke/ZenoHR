// REQ-COMP-002, CTL-SARS-006
// Interface for EMP501 annual reconciliation generator.
// EMP501 reconciles monthly EMP201 submissions against IRP5/IT3a employee certificates
// for the full SA tax year (March–February).

namespace ZenoHR.Infrastructure.Services.Filing.Emp501;

/// <summary>
/// Generates EMP501 annual reconciliation outputs for SARS submission.
/// The EMP501 reconciles monthly EMP201 declarations against IRP5/IT3(a) employee
/// certificates for the full SA tax year (March to February).
/// REQ-COMP-002, CTL-SARS-006
/// </summary>
public interface IEmp501Generator
{
    /// <summary>Generates EMP501 reconciliation CSV for SARS e@syFile import.</summary>
    string GenerateReconciliationCsv(Emp501Data data);

    /// <summary>Generates a human-readable summary report (for internal review).</summary>
    string GenerateSummaryReport(Emp501Data data);

    /// <summary>Validates the reconciliation — returns list of discrepancies found.</summary>
    IReadOnlyList<string> ValidateReconciliation(Emp501Data data);
}
