// CTL-SARS-008: IRP5/IT3(a) certificate generator.
// Aggregates per-employee monthly PayrollResult entries for a full SA tax year into
// a single annual certificate per employee.
// SA tax year: 1 March (taxYear-1) to last day of February (taxYear).
// All monetary arithmetic uses MoneyZAR (decimal) — no float/double.
// CultureInfo.InvariantCulture used for all string formatting (CA1305).

using System.Globalization;
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;
using ZenoHR.Module.Payroll.Entities;

namespace ZenoHR.Infrastructure.Services.Filing.Irp5;

/// <summary>
/// Generates an IRP5 or IT3(a) annual tax certificate for a single employee
/// from their full-year collection of monthly <see cref="PayrollResult"/> records.
/// CTL-SARS-008: Called once per employee per tax year during EMP501 reconciliation.
/// </summary>
public sealed class Irp5Generator
{
    // CTL-SARS-008: Invariant culture for all string formatting.
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generates one <see cref="Irp5Certificate"/> from all monthly payroll results
    /// for a single employee in the specified tax year.
    /// </summary>
    /// <param name="tenantId">Tenant isolation key (required).</param>
    /// <param name="taxYear">SA tax year label, e.g. "2026" (Mar 2025 – Feb 2026).</param>
    /// <param name="employeeId">Employee document ID.</param>
    /// <param name="employeeName">Full display name of the employee.</param>
    /// <param name="idNumber">SA national ID number or passport number.</param>
    /// <param name="taxRef">Employee SARS income tax reference number.</param>
    /// <param name="results">All monthly PayrollResult entries for this employee in this tax year.</param>
    /// <returns>
    /// A <see cref="Result{T}"/> containing a list with exactly one <see cref="Irp5Certificate"/>,
    /// or a failure result if inputs are invalid or no results are provided.
    /// </returns>
    public static Result<IReadOnlyList<Irp5Certificate>> Generate(
        string tenantId,
        string taxYear,
        string employeeId,
        string employeeName,
        string idNumber,
        string taxRef,
        IReadOnlyList<PayrollResult> results)
    {
        // CTL-SARS-008: Validate required inputs.
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed, "TenantId is required.");

        if (string.IsNullOrWhiteSpace(taxYear))
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed, "TaxYear is required.");

        if (!int.TryParse(taxYear, NumberStyles.None, Invariant, out var taxYearInt) || taxYearInt < 2000)
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                string.Format(Invariant, "TaxYear '{0}' is not a valid 4-digit year.", taxYear));

        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");

        if (string.IsNullOrWhiteSpace(employeeName))
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed, "EmployeeName is required.");

        if (string.IsNullOrWhiteSpace(idNumber))
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed, "IdNumber is required.");

        if (string.IsNullOrWhiteSpace(taxRef))
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed, "TaxReferenceNumber is required.");

        if (results == null || results.Count == 0)
            return Result<IReadOnlyList<Irp5Certificate>>.Failure(
                ZenoHrErrorCode.ValidationFailed,
                "At least one PayrollResult is required to generate an IRP5/IT3(a) certificate.");

        // CTL-SARS-008: Aggregate annual income codes across all monthly results.
        var code3601 = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.BasicSalary);
        var code3605 = MoneyZAR.Zero; // Bonus tracking not implemented in v1.
        var code3713 = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.Allowances);
        var code3697 = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.AdditionTotal);

        // CTL-SARS-008: Aggregate annual deduction codes across all monthly results.
        var code4001 = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.Paye);
        var code4005 = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.UifEmployee);
        var code4474 = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.PensionEmployee);
        var code4493 = results.Aggregate(MoneyZAR.Zero, (acc, r) => acc + r.MedicalEmployee);
        var code4497 = results.Aggregate(MoneyZAR.Zero, (acc, r) =>
            acc + r.OtherDeductions.Aggregate(MoneyZAR.Zero, (inner, d) => inner + new MoneyZAR(d.AmountZar)));

        var totalRemuneration = code3601 + code3605 + code3713 + code3697;
        var totalDeductions   = code4001 + code4005 + code4474 + code4493 + code4497;

        // CTL-SARS-008: Certificate type — IRP5 if any PAYE was deducted, otherwise IT3(a).
        var certificateType = code4001 > MoneyZAR.Zero ? "IRP5" : "IT3a";

        // CTL-SARS-008: Deterministic certificate number for reconciliation integrity.
        var certNumber = string.Format(Invariant, "{0}-{1}-{2}", tenantId, taxYear, employeeId);

        // CTL-SARS-008: SA tax year period — 1 March (taxYear-1) to last day of February (taxYear).
        var periodStart = new DateOnly(taxYearInt - 1, 3, 1);
        var periodEnd   = new DateOnly(taxYearInt, 2, DateTime.IsLeapYear(taxYearInt) ? 29 : 28);

        var certificate = new Irp5Certificate
        {
            CertificateType    = certificateType,
            CertificateNumber  = certNumber,
            TaxYear            = taxYear,
            TenantId           = tenantId,
            EmployeeId         = employeeId,
            EmployeeName       = employeeName,
            IdNumber           = idNumber,
            TaxReferenceNumber = taxRef,
            PeriodStart        = periodStart,
            PeriodEnd          = periodEnd,
            Code3601           = code3601,
            Code3605           = code3605,
            Code3713           = code3713,
            Code3697           = code3697,
            Code4001           = code4001,
            Code4005           = code4005,
            Code4474           = code4474,
            Code4493           = code4493,
            Code4497           = code4497,
            TotalRemuneration  = totalRemuneration,
            TotalDeductions    = totalDeductions,
            TaxableIncome      = totalRemuneration,
            GeneratedAt        = DateTimeOffset.UtcNow,
        };

        return Result<IReadOnlyList<Irp5Certificate>>.Success(
            new List<Irp5Certificate> { certificate }.AsReadOnly());
    }
}
