// VUL-024, VUL-025: BCEA pre-payroll compliance result.
// CTL-BCEA-001, CTL-BCEA-003

namespace ZenoHR.Module.Payroll.Models;

/// <summary>
/// Result of BCEA compliance checks run before payroll finalization.
/// Violations are blocking; warnings are informational only.
/// </summary>
public sealed record BceaComplianceResult
{
    /// <summary>True if there are no violations (warnings are acceptable).</summary>
    public bool IsCompliant => Violations.Count == 0;

    /// <summary>Blocking issues that must be resolved before payroll can be finalized.</summary>
    public IReadOnlyList<string> Violations { get; }

    /// <summary>Non-blocking issues that should be reviewed but do not prevent finalization.</summary>
    public IReadOnlyList<string> Warnings { get; }

    public BceaComplianceResult(IReadOnlyList<string> violations, IReadOnlyList<string> warnings)
    {
        Violations = violations;
        Warnings = warnings;
    }
}
