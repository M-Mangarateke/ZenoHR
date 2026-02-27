---
doc_id: PRD-06-COMP
version: 1.0.0
owner: Compliance Officer
updated_on: 2026-02-18
applies_to:
  - SARS, BCEA, and POPIA compliance controls
  - Audit evidence and filing workflow
depends_on:
  - PRD-01-EXECUTIVE
  - PRD-04-API
  - PRD-05-SEC
requirements:
  - REQ-COMP-001
  - REQ-COMP-016
  - REQ-OPS-015
---

# Compliance Mapping: SARS, BCEA, POPIA

## Purpose
Define legally relevant control mappings from obligations to concrete system controls, tests, and audit evidence artifacts. This section is written for implementation and verification teams and must be reviewed with legal counsel annually.

## Legal Reference Metadata
| Framework | Primary Reference | Last Validated | Next Mandatory Review |
|---|---|---|---|
| SARS Employer Payroll Obligations | SARS PAYE and employer declarations pages | 2026-02-18 | 2027-02-18 or tax-year change |
| BCEA | Basic Conditions of Employment Act 75 of 1997 | 2026-02-18 | 2027-02-18 or legislative amendment |
| POPIA | Protection of Personal Information Act 4 of 2013 | 2026-02-18 | 2027-02-18 or regulator directive |

## BCEA Clause-Mapped Control Matrix
| Control ID | BCEA Clause Anchor | Obligation Summary | System Control | Test ID | Evidence |
|---|---|---|---|---|---|
| `CTL-BCEA-001` | Section 9 | Ordinary hours limit | Validate weekly ordinary hours before approval/payroll | `TC-BCEA-001` | `EV-BCEA-001` |
| `CTL-BCEA-002` | Section 10 | Overtime compensation rules | Enforce overtime multiplier and eligibility rules | `TC-BCEA-002` | `EV-BCEA-002` |
| `CTL-BCEA-003` | Section 20 | Annual leave entitlement | Accrual engine with entitlement ledger | `TC-BCEA-003` | `EV-BCEA-003` |
| `CTL-BCEA-004` | Section 31 | Record keeping obligations | Immutable records and retention tagging | `TC-BCEA-004` | `EV-BCEA-004` |
| `CTL-BCEA-005` | Section 33 | Remuneration information requirements | Detailed payslip rendering and storage | `TC-BCEA-005` | `EV-BCEA-005` |
| `CTL-BCEA-006` | BCEA termination provisions (notice and severance as applicable) | Notice period and severance compliance | Termination settlement calculator with policy and legal checks | `TC-BCEA-006` | `EV-BCEA-006` |

Implementation notes:
1. Any breach of `CTL-BCEA-*` returns `COMPLIANCE_BLOCK`.
2. Overrides are disabled by default; any policy exception requires Compliance Officer approval and logged rationale.
3. Termination settlement outputs must include policy version, legal-check status, and approval record.

## SARS Control Matrix
| Control ID | SARS Obligation Area | Obligation Summary | System Control | Test ID | Evidence |
|---|---|---|---|---|---|
| `CTL-SARS-001` | PAYE | Accurate tax deduction computation | Versioned tax engine using active signed tables | `TC-SARS-001` | `EV-SARS-001` |
| `CTL-SARS-002` | UIF and SDL | Correct statutory contribution calculation | Contribution calculator with configured caps/rates | `TC-SARS-003` | `EV-SARS-002` |
| `CTL-SARS-003` | EMP201 | Monthly employer declaration readiness | EMP201 package generation with due-date reminders | `TC-SARS-004` | `EV-SARS-003` |
| `CTL-SARS-004` | EMP501 | Interim/annual reconciliation integrity | Payroll-to-certificate reconciliation checks | `TC-SARS-005` | `EV-SARS-004` |
| `CTL-SARS-005` | IRP5/IT3(a) | Valid employee tax certificates | Schema validation and mandatory field checks | `TC-SARS-006` | `EV-SARS-005` |
| `CTL-SARS-006` | Tax reference validity | Mandatory tax number checks before submission | Submission block on invalid/missing tax references | `TC-SARS-002` | `EV-SARS-006` |
| `CTL-SARS-007` | ETI eligibility | Correct incentive claims | Rule-based ETI validator in payroll run | `TC-SARS-007` | `EV-SARS-007` |
| `CTL-SARS-008` | Travel reimbursement | Correct reimbursive travel treatment | SARS rate configuration and calculation policy | `TC-SARS-008` | `EV-SARS-008` |

Implementation notes:
1. Tax tables must be configuration artifacts with signatures and checksums (`REQ-COMP-015`).
2. Filing output generation is blocked when any critical validation fails.

## POPIA Control Matrix
| Control ID | POPIA Clause Anchor | Obligation Summary | System Control | Test ID | Evidence |
|---|---|---|---|---|---|
| `CTL-POPIA-001` | Sections 8-11 | Accountability and lawful processing | Lawful basis and purpose metadata on personal data operations | `TC-POPIA-002` | `EV-POPIA-001` |
| `CTL-POPIA-002` | Section 19 | Security safeguards | Encryption, IAM, monitoring, and hardening controls | `TC-POPIA-003` | `EV-POPIA-002` |
| `CTL-POPIA-003` | Section 22 | Security compromise workflow | Breach detection, register, and notification workflow | `TC-POPIA-004` | `EV-POPIA-003` |
| `CTL-POPIA-004` | Section 23/24 | Data subject access/correction | SAR and correction process with SLA and audit trail | `TC-POPIA-005` | `EV-POPIA-004` |
| `CTL-POPIA-005` | Section 72 | Cross-border transfer controls | Transfer register and legal basis gating | `TC-POPIA-006` | `EV-POPIA-005` |
| `CTL-POPIA-006` | Section 14 | Retention and restriction controls | Retention schedule, legal hold, and archive workflows | `TC-POPIA-007` | `EV-POPIA-006` |

## Compliance Filing Workflow
1. Payroll run finalized and signed.
2. Compliance pre-checks executed (`CTL-SARS-*`, `CTL-BCEA-*`, `CTL-POPIA-*` as applicable).
3. Submission package generated (EMP201, EMP501, IRP5/IT3a).
4. Validation report created and attached to evidence bundle.
5. Compliance Officer reviews and approves release.
6. Submission readiness status updated and archived.
7. Filing acknowledgment and timestamps stored.

## Tax-Year Update Workflow
1. Import new SARS table data from approved source.
2. Verify schema, checksum, signature, and effective dates.
3. Execute mandatory regression pack:
- boundary salary bands
- UIF/SDL cap checks
- ETI eligibility edge cases
4. Activate version only after:
- regression pack pass
- Compliance Officer approval
- audit event creation
5. Emit `TaxTableVersionActivated` event and invalidate dependent caches.

## Compliance Evidence Pack Specification
Evidence bundle contents:
1. Rule set version manifest and checksums.
2. Payroll run summary and validation report.
3. Submission package artifacts and statuses.
4. Immutable audit event references.
5. Approval records and role attestations.
6. Incident or exception logs (if any).

Evidence formats:
- JSON for machine parsing.
- PDF for legal and auditor review.
- Signed hash manifest for integrity verification.

## RACI for Compliance Controls
| Area | Responsible | Accountable | Consulted | Informed |
|---|---|---|---|---|
| SARS controls | Payroll Lead | CFO | Compliance Officer, Legal Counsel | CTO |
| BCEA controls | HR Lead | COO | Compliance Officer, Legal Counsel | Payroll Lead |
| POPIA controls | Security Lead / Information Officer | CISO | Legal Counsel | Product |
| Evidence packs | Compliance Analyst | Compliance Officer | QA Lead | Internal Audit |

## Compliance Exceptions Policy
1. Critical controls (`CTL-*` marked critical) cannot be bypassed.
2. Non-critical exceptions require documented risk acceptance with expiry date.
3. Every exception must include:
- reason
- approver
- scope and expiry
- compensating controls

## Legal and Regulatory Change Management
1. Compliance Officer monitors official updates monthly.
2. Any law/guidance update triggers:
- impact assessment within 5 business days
- required changes in PRD docs and tests
- ADR or risk register update
3. Release blocked if required legal-control updates are not implemented and tested.

## Explicit Legal Disclaimer
This mapping supports technical compliance implementation. Final interpretation and statutory filing obligations must be validated by qualified legal and tax professionals.
