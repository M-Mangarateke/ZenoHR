// REQ-HR-003, REQ-HR-004, CTL-SARS-001, CTL-BCEA-001: Per-employee payroll result entity.
// Firestore schema: docs/schemas/firestore-collections.md §8.2.
// IMMUTABLE after the parent PayrollRun is Finalized (write-once, per critical rules).
// Invariant: net_pay == gross_pay - deduction_total + addition_total (verified to the cent).
using ZenoHR.Domain.Common;
using ZenoHR.Domain.Errors;

namespace ZenoHR.Module.Payroll.Entities;

/// <summary>
/// Per-employee payroll calculation result for one payroll run.
/// Document ID in Firestore is the <c>employee_id</c> — one result per employee per run.
/// </summary>
public sealed class PayrollResult
{
    // ── Identity ──────────────────────────────────────────────────────────────

    /// <summary>FK to employees (also Firestore document ID within the subcollection).</summary>
    public string EmployeeId { get; }

    /// <summary>Parent payroll run ID.</summary>
    public string PayrollRunId { get; }

    /// <summary>Tenant isolation key.</summary>
    public string TenantId { get; }

    // ── Gross components ──────────────────────────────────────────────────────

    /// <summary>Monthly base salary from the employment contract (REQ-HR-003).</summary>
    public MoneyZAR BasicSalary { get; }

    /// <summary>Overtime pay for the period, calculated per BCEA overtime rules.</summary>
    public MoneyZAR OvertimePay { get; }

    /// <summary>Total allowances (travel, housing, etc.) for the period.</summary>
    public MoneyZAR Allowances { get; }

    /// <summary>Gross pay = BasicSalary + OvertimePay + Allowances.</summary>
    public MoneyZAR GrossPay { get; }

    // ── Statutory deductions ──────────────────────────────────────────────────

    /// <summary>PAYE tax deduction (CTL-SARS-001). Calculated via PRD-16 Section 3.</summary>
    public MoneyZAR Paye { get; }

    /// <summary>UIF employee contribution. 1% of remuneration, ceiling R177.12/month (CTL-SARS-005).</summary>
    public MoneyZAR UifEmployee { get; }

    /// <summary>UIF employer contribution. Equal to UifEmployee (CTL-SARS-005).</summary>
    public MoneyZAR UifEmployer { get; }

    /// <summary>SDL employer contribution. 1% of gross, exempt if annual payroll &lt; R500k (CTL-SARS-006).</summary>
    public MoneyZAR Sdl { get; }

    // ── Voluntary deductions ──────────────────────────────────────────────────

    /// <summary>Employee pension/retirement fund deduction.</summary>
    public MoneyZAR PensionEmployee { get; }

    /// <summary>Employer pension/retirement fund contribution.</summary>
    public MoneyZAR PensionEmployer { get; }

    /// <summary>Employee medical aid deduction.</summary>
    public MoneyZAR MedicalEmployee { get; }

    /// <summary>Employer medical aid contribution.</summary>
    public MoneyZAR MedicalEmployer { get; }

    // ── ETI ───────────────────────────────────────────────────────────────────

    /// <summary>ETI incentive amount if eligible. Offsets PAYE employer liability (CTL-SARS-007).</summary>
    public MoneyZAR EtiAmount { get; }

    /// <summary>Whether this employee qualifies for ETI in this period.</summary>
    public bool EtiEligible { get; }

    // ── Other line items ──────────────────────────────────────────────────────

    /// <summary>Additional deductions beyond statutory and voluntary (e.g., garnishee orders).</summary>
    public IReadOnlyList<OtherLineItem> OtherDeductions { get; }

    /// <summary>Additional additions beyond gross pay (e.g., reimbursements).</summary>
    public IReadOnlyList<OtherLineItem> OtherAdditions { get; }

    // ── Totals (verified by invariant check) ──────────────────────────────────

    /// <summary>Sum of all deductions: PAYE + UIF employee + pension employee + medical employee + other deductions.</summary>
    public MoneyZAR DeductionTotal { get; }

    /// <summary>Sum of all additions beyond gross (e.g. reimbursements).</summary>
    public MoneyZAR AdditionTotal { get; }

    /// <summary>
    /// Net pay = gross_pay - deduction_total + addition_total.
    /// Verified by <see cref="PayslipInvariantVerifier.Verify"/> before storage.
    /// </summary>
    public MoneyZAR NetPay { get; }

    // ── Hours ─────────────────────────────────────────────────────────────────

    /// <summary>Ordinary hours worked in the period (BCEA standard hours).</summary>
    public decimal HoursOrdinary { get; }

    /// <summary>Overtime hours worked in the period.</summary>
    public decimal HoursOvertime { get; }

    // ── Metadata ──────────────────────────────────────────────────────────────

    /// <summary>Version of the SARS tax table used for this calculation (e.g., "SARS_PAYE_2026").</summary>
    public string TaxTableVersion { get; }

    /// <summary>Compliance check flags applied during calculation (e.g., "CTL-SARS-001:PASS").</summary>
    public IReadOnlyList<string> ComplianceFlags { get; }

    /// <summary>UTC timestamp when this calculation was performed.</summary>
    public DateTimeOffset CalculationTimestamp { get; }

    /// <summary>Schema version for Firestore document compatibility.</summary>
    public string SchemaVersion { get; } = "1.0";

    // ── Constructor (private — use factory) ───────────────────────────────────

    private PayrollResult(
        string employeeId, string payrollRunId, string tenantId,
        MoneyZAR basicSalary, MoneyZAR overtimePay, MoneyZAR allowances, MoneyZAR grossPay,
        MoneyZAR paye, MoneyZAR uifEmployee, MoneyZAR uifEmployer, MoneyZAR sdl,
        MoneyZAR pensionEmployee, MoneyZAR pensionEmployer,
        MoneyZAR medicalEmployee, MoneyZAR medicalEmployer,
        MoneyZAR etiAmount, bool etiEligible,
        IReadOnlyList<OtherLineItem> otherDeductions,
        IReadOnlyList<OtherLineItem> otherAdditions,
        MoneyZAR deductionTotal, MoneyZAR additionTotal, MoneyZAR netPay,
        decimal hoursOrdinary, decimal hoursOvertime,
        string taxTableVersion,
        IReadOnlyList<string> complianceFlags,
        DateTimeOffset calculationTimestamp)
    {
        EmployeeId = employeeId;
        PayrollRunId = payrollRunId;
        TenantId = tenantId;
        BasicSalary = basicSalary;
        OvertimePay = overtimePay;
        Allowances = allowances;
        GrossPay = grossPay;
        Paye = paye;
        UifEmployee = uifEmployee;
        UifEmployer = uifEmployer;
        Sdl = sdl;
        PensionEmployee = pensionEmployee;
        PensionEmployer = pensionEmployer;
        MedicalEmployee = medicalEmployee;
        MedicalEmployer = medicalEmployer;
        EtiAmount = etiAmount;
        EtiEligible = etiEligible;
        OtherDeductions = otherDeductions;
        OtherAdditions = otherAdditions;
        DeductionTotal = deductionTotal;
        AdditionTotal = additionTotal;
        NetPay = netPay;
        HoursOrdinary = hoursOrdinary;
        HoursOvertime = hoursOvertime;
        TaxTableVersion = taxTableVersion;
        ComplianceFlags = complianceFlags;
        CalculationTimestamp = calculationTimestamp;
    }

    // ── Factory ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a <see cref="PayrollResult"/> and verifies the payslip invariant.
    /// Returns failure if <c>net_pay != gross_pay - deduction_total + addition_total</c>.
    /// This is a Sev-1 check — any cent mismatch fails creation.
    /// </summary>
    public static Result<PayrollResult> Create(
        string employeeId, string payrollRunId, string tenantId,
        MoneyZAR basicSalary, MoneyZAR overtimePay, MoneyZAR allowances,
        MoneyZAR paye, MoneyZAR uifEmployee, MoneyZAR uifEmployer, MoneyZAR sdl,
        MoneyZAR pensionEmployee, MoneyZAR pensionEmployer,
        MoneyZAR medicalEmployee, MoneyZAR medicalEmployer,
        MoneyZAR etiAmount, bool etiEligible,
        IReadOnlyList<OtherLineItem>? otherDeductions,
        IReadOnlyList<OtherLineItem>? otherAdditions,
        decimal hoursOrdinary, decimal hoursOvertime,
        string taxTableVersion,
        IReadOnlyList<string>? complianceFlags,
        DateTimeOffset calculationTimestamp)
    {
        if (string.IsNullOrWhiteSpace(employeeId))
            return Result<PayrollResult>.Failure(ZenoHrErrorCode.ValidationFailed, "EmployeeId is required.");
        if (string.IsNullOrWhiteSpace(payrollRunId))
            return Result<PayrollResult>.Failure(ZenoHrErrorCode.ValidationFailed, "PayrollRunId is required.");
        if (string.IsNullOrWhiteSpace(tenantId))
            return Result<PayrollResult>.Failure(ZenoHrErrorCode.ValidationFailed, "TenantId is required.");
        if (string.IsNullOrWhiteSpace(taxTableVersion))
            return Result<PayrollResult>.Failure(ZenoHrErrorCode.ValidationFailed, "TaxTableVersion is required.");

        otherDeductions ??= [];
        otherAdditions ??= [];
        complianceFlags ??= [];

        // ── Compute derived totals ──────────────────────────────────────────
        var grossPay = basicSalary + overtimePay + allowances;

        var otherDeductionTotal = otherDeductions.Aggregate(MoneyZAR.Zero, (acc, d) => acc + new MoneyZAR(d.AmountZar));
        var otherAdditionTotal = otherAdditions.Aggregate(MoneyZAR.Zero, (acc, a) => acc + new MoneyZAR(a.AmountZar));

        // Deduction total = PAYE + UIF employee + pension employee + medical employee + other deductions
        var deductionTotal = paye + uifEmployee + pensionEmployee + medicalEmployee + otherDeductionTotal;
        var additionTotal = otherAdditionTotal;
        var netPay = grossPay - deductionTotal + additionTotal;

        // ── Payslip invariant check (PRD-16 §9) — Sev-1 ───────────────────
        var invariantResult = PayslipInvariantVerifier.Verify(grossPay, deductionTotal, additionTotal, netPay);
        if (invariantResult.IsFailure)
            return Result<PayrollResult>.Failure(invariantResult.Error!);

        var result = new PayrollResult(
            employeeId, payrollRunId, tenantId,
            basicSalary, overtimePay, allowances, grossPay,
            paye, uifEmployee, uifEmployer, sdl,
            pensionEmployee, pensionEmployer, medicalEmployee, medicalEmployer,
            etiAmount, etiEligible,
            otherDeductions, otherAdditions,
            deductionTotal, additionTotal, netPay,
            hoursOrdinary, hoursOvertime,
            taxTableVersion, complianceFlags, calculationTimestamp);

        return Result<PayrollResult>.Success(result);
    }

    // ── Reconstitution ────────────────────────────────────────────────────────

    /// <summary>Reconstitutes a PayrollResult from Firestore (read-path, no invariant re-check).</summary>
    public static PayrollResult Reconstitute(
        string employeeId, string payrollRunId, string tenantId,
        MoneyZAR basicSalary, MoneyZAR overtimePay, MoneyZAR allowances, MoneyZAR grossPay,
        MoneyZAR paye, MoneyZAR uifEmployee, MoneyZAR uifEmployer, MoneyZAR sdl,
        MoneyZAR pensionEmployee, MoneyZAR pensionEmployer,
        MoneyZAR medicalEmployee, MoneyZAR medicalEmployer,
        MoneyZAR etiAmount, bool etiEligible,
        IReadOnlyList<OtherLineItem> otherDeductions,
        IReadOnlyList<OtherLineItem> otherAdditions,
        MoneyZAR deductionTotal, MoneyZAR additionTotal, MoneyZAR netPay,
        decimal hoursOrdinary, decimal hoursOvertime,
        string taxTableVersion, IReadOnlyList<string> complianceFlags,
        DateTimeOffset calculationTimestamp)
        => new(employeeId, payrollRunId, tenantId,
               basicSalary, overtimePay, allowances, grossPay,
               paye, uifEmployee, uifEmployer, sdl,
               pensionEmployee, pensionEmployer, medicalEmployee, medicalEmployer,
               etiAmount, etiEligible, otherDeductions, otherAdditions,
               deductionTotal, additionTotal, netPay,
               hoursOrdinary, hoursOvertime, taxTableVersion, complianceFlags, calculationTimestamp);
}
