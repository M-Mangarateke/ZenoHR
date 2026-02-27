# PRD-16 — Payroll Calculation Specification

> **Status**: Final · **Author**: ZenoHR Design  · **Date**: 2026-02-23
> **Requirement refs**: REQ-HR-003, REQ-HR-004, CTL-SARS-001 to CTL-SARS-010

---

## Purpose

This document provides complete pseudocode and decision rules for all payroll calculation scenarios in ZenoHR. It is the single source of truth that the `ZenoHR.Module.Payroll` implementation must faithfully replicate. Any deviation from these rules is a **Sev-1 defect**.

All statutory thresholds, rates, and brackets are seeded from `docs/seed-data/` and accessed at runtime via `StatutoryRuleSet` — **never** hardcoded.

---

## 1. Standard PAYE Calculation (Monthly)

The SARS "annual equivalent" method. Apply to every monthly pay period.

```
function CalculateMonthlyPAYE(monthlyTaxableIncome: decimal, age: int, taxYear: TaxYear): decimal

  // Step 1: Annualise
  annualIncome = monthlyTaxableIncome × 12

  // Step 2: Apply progressive tax brackets
  annualTax = ApplyBrackets(annualIncome, taxYear.brackets)
  // ApplyBrackets: sum of (rate × (income − bracket.lower)) for each bracket traversed

  // Step 3: Subtract applicable rebates
  annualTax = annualTax - GetRebate(age, taxYear.rebates)
  // GetRebate: primary always; secondary if age >= 65; tertiary if age >= 75

  // Step 4: Floor at zero (income below tax threshold yields R0 PAYE, not negative)
  annualTax = Max(annualTax, 0)

  // Step 5: Round annual PAYE to nearest rand (AwayFromZero)
  annualTax = Round(annualTax, 0, MidpointRounding.AwayFromZero)

  // Step 6: De-annualise to monthly
  monthlyTax = annualTax / 12

  // Step 7: Round to nearest cent (AwayFromZero)
  return Round(monthlyTax, 2, MidpointRounding.AwayFromZero)
```

**2025/2026 statutory inputs** (from `docs/seed-data/sars-paye-2025-2026.json`):
- 7 tax brackets: 18% to 45%
- Tax threshold: R95,750 (under 65), R148,217 (65–74), R165,689 (75+)
- Primary rebate: R17,235 · Secondary: R9,444 · Tertiary: R3,145

---

## 2. Mid-Period Joiner (Pro-Rata)

An employee joins partway through the month. PAYE annualisation uses the **full monthly salary** — not the pro-rated salary. Only the PAYE result is pro-rated.

```
function CalculateJoinerPAYE(
  monthlyPackage: decimal,         // full contractual monthly salary
  daysWorked: int,                 // calendar days from join date to month-end
  daysInMonth: int,                // total calendar days in the month
  age: int,
  taxYear: TaxYear): decimal

  // Step 1: PAYE on full monthly package (SARS annualisation does not use partial salary)
  fullMonthlyPAYE = CalculateMonthlyPAYE(monthlyPackage, age, taxYear)

  // Step 2: Pro-rate the PAYE result only
  proRatedPAYE = fullMonthlyPAYE × (daysWorked / daysInMonth)

  return Round(proRatedPAYE, 2, MidpointRounding.AwayFromZero)
```

> **Pro-rated gross pay** = monthlyPackage × (daysWorked / daysInMonth) — used for earnings table, UIF and SDL
> **PAYE** = always calculated on full monthly rate, then pro-rated (as above)

**Example** — Employee earns R50,000/month, joins on the 15th (16 days remaining out of 31):
```
fullMonthlyPAYE = CalculateMonthlyPAYE(50000, 32, 2025) → R11,755.58
proRatedPAYE    = R11,755.58 × (16/31)                  → R6,069.98
grossPay        = R50,000    × (16/31)                   → R25,806.45
```

---

## 3. Weekly Pay Frequency

ZenoHR supports both Monthly and Weekly pay frequencies (ASSUME-006 override, confirmed 2026-02-19).

```
function CalculateWeeklyPAYE(weeklyTaxableIncome: decimal, age: int, taxYear: TaxYear): decimal

  // Step 1: Annualise using ×52 (not actual weeks elapsed in tax year)
  annualIncome = weeklyTaxableIncome × 52

  // Step 2-4: Identical to monthly method
  annualTax = ApplyBrackets(annualIncome, taxYear.brackets)
  annualTax = annualTax - GetRebate(age, taxYear.rebates)
  annualTax = Max(annualTax, 0)

  // Step 5: Round annual PAYE to nearest rand
  annualTax = Round(annualTax, 0, MidpointRounding.AwayFromZero)

  // Step 6: De-annualise using ÷52
  weeklyTax = annualTax / 52

  return Round(weeklyTax, 2, MidpointRounding.AwayFromZero)
```

> **Rule**: February, tax year changes, and leap years do not affect ÷52/×52. The annual equivalent method normalises this across the year. No special February handling.

---

## 4. Backdated Salary Increase (Retroactive Adjustment)

Retroactive adjustments are applied in the **current** payroll period as an adjustment document. Prior finalized periods are **never** recalculated (immutability rule).

```
Adjustment document (PayrollAdjustment entity):
  type                  = "backdated_salary_increase"
  periods_covered       = ["2026-01", "2026-02"]        // months covered
  additional_gross_zar  = MoneyZAR("2400.00")           // total retroactive gross
  additional_paye_zar   = MoneyZAR("576.00")            // total retroactive PAYE
  additional_uif_zar    = MoneyZAR("24.00")             // capped per month
  reference_payrun_id   = null                           // applies to current run
  created_by            = actor_id
  approved_by           = director_actor_id
  approved_at           = timestamp
```

EMP201 classification: include in current period under "retrospective adjustments" line item.

---

## 5. ETI — 12-Month Qualifying Period Rules

The Employment Tax Incentive qualifying period is **per employment relationship**, not per person.

```
function GetETITier(employmentStartDate: date, calculationDate: date): ETITier

  monthsEmployed = CalendarMonthsBetween(employmentStartDate, calculationDate)
  // CalendarMonthsBetween: counts complete calendar months since employmentStartDate
  // (2025-01-15 → 2025-03-15 = 2 months; 2025-01-15 → 2025-03-14 = 1 month)

  if monthsEmployed <= 12:
    return ETITier.Tier1     // higher ETI rate
  elif monthsEmployed <= 24:
    return ETITier.Tier2     // lower ETI rate
  else:
    return ETITier.Ineligible
```

**Key rules**:
- `employmentStartDate` = `employment_contracts.etiquette_start_date` (Firestore field)
- Termination + rehire = **new employment relationship** → new 24-month window, reset counter
- Multi-employment: each employment_contract has its own ETI eligibility clock
- Eligibility check: age 18–29 at calculation date, remuneration ≤ R7,500/month

---

## 6. PAYE Floor and UIF/SDL Interaction

When PAYE calculates to zero (income below threshold or rebates exceed tax):

```
// UIF is NOT affected by PAYE floor — it is a separate levy
uif_employee = Min(grossPay × 0.01, uifMonthlyEmployeeCap)   // currently R177.12
uif_employer = Min(grossPay × 0.01, uifMonthlyEmployerCap)   // currently R177.12

// SDL is NOT affected by PAYE floor — it is a separate levy
sdl          = grossPayroll × 0.01  // employer-only; exempt if annual payroll < R500k

// ETI still reduces employer's EMP201 PAYE remittance (NOT the employee's deduction)
etiReduction = CalculateETI(employee, taxYear)   // subtracted from employer EMP201 total

// Net pay calculation
netPay = grossPay
       - 0                    // PAYE = 0 (floored)
       - uif_employee
       - pension_employee
       - medical_employee
       - other_deductions
       + other_additions
```

---

## 7. February / Leap Year Edge Case

Monthly frequency always uses ÷12 / ×12. Weekly always uses ÷52 / ×52.

```
// February 2026 — 28 days. Monthly employee earns R30,000.
monthlyTaxableIncome = R30,000
annualIncome         = R30,000 × 12 = R360,000   // same as any other month
// No days-in-month adjustment for monthly frequency
```

**Rule**: The annual equivalent method inherently normalises short/long months. No special handling for February, leap years, or mid-year tax rate changes. Tax rate changes take effect at the start of the new tax year (1 March).

---

## 8. UIF Ceiling Interaction

UIF is calculated **before** PAYE deduction, on the gross remuneration. UIF is not deductible for PAYE purposes.

```
// UIF calculation (monthly)
uifRemuneration = grossMonthlyPay                 // UIF on gross — not reduced by PAYE
uif_employee    = Min(uifRemuneration × 0.01, R177.12)
uif_employer    = Min(uifRemuneration × 0.01, R177.12)

// taxable income for PAYE — UIF employee contribution is NOT deducted pre-PAYE
taxableIncomeForPAYE = grossPay                   // pension (RA) IS deductible — handled separately

// Gross-to-net waterfall order:
// 1. Calculate UIF on gross
// 2. Calculate pension deduction (reduces PAYE base if qualifying RA/pension fund)
// 3. Calculate PAYE on (gross − qualifying pension deductions)
// 4. Deduct from net: PAYE + UIF_employee + pension_employee + medical_employee + other
```

**UIF ceiling (2025/2026)**: R17,712 monthly remuneration cap → max R177.12/month each side.

---

## 9. Payslip Invariant Verification

The following invariant **must hold** to the cent before a `PayrollResult` document is written to Firestore.

```
// Required assertion — must not be skipped, bypassed, or disabled in any environment
function VerifyPayslipInvariant(result: PayrollResult): void

  expected = result.grossPay
           - result.paye
           - result.uifEmployee
           - result.pensionEmployee
           - result.medicalEmployee
           - result.otherDeductions
           + result.otherAdditions

  if expected != result.netPay:
    throw new PayrollInvariantException(
      $"Payslip invariant violated for employee {result.employeeId} in run {result.payrollRunId}. " +
      $"Expected netPay={expected:F2}, actual netPay={result.netPay:F2}. " +
      $"Halting payroll run — no results written."
    )
```

**Severity**: `PayrollInvariantException` is a **Sev-1 defect**. The entire payroll run must halt. No partial results are written. The `PayrollRun.status` remains `Processing` and is flagged `error: "invariant_violation"`. Manual investigation is required before retry.

---

## 10. EMP201 Filing Format

### Phase 1 (MVP)
Generate EMP201-compliant CSV for manual upload by HR Manager or Director via SARS eFiling web portal.

**CSV structure** (field mapping defined in `docs/seed-data/sars-filing-formats/emp201-field-layout.json`):

| Column | Field | Format |
|--------|-------|--------|
| PAYE_REF | employer.paye_reference | 10-char alphanumeric |
| PERIOD | tax_period | YYYYMM |
| EMPLOYEE_ID | employee.tax_ref_number | 13-digit |
| GROSS_REMUNERATION | payroll_result.gross_pay | ZAR integer (cents) |
| PAYE_WITHHELD | payroll_result.paye | ZAR integer (cents) |
| UIF_EMPLOYEE | payroll_result.uif_employee | ZAR integer (cents) |
| UIF_EMPLOYER | payroll_result.uif_employer | ZAR integer (cents) |
| SDL | payroll_result.sdl | ZAR integer (cents) |
| ETI_REDUCTION | payroll_result.eti_reduction | ZAR integer (cents) |

**Encoding**: UTF-8, CRLF line endings, comma delimiter, header row included.

### Phase 2 (Post-ISV Accreditation)
Direct submission via SARS ISV eFiling API (IBIR-006). Requires ISV registration number and trade testing approval. Tracked in `company_settings.sars_isv_config`. See `docs/schemas/firestore-collections.md` Section 18.

> **Action (Phase 3)**: Confirm exact CSV column specifications and submission endpoint with SARS eFiling documentation before generating the first live EMP201. Validate against SARS test environment.

---

## 11. EMP501 Year-End Reconciliation

The EMP501 reconciles the employer's total PAYE remittances for the tax year (March–February) against the sum of individual IRP5/IT3a certificates.

```
function GenerateEMP501(tenant_id: string, taxYear: int): EMP501Package

  // Aggregate from finalized PayrollRun records for the tax year
  runs = GetFinalizedRunsForTaxYear(tenant_id, taxYear)

  totalGross    = Sum(runs.SelectMany(r => r.results).Select(r => r.grossPay))
  totalPAYE     = Sum(runs.SelectMany(r => r.results).Select(r => r.paye))
  totalUIF      = Sum(runs.SelectMany(r => r.results).Select(r => r.uifEmployee + r.uifEmployer))
  totalSDL      = Sum(runs.SelectMany(r => r.results).Select(r => r.sdl))
  totalETI      = Sum(runs.SelectMany(r => r.results).Select(r => r.etiReduction))

  // Each employee's year-to-date figures → IRP5 certificate
  irp5List = GenerateIRP5Certificates(tenant_id, taxYear, runs)

  // Reconciliation check: sum of IRP5 PAYE == total PAYE remitted
  Assert irp5List.Sum(c => c.totalPAYE) == totalPAYE
    else throw EMP501ReconciliationException("IRP5 sum does not match EMP201 total PAYE")

  return new EMP501Package { totalGross, totalPAYE, totalUIF, totalSDL, totalETI, irp5List }
```

---

## 12. Termination Settlement Calculation

When an employee is terminated, the following apply:

```
function CalculateTerminationSettlement(employee, terminationDate, reason): TerminationSettlement

  // Pro-rate final month salary
  daysWorked = CalendarDaysBetween(periodStart, terminationDate)
  daysInMonth = CalendarDaysInMonth(periodStart)
  finalGross = contractMonthlySalary × (daysWorked / daysInMonth)

  // Leave payout: outstanding annual leave days at daily rate
  // BCEA S.40: annual leave must be paid out on termination
  dailyRate     = contractMonthlySalary / 21.67    // BCEA average working days/month
  leavePayout   = outstandingAnnualLeaveDays × dailyRate

  // Notice pay (BCEA S.37 — if not worked)
  noticePay = contractMonthlySalary × (noticePeriodDays / 30)   // if garden leave applies

  // Severance pay (BCEA S.41 — only for operational requirements dismissal)
  // = 1 week per completed year of service
  severancePay = (completedYearsOfService × weeklyRate)
               if reason == DismissalReason.OperationalRequirements else 0

  // PAYE on lump sum: apply directive-based PAYE or standard PAYE
  // Severance pay is taxed separately: first R500k exempt (SARS IRP3(a) directive required)
  totalGross = finalGross + leavePayout + noticePay + severancePay
  paye       = CalculatePAYEWithDirective(totalGross, employee, taxYear, directive)

  return TerminationSettlement { finalGross, leavePayout, noticePay, severancePay, paye }
```

---

## Appendix A — MoneyZAR Arithmetic Rules

See `docs/schemas/monetary-precision.md` for the full MoneyZAR spec.

- **Storage**: All monetary values stored as strings in Firestore (e.g., `"11755.58"`)
- **In-memory type**: `System.Decimal` (never `float` or `double`)
- **Rounding**: `MidpointRounding.AwayFromZero` throughout payroll calculations
- **Annual PAYE rounding**: nearest rand (0 decimal places)
- **Period PAYE rounding**: nearest cent (2 decimal places)
- **Addition/subtraction**: exact (no rounding between operations)
- **Percentage multiplication**: `decimal × (rate / 100m)` — no intermediate rounding

---

## Appendix B — Property-Based Test Scenarios (FsCheck)

The following properties must hold for all valid inputs (implemented in `ZenoHR.Module.Payroll.Tests`):

1. `CalculateMonthlyPAYE(income, age, year) >= 0` for all income values
2. `CalculateMonthlyPAYE(95750/12, under65, 2025) == 0` (at-threshold = zero PAYE)
3. `CalculateMonthlyPAYE(x, age, y) <= x` (PAYE never exceeds gross income)
4. `CalculateWeeklyPAYE(w, a, y) ≈ CalculateMonthlyPAYE(w*52/12, a, y) / (52/12)` (±2c tolerance)
5. `VerifyPayslipInvariant(result)` never throws for any valid payroll run input
6. `uifEmployee <= 177.12` for all gross pay values (ceiling enforced)
7. `CalculatePAYE(threshold - 1c, under65, y) == 0` (just below threshold = zero)
8. `CalculatePAYE(threshold + 1c, under65, y) > 0` (just above threshold = positive)
