// REQ-COMP-002, CTL-SARS-006
// EMP501 annual reconciliation generator.
// Reconciles monthly EMP201 PAYE submissions against IRP5/IT3a employee certificates.
// South African tax year: March (prev year) – February (current year), e.g. "2026" = Mar 2025–Feb 2026.
// All monetary formatting uses CultureInfo.InvariantCulture. Monetary arithmetic uses decimal only.
// CSV format: semicolon-delimited per SARS e@syFile specification.

using System.Globalization;
using System.Text;

namespace ZenoHR.Infrastructure.Services.Filing.Emp501;

/// <summary>
/// Generates SARS EMP501 annual reconciliation CSV files, human-readable summary reports,
/// and validates that monthly EMP201 totals reconcile with employee IRP5/IT3a certificates.
/// </summary>
public sealed class Emp501Generator : IEmp501Generator
{
    // REQ-COMP-002: All decimal formatting uses InvariantCulture to prevent locale-specific separators.
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <inheritdoc/>
    public string GenerateReconciliationCsv(Emp501Data data)
    {
        // REQ-COMP-002, CTL-SARS-006
        ArgumentNullException.ThrowIfNull(data);

        var sb = new StringBuilder();

        // ── H record: file header ─────────────────────────────────────────────
        sb.AppendLine(string.Join(";",
            "H",
            "EMP501",
            data.TaxYear,
            data.EmployerTaxRef,
            data.EmployerName,
            DateTime.UtcNow.ToString("yyyyMMdd", Invariant)));

        // ── D records: one per employee (IRP5/IT3a certificate) ──────────────
        foreach (var emp in data.EmployeeEntries)
        {
            sb.AppendLine(string.Join(";",
                "D",
                emp.CertificateNumber,
                emp.EmployeeName,
                emp.IdNumber,
                emp.TaxRef,
                emp.AnnualGross.ToString("F2", Invariant),
                emp.AnnualPaye.ToString("F2", Invariant),
                emp.AnnualUifEmployee.ToString("F2", Invariant),
                emp.AnnualEti.ToString("F2", Invariant),
                emp.Irp5Code));
        }

        // ── S records: monthly EMP201 totals ─────────────────────────────────
        foreach (var month in data.MonthlySubmissions.OrderBy(m => m.Period))
        {
            sb.AppendLine(string.Join(";",
                "S",
                month.Period,
                month.TotalGross.ToString("F2", Invariant),
                month.TotalPaye.ToString("F2", Invariant)));
        }

        // ── T record: aggregate totals ────────────────────────────────────────
        var totalGross = data.EmployeeEntries.Sum(e => e.AnnualGross);
        var totalPaye  = data.EmployeeEntries.Sum(e => e.AnnualPaye);
        var totalUif   = data.EmployeeEntries.Sum(e => e.AnnualUifEmployee + e.AnnualUifEmployer);
        var totalSdl   = data.EmployeeEntries.Sum(e => e.AnnualSdl);
        var totalEti   = data.EmployeeEntries.Sum(e => e.AnnualEti);

        sb.AppendLine(string.Join(";",
            "T",
            totalGross.ToString("F2", Invariant),
            totalPaye.ToString("F2", Invariant),
            totalUif.ToString("F2", Invariant),
            totalSdl.ToString("F2", Invariant),
            totalEti.ToString("F2", Invariant),
            data.EmployeeEntries.Count.ToString(Invariant)));

        return sb.ToString();
    }

    /// <inheritdoc/>
    public string GenerateSummaryReport(Emp501Data data)
    {
        // REQ-COMP-002, CTL-SARS-006
        ArgumentNullException.ThrowIfNull(data);

        var sb = new StringBuilder();

        var totalGross = data.EmployeeEntries.Sum(e => e.AnnualGross);
        var totalPaye  = data.EmployeeEntries.Sum(e => e.AnnualPaye);
        var totalUif   = data.EmployeeEntries.Sum(e => e.AnnualUifEmployee);
        var totalSdl   = data.EmployeeEntries.Sum(e => e.AnnualSdl);
        var totalEti   = data.EmployeeEntries.Sum(e => e.AnnualEti);

        // Use string.Format(Invariant,...) to satisfy CA1305 — no locale-variable format calls.
        sb.AppendLine(string.Format(Invariant, "EMP501 ANNUAL RECONCILIATION — {0}", data.TaxYear));
        sb.AppendLine(string.Format(Invariant, "Employer: {0} ({1})", data.EmployerName, data.EmployerTaxRef));
        sb.AppendLine(string.Format(Invariant, "Generated: {0} UTC", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm", Invariant)));
        sb.AppendLine();

        sb.AppendLine("MONTHLY EMP201 SUMMARY");
        sb.AppendLine("======================");
        sb.AppendLine(string.Format(Invariant, "{0,-12} {1,16} {2,14} {3}", "Period", "Gross", "PAYE", "Status"));

        foreach (var month in data.MonthlySubmissions.OrderBy(m => m.Period))
        {
            var status    = month.Filed ? "Filed" : "Not Filed";
            var grossStr  = "R " + month.TotalGross.ToString("N2", Invariant);
            var payeStr   = "R " + month.TotalPaye.ToString("N2", Invariant);
            sb.AppendLine(string.Format(Invariant, "{0,-12} {1,16} {2,14} {3}", month.Period, grossStr, payeStr, status));
        }

        sb.AppendLine();
        sb.AppendLine(string.Format(Invariant, "EMPLOYEE RECONCILIATION ({0} employees)", data.EmployeeEntries.Count));
        sb.AppendLine("=============================================");
        sb.AppendLine(string.Format(Invariant, "{0,-28} {1,16} {2,14} {3}", "Name", "Annual Gross", "Annual PAYE", "Cert"));

        foreach (var emp in data.EmployeeEntries)
        {
            var grossStr = "R " + emp.AnnualGross.ToString("N2", Invariant);
            var payeStr  = "R " + emp.AnnualPaye.ToString("N2", Invariant);
            sb.AppendLine(string.Format(Invariant, "{0,-28} {1,16} {2,14} {3}", emp.EmployeeName, grossStr, payeStr, emp.Irp5Code));
        }

        sb.AppendLine();
        sb.AppendLine("TOTALS");
        sb.AppendLine("======");
        sb.AppendLine(string.Format(Invariant, "Annual Gross:    R {0}", totalGross.ToString("N2", Invariant)));
        sb.AppendLine(string.Format(Invariant, "Annual PAYE:     R {0}", totalPaye.ToString("N2", Invariant)));
        sb.AppendLine(string.Format(Invariant, "Annual UIF (EE): R {0}", totalUif.ToString("N2", Invariant)));
        sb.AppendLine(string.Format(Invariant, "Annual SDL:      R {0}", totalSdl.ToString("N2", Invariant)));
        sb.AppendLine(string.Format(Invariant, "Annual ETI:      R {0}", totalEti.ToString("N2", Invariant)));
        sb.AppendLine();

        var discrepancies = ValidateReconciliation(data);
        if (discrepancies.Count == 0)
        {
            sb.AppendLine("RECONCILIATION STATUS: PASS — no discrepancies found.");
        }
        else
        {
            sb.AppendLine("RECONCILIATION STATUS: FAIL");
            foreach (var d in discrepancies)
                sb.AppendLine(string.Format(Invariant, "  - {0}", d));
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> ValidateReconciliation(Emp501Data data)
    {
        // REQ-COMP-002, CTL-SARS-006
        ArgumentNullException.ThrowIfNull(data);

        var issues = new List<string>();

        // 1. Sum of monthly EMP201 PAYE vs sum of employee annual PAYE — must match within R0.01
        var monthlyPayeSum   = data.MonthlySubmissions.Sum(m => m.TotalPaye);
        var employeePayeSum  = data.EmployeeEntries.Sum(e => e.AnnualPaye);
        if (Math.Abs(monthlyPayeSum - employeePayeSum) > 0.01m)
        {
            issues.Add(
                $"EMP201 PAYE total R {monthlyPayeSum.ToString("N2", Invariant)} " +
                $"≠ employee sum R {employeePayeSum.ToString("N2", Invariant)} " +
                $"— R {Math.Abs(monthlyPayeSum - employeePayeSum).ToString("N2", Invariant)} discrepancy");
        }

        // 2. Sum of monthly EMP201 Gross vs sum of employee annual Gross — must match within R0.01
        var monthlyGrossSum  = data.MonthlySubmissions.Sum(m => m.TotalGross);
        var employeeGrossSum = data.EmployeeEntries.Sum(e => e.AnnualGross);
        if (Math.Abs(monthlyGrossSum - employeeGrossSum) > 0.01m)
        {
            issues.Add(
                $"EMP201 Gross total R {monthlyGrossSum.ToString("N2", Invariant)} " +
                $"≠ employee sum R {employeeGrossSum.ToString("N2", Invariant)} " +
                $"— R {Math.Abs(monthlyGrossSum - employeeGrossSum).ToString("N2", Invariant)} discrepancy");
        }

        // 3. Each employee must have a unique certificate number
        var certNumbers = data.EmployeeEntries.Select(e => e.CertificateNumber).ToList();
        var duplicates = certNumbers
            .GroupBy(c => c)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        foreach (var dup in duplicates)
        {
            issues.Add($"Duplicate certificate number detected: {dup}");
        }

        // 4. All 12 months of the tax year must be present (warn about gaps)
        // SA tax year: TaxYear "2026" → March 2025 (2025-03) to February 2026 (2026-02)
        if (int.TryParse(data.TaxYear, out var taxYear))
        {
            var expectedPeriods = new HashSet<string>();
            for (var month = 3; month <= 12; month++)
                expectedPeriods.Add($"{taxYear - 1}-{month:D2}");
            for (var month = 1; month <= 2; month++)
                expectedPeriods.Add($"{taxYear}-{month:D2}");

            var filedPeriods = data.MonthlySubmissions.Select(m => m.Period).ToHashSet();
            var missingPeriods = expectedPeriods.Except(filedPeriods).OrderBy(p => p).ToList();
            if (missingPeriods.Count > 0)
            {
                issues.Add($"Missing monthly submissions for periods: {string.Join(", ", missingPeriods)}");
            }
        }

        return issues.AsReadOnly();
    }
}
