---
doc_id: SCHEMA-FIRESTORE-COLLECTIONS
version: 1.0.0
owner: Solution Architect
updated_on: 2026-02-18
applies_to:
  - All Firestore collection design and access patterns
  - Security rules and index configuration
  - Transaction boundary planning
depends_on:
  - PRD-02-DOMAIN
  - PRD-03-ARCH
  - PRD-04-API
  - PRD-13-GLOSSARY
  - SCHEMA-MONETARY-PRECISION
requirements:
  - REQ-HR-001
  - REQ-HR-012
  - REQ-COMP-011
  - REQ-OPS-001
  - REQ-SEC-003
  - REQ-SEC-004
---

# Firestore Collection Schema

## Table of Contents

1. [Design Principles](#1-design-principles)
2. [Tenant Isolation Strategy](#2-tenant-isolation-strategy)
3. [Document ID Generation Strategy](#3-document-id-generation-strategy)
4. [Collection Hierarchy Overview](#4-collection-hierarchy-overview)
5. [Employee Context](#5-employee-context)
6. [Time and Attendance Context](#6-time-and-attendance-context)
7. [Leave Context](#7-leave-context)
8. [Payroll Context](#8-payroll-context)
9. [Compliance Context](#9-compliance-context)
10. [Audit and Evidence Context](#10-audit-and-evidence-context)
11. [Risk and Insights Context](#11-risk-and-insights-context)
12. [Composite Index Definitions](#12-composite-index-definitions)
13. [Transaction Boundary Analysis](#13-transaction-boundary-analysis)
14. [Immutability Enforcement](#14-immutability-enforcement)
15. [Subcollection Design Rationale](#15-subcollection-design-rationale)

---

## 1. Design Principles

1. **Compliance-first storage**: Every document that participates in legal or financial workflows is append-only or write-once after finalization. Corrections use compensating records.
2. **Monetary precision**: All ZAR monetary amounts are stored as Firestore `string` type to preserve exact `decimal(18,2)` representation. See `SCHEMA-MONETARY-PRECISION` for rounding rules.
3. **Tenant isolation**: Every root-level document carries a mandatory `tenant_id` field. Security rules enforce that queries and writes are scoped to the authenticated tenant.
4. **Deterministic IDs where audit matters**: Payroll runs, submission packages, and evidence bundles use deterministic, human-readable IDs. Employee records and audit events use UUID-based IDs.
5. **Subcollections for unbounded child data**: Line items, time entries, and ledger entries live in subcollections to avoid document size limits (1 MiB) and to enable efficient pagination.

---

## 2. Tenant Isolation Strategy

### Approach: Document-Level `tenant_id` Field

Every root-level collection document includes a required `tenant_id: string` field. This field is set at document creation and is immutable.

**Why document-level instead of tenant-scoped root collections:**

- Firestore does not support collection-level access control natively; security rules operate on documents.
- Document-level `tenant_id` allows a single deployment to serve multiple tenants without collection namespace fragmentation.
- All queries MUST include a `tenant_id` equality filter. Composite indexes include `tenant_id` as the first field.

**Security rule pattern:**

```
match /employees/{docId} {
  allow read, write: if request.auth.token.tenant_id == resource.data.tenant_id;
  allow create: if request.auth.token.tenant_id == request.resource.data.tenant_id;
}
```

**Subcollection inheritance:** Subcollections do NOT carry their own `tenant_id`. Access to subcollections is gated by the parent document's security rule. The application layer must traverse the parent to reach child documents, inheriting tenant scope.

---

## 3. Document ID Generation Strategy

| Collection | ID Pattern | Format | Example | Rationale |
|---|---|---|---|---|
| `employees` | `emp_<uuid>` | UUID v7 (time-ordered) | `emp_019503a1-b2c4-7d00-8e1f-abcdef123456` | Uniqueness; no business-key collisions |
| `employment_contracts` | `con_<uuid>` | UUID v7 | `con_019503a1-c3d5-7d00-9f2a-abcdef654321` | Multiple contracts per employee; UUID avoids conflicts |
| `termination_cases` | `trm_<uuid>` | UUID v7 | `trm_019503a2-d4e6-7d00-af3b-abcdef789012` | One per termination event |
| `timesheets` | `ts_<emp>_<YYYY>_W<WW>` | Deterministic | `ts_emp_019503a1_2026_W08` | One timesheet per employee per week; deduplication |
| `clock_entries` | `ce_<uuid>` | UUID v7 | `ce_019503a7-c1d2-7d00-ef8g-abcdef012345` | Employee self-service clock-in events; time-ordered |
| `timesheet_flags` | `flag_<uuid>` | UUID v7 | `flag_019503a8-d2e3-7d00-fg9h-abcdef123456` | Manager-created attendance flags |
| `leave_balances` | `lb_<emp>_<type>_<cycle>` | Deterministic | `lb_emp_019503a1_annual_2026` | One balance per employee per type per cycle |
| `leave_requests` | `lr_<uuid>` | UUID v7 | `lr_019503a3-e5f7-7d00-bf4c-abcdef345678` | Arbitrary number of requests |
| `payroll_runs` | `pr_<YYYY>_<MM>_<seq>` | Deterministic | `pr_2026_02_001` | Human-readable; auditable; sequential per period |
| `payroll_runs` (weekly) | `pr_<YYYY>_W<WW>_<seq>` | Deterministic | `pr_2026_W08_001` | Weekly pay period variant |
| `payroll_adjustments` | `adj_<uuid>` | UUID v7 | `adj_019503a4-f6a8-7d00-cf5d-abcdef567890` | Corrections are append-only |
| `statutory_rule_sets` | `rules_<domain>_<version>` | Deterministic | `rules_sars_2026_1` | Version-addressable configuration |
| `submission_packages` | `sub_<type>_<period>_<seq>` | Deterministic | `sub_emp201_2026_02_001` | Auditable filing packages |
| `compliance_results` | `cr_<control>_<period>_<seq>` | Deterministic | `cr_CTL-SARS-001_2026_02_001` | Traceable to control ID and period |
| `audit_events` | `aud_<uuid>` | UUID v7 (time-ordered) | `aud_019503a5-a7b9-7d00-df6e-abcdef678901` | High-volume append-only; time-ordered for range queries |
| `evidence_bundles` | `evb_<period>_<seq>` | Deterministic | `evb_2026_02_001` | Period-scoped evidence packs |
| `risk_alerts` | `ra_<uuid>` | UUID v7 | `ra_019503a6-b8ca-7d00-ef7f-abcdef789012` | Dynamic risk events |
| `risk_scores` | `rs_<tenant>_<period>` | Deterministic | `rs_tenant01_2026_02` | One aggregate score per tenant per period |

**UUID v7 rationale:** Time-ordered UUIDs provide natural chronological ordering in Firestore, reducing index fragmentation compared to random UUIDs while maintaining uniqueness.

---

## 4. Collection Hierarchy Overview

```
firestore-root/
|
|-- employees/{emp_uuid}                          # Employee Context
|   |-- addresses/{addr_uuid}                     #   Subcollection: addresses
|   |-- emergency_contacts/{ec_uuid}              #   Subcollection: emergency contacts
|   |-- bank_accounts/{ba_uuid}                   #   Subcollection: bank accounts (encrypted)
|   |-- next_of_kin/{nok_uuid}                    #   Subcollection: next of kin / beneficiaries
|   |-- benefits/{ben_uuid}                       #   Subcollection: medical aid / pension / group life
|
|-- employment_contracts/{con_uuid}               # Employee Context
|
|-- termination_cases/{trm_uuid}                  # Employee Context
|
|-- timesheets/{ts_id}                            # Time & Attendance Context
|   |-- time_entries/{entry_uuid}                 #   Subcollection: daily entries
|
|-- clock_entries/{ce_uuid}                       # Time & Attendance Context (employee self-service)
|
|-- timesheet_flags/{flag_uuid}                   # Time & Attendance Context (manager flags)
|
|-- leave_balances/{lb_id}                        # Leave Context
|   |-- accrual_ledger/{ledger_uuid}              #   Subcollection: append-only ledger
|
|-- leave_requests/{lr_uuid}                      # Leave Context
|
|-- payroll_runs/{pr_id}                          # Payroll Context
|   |-- payroll_results/{emp_uuid}                #   Subcollection: per-employee results
|
|-- payroll_adjustments/{adj_uuid}                # Payroll Context
|
|-- termination_settlements/{settlement_uuid}     # Payroll Context
|
|-- statutory_rule_sets/{rules_id}                # Compliance Context
|
|-- submission_packages/{sub_id}                  # Compliance Context
|
|-- compliance_results/{cr_id}                    # Compliance Context
|
|-- audit_events/{aud_uuid}                       # Audit & Evidence Context
|
|-- evidence_bundles/{evb_id}                     # Audit & Evidence Context
|   |-- document_refs/{ref_uuid}                  #   Subcollection: referenced artifacts
|
|-- risk_alerts/{ra_uuid}                         # Risk & Insights Context
|
|-- risk_scores/{rs_id}                           # Risk & Insights Context
```

---

## 5. Employee Context

### 5.1 `employees` (Root Collection)

Aggregate root for employee profile data.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. Immutable after creation. |
| `employee_id` | string | Yes | Canonical ID (`emp_<uuid>`). Matches document ID. Immutable. |
| `firebase_uid` | string | Yes | Firebase Authentication UID. 1:1 mapping (one employee per Firebase account). Required for self-access enforcement (`request.auth.uid == resource.data.firebase_uid`). Immutable after creation. |
| `legal_name` | string | Yes | Full legal name. Validated character set. |
| `national_id_or_passport` | string | Yes | SA ID number or passport number. Encrypted at rest. Data classification: `restricted`. |
| `tax_reference` | string | Yes | SARS tax reference number. Must pass format validation before payroll finalization. Data classification: `restricted`. |
| `bank_account_ref` | string | Yes | Reference to encrypted bank account record. Masked in all read outputs. Data classification: `restricted`. |
| `employment_status` | string | Yes | Enum: `active`, `suspended`, `terminated`. |
| `date_of_birth` | timestamp | Yes | Required for tax rebate and ETI eligibility calculations. |
| `hire_date` | timestamp | Yes | Employment start date. Used for leave accrual and ETI eligibility. |
| `data_classification` | string | Yes | Enum: `public`, `internal`, `confidential`, `restricted`. |
| `created_at` | timestamp | Yes | Server timestamp at creation. |
| `updated_at` | timestamp | Yes | Server timestamp at last update. |
| `created_by` | string | Yes | Actor ID of creator. |
| `updated_by` | string | Yes | Actor ID of last updater. |
| `schema_version` | string | Yes | Document schema version (e.g. `"1.0"`). |
| `department_id` | string | Yes | **Primary employment department** — used for payroll cost-centre allocation and leave approval routing. For multi-department managers, additional department access scope comes from `user_role_assignments`, not this field. FK to `departments`. **Director/HRManager**: assigned to the auto-created `Executive` system department at tenant onboarding. |
| `role_id` | string | Yes | FK to `roles` collection or system role constant. Reflects the primary/highest-privilege role. |
| `system_role` | string | Yes | System role enum: `Director`, `HRManager`, `Manager`, `Employee`. Never `SaasAdmin` (platform-only). Reflects the highest-privilege active assignment. |
| `employee_type` | string | Yes | Enum: `Permanent`, `PartTime`, `Contractor`, `Intern`. |
| `reports_to_employee_id` | string | No | `employee_id` of direct manager for primary department. Used for leave approval routing. Null for Director/HRManager (they self-approve leave — see PRD-15 Section 1.5). If not null, must reference an employee whose `system_role` is `Manager`, `HRManager`, or `Director`. |
| `personal_phone_number` | string | Yes | Personal mobile/landline number. Encrypted at rest. Data classification: `confidential`. |
| `personal_email` | string | Yes | Personal email address. Used for payslip delivery. Data classification: `internal`. |
| `work_email` | string | No | Work email address. Only populated if different from Firebase Auth `email`. Data classification: `internal`. |
| `marital_status` | string | No | Enum: `Single`, `Married`, `Divorced`, `Widowed`, `Life_Partner`. Data classification: `confidential`. |
| `nationality` | string | Yes | ISO 3166-1 alpha-2 country code (e.g. `ZA`, `ZW`). Data classification: `internal`. |
| `passport_expiry_date` | timestamp | Conditional | Required when `national_id_or_passport` contains a passport number (non-SA-ID). Data classification: `confidential`. |
| `gender` | string | Yes | Enum: `Male`, `Female`, `NonBinary`, `PreferNotToSay`. Required for Employment Equity Act (EEA S.20) reporting. Data classification: `confidential`. |
| `race` | string | Yes | Enum: `African`, `Coloured`, `Indian`, `White`, `PreferNotToSay`. Required for EEA S.20 reporting. Data classification: `confidential`. |
| `disability_status` | boolean | Yes | Whether the employee has a declared disability. EEA S.20. Affects UIF disability benefit eligibility. Data classification: `confidential`. |
| `disability_description` | string | Conditional | Human-readable disability description. Only populated when `disability_status == true`. Data classification: `restricted`. |
| `employment_equity_category` | string | No | Derived EEA category string (e.g. `African_Female`, `White_Male`). Stored for EE reporting. Data classification: `confidential`. |
| `seta_registration_number` | string | No | Skills Development Levy (SDL) — employer SETA registration number for employee. Data classification: `internal`. |
| `data_status` | string | Yes | Enum: `active`, `archived`, `legal_hold`. Defaults to `active`. Records transition to `archived` via scheduled background job after retention window. `legal_hold` prevents archival regardless of retention schedule. |
| `archived_at` | timestamp | No | Timestamp when `data_status` transitioned to `archived`. Null while `active` or `legal_hold`. |
| `archive_reason` | string | No | Reason for archival. Enum: `retention_expiry`, `employment_ended`, `legal_hold`. |

**Invariants:**
- `employee_id` is immutable after creation.
- `firebase_uid` is immutable after creation (1:1 with Firebase Auth account).
- `tax_reference` must pass SARS format validation before any payroll finalization that includes this employee.
- Deletion is forbidden; termination sets `employment_status` to `terminated`.
- `department_id` must reference an active department in the same tenant. For Director/HRManager, must reference the tenant's `Executive` system department.
- Every Director, HRManager, Manager, and Employee must have an `employees` document. `SaasAdmin` does not (platform operator).

### 5.2 `employees/{emp_id}/addresses` (Subcollection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `address_id` | string | Yes | UUID v7. Matches document ID. |
| `address_type` | string | Yes | Enum: `residential`, `postal`. |
| `line_1` | string | Yes | Street address line 1. |
| `line_2` | string | No | Street address line 2. |
| `city` | string | Yes | City or town. |
| `province` | string | Yes | South African province. |
| `postal_code` | string | Yes | Postal code. |
| `country` | string | Yes | ISO 3166-1 alpha-2 code. Default: `ZA`. |
| `effective_from` | timestamp | Yes | Date this address became active. |
| `effective_to` | timestamp | No | Date this address was superseded. Null if current. |
| `created_at` | timestamp | Yes | Server timestamp. |

### 5.3 `employees/{emp_id}/emergency_contacts` (Subcollection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `contact_id` | string | Yes | UUID v7. Matches document ID. |
| `contact_name` | string | Yes | Full name of emergency contact. |
| `relationship` | string | Yes | Relationship to employee. |
| `phone_number` | string | Yes | Contact phone number. |
| `is_primary` | boolean | Yes | Whether this is the primary emergency contact. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |

### 5.4 `employment_contracts` (Root Collection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `contract_id` | string | Yes | UUID v7. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees` collection. |
| `start_date` | timestamp | Yes | Contract effective start date. Must be <= `end_date`. |
| `end_date` | timestamp | No | Contract end date. Null for indefinite contracts. Must be >= `start_date`. |
| `occupational_level` | string | Yes | Occupational level classification (e.g. `Junior`, `Senior`, `Management`, `Executive`). |
| `salary_basis` | string | Yes | Enum: `monthly`, `weekly`, `hourly`. Determines payroll period handling. |
| `base_salary_zar` | string | Yes | Monthly/weekly/hourly base salary. Stored as string for decimal precision. |
| `ordinary_hours_per_week` | number | Yes | Contracted ordinary hours. Used for BCEA validation. |
| `ordinary_hours_policy_version` | string | Yes | Reference to active hours policy version at contract creation. |
| `is_active` | boolean | Yes | Whether this is the current active contract. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `created_by` | string | Yes | Actor ID. |
| `schema_version` | string | Yes | Document schema version. |

**Invariants:**
- Contract effective periods must not overlap for the same employee (`employee_id` + date range uniqueness enforced at application layer).
- Only one contract may have `is_active = true` per employee at any time.

### 5.5 `termination_cases` (Root Collection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `termination_case_id` | string | Yes | UUID v7. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees`. |
| `termination_reason_code` | string | Yes | Reason code (e.g. `resignation`, `retrenchment`, `dismissal`, `retirement`). |
| `notice_period_days` | number | Yes | Calculated notice period in days. Derived from policy and legal checks. |
| `severance_amount_zar` | string | No | Severance pay amount. String for decimal precision. Null if not applicable. |
| `effective_termination_date` | timestamp | Yes | Date termination takes effect. |
| `policy_version` | string | Yes | Policy version used for calculation. |
| `legal_check_status` | string | Yes | Enum: `pending`, `passed`, `failed`. Must be `passed` before settlement finalization. |
| `status` | string | Yes | Enum: `draft`, `calculated`, `approved`, `finalized`. |
| `approved_by` | string | No | Actor ID of approver. Required when status is `approved` or `finalized`. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `created_by` | string | Yes | Actor ID. |
| `schema_version` | string | Yes | Document schema version. |

### 5.6 `employees/{emp_id}/bank_accounts` (Subcollection)

Encrypted bank account records. Replaces the opaque `bank_account_ref` pointer on the root document — that field now holds the `bank_account_id` of the primary active account.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `bank_account_id` | string | Yes | UUID v7. Matches document ID. |
| `account_holder_name` | string | Yes | Full legal name of account holder. Must match employee `legal_name`. Encrypted at rest. Data classification: `restricted`. |
| `bank_name` | string | Yes | Name of the financial institution (e.g. `FNB`, `Absa`, `Standard Bank`). Encrypted at rest. Data classification: `restricted`. |
| `account_number` | string | Yes | Bank account number. Encrypted at rest. Masked to last 4 digits in all UI outputs (`****1234`). Data classification: `restricted`. |
| `branch_code` | string | Yes | 6-digit SA universal branch code. |
| `account_type` | string | Yes | Enum: `cheque`, `savings`, `transmission`. |
| `is_primary` | boolean | Yes | Whether this is the primary account for payroll disbursement. Only one account may be `is_primary = true` per employee at any time. |
| `effective_from` | timestamp | Yes | Date this account became active. |
| `effective_to` | timestamp | No | Date this account was superseded. Null if current. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `created_by` | string | Yes | Actor ID. |
| `schema_version` | string | Yes | Document schema version. |

**Invariants:**
- Only one `bank_account` may have `is_primary = true` per employee at any time.
- Account numbers are never stored in plaintext; all read outputs mask to last 4 digits.

### 5.7 `employees/{emp_id}/next_of_kin` (Subcollection)

Legally distinct from emergency contacts. Used for pension/provident fund death benefit designation and legal next-of-kin notifications.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `nok_id` | string | Yes | UUID v7. Matches document ID. |
| `full_name` | string | Yes | Full legal name of next of kin. |
| `relationship` | string | Yes | Enum: `Spouse`, `Child`, `Parent`, `Sibling`, `Other`. |
| `id_or_passport` | string | No | SA ID or passport number of next of kin. Encrypted at rest. Data classification: `restricted`. |
| `phone_number` | string | Yes | Contact phone number. Encrypted at rest. Data classification: `confidential`. |
| `email` | string | No | Email address. Data classification: `confidential`. |
| `is_primary_beneficiary` | boolean | Yes | Whether this person is the primary beneficiary for pension/death benefits. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |

### 5.8 `employees/{emp_id}/benefits` (Subcollection)

Medical aid, pension fund, provident fund, and group life insurance memberships. Contribution rates here drive the deduction line items in `payroll_results`.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `benefit_id` | string | Yes | UUID v7. Matches document ID. |
| `benefit_type` | string | Yes | Enum: `medical_aid`, `pension_fund`, `provident_fund`, `group_life`. |
| `provider_name` | string | Yes | Name of provider (e.g. `Discovery Health`, `Old Mutual Superfund`). |
| `membership_number` | string | Yes | Member/policy number. Encrypted at rest. Masked in UI. Data classification: `restricted`. |
| `plan_name` | string | Yes | Specific plan or option name (e.g. `Executive Plan`, `Retirement Annuity`). |
| `employee_contribution_rate` | string | Yes | Employee contribution as decimal percentage string (e.g. `"0.07500"` = 7.5%). MoneyZAR precision rules apply. |
| `employer_contribution_rate` | string | Yes | Employer contribution as decimal percentage string. MoneyZAR precision rules apply. |
| `effective_from` | timestamp | Yes | Date this benefit membership became active. |
| `effective_to` | timestamp | No | Date this membership was superseded. Null if current. |
| `is_active` | boolean | Yes | Whether this is a current active benefit. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |

---

## 6. Time and Attendance Context

### 6.1 `timesheets` (Root Collection)

Aggregate root for weekly time records.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `timesheet_id` | string | Yes | Deterministic: `ts_<emp_short>_<YYYY>_W<WW>`. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees`. |
| `week_start` | timestamp | Yes | Monday of the ISO week. Must align to configured week start. |
| `week_end` | timestamp | Yes | Sunday of the ISO week. |
| `total_ordinary_hours` | number | Yes | Aggregated ordinary hours. Cannot exceed legal policy without approved exception. |
| `total_overtime_hours` | number | Yes | Aggregated overtime hours. Requires eligibility and approval metadata. |
| `status` | string | Yes | Enum: `draft`, `submitted`, `approved`, `rejected`. |
| `approved_by` | string | No | Actor ID of approver. Required when status is `approved`. |
| `approved_at` | timestamp | No | Timestamp of approval. |
| `rejection_reason` | string | No | Reason for rejection, if applicable. |
| `overtime_agreement_ref` | string | No | Reference to overtime agreement document. Required if overtime > 0. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

**Invariants:**
- Approved timesheets are immutable. Corrections require adjustment entries (new timesheet with correction type).
- Ordinary hours cannot exceed BCEA limits (`CTL-BCEA-001`) without an approved exception.
- Overtime entries require explicit agreement metadata (`CTL-BCEA-002`).

### 6.2 `timesheets/{ts_id}/time_entries` (Subcollection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `entry_id` | string | Yes | UUID v7. Matches document ID. |
| `date` | timestamp | Yes | Date of the time entry. Must fall within parent timesheet week range. |
| `hours` | number | Yes | Hours worked. Must be >= 0. |
| `category` | string | Yes | Enum: `ordinary`, `overtime`, `public_holiday`, `sunday`. |
| `source` | string | Yes | Enum: `manual`, `biometric`, `import`. |
| `approval_state` | string | Yes | Enum: `pending`, `approved`, `rejected`. |
| `notes` | string | No | Optional notes or comments. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |

### 6.3 `clock_entries` (Root Collection)

Employee self-service clock-in/clock-out records. Source of truth for raw attendance data before aggregation into weekly `timesheets`. Created when an employee presses the Clock In or Clock Out button.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `entry_id` | string | Yes | UUID v7. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees`. |
| `clock_in_at` | timestamp | Yes | Server timestamp at the moment the employee pressed Clock In. Immutable after creation. |
| `clock_out_at` | timestamp | No | Server timestamp at the moment the employee pressed Clock Out. Null until clocked out. |
| `calculated_hours` | number | No | Derived decimal hours: `(clock_out_at - clock_in_at)` in hours. Null until `clock_out_at` is set. |
| `date` | timestamp | Yes | Calendar date of the clock-in (date-only, used for day-scoped queries). |
| `source` | string | Yes | Enum: `employee_self`, `manager_entry`, `system_correction`. |
| `status` | string | Yes | Enum: `open` (clocked in, not yet out), `completed` (clocked out), `flagged` (manager-flagged concern). |
| `flag_note` | string | No | Manager note when status is `flagged`. |
| `linked_time_entry_id` | string | No | FK to `timesheets/{ts_id}/time_entries` once aggregated into the weekly timesheet. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

**Invariants:**
- `clock_in_at` is immutable after creation. Corrections create new entries with `source: system_correction`.
- An employee may have at most one `status = open` entry per day. A second Clock In is rejected if an open entry exists.
- `calculated_hours` is derived; never accepted from client input.

### 6.4 `timesheet_flags` (Root Collection)

Manager-created flags indicating suspected absence, missing clock-out, or suspicious hours. Drives the manager verification workflow in the Clock-In screen.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `flag_id` | string | Yes | UUID v7. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees` (flagged employee). |
| `flagged_by` | string | Yes | Actor ID of the Manager who created the flag. |
| `flag_date` | timestamp | Yes | The calendar date the suspected absence or issue occurred. |
| `reason` | string | Yes | Enum: `suspected_absence`, `suspicious_hours`, `missing_clock_out`, `other`. |
| `notes` | string | No | Optional manager notes. |
| `status` | string | Yes | Enum: `open`, `resolved`, `dismissed`. |
| `resolved_by` | string | No | Actor ID who resolved the flag. |
| `resolved_at` | timestamp | No | Timestamp of resolution. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |

---

## 7. Leave Context

### 7.1 `leave_balances` (Root Collection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `balance_id` | string | Yes | Deterministic: `lb_<emp_short>_<type>_<cycle>`. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees`. |
| `leave_type` | string | Yes | Enum from policy catalog (e.g. `annual`, `sick`, `family_responsibility`, `maternity`, `parental`). |
| `cycle_id` | string | Yes | Leave cycle identifier (e.g. `2026` for annual cycle). |
| `accrued_hours` | number | Yes | Total accrued hours. Non-negative. |
| `consumed_hours` | number | Yes | Total consumed hours. Non-negative. |
| `adjustment_hours` | number | Yes | Net adjustment hours (can be negative for clawbacks). |
| `available_hours` | number | Yes | Derived: `accrued_hours - consumed_hours + adjustment_hours`. |
| `policy_version` | string | Yes | Leave policy version active for this cycle. |
| `last_accrual_date` | timestamp | Yes | Date of most recent accrual posting. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

**Invariants:**
- Balance cannot become negative unless an approved policy exception exists with recorded reason.
- Accrual follows policy version active for accrual date.
- Approved leave reduces `available_hours` atomically.

### 7.2 `leave_balances/{lb_id}/accrual_ledger` (Subcollection)

Append-only ledger. Each entry records a single accrual, consumption, or adjustment event.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `ledger_entry_id` | string | Yes | UUID v7. Matches document ID. |
| `entry_type` | string | Yes | Enum: `accrual`, `consumption`, `adjustment`, `forfeiture`, `carryover`. |
| `hours` | number | Yes | Hours affected. Positive for accrual/carryover, negative for consumption/forfeiture. |
| `effective_date` | timestamp | Yes | Date this entry takes effect. |
| `reason_code` | string | Yes | Reason for the entry (e.g. `monthly_accrual`, `leave_taken`, `policy_exception`). |
| `leave_request_id` | string | No | FK to `leave_requests` if entry is a consumption. |
| `policy_version` | string | Yes | Policy version active at time of posting. |
| `posted_by` | string | Yes | Actor ID or `system` for automated accruals. |
| `created_at` | timestamp | Yes | Server timestamp. Immutable. |

**Invariants:**
- Append-only. No updates or deletes permitted.
- Sum of all ledger entries must reconcile to parent balance fields.

### 7.3 `leave_requests` (Root Collection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `leave_request_id` | string | Yes | UUID v7. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees`. |
| `leave_type` | string | Yes | Must match value in leave policy catalog. |
| `start_date` | timestamp | Yes | First day of requested leave. |
| `end_date` | timestamp | Yes | Last day of requested leave. |
| `total_hours` | number | Yes | Total leave hours requested. |
| `reason_code` | string | Yes | Reason for leave request. |
| `status` | string | Yes | Enum: `submitted`, `manager_review`, `approved`, `rejected`, `cancelled`. |
| `approver_id` | string | No | Actor ID of the approving manager. Required when status is `approved`. |
| `approved_at` | timestamp | No | Timestamp of approval. |
| `rejection_reason` | string | No | Reason text when rejected. |
| `balance_snapshot_at_request` | number | No | Available balance at time of submission. Informational. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `created_by` | string | Yes | Actor ID (typically the employee). |
| `schema_version` | string | Yes | Document schema version. |

**State machine:** `Submitted -> ManagerReview -> Approved | Rejected | Cancelled`

---

## 8. Payroll Context

### 8.1 `payroll_runs` (Root Collection)

Aggregate root for payroll run lifecycle.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `payroll_run_id` | string | Yes | Deterministic. Monthly: `pr_<YYYY>_<MM>_<seq>`. Weekly: `pr_<YYYY>_W<WW>_<seq>`. Matches document ID. |
| `period` | string | Yes | Period identifier. Monthly: `YYYY-MM`. Weekly: `YYYY-W<WW>`. |
| `run_type` | string | Yes | Enum: `monthly`, `weekly`. |
| `status` | string | Yes | Enum: `Draft`, `Calculated`, `Finalized`, `Filed`. |
| `employee_count` | number | Yes | Number of employees included in this run. |
| `rule_set_version` | string | Yes | Statutory rule set version used. Must be signed and active. |
| `tax_table_version` | string | Yes | SARS tax table version used. Must be signed and active. |
| `gross_total_zar` | string | Yes | Aggregate gross pay. String for decimal precision. |
| `deduction_total_zar` | string | Yes | Aggregate deductions. String for decimal precision. |
| `net_total_zar` | string | Yes | Aggregate net pay. String for decimal precision. |
| `paye_total_zar` | string | Yes | Aggregate PAYE. String for decimal precision. |
| `uif_total_zar` | string | Yes | Aggregate UIF (employee + employer). String for decimal precision. |
| `sdl_total_zar` | string | Yes | Aggregate SDL. String for decimal precision. |
| `checksum` | string | Yes | SHA-256 hash of sorted payroll result payloads + rule version. Integrity proof. |
| `initiated_by` | string | Yes | Actor ID who created the run (Director or HRManager). |
| `calculated_at` | timestamp | No | Timestamp when system completed calculation. Set automatically. |
| `compliance_flags` | array | No | Array of compliance flag strings set during calculation (e.g. `["CTL-SARS-001:PASS", "CTL-BCEA-001:PASS"]`). Warnings do not block finalization; only critical flags block. |
| `finalized_by` | string | No | Actor ID of the Director or HRManager who clicked Finalize & Lock. Required for `Finalized` and `Filed`. |
| `finalized_at` | timestamp | No | Timestamp of finalization. |
| `idempotency_key` | string | Yes | Idempotency token for creation. |
| `data_status` | string | Yes | Enum: `active`, `archived`, `legal_hold`. Defaults to `active`. Transitions to `archived` after 5-year retention window. |
| `archived_at` | timestamp | No | Timestamp when `data_status` transitioned to `archived`. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

**State machine:** `Draft → Calculated → Finalized → Filed`

- `Draft`: Run created by Director or HRManager. Active employee contracts and approved timesheets are loaded.
- `Calculated`: System automatically calculates PAYE, UIF, SDL, ETI for every employee using the active `StatutoryRuleSet`. Compliance flags are written. Transition is triggered automatically after run creation (no user action).
- `Finalized`: Director or HRManager reviews the calculated totals and clicks **Finalize & Lock**. Single-actor action — no dual control required (GAP-FEAT-005). Record becomes immutable at this point.
- `Filed`: HR downloads the EMP201 CSV and marks the run as filed. Status set programmatically when the export is generated.

**Invariants:**
- `Calculated → Finalized` requires role `Director` or `HRManager`. Any critical compliance flag blocks this transition with an explanatory error. Warning-level flags are shown but do not block.
- Finalized payroll runs are immutable. No field may be updated after `status = Finalized`, except the single allowed transition to `Filed`.
- `Finalized → Filed` is the only permitted write on an otherwise immutable document (enforced by Firestore security rules: `allow update: if resource.data.status == 'Finalized' && request.resource.data.status == 'Filed' && onlyFieldChanged('status')`).
- `Finalized` and `Filed` cannot transition backward under any circumstances.

### 8.2 `payroll_runs/{pr_id}/payroll_results` (Subcollection)

Per-employee payroll calculation results. Document ID is the `employee_id` to enforce one result per employee per run.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `employee_id` | string | Yes | FK reference to `employees`. Matches document ID. |
| `gross_pay_zar` | string | Yes | Gross pay amount. String for decimal precision. |
| `basic_salary_zar` | string | Yes | Basic salary component. |
| `overtime_pay_zar` | string | Yes | Overtime pay component. |
| `allowances_zar` | string | Yes | Total allowances. |
| `paye_zar` | string | Yes | PAYE tax deduction. |
| `uif_employee_zar` | string | Yes | UIF employee contribution. |
| `uif_employer_zar` | string | Yes | UIF employer contribution. |
| `sdl_zar` | string | Yes | SDL contribution. |
| `pension_employee_zar` | string | No | Employee pension contribution. |
| `pension_employer_zar` | string | No | Employer pension contribution. |
| `medical_employee_zar` | string | No | Employee medical aid contribution. |
| `medical_employer_zar` | string | No | Employer medical aid contribution. |
| `eti_amount_zar` | string | No | ETI incentive amount if eligible. |
| `eti_eligible` | boolean | Yes | Whether employee qualifies for ETI. |
| `other_deductions` | array | No | Array of maps: `[{code: string, description: string, amount_zar: string}]`. |
| `other_additions` | array | No | Array of maps: `[{code: string, description: string, amount_zar: string}]`. |
| `deduction_total_zar` | string | Yes | Sum of all deductions. |
| `addition_total_zar` | string | Yes | Sum of all additions. |
| `net_pay_zar` | string | Yes | Net pay: `gross - deductions + additions`. |
| `tax_table_version` | string | Yes | Tax table version used for this employee. |
| `hours_ordinary` | number | Yes | Ordinary hours from approved timesheet. |
| `hours_overtime` | number | Yes | Overtime hours from approved timesheet. |
| `compliance_flags` | array | No | Per-employee compliance flag strings. |
| `calculation_timestamp` | timestamp | Yes | When this result was calculated. |
| `schema_version` | string | Yes | Document schema version. |

**Invariant:** `net_pay_zar == gross_pay_zar - deduction_total_zar + addition_total_zar` (verified programmatically; mismatch blocks finalization).

### 8.3 `payroll_adjustments` (Root Collection)

Post-finalization corrections. Append-only compensating records.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `adjustment_id` | string | Yes | UUID v7. Matches document ID. |
| `payroll_run_id` | string | Yes | FK reference to the finalized `payroll_runs` document. |
| `employee_id` | string | Yes | FK reference to `employees`. |
| `adjustment_type` | string | Yes | Enum: `correction`, `reversal`, `supplementary`. |
| `reason` | string | Yes | Human-readable explanation. |
| `amount_zar` | string | Yes | Adjustment amount. String for decimal precision. Can be negative. |
| `affected_fields` | array | Yes | List of field names affected (e.g. `["paye_zar", "net_pay_zar"]`). |
| `created_by` | string | Yes | Actor ID. |
| `approved_by` | string | No | Actor ID of approver. |
| `created_at` | timestamp | Yes | Server timestamp. Immutable. |
| `schema_version` | string | Yes | Document schema version. |

**Invariants:**
- Append-only. Adjustments are never updated or deleted.
- Must reference a finalized payroll run.

### 8.4 `termination_settlements` (Root Collection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `settlement_id` | string | Yes | UUID v7. Matches document ID. |
| `employee_id` | string | Yes | FK reference to `employees`. |
| `termination_case_id` | string | Yes | FK reference to `termination_cases`. |
| `notice_pay_zar` | string | Yes | Notice period pay. String for decimal precision. |
| `severance_pay_zar` | string | Yes | Severance pay amount. String for decimal precision. `"0.00"` if not applicable. |
| `leave_payout_zar` | string | Yes | Accrued leave payout. String for decimal precision. |
| `total_settlement_zar` | string | Yes | Total settlement amount. |
| `policy_version` | string | Yes | Policy version used. |
| `rule_set_version` | string | Yes | Statutory rule set version used. |
| `legal_check_status` | string | Yes | Enum: `pending`, `passed`, `failed`. Must be `passed` for finalization. |
| `compliance_control_results` | array | Yes | Array of control result maps (e.g. `[{control_id: "CTL-BCEA-006", status: "PASS"}]`). |
| `status` | string | Yes | Enum: `draft`, `calculated`, `approved`, `finalized`. |
| `approved_by` | string | No | Actor ID. Required for `approved` and `finalized`. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `created_by` | string | Yes | Actor ID. |
| `schema_version` | string | Yes | Document schema version. |

---

## 9. Compliance Context

### 9.1 `statutory_rule_sets` (Root Collection)

Versioned, signed configuration artifacts for statutory logic.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. (Shared rule sets use a system tenant.) |
| `rule_set_id` | string | Yes | Deterministic: `rules_<domain>_<version>`. Matches document ID. |
| `jurisdiction` | string | Yes | Jurisdiction code (e.g. `ZA`). |
| `domain` | string | Yes | Rule domain (e.g. `sars`, `bcea`, `popia`). |
| `version` | string | Yes | Semantic version of the rule set. |
| `effective_from` | timestamp | Yes | Date this rule set becomes active. |
| `effective_to` | timestamp | No | Date this rule set expires. Null if currently active. |
| `is_active` | boolean | Yes | Whether this is the active rule set for its domain. |
| `signature` | string | Yes | Digital signature over rule content. |
| `signature_hash` | string | Yes | SHA-256 checksum of rule set content. |
| `source_url` | string | No | Reference URL for the source of the rule data. |
| `source_published_date` | timestamp | No | Date the source was published. |
| `rule_data` | map | Yes | Nested map containing the actual rule parameters (tax brackets, thresholds, rates, etc.). |
| `activated_by` | string | No | Actor ID who activated this version. |
| `activated_at` | timestamp | No | Activation timestamp. |
| `regression_pack_status` | string | Yes | Enum: `pending`, `passed`, `failed`. Must be `passed` before activation. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

**Invariants:**
- Only one active rule set per domain and date window.
- Activation requires: signed artifact, passed regression pack, ComplianceOfficer approval.

### 9.2 `submission_packages` (Root Collection)

Filing packages for SARS submissions.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `submission_id` | string | Yes | Deterministic: `sub_<type>_<period>_<seq>`. Matches document ID. |
| `submission_type` | string | Yes | Enum: `EMP201`, `EMP501`, `IRP5`, `IT3A`. |
| `period` | string | Yes | Reporting period (e.g. `2026-02` for monthly, `2025-2026` for annual). |
| `payroll_run_id` | string | Yes | FK reference to the source `payroll_runs`. |
| `status` | string | Yes | Enum: `Prepared`, `Validated`, `Signed`, `Submitted`, `Acknowledged`, `Rejected`. |
| `validation_status` | string | Yes | Enum: `pending`, `passed`, `failed`. |
| `validation_errors` | array | No | Array of validation error maps: `[{control_id: string, message: string, severity: string}]`. |
| `artifact_refs` | array | Yes | Array of artifact reference strings (storage paths or URLs). |
| `due_date` | timestamp | Yes | Statutory filing due date. |
| `submitted_at` | timestamp | No | Actual submission timestamp. |
| `acknowledged_at` | timestamp | No | SARS acknowledgment timestamp. |
| `sars_reference` | string | No | SARS-issued reference number after submission. |
| `signed_by` | string | No | Actor ID of ComplianceOfficer who signed. |
| `signed_at` | timestamp | No | Signing timestamp. |
| `rejection_reason` | string | No | Reason if status is `Rejected`. |
| `successor_package_id` | string | No | FK to corrected successor if this was rejected. Original remains immutable. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `created_by` | string | Yes | Actor ID. |
| `schema_version` | string | Yes | Document schema version. |

**State machine:** `Prepared -> Validated -> Signed -> Submitted -> Acknowledged | Rejected`

**Invariants:**
- Validation failure blocks progression to `Signed`.
- Rejected packages are immutable; corrections require a new successor package with `successor_package_id` link.

### 9.3 `compliance_results` (Root Collection)

Individual compliance control execution results.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `result_id` | string | Yes | Deterministic: `cr_<control_id>_<period>_<seq>`. Matches document ID. |
| `control_id` | string | Yes | Control identifier (e.g. `CTL-SARS-001`, `CTL-BCEA-003`). |
| `period` | string | Yes | Period this result applies to. |
| `status` | string | Yes | Enum: `pass`, `fail`, `warning`, `not_applicable`. |
| `evidence_ref` | string | Yes | FK reference to `evidence_bundles` document. Required for all pass/fail results. |
| `entity_ref` | string | Yes | Reference to the entity checked (e.g. `payroll_run:pr_2026_02_001`). |
| `rule_set_version` | string | Yes | Rule set version used for evaluation. |
| `details` | map | No | Additional structured details about the check result. |
| `executed_at` | timestamp | Yes | When the control was executed. |
| `executed_by` | string | Yes | Actor or `system` for automated checks. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

---

## 10. Audit and Evidence Context

### 10.1 `audit_events` (Root Collection)

Immutable, hash-linked audit trail.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `event_id` | string | Yes | UUID v7 (time-ordered). Matches document ID. |
| `actor_id` | string | Yes | Actor who performed the action. `system` for automated operations. |
| `action` | string | Yes | Action descriptor (e.g. `PayrollFinalized`, `EmployeeCreated`, `LeaveAccrued`). |
| `entity_ref` | string | Yes | Target entity reference (e.g. `payroll_run:pr_2026_02_001`, `employee:emp_019503a1`). |
| `entity_type` | string | Yes | Entity type (e.g. `payroll_run`, `employee`, `leave_request`). |
| `before_hash` | string | No | SHA-256 hash of entity state before the action. Required for updates. |
| `after_hash` | string | Yes | SHA-256 hash of entity state after the action. |
| `previous_event_hash` | string | No | SHA-256 hash of the previous audit event for this tenant. Creates the hash chain. Null for the first event. |
| `event_hash` | string | Yes | SHA-256 hash of this event's content (including `previous_event_hash`). |
| `timestamp_utc` | timestamp | Yes | UTC timestamp of the event. |
| `trace_id` | string | Yes | Distributed trace ID. Propagated across services. |
| `correlation_id` | string | Yes | Correlation ID linking related events across a workflow. |
| `schema_version` | string | Yes | Event schema version (e.g. `"1.0"`). |
| `metadata` | map | No | Additional context (e.g. IP address, user agent, MFA status). |
| `data_status` | string | Yes | Enum: `active`, `archived`, `legal_hold`. Defaults to `active`. Archived records remain in collection; excluded from standard UI queries. |
| `archived_at` | timestamp | No | Timestamp when `data_status` transitioned to `archived`. |

**Invariants:**
- Append-only. No updates, no deletes.
- Hash-linked: each event includes the hash of the previous event, creating a tamper-evident chain.
- Deletion is forbidden. Retention expiry transitions `data_status` to `archived` after 5-year window.
- Any break in the hash chain raises `SEV-1` and freezes affected workflows.

### 10.2 `evidence_bundles` (Root Collection)

Signed artifact bundles proving control operation and outputs.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `bundle_id` | string | Yes | Deterministic: `evb_<period>_<seq>`. Matches document ID. |
| `period` | string | Yes | Period this evidence covers. |
| `control_scope` | array | Yes | Array of control IDs covered (e.g. `["CTL-SARS-001", "CTL-BCEA-003"]`). |
| `rule_set_version_manifest` | map | Yes | Map of rule set versions and checksums used. |
| `payroll_run_ref` | string | No | FK to `payroll_runs` if payroll-related. |
| `submission_ref` | string | No | FK to `submission_packages` if filing-related. |
| `validation_report_ref` | string | Yes | Storage reference to the validation report artifact. |
| `approval_records` | array | Yes | Array of approval maps: `[{role: string, actor_id: string, approved_at: timestamp}]`. |
| `signature` | string | Yes | Digital signature over bundle content. |
| `signature_hash` | string | Yes | SHA-256 hash of bundle content. |
| `incident_log_refs` | array | No | Array of incident or exception log references. |
| `status` | string | Yes | Enum: `assembled`, `signed`, `archived`. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `created_by` | string | Yes | Actor ID. |
| `schema_version` | string | Yes | Document schema version. |

### 10.3 `evidence_bundles/{evb_id}/document_refs` (Subcollection)

Individual artifacts referenced by the evidence bundle.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `ref_id` | string | Yes | UUID v7. Matches document ID. |
| `document_type` | string | Yes | Enum: `json`, `pdf`, `hash_manifest`. |
| `storage_path` | string | Yes | Cloud storage path to the artifact. |
| `content_hash` | string | Yes | SHA-256 hash of the artifact content. |
| `description` | string | Yes | Human-readable description. |
| `created_at` | timestamp | Yes | Server timestamp. |

---

## 11. Risk and Insights Context

### 11.1 `risk_alerts` (Root Collection)

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `alert_id` | string | Yes | UUID v7. Matches document ID. |
| `alert_type` | string | Yes | Enum: `deadline_approaching`, `compliance_gap`, `fine_exposure`, `data_quality`. |
| `control_id` | string | No | Related compliance control ID. |
| `severity` | string | Yes | Enum: `Sev-1`, `Sev-2`, `Sev-3`, `Sev-4`. |
| `title` | string | Yes | Short title for the alert. |
| `description` | string | Yes | Detailed description. |
| `legal_due_date` | timestamp | No | Statutory deadline if applicable. |
| `projected_exposure_min_zar` | string | No | Lower bound of fine exposure estimate. String for decimal precision. |
| `projected_exposure_max_zar` | string | No | Upper bound of fine exposure estimate. String for decimal precision. |
| `recommended_action` | string | Yes | Actionable recommendation. |
| `recommended_owner_role` | string | Yes | Role that should own the action (e.g. `PayrollOfficer`). |
| `status` | string | Yes | Enum: `open`, `acknowledged`, `mitigated`, `expired`. |
| `acknowledged_by` | string | No | Actor ID who acknowledged. |
| `mitigated_at` | timestamp | No | Timestamp when mitigated. |
| `entity_refs` | array | No | Array of related entity references. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `updated_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

### 11.2 `risk_scores` (Root Collection)

Aggregate risk score per tenant per period.

| Field | Firestore Type | Required | Description |
|---|---|---|---|
| `tenant_id` | string | Yes | Tenant isolation key. |
| `score_id` | string | Yes | Deterministic: `rs_<tenant_short>_<period>`. Matches document ID. |
| `period` | string | Yes | Period this score covers. |
| `overall_score` | number | Yes | Composite risk score (0-100, lower is better). |
| `sars_compliance_score` | number | Yes | SARS compliance component score. |
| `bcea_compliance_score` | number | Yes | BCEA compliance component score. |
| `popia_compliance_score` | number | Yes | POPIA compliance component score. |
| `data_quality_score` | number | Yes | Data quality component score. |
| `open_alert_count` | number | Yes | Number of currently open risk alerts. |
| `critical_alert_count` | number | Yes | Number of Sev-1 and Sev-2 alerts. |
| `total_exposure_zar` | string | Yes | Aggregate projected fine exposure. String for decimal precision. |
| `calculated_at` | timestamp | Yes | When this score was computed. |
| `created_at` | timestamp | Yes | Server timestamp. |
| `schema_version` | string | Yes | Document schema version. |

---

## 12. Composite Index Definitions

Firestore requires explicit composite indexes for queries that filter or order on multiple fields. All indexes below assume ascending order unless noted.

### 12.1 Employee Context

| Index Name | Collection | Fields | Purpose |
|---|---|---|---|
| `idx_emp_tenant_status` | `employees` | `tenant_id`, `employment_status` | List active/terminated employees for a tenant. |
| `idx_emp_tenant_name` | `employees` | `tenant_id`, `legal_name` | Alphabetical employee listing. |
| `idx_emp_tenant_hire` | `employees` | `tenant_id`, `hire_date` DESC | Recently hired employees. |
| `idx_con_tenant_emp` | `employment_contracts` | `tenant_id`, `employee_id`, `is_active` | Find active contract for an employee. |
| `idx_con_tenant_emp_dates` | `employment_contracts` | `tenant_id`, `employee_id`, `start_date`, `end_date` | Overlap validation query. |
| `idx_trm_tenant_status` | `termination_cases` | `tenant_id`, `status` | List pending termination cases. |

### 12.2 Time and Attendance Context

| Index Name | Collection | Fields | Purpose |
|---|---|---|---|
| `idx_ts_tenant_emp_week` | `timesheets` | `tenant_id`, `employee_id`, `week_start` DESC | Employee timesheet history. |
| `idx_ts_tenant_status` | `timesheets` | `tenant_id`, `status`, `week_start` DESC | List timesheets by approval status. |
| `idx_ts_tenant_week_status` | `timesheets` | `tenant_id`, `week_start`, `status` | All timesheets for a week, filtered by status. |
| `idx_ce_tenant_emp_date` | `clock_entries` | `tenant_id`, `employee_id`, `date` DESC | Employee clock history by date. |
| `idx_ce_tenant_emp_status` | `clock_entries` | `tenant_id`, `employee_id`, `status` | Find open (uncompleted) clock entry for employee. |
| `idx_ce_tenant_date_status` | `clock_entries` | `tenant_id`, `date`, `status` | All clock entries for a day (manager view). |
| `idx_flag_tenant_emp_status` | `timesheet_flags` | `tenant_id`, `employee_id`, `status` | Open flags for an employee. |
| `idx_flag_tenant_flagged_by` | `timesheet_flags` | `tenant_id`, `flagged_by`, `status` | Flags raised by a specific manager. |

### 12.3 Leave Context

| Index Name | Collection | Fields | Purpose |
|---|---|---|---|
| `idx_lb_tenant_emp_type` | `leave_balances` | `tenant_id`, `employee_id`, `leave_type` | Employee balance lookup by type. |
| `idx_lb_tenant_emp_cycle` | `leave_balances` | `tenant_id`, `employee_id`, `cycle_id` | All balances for an employee in a cycle. |
| `idx_lr_tenant_status` | `leave_requests` | `tenant_id`, `status`, `created_at` DESC | Pending leave requests queue. |
| `idx_lr_tenant_emp` | `leave_requests` | `tenant_id`, `employee_id`, `start_date` DESC | Employee leave request history. |

### 12.4 Payroll Context

| Index Name | Collection | Fields | Purpose |
|---|---|---|---|
| `idx_pr_tenant_period` | `payroll_runs` | `tenant_id`, `period`, `run_type` | Find runs for a period. |
| `idx_pr_tenant_status` | `payroll_runs` | `tenant_id`, `status`, `created_at` DESC | List runs by status (e.g. all Draft runs). |
| `idx_pr_tenant_type_period` | `payroll_runs` | `tenant_id`, `run_type`, `period` DESC | Monthly vs weekly run listing. |
| `idx_adj_tenant_run` | `payroll_adjustments` | `tenant_id`, `payroll_run_id`, `created_at` DESC | Adjustments for a specific run. |
| `idx_adj_tenant_emp` | `payroll_adjustments` | `tenant_id`, `employee_id`, `created_at` DESC | Adjustments affecting a specific employee. |
| `idx_settle_tenant_emp` | `termination_settlements` | `tenant_id`, `employee_id` | Settlements for an employee. |

### 12.5 Compliance Context

| Index Name | Collection | Fields | Purpose |
|---|---|---|---|
| `idx_rules_tenant_domain_active` | `statutory_rule_sets` | `tenant_id`, `domain`, `is_active` | Find active rule set for a domain. |
| `idx_rules_tenant_domain_effective` | `statutory_rule_sets` | `tenant_id`, `domain`, `effective_from` DESC | Rule set version history. |
| `idx_sub_tenant_type_period` | `submission_packages` | `tenant_id`, `submission_type`, `period` | Find submission for a type and period. |
| `idx_sub_tenant_status` | `submission_packages` | `tenant_id`, `status`, `due_date` | Upcoming submissions by status. |
| `idx_cr_tenant_control_period` | `compliance_results` | `tenant_id`, `control_id`, `period` | Results for a control across periods. |
| `idx_cr_tenant_period_status` | `compliance_results` | `tenant_id`, `period`, `status` | All results for a period filtered by pass/fail. |

### 12.6 Audit and Evidence Context

| Index Name | Collection | Fields | Purpose |
|---|---|---|---|
| `idx_aud_tenant_time` | `audit_events` | `tenant_id`, `timestamp_utc` DESC | Chronological audit trail. |
| `idx_aud_tenant_entity` | `audit_events` | `tenant_id`, `entity_type`, `entity_ref`, `timestamp_utc` DESC | Audit history for a specific entity. |
| `idx_aud_tenant_actor` | `audit_events` | `tenant_id`, `actor_id`, `timestamp_utc` DESC | Actions by a specific actor. |
| `idx_aud_tenant_action` | `audit_events` | `tenant_id`, `action`, `timestamp_utc` DESC | Events by action type. |
| `idx_aud_tenant_trace` | `audit_events` | `tenant_id`, `trace_id` | Events in a distributed trace. |
| `idx_evb_tenant_period` | `evidence_bundles` | `tenant_id`, `period` | Evidence bundles for a period. |

### 12.7 Risk and Insights Context

| Index Name | Collection | Fields | Purpose |
|---|---|---|---|
| `idx_ra_tenant_status_sev` | `risk_alerts` | `tenant_id`, `status`, `severity`, `created_at` DESC | Open alerts by severity. |
| `idx_ra_tenant_due` | `risk_alerts` | `tenant_id`, `status`, `legal_due_date` | Alerts approaching deadline. |
| `idx_rs_tenant_period` | `risk_scores` | `tenant_id`, `period` DESC | Score history for a tenant. |

---

## 13. Transaction Boundary Analysis

### 13.1 Firestore Transaction Limits

Firestore enforces the following hard limits per transaction:

- **Maximum 500 document writes** per transaction.
- **Maximum 10 MiB** total request size per transaction.
- **Maximum 270 seconds** transaction lifetime (server-side).

### 13.2 Payroll Run: The Critical Bottleneck

The ZenoHR launch capacity envelope targets up to **500 active employees**. A single payroll run at maximum capacity produces:

| Operation | Document Writes | Calculation |
|---|---|---|
| Update `payroll_runs` status | 1 | Single document |
| Create `payroll_results` (subcollection) | 500 | One per employee |
| **Total** | **501** | **Exceeds 500-document limit** |

**Resolution strategy: Batched Writes with Eventual Consistency**

Payroll calculation is decomposed into phases that respect the 500-document limit:

1. **Phase 1 -- Calculate and Write Results (batched):**
   - Partition employees into batches of at most 400 (leaving headroom for overhead writes).
   - Each batch executes in its own Firestore transaction.
   - Each batch writes its `payroll_results` subcollection documents.
   - The `payroll_runs` document remains in `Draft` status during this phase.
   - Batch progress is tracked via a transient `_batch_progress` field on the payroll run document (or a separate ephemeral tracking document).

2. **Phase 2 -- Aggregate and Transition (single transaction):**
   - Once all batches complete, a single finalizing transaction:
     - Reads all `payroll_results` to compute aggregates.
     - Writes the aggregate totals and checksum to the `payroll_runs` document.
     - Transitions status from `Draft` to `Calculated`.
   - This transaction writes exactly 1 document (the payroll run itself).

3. **Phase 3 -- Finalization (single transaction):**
   - Status transition `Calculated -> Finalized` is a single-document write (the `payroll_runs` document), triggered when the Director or HRManager clicks Finalize & Lock.
   - Audit events are written in a separate transaction immediately following finalization.
   - The audit event write and payroll finalization are NOT in the same transaction to respect the 500-doc limit when combined with the audit chain.

**Idempotency guarantee:** Each batch uses an idempotency key derived from `payroll_run_id + batch_number`. Retried batches overwrite the same document IDs (since `payroll_results` uses `employee_id` as document ID), making the operation naturally idempotent.

### 13.3 Leave Approval Transaction

A leave approval involves:

| Operation | Document Writes |
|---|---|
| Update `leave_requests` status | 1 |
| Update `leave_balances` totals | 1 |
| Create `accrual_ledger` entry (consumption) | 1 |
| Create `audit_events` entry | 1 |
| **Total** | **4** |

This fits comfortably within a single transaction. All four writes execute atomically.

### 13.4 Timesheet Approval Transaction

| Operation | Document Writes |
|---|---|
| Update `timesheets` status and approver | 1 |
| Update individual `time_entries` approval states | up to 7 (one per day) |
| Create `audit_events` entry | 1 |
| **Total** | **up to 9** |

Fits within a single transaction.

### 13.5 Compliance Submission Transaction

| Operation | Document Writes |
|---|---|
| Create/update `submission_packages` | 1 |
| Create `compliance_results` (one per control) | up to 20 |
| Create `evidence_bundles` | 1 |
| Create `evidence_bundles/document_refs` | up to 10 |
| Create `audit_events` | 1 |
| **Total** | **up to 33** |

Fits within a single transaction.

### 13.6 Monthly Accrual Batch

Leave accrual for 500 employees:

| Operation | Document Writes |
|---|---|
| Update 500 `leave_balances` | 500 |
| Create 500 `accrual_ledger` entries | 500 |
| **Total** | **1000** |

**Resolution:** Partition into batches of 200 employees (200 balance updates + 200 ledger entries = 400 writes per batch). Each batch executes in its own transaction. A background worker orchestrates the batches with checkpoint tracking.

---

## 14. Immutability Enforcement

### 14.1 Write-Once Collections

The following collections or document states are immutable after creation. Security rules must enforce this.

| Collection / Condition | Rule | Enforcement |
|---|---|---|
| `audit_events` | Entire collection is append-only. No updates, no deletes. | Security rule: `allow create: if [auth]; allow read: if [auth]; allow update, delete: if false;` |
| `leave_balances/{lb_id}/accrual_ledger` | Entire subcollection is append-only. No updates, no deletes. | Security rule: `allow create: if [auth]; allow update, delete: if false;` |
| `payroll_adjustments` | Entire collection is append-only. No updates, no deletes. | Security rule: `allow create: if [auth]; allow update, delete: if false;` |
| `evidence_bundles/{evb_id}/document_refs` | Entire subcollection is append-only. | Security rule: `allow create: if [auth]; allow update, delete: if false;` |

### 14.2 Conditionally Immutable Documents

These documents become immutable after reaching a specific status.

| Collection | Immutable After Status | Fields Affected | Enforcement |
|---|---|---|---|
| `payroll_runs` | `Finalized` | All fields except `status` (to allow `Filed` transition) | Application-layer check + security rule: `allow update: if resource.data.status != 'Finalized' \|\| (resource.data.status == 'Finalized' && request.resource.data.status == 'Filed' && onlyFieldChanged('status'));` |
| `payroll_runs` | `Filed` | All fields. Fully immutable. | Security rule: `allow update: if resource.data.status != 'Filed';` |
| `payroll_runs/{pr_id}/payroll_results` | Parent run is `Finalized` | All fields. | Application-layer enforcement. Subcollection immutability checked by reading parent status. |
| `timesheets` | `approved` | All fields. Corrections require new adjustment entries. | Application-layer check + security rule. |
| `submission_packages` | `Submitted` | All fields except `status`, `acknowledged_at`, `sars_reference` (for acknowledgment flow). | Application-layer check + security rule. |
| `submission_packages` | `Rejected` | All fields. Original immutable; corrected successor is a new document. | Security rule: `allow update: if resource.data.status != 'Rejected';` |

### 14.3 Immutable Fields on All Documents

Regardless of collection, the following fields are immutable after document creation:

- `tenant_id` -- tenant isolation key must never change.
- `*_id` fields that match the document ID (e.g. `employee_id`, `payroll_run_id`).
- `created_at` -- creation timestamp.
- `created_by` -- creator actor ID.

Security rules enforce this via field-level checks:

```
allow update: if request.resource.data.tenant_id == resource.data.tenant_id
               && request.resource.data.employee_id == resource.data.employee_id
               && request.resource.data.created_at == resource.data.created_at
               && request.resource.data.created_by == resource.data.created_by;
```

---

## 15. Subcollection Design Rationale

### 15.1 Why Subcollections Instead of Arrays or Flat Collections

Firestore documents have a **1 MiB size limit**. Several entities in ZenoHR have unbounded child data that would exceed this limit if stored as arrays within the parent document.

| Parent | Subcollection | Why Subcollection |
|---|---|---|
| `employees` | `addresses` | Employees may have historical address records (effective-dated). Array would grow unbounded and complicate querying by effective date. |
| `employees` | `emergency_contacts` | Modest cardinality, but separating keeps the employee document lean for frequent reads (payroll, compliance checks) that do not need contact data. |
| `timesheets` | `time_entries` | Up to 7 entries per week. While small, subcollection allows independent reads of summary (parent) vs detail (entries). Timesheet approval reads only the parent. |
| `leave_balances` | `accrual_ledger` | Append-only ledger grows continuously over the employment lifecycle. Storing as array would eventually exceed 1 MiB for long-tenured employees. Subcollection supports efficient pagination and append operations. |
| `payroll_runs` | `payroll_results` | Up to 500 results per run (one per employee). At an estimated 500 bytes per result, a 500-employee run would produce 250 KB as an array -- feasible but problematic for atomic reads and writes. Subcollection allows batched writes (critical for the 500-doc transaction limit) and independent per-employee reads. |
| `evidence_bundles` | `document_refs` | Variable number of artifact references. Subcollection supports independent access for verification workflows. |

### 15.2 Why Root Collections Instead of Subcollections for Some Entities

| Entity | Design Choice | Rationale |
|---|---|---|
| `employment_contracts` | Root collection (not under `employees`) | Contracts are queried across employees for compliance reporting (e.g. "all contracts expiring this month"). Collection group queries would work for subcollections, but root-level is simpler for cross-employee queries and respects module ownership boundaries. |
| `leave_requests` | Root collection (not under `employees`) | Leave requests are queried by managers across their reports, by status, and by date range. Root collection with `employee_id` field supports these access patterns with standard composite indexes. |
| `payroll_adjustments` | Root collection (not under `payroll_runs`) | Adjustments need to be queried by employee across multiple runs. Root collection avoids collection group queries and supports the cross-run view. |
| `termination_cases` | Root collection (not under `employees`) | Queried independently by HR and compliance for status tracking and settlement workflows. |
| `audit_events` | Root collection | Queried across all entity types. Must support efficient chronological, entity-scoped, and actor-scoped queries. Root collection with composite indexes is the most efficient design. |

### 15.3 Collection Group Query Avoidance

This schema deliberately avoids reliance on Firestore collection group queries. While collection group queries are supported, they have operational implications:

1. They require separate index definitions.
2. They scan across all subcollections with the same name in the entire database, requiring careful `tenant_id` filtering.
3. They are harder to reason about for security rule enforcement.

By placing cross-query entities at the root level and using subcollections only for tightly-scoped child data, the schema avoids these complications.

---

## 16. RBAC Collections (REQ-SEC-002, PRD-15-RBAC)

These collections support the 5-tier role architecture and dynamic role management. See `docs/prd/15_rbac_screen_access.md` for the authoritative specification.

### 16.1 `departments` Collection

Tenant-managed departments. Created by Director/HRManager via Settings → Department Management.

```
departments/{department_id}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `tenant_id` | string | Yes | Tenant isolation — all queries filter by this |
| `department_id` | string | Yes | Auto-generated, immutable |
| `display_name` | string | Yes | e.g., "Finance", "Warehouse" |
| `cost_centre_code` | string | No | e.g., "CC-001" |
| `head_employee_id` | string | No | `employee_id` of department head |
| `created_by` | string | Yes | Firebase UID of creator |
| `created_at` | timestamp | Yes | |
| `updated_at` | timestamp | Yes | |
| `is_active` | boolean | Yes | Soft delete — inactive depts not shown in dropdowns |
| `is_system_dept` | boolean | No | `true` for the `Executive` department auto-created at tenant onboarding. System departments cannot be deactivated or renamed. Defaults to `false` for all user-created departments. |

**Invariants**:
- `department_id` is immutable once created
- Deactivating a department does not delete it — employees retain their `department_id` reference
- Cannot delete or deactivate a department with active employees
- Each tenant has exactly one department where `is_system_dept == true` (the `Executive` department). Created during tenant provisioning by SaasAdmin; never created by Directors/HRManagers.

### 16.2 `roles` Collection

Custom role definitions created by Director/HRManager. System roles (Director, HRManager, Employee, SaasAdmin) are not stored here — they are hardcoded enum values.

```
roles/{role_id}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `tenant_id` | string | Yes | Tenant isolation |
| `role_id` | string | Yes | Auto-generated, immutable |
| `display_name` | string | Yes | e.g., "Finance Manager", "Contractor" |
| `base_system_role` | string | Yes | `"Manager"` or `"Employee"` — constrains permission ceiling |
| `department_id` | string | Manager only | Required when `base_system_role == "Manager"` |
| `is_system_role` | boolean | Yes | Always `false` for custom roles |
| `created_by` | string | Yes | Firebase UID |
| `created_at` | timestamp | Yes | |
| `updated_at` | timestamp | Yes | |
| `is_active` | boolean | Yes | Soft delete |

**Invariants**:
- `role_id` and `base_system_role` are immutable once created
- Cannot change `base_system_role` — create a new role instead
- Deactivating a role does not affect users already assigned to it (they retain access until reassigned)

### 16.3 `role_permissions` Collection

Permission token sets for custom roles. Only used for Manager base-type roles — Employee base type always has base Employee permissions only.

```
role_permissions/{role_id}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `tenant_id` | string | Yes | Tenant isolation |
| `role_id` | string | Yes | FK to `roles` — document ID matches |
| `permissions` | string[] | Yes | Subset of allowed Manager permission tokens |
| `version` | integer | Yes | Increments on each update |
| `updated_by` | string | Yes | Firebase UID of last editor |
| `updated_at` | timestamp | Yes | |

**Allowed permission tokens** (Manager base only):
- `leave.team.view`, `leave.team.approve`
- `timesheet.team.view`, `timesheet.team.approve`
- `employee.team.view`, `reports.team.view`

**Invariants**:
- Only tokens from the allowed set above may be stored
- Payroll/compliance/audit tokens are never valid values — rejected by Firestore security rules
- Version number is append-only (increment only)

### 16.4 `user_role_assignments` Collection

Maps Firebase Auth UIDs to roles. Supports effective date ranges for POPIA CTL-POPIA-007 (monthly access review).
**A user may have multiple active assignment documents** (same `firebase_uid`, different `role_id`/`department_id`) to support dual roles and multi-department management. See PRD-15 Section 1.7.

```
user_role_assignments/{assignment_id}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `tenant_id` | string | Yes | Tenant isolation |
| `assignment_id` | string | Yes | Auto-generated |
| `firebase_uid` | string | Yes | Firebase Auth UID |
| `employee_id` | string | Yes | FK to `employees` |
| `role_id` | string | Yes | FK to `roles` (or system role constant) |
| `system_role` | string | Yes | `SaasAdmin`, `Director`, `HRManager`, `Manager`, `Employee` |
| `department_id` | string | Manager only | Dept scope for Manager-base roles |
| `is_primary` | boolean | Yes | `true` = main/highest-privilege assignment; `false` = secondary (extra dept or lower role). System-managed — not editable by users. |
| `is_active` | boolean | Yes | `true` = active assignment, `false` = revoked. Set to `false` instead of deleting — preserves audit history and enables efficient Firestore index queries. |
| `effective_from` | date | Yes | Inclusive |
| `effective_to` | date | No | Null = indefinite |
| `assigned_by` | string | Yes | Firebase UID of assigner |
| `assigned_at` | timestamp | Yes | |
| `revoked_by` | string | No | Set when assignment is ended |
| `revoked_at` | timestamp | No | |

**Invariants**:
- Write-once with append-only corrections (revoke by setting `is_active: false` and `effective_to`; create a new assignment for the replacement)
- A user may have multiple active assignments per tenant (multi-dept managers, dual roles)
- Exactly one active assignment per user must have `is_primary: true`. When the primary assignment is revoked, the application auto-promotes the oldest remaining active assignment. This is system-managed.
- `department_id` must be **null** for Director/HRManager assignments; must be **non-null** for Manager assignments
- Effective `system_role` = highest-privilege active assignment; effective dept scope = union of all active Manager assignments' `department_id` values
- An "active" assignment satisfies: `is_active == true AND effective_from <= today AND (effective_to IS NULL OR effective_to >= today)`
- If all assignments have `is_active: false` the user is denied all access and redirected to `/unauthorized` with message "Your role has expired. Contact your administrator."
- `system_role` must match the `base_system_role` of the referenced `role_id`

### 16.5 Indexes for RBAC Collections

```
departments:   [tenant_id ASC, is_active ASC, display_name ASC]
roles:         [tenant_id ASC, base_system_role ASC, is_active ASC]
user_role_assignments: [tenant_id ASC, firebase_uid ASC, is_active ASC, effective_from DESC]   -- active assignments for a user (JWT claim population, middleware)
user_role_assignments: [tenant_id ASC, firebase_uid ASC, effective_from DESC]                  -- full history for access review audit
user_role_assignments: [tenant_id ASC, department_id ASC, system_role ASC, effective_from DESC] -- team member lookup by department
```

---

## Section 17 — Analytics Collections

Pre-aggregated analytics data materialized by `AnalyticsSnapshotService` (BackgroundService). The UI reads from these snapshots instead of aggregating raw collections on demand. Avoids N+1 Firestore reads for large employee populations.

**Context**: Risk & Insights bounded context.

### 17.1 `analytics_snapshots` Collection

Computed nightly at 01:00 SAST and re-triggered by MediatR domain events (`PayrollRunFinalized`, `ComplianceChecked`).

**Document ID pattern**: `snap_{tenant_id_short}_{scope}_{period}` — e.g. `snap_t01_company_2026_02`, `snap_t01_department_fin_2026_02`

```
analytics_snapshots/{snapshot_id}
```

| Field | Type | Required | Notes |
|-------|------|----------|-------|
| `tenant_id` | string | Yes | Tenant isolation. All queries filter by this. Immutable. |
| `snapshot_id` | string | Yes | Matches document ID. Immutable. |
| `scope` | string | Yes | `company` = full-tenant aggregates. `department_{dept_id}` = Manager-scoped aggregates for a single department. |
| `period` | string | Yes | ISO period: `YYYY-MM` for monthly, `YYYY` for annual. |
| `period_type` | string | Yes | `month` or `year`. |
| `calculated_at` | timestamp | Yes | When the snapshot was computed. Used to detect stale data. |
| `payroll` | map | No | Payroll cost aggregates. **Absent** (not null) for `department_*`-scope snapshots — prevents Manager payroll data exposure. |
| `workforce` | map | Yes | Headcount aggregates. Present for all scopes. |
| `leave` | map | Yes | Leave aggregates. Present for all scopes. |
| `compliance` | map | No | Compliance score aggregates. **Absent** for `department_*`-scope snapshots — Manager has no compliance access. |
| `schema_version` | string | Yes | `"1.0"`. Increment on breaking schema changes. |

**`payroll` sub-map fields** (company scope only):

| Sub-field | Type | Notes |
|-----------|------|-------|
| `gross_total_zar` | string | MoneyZAR as string. Sum of all `payroll_runs.gross_total_zar` for the period. |
| `net_total_zar` | string | MoneyZAR as string. |
| `paye_total_zar` | string | MoneyZAR as string. |
| `uif_total_zar` | string | MoneyZAR as string. |
| `sdl_total_zar` | string | MoneyZAR as string. |
| `eti_total_zar` | string | MoneyZAR as string. Aggregate ETI credit claimed. |
| `overtime_total_zar` | string | MoneyZAR as string. Sum of overtime_pay_zar across payroll_results. |
| `employee_count` | number | Number of employees in the payroll run. |
| `payroll_run_id` | string | FK to the source `payroll_runs` document for this period. |

**`workforce` sub-map fields**:

| Sub-field | Type | Notes |
|-----------|------|-------|
| `total_active` | number | Active employee count. |
| `by_type` | map | `{ "Permanent": n, "PartTime": n, "Contractor": n, "Intern": n }` |
| `by_department` | map | `{ "<dept_id>": n, ... }` — company scope only; absent for dept-scope snapshots. |
| `new_hires_count` | number | Employees with `hire_date` within the period. |
| `terminations_count` | number | Employees terminated within the period. |
| `turnover_rate_pct` | number | `terminations / avg_active * 100`, 2 decimal places. |

**`leave` sub-map fields**:

| Sub-field | Type | Notes |
|-----------|------|-------|
| `total_pending_requests` | number | Count of `leave_requests` with `status == submitted` or `status == manager_review`. |
| `total_approved_requests` | number | Count of approved requests in the period. |
| `on_leave_today` | number | Employees currently on active approved leave (computed at snapshot time). |
| `upcoming_leave_30d` | number | Employees with approved leave starting within 30 days of snapshot. |
| `by_type` | map | Per leave type: `{ "annual": { avg_available_days: n, total_consumed_hours: n }, "sick": {...}, ... }` |

**`compliance` sub-map fields** (company scope only):

| Sub-field | Type | Notes |
|-----------|------|-------|
| `sars_score` | number | 0–100 integer. From `risk_scores.sars_compliance_score`. |
| `bcea_score` | number | 0–100 integer. |
| `popia_score` | number | 0–100 integer. |
| `overall_score` | number | 0–100 integer. Composite score. |
| `open_alert_count` | number | Count of `risk_alerts` with `status == open`. |
| `total_exposure_zar` | string | MoneyZAR string. Sum of `projected_exposure_max_zar` for open alerts. |
| `score_trend` | array | Last 6 monthly scores: `[{ "period": "YYYY-MM", "overall": n }]` ordered oldest first. |

**Invariants**:
- Document is write-only from `AnalyticsSnapshotService`. Client reads only — Firestore security rules deny all client writes.
- Firestore security rules: deny `list` operations; allow `get` only when `request.auth.token.tenant_id == resource.data.tenant_id`.
- `payroll` and `compliance` maps MUST be **absent** (not null) for `department_*`-scope snapshots. API layer enforces this by reading only `workforce` and `leave` sub-maps for Manager-role requests.
- `tenant_id` is immutable after creation.
- `schema_version` must be read and validated by the API before processing snapshot data.
- Snapshots are never deleted — retained as historical record of aggregated periods.

### 17.2 Indexes for Analytics Collections

```
analytics_snapshots: [tenant_id ASC, scope ASC, period DESC]          -- load all months for a scope (chart data)
analytics_snapshots: [tenant_id ASC, period_type ASC, period DESC]     -- load monthly vs annual snapshots
```

### 17.3 Firestore Security Rules for `analytics_snapshots`

```
match /analytics_snapshots/{snapshotId} {
  // REQ-ANA-001, REQ-SEC-002: Read allowed only to authenticated tenant members
  // Write forbidden from all clients — only AnalyticsSnapshotService writes via Admin SDK
  allow read: if request.auth != null
               && request.auth.token.tenant_id == resource.data.tenant_id;
  allow write: if false;  // Admin SDK bypasses security rules
}
```

### 17.4 `AnalyticsSnapshotService` Computation Schedule

| Trigger | Action |
|---------|--------|
| Nightly cron at 01:00 SAST (23:00 UTC) | Full company + all department snapshots for current month-to-date |
| `PayrollRunFinalized` MediatR event | Re-compute `company` scope snapshot for the finalized period |
| `ComplianceChecked` MediatR event | Re-compute `company` scope compliance sub-map for the current month |
| Tenant provisioning | Create initial company + all department snapshots for the onboarding month |

**Note**: Personal analytics (`/my-analytics`) does **not** use `analytics_snapshots`. It reads own `payroll_results` and `leave_balances` directly (max 12–24 documents per tax year). See PRD-15 Section 1.6 (Self-Access Guarantee). — DEC-024

---

## Section 18: Platform Configuration & SARS ISV Collections

### 18.1 `company_settings` Collection (REQ-COMP-001, REQ-SEC-010)

Tenant-level configuration document. One document per tenant, keyed by `tenant_id`. Holds SARS ISV credentials, filing preferences, backup dismissal flags, and user preferences.

**Root Collection**: `company_settings/{tenant_id}`

| Field | Type | Required | Classification | Notes |
|-------|------|----------|----------------|-------|
| `tenant_id` | string | Yes | internal | Immutable. Equals document ID. |
| `company_name` | string | Yes | internal | Legal trading name. |
| `registration_number` | string | Yes | confidential | CIPC company registration number. |
| `tax_number` | string | Yes | confidential | SARS employer tax reference number (10 digits). Encrypted at rest via Key Vault. |
| `uif_reference_number` | string | Yes | confidential | UIF registration number (format: `UF-XXXXXXX`). |
| `sdl_number` | string | No | confidential | Skills Development Levy reference number. |
| `paye_reference_number` | string | Yes | confidential | SARS PAYE reference (format: `7XXXXXXXXX`). |
| `physical_address` | map | Yes | internal | `{ line1, line2?, city, province, postal_code, country: "ZA" }` |
| `postal_address` | map | No | internal | Same structure as physical_address. Defaults to physical if absent. |
| `payroll_frequency` | string | Yes | internal | `"monthly"` \| `"weekly"` (REQ-HR-003). |
| `pay_day` | number | No | internal | Day of month for monthly pay (1–31). |
| `financial_year_end_month` | number | Yes | internal | Month number (1–12). Defaults to 2 (February = SARS tax year). |
| `industry_sic_code` | string | No | internal | Standard Industrial Classification code. |
| `sars_isv_config` | map | No | restricted | SARS ISV integration block — see sub-map below. |
| `filing_preferences` | map | No | internal | Filing automation preferences — see sub-map below. |
| `backup_config` | map | No | internal | Local backup prompt settings. |
| `created_at` | timestamp | Yes | internal | Provisioned at tenant onboarding. |
| `updated_at` | timestamp | Yes | internal | Last settings change. |
| `schema_version` | string | Yes | internal | Schema version for migration guards. |

**`sars_isv_config` sub-map** (SARS Integrated Software Vendor accreditation — GAP-FEAT-006):

| Sub-field | Type | Required | Classification | Notes |
|-----------|------|----------|----------------|-------|
| `isv_registration_number` | string | Yes (if config present) | confidential | SARS-issued ISV registration number (e.g., `ISV-2025-XXXXX`). |
| `isv_access_key_kv_ref` | string | Yes (if config present) | restricted | Azure Key Vault secret URI for ISV API access key. **Never stored as plaintext in Firestore.** |
| `isv_environment` | string | Yes | internal | `"test"` \| `"production"`. Must be `"test"` until SARS approves production. |
| `isv_approved_at` | timestamp | No | internal | Date SARS granted production ISV approval. Null if pending. |
| `isv_accreditation_categories` | string[] | No | internal | e.g., `["EMP201", "EMP501", "IRP5", "IT3a", "tax_directives"]` |
| `efiling_api_endpoint` | string | No | internal | SARS eFiling API base URL (test or production). Set by ISV config wizard. |
| `trade_testing_completed` | boolean | Yes | internal | Whether SARS trade-testing phase has been signed off. Default `false`. |
| `inf001_submitted_at` | timestamp | No | internal | Date Form INF001 (ISV application) was submitted to SARS. |
| `inf001_approved_at` | timestamp | No | internal | Date SARS approved Form INF001. Null if pending. |
| `last_connectivity_check_at` | timestamp | No | internal | Last successful ping to SARS eFiling API. |
| `connectivity_status` | string | No | internal | `"ok"` \| `"degraded"` \| `"down"` \| `"unknown"`. |

**`filing_preferences` sub-map**:

| Sub-field | Type | Notes |
|-----------|------|-------|
| `auto_submit_emp201` | boolean | Auto-submit EMP201 after payroll finalisation. Default `false`. |
| `auto_generate_payslips` | boolean | Auto-generate PDF payslips after finalisation. Default `true`. |
| `payslip_delivery` | string | `"portal"` \| `"email"` \| `"both"`. |
| `emp501_auto_reconcile` | boolean | Auto-run EMP501 reconciliation at year-end. Default `false`. |
| `irp5_bulk_issue` | boolean | Bulk-issue IRP5 certificates to all employees after EMP501 filing. Default `true`. |

**`backup_config` sub-map** (month-end/year-end local backup prompts — PRD plan step 3):

| Sub-field | Type | Notes |
|-----------|------|-------|
| `month_backup_enabled` | boolean | Show month-end download prompt after payroll finalisation. Default `true`. |
| `year_backup_enabled` | boolean | Show year-end evidence pack prompt after EMP501 filing. Default `true`. |

**Subcollection**: `company_settings/{tenant_id}/user_preferences/{user_id}`

Stores per-user dismissal flags for banners and prompts.

| Field | Type | Notes |
|-------|------|-------|
| `user_id` | string | Firebase UID. Equals document ID. |
| `month_backup_dismissed_at` | map | `{ "YYYY-MM": timestamp }` — dismissed banner per payroll month. |
| `year_backup_dismissed_at` | map | `{ "YYYY": timestamp }` — dismissed year-end banner per tax year. |
| `updated_at` | timestamp | |

**Invariants**:
- One `company_settings` document per tenant. Created at provisioning, never deleted.
- `sars_isv_config.isv_access_key_kv_ref` must reference Azure Key Vault — direct API keys must never appear in this document.
- `isv_environment` must equal `"test"` unless `inf001_approved_at` is set.
- Firestore security rules: allow read/write only to authenticated users with matching `tenant_id` and `system_role in ["Director", "HRManager"]`.

---

### 18.2 `tax_directives` Collection (REQ-COMP-004, CTL-SARS-008)

Tax directives issued under IBIR-006 (SARS Tax Directives for lump-sum payments, retirement, redundancy, etc.). Required for correct PAYE withholding on non-regular income. Phase 2 ISV accreditation target.

**Root Collection**: `tax_directives/{directive_id}`

| Field | Type | Required | Classification | Notes |
|-------|------|----------|----------------|-------|
| `tenant_id` | string | Yes | internal | Tenant isolation. Immutable. |
| `directive_id` | string | Yes | internal | UUID v7. Immutable. |
| `employee_id` | string | Yes | internal | FK to `employees/{emp_id}`. |
| `directive_type` | string | Yes | internal | SARS directive form type: `"IRP3a"` (gratuity) \| `"IRP3b"` (retirement fund lump sum) \| `"IRP3c"` (retrenchment) \| `"IRP3s"` (death benefit) \| `"IRP3e"` (emigration). |
| `directive_number` | string | Yes | confidential | SARS-issued directive number (encrypted at rest). Unique per directive. |
| `tax_year` | string | Yes | internal | Tax year string (e.g., `"2025-2026"`). |
| `withholding_rate` | string | No | confidential | MoneyZAR — withholding rate as decimal (e.g., `"0.18000"` = 18%). Null if amount-based. |
| `withholding_amount_zar` | string | No | confidential | MoneyZAR — fixed rand withholding amount. Null if rate-based. |
| `lump_sum_amount_zar` | string | Yes | confidential | MoneyZAR — gross lump-sum amount subject to directive. |
| `reason_code` | string | Yes | internal | SARS reason code (e.g., `"01"` = Retrenchment, `"02"` = Retirement, `"03"` = Death). |
| `effective_date` | timestamp | Yes | internal | Date directive takes effect. |
| `expiry_date` | timestamp | No | internal | Date directive expires. Null for once-off directives. |
| `source` | string | Yes | internal | `"manual_entry"` \| `"sars_api"` (retrieved via ISV API) \| `"imported_xml"`. |
| `status` | string | Yes | internal | `"pending_verification"` \| `"active"` \| `"applied"` \| `"expired"` \| `"revoked"`. |
| `applied_to_payroll_run_id` | string | No | internal | FK to `payroll_runs/{run_id}` once applied. Null until applied. |
| `verification_timestamp` | timestamp | No | internal | When SARS API confirmed directive. Null for manual entries. |
| `uploaded_document_ref` | string | No | internal | Azure Blob Storage URI for scanned directive PDF (if manually uploaded). |
| `notes` | string | No | internal | HR notes (reason for directive, employee communication). Max 500 chars. |
| `created_at` | timestamp | Yes | internal | Immutable. |
| `created_by` | string | Yes | internal | Actor `firebase_uid` of HR user who captured the directive. |
| `updated_at` | timestamp | Yes | internal | |
| `schema_version` | string | Yes | internal | |

**Invariants**:
- `directive_number` must be unique per `tenant_id` (enforced by Firestore transaction on creation).
- Once `status == "applied"`, the document is effectively immutable — corrections create a new directive referencing the original.
- `withholding_rate` and `withholding_amount_zar` are mutually exclusive — exactly one must be set.
- `lump_sum_amount_zar` must be a valid MoneyZAR string (non-negative, max 15 significant digits).
- Firestore security rules: allow read to `Director`, `HRManager`; deny all client writes (directives written via Admin SDK only from `TaxDirectiveService`).

**Indexes**:
```
tax_directives: [tenant_id ASC, employee_id ASC, status ASC, effective_date DESC]
tax_directives: [tenant_id ASC, tax_year ASC, status ASC]
tax_directives: [tenant_id ASC, applied_to_payroll_run_id ASC]              -- find directives used in a run
```

---

## Section 19: Security Operations Collections (REQ-SEC-001, REQ-SEC-009)

Used by the SaasAdmin Security Operations Centre (`15-security-ops.html`). These are **platform-level** (cross-tenant) collections — they do NOT carry `tenant_id` for the _events_ that affect platform infrastructure, but _tenant-scoped_ incidents do reference `affected_tenant_id`.

> **Access**: SaasAdmin role only (via Firebase Admin SDK — no Firestore security rule `allow read` for tenant users). Platform-level collections are completely invisible to tenant users.

---

### 19.1 `security_incidents` Collection (REQ-SEC-009, CTL-POPIA-013)

Tracks active and historical security incidents raised by automated detection or SaasAdmin staff.

**Root Collection**: `security_incidents/{incident_id}`

| Field | Type | Required | Classification | Notes |
|-------|------|----------|----------------|-------|
| `incident_id` | string | Yes | restricted | UUID v7. Immutable. Display format: `INC-YYYY-NNN`. |
| `title` | string | Yes | restricted | Short human-readable title (e.g., `"Auth Anomaly — Brute Force Detected"`). |
| `description` | string | No | restricted | Detailed description. Max 2000 chars. |
| `category` | string | Yes | restricted | `"auth_anomaly"` \| `"tenant_isolation_breach"` \| `"data_exfiltration"` \| `"ransomware"` \| `"supply_chain"` \| `"audit_tampering"` \| `"api_abuse"` \| `"other"`. |
| `severity` | string | Yes | restricted | `"sev1"` \| `"sev2"` \| `"sev3"` \| `"sev4"`. Sev-1 = critical breach, Sev-4 = informational. |
| `status` | string | Yes | restricted | `"open"` \| `"investigating"` \| `"contained"` \| `"resolved"` \| `"false_positive"`. |
| `affected_tenant_id` | string | No | restricted | Tenant impacted. Null for platform-wide incidents. |
| `affected_employee_id` | string | No | restricted | Employee impacted (e.g., whose account was targeted). |
| `source_ip` | string | No | restricted | Attacker source IP (if known). Stored encrypted. |
| `source_asn` | string | No | restricted | ASN / ISP of source IP (e.g., `"AS36874 Vodacom (Pty) Ltd ZA"`). |
| `detection_method` | string | No | restricted | `"automated_rule"` \| `"saas_admin_manual"` \| `"tenant_report"` \| `"pen_test"`. |
| `failed_login_count` | number | No | restricted | For `auth_anomaly` category: count of failed attempts. |
| `popia_breach_reportable` | boolean | Yes | restricted | Whether incident meets POPIA Section 22 mandatory reporting threshold. |
| `popia_reported_at` | timestamp | No | restricted | Timestamp of POPIA Information Regulator report submission. Null if not yet reported. |
| `assigned_to` | string | No | restricted | Firebase UID of SaasAdmin handling the incident. |
| `resolution_notes` | string | No | restricted | How incident was resolved. |
| `resolved_at` | timestamp | No | restricted | Resolution timestamp. |
| `timeline` | array | No | restricted | Array of `{ timestamp, actor_uid, action, notes }` — incident activity log. |
| `created_at` | timestamp | Yes | restricted | |
| `updated_at` | timestamp | Yes | restricted | |
| `schema_version` | string | Yes | restricted | |

**Invariants**:
- `severity` must be reassessed if `popia_breach_reportable` is set to `true` — minimum `"sev2"` required.
- If `status == "resolved"`, `resolved_at` and `resolution_notes` must be populated.
- POPIA Section 22: if `popia_breach_reportable == true` and `status` transitions to `"contained"` or `"resolved"`, the system must prompt SaasAdmin to file via the Information Regulator e-portal within 72 hours.

**Indexes**:
```
security_incidents: [status ASC, severity ASC, created_at DESC]
security_incidents: [affected_tenant_id ASC, status ASC, created_at DESC]
security_incidents: [popia_breach_reportable ASC, popia_reported_at ASC]    -- find unreported POPIA breaches
```

---

### 19.2 `vulnerability_findings` Collection (REQ-SEC-001, CTL-POPIA-007)

Tracks known vulnerabilities found by automated scans (Dependabot, Trivy, dotnet-ossindex) or manual pen testing.

**Root Collection**: `vulnerability_findings/{finding_id}`

| Field | Type | Required | Classification | Notes |
|-------|------|----------|----------------|-------|
| `finding_id` | string | Yes | restricted | UUID v7. Display format: `VLN-YYYY-NNN`. |
| `title` | string | Yes | restricted | e.g., `"CSP nonce not implemented on SignalR hub endpoint"`. |
| `owasp_category` | string | No | restricted | OWASP Top 10 category (e.g., `"A05:2021 Security Misconfiguration"`). |
| `cve_id` | string | No | restricted | CVE identifier if applicable (e.g., `"CVE-2025-6725"`). |
| `severity` | string | Yes | restricted | `"critical"` \| `"high"` \| `"medium"` \| `"low"` \| `"informational"`. |
| `cvss_score` | number | No | restricted | CVSS v3.1 base score (0.0–10.0). |
| `affected_component` | string | Yes | restricted | Component/package/endpoint affected (e.g., `"Lucide CDN script tag"`). |
| `affected_version` | string | No | restricted | Version string of affected component. |
| `fixed_version` | string | No | restricted | Version that resolves the vulnerability (if known). |
| `description` | string | Yes | restricted | Technical description. Max 2000 chars. |
| `remediation_steps` | string | No | restricted | Recommended fix steps. Max 2000 chars. |
| `source` | string | Yes | restricted | `"dependabot"` \| `"trivy"` \| `"ossindex"` \| `"pen_test"` \| `"saas_admin_manual"`. |
| `status` | string | Yes | restricted | `"open"` \| `"in_remediation"` \| `"resolved"` \| `"accepted_risk"` \| `"false_positive"`. |
| `due_date` | timestamp | No | restricted | Target remediation date. |
| `resolved_at` | timestamp | No | restricted | When status moved to `"resolved"`. |
| `resolved_by` | string | No | restricted | Firebase UID of SaasAdmin who resolved. |
| `linked_incident_id` | string | No | restricted | FK to `security_incidents` if this finding triggered an incident. |
| `created_at` | timestamp | Yes | restricted | |
| `updated_at` | timestamp | Yes | restricted | |
| `schema_version` | string | Yes | restricted | |

**Invariants**:
- `cvss_score >= 7.0` findings must have `due_date` set within 14 days of discovery (CI gate policy from `dotnet-ossindex`).
- `severity == "critical"` automatically triggers creation of a linked `security_incidents` document if none exists.
- `status == "accepted_risk"` requires a `due_date` for next review (maximum 90 days acceptance window).

**Indexes**:
```
vulnerability_findings: [status ASC, severity ASC, due_date ASC]
vulnerability_findings: [owasp_category ASC, status ASC]
vulnerability_findings: [source ASC, created_at DESC]
```

---

### 19.3 `auth_anomaly_events` Collection (REQ-SEC-003, REQ-SEC-004)

Platform-level authentication anomaly log. Written by the `AuthAnomalyDetectionService` (background service). Feeds the Auth Anomaly Log panel in `15-security-ops.html`.

**Root Collection**: `auth_anomaly_events/{event_id}`

| Field | Type | Required | Classification | Notes |
|-------|------|----------|----------------|-------|
| `event_id` | string | Yes | restricted | UUID v7. Immutable. |
| `anomaly_type` | string | Yes | restricted | `"brute_force"` \| `"impossible_travel"` \| `"credential_stuffing"` \| `"mfa_bypass_attempt"` \| `"token_replay"` \| `"bulk_download"` \| `"privilege_escalation_attempt"` \| `"cross_tenant_probe"`. |
| `severity` | string | Yes | restricted | `"critical"` \| `"high"` \| `"medium"` \| `"low"`. |
| `affected_tenant_id` | string | No | restricted | Null for platform-level anomalies (e.g., cross-tenant probe). |
| `affected_user_uid` | string | No | restricted | Firebase UID of targeted user. Null if user unknown. |
| `source_ip` | string | No | restricted | Source IP (encrypted). |
| `source_country` | string | No | restricted | ISO 3166-1 alpha-2 country (e.g., `"ZA"`, `"NG"`). Derived from IP geolocation. |
| `source_isp` | string | No | restricted | ISP / ASN description. |
| `failed_attempt_count` | number | No | restricted | For `brute_force` / `credential_stuffing`: count of failures. |
| `previous_login_location` | string | No | restricted | For `impossible_travel`: prior known location (city, country). |
| `time_delta_minutes` | number | No | restricted | For `impossible_travel`: minutes since prior login. |
| `action_taken` | string | No | restricted | Automated response: `"none"` \| `"account_locked"` \| `"token_revoked"` \| `"alert_only"` \| `"ip_blocked"`. |
| `linked_incident_id` | string | No | restricted | FK to `security_incidents` if escalated. |
| `is_resolved` | boolean | Yes | restricted | False until SaasAdmin marks as reviewed or incident resolved. |
| `resolved_by` | string | No | restricted | Firebase UID of SaasAdmin who reviewed. |
| `resolved_at` | timestamp | No | restricted | Review timestamp. |
| `created_at` | timestamp | Yes | restricted | Immutable. Event occurrence timestamp. |
| `schema_version` | string | Yes | restricted | |

**Invariants**:
- Events are **append-only** (immutable after creation). Corrections via `linked_incident_id` updates only.
- `anomaly_type == "cross_tenant_probe"` or `"mfa_bypass_attempt"` or `"privilege_escalation_attempt"` must auto-create a `security_incidents` document with minimum `"sev2"`.
- `action_taken == "ip_blocked"` must log the IP to an Azure Firewall deny list via the `FirewallManagementService`.
- 90-day retention: events older than 90 days with `is_resolved == true` are archived to Azure Blob Storage cold tier. Events with `linked_incident_id` are retained for 5 years.

**Indexes**:
```
auth_anomaly_events: [is_resolved ASC, severity ASC, created_at DESC]
auth_anomaly_events: [affected_tenant_id ASC, created_at DESC]
auth_anomaly_events: [anomaly_type ASC, created_at DESC]
```

---

### 19.4 Firestore Security Rules for Security Operations Collections

```
// REQ-SEC-001, REQ-SEC-009: Security ops collections — SaasAdmin only via Admin SDK
// Client SDK access is completely denied. SaasAdmin reads via Admin SDK (bypasses rules).
// These rules act as a safety net if Admin SDK is misconfigured.

match /security_incidents/{incidentId} {
  allow read, write: if false;  // Admin SDK only
}

match /vulnerability_findings/{findingId} {
  allow read, write: if false;  // Admin SDK only
}

match /auth_anomaly_events/{eventId} {
  allow read, write: if false;   // Admin SDK only
  // Immutability enforced in AuthAnomalyDetectionService — no client updates ever
}
```

---

### 19.5 Indexes for Security Operations Collections

```
security_incidents:       [status ASC, severity ASC, created_at DESC]
security_incidents:       [affected_tenant_id ASC, status ASC]
security_incidents:       [popia_breach_reportable ASC, popia_reported_at ASC]
vulnerability_findings:   [status ASC, severity ASC, due_date ASC]
vulnerability_findings:   [owasp_category ASC, status ASC]
auth_anomaly_events:      [is_resolved ASC, severity ASC, created_at DESC]
auth_anomaly_events:      [affected_tenant_id ASC, created_at DESC]
auth_anomaly_events:      [anomaly_type ASC, created_at DESC]
```
