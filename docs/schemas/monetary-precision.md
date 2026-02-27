---
doc_id: SCHEMA-MONETARY-PRECISION
version: 1.0.0
owner: Engineering Lead
updated_on: 2026-02-18
applies_to:
  - All payroll calculation code
  - All monetary storage in Firestore
  - All API response amounts
  - All report/payslip generation
---

# Monetary Precision and Rounding Rules

## Core Rule: Never Use Floating Point for Money

All monetary values in ZenoHR MUST use `decimal` (C# `System.Decimal`). The use of `float`, `double`, or any IEEE 754 binary floating-point type for monetary amounts is a **Sev-1 defect**.

## C# Type: MoneyZAR

```
public readonly record struct MoneyZAR(decimal Amount)
```

- Internal precision: `decimal(18,2)` -- 18 total digits, 2 decimal places
- All arithmetic on `MoneyZAR` preserves 2 decimal places
- Comparison is exact (no epsilon tolerance)
- Serialization to Firestore: stored as string to preserve exact decimal representation
- JSON serialization: numeric with exactly 2 decimal places

## Rounding Rules by Context

| Context | Rounding Mode | Decimal Places | C# Method |
|---------|--------------|----------------|-----------|
| **PAYE annual tax** | Round half away from zero | 0 (nearest rand) | `Math.Round(amount, 0, MidpointRounding.AwayFromZero)` |
| **PAYE monthly/weekly** | Round half away from zero | 2 (nearest cent) | `Math.Round(amount, 2, MidpointRounding.AwayFromZero)` |
| **UIF contribution** | Round half away from zero | 2 | `Math.Round(amount, 2, MidpointRounding.AwayFromZero)` |
| **SDL contribution** | Round half away from zero | 2 | `Math.Round(amount, 2, MidpointRounding.AwayFromZero)` |
| **ETI amount** | Round half away from zero | 2 | `Math.Round(amount, 2, MidpointRounding.AwayFromZero)` |
| **Gross pay** | No rounding (exact sum) | 2 | Sum of components |
| **Net pay** | No rounding (exact difference) | 2 | `gross - sum(deductions)` |
| **Hourly rate** | Truncate | 4 (internal precision) | `Math.Truncate(rate * 10000) / 10000` |
| **Daily rate** | As per hourly rate | 4 (internal) | Same |
| **Travel reimbursement** | Round half away from zero | 2 | Rate per km * km = amount |

## PAYE Calculation Method

The **annual equivalent method** is used for all pay periods:

### Monthly payroll:
1. Annualise: `annual_taxable = monthly_taxable * 12`
2. Apply tax brackets to get `annual_tax` (see `sars-paye-2025-2026.json`)
3. Subtract rebates: `annual_tax_after_rebates = annual_tax - applicable_rebates`
4. Floor at zero: `annual_tax_after_rebates = max(0, annual_tax_after_rebates)`
5. Round annual tax: `round(annual_tax_after_rebates, 0, AwayFromZero)`
6. De-annualise: `monthly_paye = annual_tax_after_rebates / 12`
7. Round monthly: `round(monthly_paye, 2, AwayFromZero)`

### Weekly payroll:
1. Annualise: `annual_taxable = weekly_taxable * 52`
2. Steps 2-5 same as monthly
3. De-annualise: `weekly_paye = annual_tax_after_rebates / 52`
4. Round weekly: `round(weekly_paye, 2, AwayFromZero)`

## Payslip Verification Invariant

Every payslip MUST satisfy:
```
net_pay == gross_pay - paye - uif_employee - pension_employee - medical_employee - other_deductions
```

This invariant is checked programmatically after every payroll calculation. A mismatch of even 1 cent is a **validation failure** that blocks finalization.

## Firestore Storage

| Field Type | Firestore Type | Example |
|-----------|---------------|---------|
| MoneyZAR | string | `"15234.56"` |
| Rate (percentage) | number | `0.18` |
| Hours | number | `45.5` |
| Multiplier | number | `1.5` |

**Why string for MoneyZAR**: Firestore's `number` type is IEEE 754 double-precision, which cannot exactly represent all decimal values. Storing as string preserves the exact value. The application deserializes to `decimal` on read.

## API Response Format

All monetary amounts in API responses use JSON number type with exactly 2 decimal places:
```json
{ "gross_pay": 25000.00, "paye": 3456.78, "net_pay": 19876.54 }
```

## Test Requirements

Property-based tests MUST verify:
1. `net_pay == gross_pay - sum(deductions)` for all generated payslips
2. PAYE for same annual income is identical regardless of pay period (monthly vs weekly, within rounding tolerance of R1)
3. Rounding never produces negative PAYE when income is below threshold
4. Tax bracket boundary values produce correct amounts (test at exact boundary, boundary+1, boundary-1)
5. Maximum UIF contribution is exactly R177.12 for any income above R17,712/month
