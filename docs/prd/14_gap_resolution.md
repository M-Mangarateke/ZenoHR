---
doc_id: PRD-14-GAP-RESOLUTION
version: 1.0.0
owner: Product Manager
updated_on: 2026-02-18
applies_to:
  - All PRD documents (PRD-00 through PRD-13)
  - Implementation agents and development sessions
depends_on:
  - PRD-00-MANIFEST
  - PRD-01-EXECUTIVE
  - PRD-02-DOMAIN
  - PRD-03-ARCH
  - PRD-06-COMP
---

# PRD Gap Resolution

This document resolves all identified gaps, ambiguities, and missing specifications across PRD-00 through PRD-13. Each resolution is traceable to the originating gap and the seed data or decision that closes it.

## 1. Architectural Decisions (Resolved)

These decisions were left open in the PRD and have been resolved through stakeholder input.

| ID | Decision | Resolution | Rationale | Impacts |
|----|----------|------------|-----------|---------|
| GAP-ARCH-001 | Frontend framework | **Blazor Server** | Full .NET stack, SSR suits compliance UIs, matches ASP.NET Core lock | All UI tasks, TASK-160+ |
| GAP-ARCH-002 | Authentication provider | **Firebase Auth** | Native Firestore integration, OIDC/JWT, MFA built-in | REQ-SEC-001, TASK-024 |
| GAP-ARCH-003 | Cloud hosting platform | **Azure (SA North region) + Firestore via Google SDK** | South Africa data residency solves POPIA cross-border concerns | ADR-006, CTL-POPIA-013, REQ-OPS-007 |
| GAP-ARCH-004 | PDF generation library | **QuestPDF** | Open source, .NET native, good for payslips and evidence packs | REQ-HR-004, CTL-BCEA-005 |
| GAP-ARCH-005 | Domain event bus | **MediatR (in-process)** | Modular monolith pattern, synchronous within module, async for cross-module | REQ-OPS-002 |
| GAP-ARCH-006 | Background jobs | **.NET BackgroundService + Azure scheduled jobs** | In-process for simple tasks, Azure-native for cron triggers | REQ-OPS-005, RISK-005 |
| GAP-ARCH-007 | CI/CD platform | **GitHub Actions** | Standard for GitHub repositories | REQ-OPS-009 |
| GAP-ARCH-008 | Observability stack | **OpenTelemetry SDK + Azure Monitor (Application Insights)** | Native Azure integration | REQ-OPS-005, REQ-OPS-006 |
| GAP-ARCH-009 | Secret management | **Azure Key Vault** | Native Azure integration, SA region available | REQ-SEC-008 |

### ADR Update: ASSUME-006 Override

**Original**: "Payroll cadence is monthly."
**Resolution**: **Monthly AND Weekly payroll are both in scope for v1.** Many SA SMEs in retail and manufacturing pay weekly. The payroll calculation engine must support configurable pay periods. PAYE must use both weekly and monthly tax deduction tables.

**Impact**: PayrollRun entity gains a `pay_period_type` field (weekly/monthly). PAYE calculation uses annual equivalent method regardless of period but de-annualises differently (÷12 for monthly, ÷52 for weekly).

### ADR Update: Cross-Cloud Architecture

Azure hosts the compute (Azure Container Apps). Firestore remains on GCP (accessed via Google.Cloud.Firestore NuGet SDK over HTTPS). This is a supported pattern. Authentication uses Firebase Auth (GCP) with JWT tokens validated by ASP.NET Core middleware. Secrets stored in Azure Key Vault.

**POPIA implication**: Employee PII is stored in Firestore (GCP) but the GCP project's data location can be set to `eur3` (Europe multi-region). Cross-border processing basis: legitimate interest for employment administration under POPIA Section 11(1)(f) and contractual necessity under Section 11(1)(b). The lawful basis is documented per CTL-POPIA-001.

---

## 2. Missing Numerical Specifications (Resolved)

All numerical values are now captured in seed data files under `docs/seed-data/`. Each file is a versioned `StatutoryRuleSet` JSON document.

| ID | Gap | Resolution File | Key Values |
|----|-----|----------------|------------|
| GAP-NUM-001 | PAYE tax brackets | `sars-paye-2025-2026.json` | 7 brackets: 18%-45%, rebates: R17,235/R9,444/R3,145 |
| GAP-NUM-002 | UIF rates and ceiling | `sars-uif-sdl.json` | 1%+1%, ceiling R17,712/month, max R177.12 each |
| GAP-NUM-003 | SDL rate and threshold | `sars-uif-sdl.json` | 1% employer-only, exempt if payroll <R500k/year |
| GAP-NUM-004 | ETI eligibility and amounts | `sars-eti.json` | Age 18-29, R2,500 min wage, R7,500 max, 60%/30% tiers (April 2025 update) |
| GAP-NUM-005 | BCEA ordinary hours | `bcea-working-time.json` | 45hrs/week, 9hrs/day (5-day), 8hrs/day (6+day) |
| GAP-NUM-006 | Overtime multipliers | `bcea-working-time.json` | 1.5x normal, 2x Sunday/public holiday |
| GAP-NUM-007 | Leave entitlements | `bcea-leave.json` | Annual 21 days, sick 30/36mo, family 3/year, maternity 4mo, parental 10 days |
| GAP-NUM-008 | Notice periods | `bcea-notice-severance.json` | 1wk/<6mo, 2wk/<1yr, 4wk/1yr+ |
| GAP-NUM-009 | Severance rate | `bcea-notice-severance.json` | 1 week per completed year (operational dismissal only) |
| GAP-NUM-010 | Travel allowance rates | `sars-travel-rates.json` | R4.76/km reimbursive, 8-tier fixed cost table to R800k+ |
| GAP-NUM-011 | SA ID format | `sa-id-validation.json` | 13-digit YYMMDDSSSSCAZ with Luhn checksum |
| GAP-NUM-012 | Tax reference format | `sars-tax-ref-format.json` | 10-digit numeric, prefix indicates type |
| GAP-NUM-013 | Public holidays | `sa-public-holidays-2026.json` | 12 holidays + substitution rule for Sundays |
| GAP-NUM-014 | Payslip required fields | `bcea-section33-payslip-fields.json` | 8 mandatory + 6 conditional fields per BCEA Section 33 |

---

## 3. Missing Filing Format Specifications (Resolved)

| ID | Gap | Resolution File |
|----|-----|----------------|
| GAP-FILE-001 | EMP201 field layout | `sars-filing-formats/emp201-field-layout.json` |
| GAP-FILE-002 | IRP5/IT3(a) source codes | `sars-filing-formats/irp5-it3a-source-codes.json` |
| GAP-FILE-003 | EMP501 reconciliation | Covered in `irp5-it3a-source-codes.json` (emp501_reconciliation section) |

**SARS filing channel resolution**: ZenoHR generates CSV files compatible with e@syFile Employer import format. Files are NOT submitted directly to SARS via API. The workflow is: ZenoHR generates file → Compliance Officer downloads → imports into e@syFile → submits to SARS eFiling. This is the "manual/assisted operational submission path" referenced in the PRD.

---

## 4. Rounding and Precision Rules (Resolved)

See `docs/schemas/monetary-precision.md` for full specification. Summary:

| Context | Rule |
|---------|------|
| All monetary amounts in code | `decimal` type. Never `float` or `double`. |
| PAYE annual tax | Round to nearest rand (MidpointRounding.AwayFromZero) |
| PAYE monthly/weekly | Round to nearest cent (2 decimal places) |
| UIF contribution | Round to nearest cent |
| SDL contribution | Round to nearest cent |
| Payslip net pay | net_pay = gross_pay - sum(all_deductions), verified to cent |
| Firestore storage | String representation of decimal to preserve precision |

---

## 5. Ambiguity Resolutions

| ID | Ambiguity | Resolution |
|----|-----------|------------|
| GAP-AMB-001 | Payroll cadence (monthly only per ASSUME-006) | **Monthly AND Weekly supported.** See ADR update above. |
| GAP-AMB-002 | Leave types in scope | **Annual + Sick + Family Responsibility + Maternity + Parental** all in v1. BCEA mandates all five. |
| GAP-AMB-003 | Public holiday handling | Public holidays calendar is seed data (updated annually). Overtime on public holidays = 2x rate. Non-working public holidays = normal daily pay. |
| GAP-AMB-004 | tenant_id vs "single entity" (ASSUME-005) | Document-level `tenant_id` field on all Firestore documents from day one. Single-tenant at launch but data model supports multi-tenant. No separate databases or collection prefixes. |
| GAP-AMB-005 | Payslip delivery method | **Both**: downloadable from self-service portal AND optionally emailed as PDF. Portal is primary; email is configurable per employee. |
| GAP-AMB-006 | SARS filing format versions | e@syFile CSV import format per BRS v24.0. Source codes per 2026 guide. |
| GAP-AMB-007 | Email/SMS provider | **Deferred to Phase 2.** Phase 1 uses in-app notifications only. Email integration for filing reminders and breach notifications added in Phase 2. Provider choice: Azure Communication Services (consistent with Azure hosting). |
| GAP-AMB-008 | Container/serverless model | **Azure Container Apps** for the API + Blazor Server host. Serverless scaling with minimum 1 instance for warm starts during business hours. |

---

## 6. Firestore Data Design (Resolved)

See `docs/schemas/firestore-collections.md` for full specification. Key decisions:

- **Root collections**: `employees`, `payroll_runs`, `leave_balances`, `timesheets`, `compliance_submissions`, `audit_events`, `statutory_rule_sets`, `risk_assessments`
- **Subcollections**: `employees/{id}/contracts`, `employees/{id}/leave_requests`, `payroll_runs/{id}/results`, `payroll_runs/{id}/adjustments`
- **Tenant isolation**: `tenant_id` field on every root document. Firestore security rules enforce tenant scoping.
- **Transaction boundaries**: Payroll finalization uses batched writes (max 500 ops per batch). For >250 employees, split into multiple sequential batches within a server-side transaction coordinator.
- **Immutability**: Finalized PayrollRun documents and AuditEvent documents use Firestore security rules to prevent updates/deletes.

---

## 7. Remaining Deferred Gaps (Design)

These gaps are intentionally deferred and will be closed after wireframe generation:

| ID | Gap | Deferred To |
|----|-----|------------|
| GAP-DESIGN-001 | Wireframes for all persona workflows | After Google Stitch wireframe generation |
| GAP-DESIGN-002 | Payslip PDF template design | After wireframe generation |
| GAP-DESIGN-003 | Component-level UI specifications | After wireframe generation |

---

## 8. Feature Gap Resolutions (Added 2026-02-21)

Decisions made during the design-phase review. Each resolves a previously unspecified behaviour.

### GAP-FEAT-001: Employee Self-Service Clock-In/Out

**Gap**: Time & Attendance assumed HR manually enters all employee hours. No self-service clock mechanism was specified.

**Resolution**: Employees clock in and out themselves via the `/clock-in` route. Every button press creates an immutable `clock_entries` document with a server-side timestamp (not client time). A background job aggregates daily clock entries into the weekly `timesheets` record at end-of-day.

Managers access the same `/clock-in` route and see a team panel (today's clock status per employee). If a Manager suspects an employee has not clocked in, they raise a `timesheet_flags` record specifying the reason (`suspected_absence`, `missing_clock_out`, etc.). Flagged employees appear with an amber indicator until the flag is resolved or dismissed. HR Managers and Directors have read-only access to all clock entries.

**Impacts**: New `clock_entries` and `timesheet_flags` Firestore root collections. New screen S14 `/clock-in`. RBAC updated (PRD-15). `time_entries.source` gains `employee_self` value. See `docs/schemas/firestore-collections.md` sections 6.3–6.4.

**Requirement ref**: REQ-HR-003 (time tracking)

---

### GAP-FEAT-002: Data Archival After 5-Year Retention Window

**Gap**: PRD-02 stated "Deletion is forbidden; retention expiry is archive-only with legal hold support" but no concrete archival mechanism, field definitions, UI, or deletion pathway was specified.

**Resolution**: Records transition to `data_status: "archived"` automatically after 5 years (via scheduled BackgroundService). Archived records appear in the **Settings > Data Archival** tab and are excluded from all normal UI queries. The HR Manager/Director then chooses when (and whether) to free the database space by following the **download-then-delete** workflow:

1. **Archived** — record is past 5 years; shows in archival tab. No user action required yet. Record is still in Firestore but excluded from all active views.
2. **Download** — HR Manager clicks "Download" to export the record as a PDF/CSV archive to their local device or external storage. The system marks `download_confirmed_at` on the document. The "Remove from ZenoHR" button unlocks.
3. **Remove from ZenoHR** — after download confirmed, HR Manager clicks "Remove from ZenoHR". A confirmation dialog warns that this is permanent and irreversible. On confirm, the Firestore document is **hard-deleted** and an AuditEvent is written capturing who deleted it, when, and the pre-deletion checksum.
4. **Legal Hold** — `data_status: "legal_hold"` blocks all deletion. The "Remove from ZenoHR" button is disabled and greyed. Hold can only be lifted by a Director.

**If HR Manager never downloads**: the record stays in the archive tab indefinitely. There is no forced deletion. The system never auto-deletes — only the Director/HRManager initiates removal after download.

**Fields added/updated**:
- `data_status`: `active` | `archived` | `legal_hold` (on `employees`, `payroll_runs`, `audit_events`, `compliance_submissions`)
- `archived_at`: timestamp when transitioned to archived
- `archive_reason`: string (e.g., `retention_expiry`, `legal_hold`)
- `download_confirmed_at`: timestamp when export was downloaded (prerequisite for deletion)
- `deleted_at`, `deleted_by`: written to AuditEvent only — the source document itself is gone

**Requirement ref**: REQ-COMP-004 (retention), CTL-POPIA-009 (data minimisation), CTL-POPIA-010 (storage limitation)

---

### GAP-FEAT-003: Month-End and Year-End Local Backup Prompts

**Gap**: No mechanism existed to encourage local copies of payroll and compliance data. All exports were on-demand only with no prompting.

**Resolution**: After a payroll run reaches `status: Filed`, a dismissible banner appears above the payroll run table for Director and HRManager roles:

> *"[Month] [Year] payroll finalised. Save a local backup for your records. [Download Summary CSV] [Download Payslips ZIP] [Dismiss]"*

After year-end EMP501/IRP5 submission (February filing period), a year-end banner appears on the Compliance screen:

> *"Tax year [YYYY/YYYY] filing complete. Download your evidence pack for offline storage. [Download Evidence Pack PDF] [Download EMP501 CSV] [Dismiss]"*

Dismissal state is stored per-user in `company_settings/{tenant_id}/user_preferences/{user_id}` as `month_backup_dismissed_at` / `yearend_backup_dismissed_at`. Banners re-appear on the next period.

**Impacts**: UI changes to `04-payroll.html` and `06-compliance.html`. No new collections required.

**Requirement ref**: REQ-OPS-007 (disaster recovery), REQ-COMP-005 (evidence retention)

---

### GAP-FEAT-004: Comprehensive Employee Data Fields

**Gap**: The `employees` schema was missing several fields needed for SARS EMP501, EE Act reporting, payroll deductions, and HR administration.

**Resolution**: The following fields are added to the `employees` collection and its subcollections. See `docs/schemas/firestore-collections.md` section 5 for full field definitions.

**Added to `employees` root document**: `personal_phone_number`, `personal_email`, `work_email`, `marital_status`, `nationality`, `passport_expiry_date`, `gender`, `race`, `disability_status`, `disability_description`, `employment_equity_category`, `seta_registration_number`, `data_status`, `archived_at`, `archive_reason`.

**New subcollections**:
- `employees/{emp_id}/bank_accounts` — Full encrypted bank account details (replaces opaque `bank_account_ref` pointer)
- `employees/{emp_id}/next_of_kin` — Legal next of kin / pension beneficiary (distinct from emergency contacts)
- `employees/{emp_id}/benefits` — Medical aid, pension fund, provident fund, and group life membership + contribution rates

**Schema rename**: `employment_contracts.grade` → `employment_contracts.occupational_level` to align with QCTO/SAQA occupational level terminology used in South African EE reporting.

**Requirement ref**: REQ-HR-001 (employee data), CTL-SARS-001 (EMP501 fields), EEA S.20 (Employment Equity reporting)

---

### GAP-FEAT-005: Payroll Finalization — Single-Actor, Simplified State Machine

**Gap**: The original payroll state machine (`Draft → Calculated → Validated → Approved → Finalized → Filed`) assumed two distinct actors: a `PayrollOfficer` approving and a `ComplianceOfficer` finalizing (dual control, REQ-SEC-003). In the SA SME context, the HR Manager is the sole person responsible for payroll. Requiring two separate people is impractical and adds no meaningful control where only one person has payroll access.

**Resolution**: The state machine is simplified to four stages: **`Draft → Calculated → Finalized → Filed`**.

- `Draft → Calculated`: Triggered **automatically** when the run is created. The system loads all active employee contracts and timesheets, calculates PAYE/UIF/SDL/ETI, runs BCEA compliance checks, and writes compliance flags. No user action required.
- `Calculated → Finalized`: Triggered by a **single click** ("Finalize & Lock") by the Director or HRManager after reviewing the calculated totals. No second actor required.
- `Finalized → Filed`: Set programmatically when the EMP201 CSV export is generated. Marks the run as filed for the period.

The `Validated` and `Approved` statuses are removed from the state machine. Compliance validation runs automatically as part of the `Calculated` phase — critical flag violations block finalization with a clear error message; warning-level flags are displayed but non-blocking.

The `approved_by` / `approved_at` fields are removed from `payroll_runs`. The `finalized_by` field records the sole actor (Director or HRManager) who locked the run.

**HR Manager's full payroll workflow (monthly)**:
1. Click **New Run** → select month → system calculates automatically (status: `Calculated`)
2. Review the four KPI totals (Gross, PAYE, UIF+SDL, Net) and the employee breakdown table
3. Click **Finalize & Lock** (status: `Finalized` → record immutable)
4. Download **EMP201 CSV** → import into e@syFile → submit to SARS (status: `Filed`)

**Impacts**: Removed `Validated`, `Approved` statuses and `approved_by`/`approved_at` fields from `payroll_runs` schema. `finalized_by` now records the Director/HRManager actor (not a dual-control ComplianceOfficer). Action bar in `04-payroll.html` updated: "Finalize & Lock" enabled for HRManager/Director; EMP201 CSV enabled only after finalization.

**Requirement ref**: REQ-HR-003 (payroll run), REQ-SEC-003 (finalization control — SME governance exception documented here)

---

### GAP-FEAT-006: SARS Independent Software Vendor (ISV) Accreditation Readiness

**Gap**: No explicit SARS ISV accreditation checklist existed. The PRD covered filing formats (EMP201, IRP5/IT3(a)) but did not map these to SARS's ISV program requirements, leaving it unclear whether the system was on track for eventual direct submission accreditation.

**Context**: SARS operates an ISV program ([sars.gov.za/isv](https://www.sars.gov.za/individuals/i-need-help-with-my-tax/your-tax-questions-answered/independent-software-vendors/)) covering three categories: Tax Return Submissions, Transfer Duty, and Tax Directives. For a payroll/HR system, the relevant category is **PAYE returns + Tax Directives (IBIR-006)**. Direct submission requires: (1) signed Terms & Conditions submitted to [[email protected]](mailto:[email protected]); (2) a unique ISV access key per product; (3) trade testing against SARS specifications; and (4) Form INF001 for Tax Directive interface access.

**What ZenoHR already has (ISV-ready):**

| Requirement | Coverage | Source |
|---|---|---|
| EMP201 monthly declaration format | ✓ Complete | `sars-filing-formats/emp201-field-layout.json` |
| IRP5/IT3(a) certificate source codes | ✓ Complete | `sars-filing-formats/irp5-it3a-source-codes.json` |
| EMP501 annual reconciliation | ✓ Covered | `irp5-it3a-source-codes.json` (emp501_reconciliation section) |
| PAYE tax table (7 brackets, rebates, thresholds) | ✓ Complete | `sars-paye-2025-2026.json` |
| UIF ceiling and contribution rates | ✓ Complete | `sars-uif-sdl.json` |
| SDL rate and exemption threshold | ✓ Complete | `sars-uif-sdl.json` |
| ETI eligibility rules and amounts (April 2025) | ✓ Complete | `sars-eti.json` |
| Travel allowance rates and fixed cost table | ✓ Complete | `sars-travel-rates.json` |
| SA ID validation (13-digit, Luhn checksum) | ✓ Complete | `sa-id-validation.json` |
| Tax reference number format validation | ✓ Complete | `sars-tax-ref-format.json` |
| BCEA Section 33 payslip mandatory fields | ✓ Complete | `bcea-section33-payslip-fields.json` |
| EMP201 CSV for e@syFile import (Phase 1 filing path) | ✓ Complete | GAP-FILE-001 |
| Annual/weekly pay period support | ✓ Complete | GAP-ARCH-001 (ASSUME-006 override) |
| Employer registration fields for EMP501 | ✓ Covered via `employees` + `company_settings` collections | |

**What ZenoHR needs to add before ISV accreditation (Phase 2):**

| Requirement | Gap | Action Required |
|---|---|---|
| Tax Directives (IBIR-006) | Not yet specified. Needed for lump-sum pension/retirement payments on termination. | Add `tax_directives` collection; implement IBIR-006 interface spec; add seed data file `sars-ibir-006-directives.json` |
| Bulk IRP5 XML export | Currently CSV only. SARS eFiling bulk submission uses XML format. | Add XML export generation alongside existing CSV |
| ISV access key management | No mechanism to store/rotate the SARS-issued ISV access key per product. | Store in Azure Key Vault; expose as `company_settings.sars_isv_key` (encrypted) |
| Form INF001 submission process | Out-of-scope for code — a business process. | Director completes INF001 and emails to [[email protected]](mailto:[email protected]) to initiate accreditation |
| Trade testing environment | SARS requires test submissions against their test environment. | Set up a SARS test eFiling account; add `SARS_ENVIRONMENT: test|production` config flag |
| Direct eFiling API submission | Currently download-then-import via e@syFile. Direct submission requires ISV accreditation. | Phase 2 feature: implement eFiling API HTTP submission using ISV access key + employer eFiling credentials (stored in Key Vault) |

**Resolution**: ZenoHR is already spec-complete for Phase 1 (e@syFile CSV path). Phase 2 (direct API submission) requires the six items above. The Tax Directives gap (IBIR-006) is the highest-priority item because it is needed for any employee who exits with a pension or provident fund lump sum — a common scenario in SA SMEs.

**Requirement ref**: REQ-COMP-001 (SARS filing), CTL-SARS-003 (EMP201 readiness), CTL-SARS-004 (EMP501 reconciliation)

---

## Governance

This document is governed by the same review cadence as the PRD package (PRD-00-MANIFEST). All seed data files must be updated when SARS publishes new tax tables or when BCEA amendments take effect. The `version` field in each seed data file tracks these updates.

**Annual update triggers:**
- SARS Budget Speech (March) → Update PAYE brackets, rebates, thresholds
- SARS rate per km notice (March) → Update travel rates
- UIF ceiling gazette notice → Update UIF ceiling
- ETI amendments → Update ETI thresholds
- Public Holidays Act amendments → Update holiday calendar
