---
doc_id: PRD-13-GLOSSARY
version: 1.0.0
owner: Product Manager; Data Steward
updated_on: 2026-02-18
applies_to:
  - Common language and canonical field definitions
depends_on:
  - PRD-02-DOMAIN
  - PRD-04-API
requirements:
  - REQ-OPS-012
---

# Glossary and Data Dictionary

## Glossary
| Term | Definition |
|---|---|
| PAYE | Pay-As-You-Earn employee tax withholding process in South Africa |
| UIF | Unemployment Insurance Fund contribution |
| SDL | Skills Development Levy contribution |
| ETI | Employment Tax Incentive eligibility and claim process |
| EMP201 | Monthly employer declaration form and process |
| EMP501 | Employer reconciliation process (interim/annual) |
| IRP5/IT3(a) | Employee tax certificate outputs |
| BCEA | Basic Conditions of Employment Act, labor standards baseline |
| POPIA | Protection of Personal Information Act, privacy regulation baseline |
| Compliance Block | Hard system stop caused by legal or policy control breach |
| Compensating Entry | Correction method that appends offsetting records without rewriting originals |
| Evidence Pack | Signed artifact bundle proving control operation and outputs |
| Rule Set Version | Signed, effective-dated configuration artifact for statutory logic |
| Data Classification | Sensitivity tag used for access control and handling requirements |
| RPO | Recovery Point Objective (maximum acceptable data loss window) |
| RTO | Recovery Time Objective (maximum acceptable service restoration time) |
| SoD | Separation of Duties to avoid single-role control of critical workflow |
| MCP | Model Context Protocol local context delivery layer for the agent |

## Canonical Identifier Standards
| Entity | ID Pattern | Example |
|---|---|---|
| Employee | `emp_<uuid>` | `emp_9b6f...` |
| Payroll Run | `pr_<YYYY_MM>_<seq>` | `pr_2026_02_001` |
| Submission Package | `sub_<type>_<period>_<seq>` | `sub_emp201_2026_02_001` |
| Audit Event | `aud_<uuid>` | `aud_4f2...` |
| Evidence Bundle | `evb_<period>_<seq>` | `evb_2026_02_001` |
| Rule Set | `rules_<domain>_<version>` | `rules_sars_2026_1` |
| Test Case | `TC-<DOMAIN>-###` | `TC-SARS-001` |
| Requirement | `REQ-<DOMAIN>-###` | `REQ-COMP-001` |

## Canonical Field Dictionary
| Field | Type | Required | Validation Rule | Classification | Owner |
|---|---|---|---|---|---|
| `employee_id` | string | Yes | Immutable; unique | Internal | HR |
| `legal_name` | string | Yes | Non-empty; validated character set | Confidential | HR |
| `national_id_or_passport` | string | Yes | Format validation by type | Restricted | HR |
| `tax_reference` | string | Yes | Mandatory format validation before filing | Restricted | Payroll |
| `bank_account_ref` | string | Yes | Encrypted storage; masked output | Restricted | Payroll |
| `employment_status` | enum | Yes | `active|suspended|terminated` | Internal | HR |
| `contract_start_date` | date | Yes | <= end date | Internal | HR |
| `contract_end_date` | date | No | >= start date | Internal | HR |
| `ordinary_hours_policy` | object | Yes | Must reference active policy version | Internal | HR |
| `timesheet_week_start` | date | Yes | Must align to configured week start | Internal | Time |
| `total_ordinary_hours` | decimal | Yes | Cannot exceed legal policy without exception | Internal | Time |
| `total_overtime_hours` | decimal | Yes | Requires eligibility and approval metadata | Internal | Time |
| `leave_type` | enum | Yes | Value in policy catalog | Internal | HR |
| `accrued_hours` | decimal | Yes | Non-negative | Internal | HR |
| `consumed_hours` | decimal | Yes | Non-negative | Internal | HR |
| `available_hours` | decimal | Yes | Derived: accrued-consumed+adjustments | Internal | HR |
| `payroll_period` | string | Yes | `YYYY-MM` format | Internal | Payroll |
| `gross_amount_zar` | decimal(18,2) | Yes | >=0, deterministic rounding | Confidential | Payroll |
| `deduction_total_zar` | decimal(18,2) | Yes | >=0 | Confidential | Payroll |
| `net_amount_zar` | decimal(18,2) | Yes | Equation validation required | Confidential | Payroll |
| `paye_amount_zar` | decimal(18,2) | Yes | Derived from active PAYE table version | Confidential | Payroll |
| `uif_amount_zar` | decimal(18,2) | Yes | Must respect statutory cap/rate | Confidential | Payroll |
| `sdl_amount_zar` | decimal(18,2) | Yes | Must respect statutory rate | Confidential | Payroll |
| `eti_flag` | boolean | Yes | True only if eligibility rules pass | Internal | Payroll |
| `submission_type` | enum | Yes | `EMP201|EMP501|IRP5|IT3A` | Internal | Compliance |
| `submission_status` | enum | Yes | State-machine constrained | Internal | Compliance |
| `rule_set_version` | string | Yes | Must exist, signed, and effective | Internal | Compliance |
| `rule_set_checksum` | string | Yes | SHA-256 format | Internal | Compliance |
| `termination_reason_code` | string | No | Required when termination case exists | Internal | HR |
| `notice_period_days` | integer | No | Derived by policy and legal checks | Internal | HR |
| `severance_amount_zar` | decimal(18,2) | No | Derived by policy and legal checks | Confidential | Payroll |
| `audit_event_id` | string | Yes | Unique, immutable | Internal | Security |
| `audit_before_hash` | string | No | Required for updates | Internal | Security |
| `audit_after_hash` | string | Yes | SHA-256 format | Internal | Security |
| `trace_id` | string | Yes | Propagated across services | Internal | SRE |
| `data_purpose_code` | string | Yes for personal data operations | Must match approved catalog | Internal | Compliance |
| `lawful_basis_code` | string | Yes for POPIA-controlled operations | Must match approved lawful basis catalog | Internal | Compliance |

## Enumerations

### Role Enumeration
- `HRAdmin`
- `PayrollOfficer`
- `ComplianceOfficer`
- `Manager`
- `Employee`
- `Auditor`
- `SecurityAdmin`

### PayrollRun Status Enumeration
- `Draft`
- `Calculated`
- `Validated`
- `Approved`
- `Finalized`
- `Filed`

### Severity Enumeration
- `Sev-1`
- `Sev-2`
- `Sev-3`
- `Sev-4`

### Data Classification Enumeration
- `public`
- `internal`
- `confidential`
- `restricted`

## Derived Field Formulas
1. `net_amount_zar = gross_amount_zar - deduction_total_zar + addition_total_zar`
2. `available_hours = accrued_hours - consumed_hours + adjustment_hours`
3. `payroll_checksum = sha256(concatenated sorted payroll line item payloads + rule_set_version)`

## Validation and Ownership Rules
1. Every canonical field must have a declared owner.
2. Field definition changes require:
- ADR update
- contract test update
- traceability matrix update
3. Any field used for legal submissions must include:
- provenance metadata
- source version
- validation status

## MCP Retrieval Hints
To improve local MCP retrieval precision:
1. Keep field names exact and stable.
2. Use same ID values (`REQ-*`, `TC-*`, `CTL-*`) across docs.
3. Avoid synonyms for legal controls unless mapped in this glossary.
