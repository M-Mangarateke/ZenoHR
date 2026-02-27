---
doc_id: PRD-00-MANIFEST
version: 1.0.0
owner: Product Manager; Solution Architect; Compliance Officer
updated_on: 2026-02-18
applies_to:
  - Zenowethu HR System
  - Local MCP knowledge pack for Claude Desktop
depends_on:
  - Zenowethu_HR_System_Innovation_and_Compliance_Architecture.docx
requirements:
  - REQ-OPS-012
  - REQ-OPS-015
---

# Zenowethu HR PRD Manifest

## Purpose
This manifest defines the canonical PRD package for the Zenowethu HR System. It is optimized for machine retrieval by a local MCP server consumed by Claude Desktop. Every document includes stable metadata and requirement IDs so an implementation agent can execute without adding hidden assumptions.

## Scope Baseline
Source-of-truth baseline: `C:/Users/manga/Downloads/Zenowethu_HR_System_Innovation_and_Compliance_Architecture.docx` (last updated 2026-02-17).

The baseline items were promoted into requirement IDs under:
- `REQ-HR-*` for HR domain and product behavior.
- `REQ-COMP-*` for statutory and compliance controls.
- `REQ-SEC-*` for security and privacy controls.
- `REQ-OPS-*` for architecture, runtime, release, and documentation operations.

## Document Index
| Order | File | Doc ID | Primary Purpose |
|---|---|---|---|
| 00 | `docs/prd/00_manifest.md` | `PRD-00-MANIFEST` | Package index, standards, cadence |
| 01 | `docs/prd/01_executive_prd.md` | `PRD-01-EXECUTIVE` | Product goals, scope, roadmap, release gates |
| 02 | `docs/prd/02_domain_model.md` | `PRD-02-DOMAIN` | Bounded contexts, entities, invariants |
| 03 | `docs/prd/03_architecture.md` | `PRD-03-ARCH` | Logical architecture and integration boundaries |
| 04 | `docs/prd/04_api_contracts.md` | `PRD-04-API` | API/event contracts and validation |
| 05 | `docs/prd/05_security_privacy.md` | `PRD-05-SEC` | Threat model, IAM, encryption, POPIA safeguards |
| 06 | `docs/prd/06_compliance_sars_bcea.md` | `PRD-06-COMP` | Clause-mapped controls, filings, evidence |
| 07 | `docs/prd/07_caching_performance.md` | `PRD-07-CACHE` | Cache design, invalidation, SLO budgets |
| 08 | `docs/prd/08_testing_quality.md` | `PRD-08-TEST` | Test strategy, quality gates, defect policy |
| 09 | `docs/prd/09_observability_ops.md` | `PRD-09-OPS` | Telemetry, IR, BCP, backups, DR |
| 10 | `docs/prd/10_rollout_change_mgmt.md` | `PRD-10-ROLLOUT` | Phased rollout, training, rollback controls |
| 11 | `docs/prd/11_traceability_matrix.md` | `PRD-11-TRACE` | End-to-end mapping of requirements |
| 12 | `docs/prd/12_risks_decisions.md` | `PRD-12-RISK-ADR` | ADR log, risk register, mitigation ownership |
| 13 | `docs/prd/13_glossary_and_data_dictionary.md` | `PRD-13-GLOSSARY` | Canonical terms and field definitions |
| 14 | `docs/prd/14_gap_resolution.md` | `PRD-14-GAPS` | Gap resolution for non-design PRD gaps |
| 15 | `docs/prd/15_rbac_screen_access.md` | `PRD-15-RBAC` | **RBAC source of truth**: role definitions, screen matrix, nav per role, field-level access, dynamic role management — supersedes PRD-05 role model |

## Version Map
| Artifact | Version | Approved By | Effective Date |
|---|---|---|---|
| Entire PRD pack | 1.0.0 | Product + Architecture + Compliance | 2026-02-18 |
| Tax/compliance references | 2026.1 | Compliance Officer + External Counsel | 2026-02-18 |
| Test policy baseline | 1.0.0 | QA Lead + Engineering Manager | 2026-02-18 |

## ID Conventions
- Requirements: `REQ-HR-###`, `REQ-COMP-###`, `REQ-SEC-###`, `REQ-OPS-###`
- Assumptions: `ASSUME-###`
- Controls: `CTL-BCEA-###`, `CTL-SARS-###`, `CTL-POPIA-###`
- Test cases: `TC-<DOMAIN>-###`
- Evidence artifacts: `EV-<DOMAIN>-###`
- Risks: `RISK-###`
- Architecture decisions: `ADR-###`

## Mandatory MCP Metadata Contract
Every PRD file must provide this metadata block:
- `doc_id`
- `version`
- `owner`
- `updated_on`
- `applies_to`
- `depends_on`
- `requirements`

## Ownership and RACI
| Area | Responsible | Accountable | Consulted | Informed |
|---|---|---|---|---|
| Product scope | Product Manager | Head of Product | HR Lead, Payroll Lead | Engineering |
| Architecture | Solution Architect | CTO | Security Lead, Dev Lead | Compliance |
| Security and POPIA | Security Lead | CISO | Legal Counsel, DPO/IO | Product |
| SARS and BCEA controls | Compliance Officer | CFO | Payroll Lead, Legal Counsel | Engineering |
| Test quality gates | QA Lead | Engineering Manager | Security Lead, Compliance Officer | Product |
| MCP doc governance | Tech Writer / Architect | CTO | QA Lead | All stakeholders |

## Review Cadence
| Cadence | Required Review | Trigger |
|---|---|---|
| Monthly | PRD and risk register review | New feature, incident, or legal interpretation change |
| Quarterly | DR drill evidence and control effectiveness | Completed DR exercise |
| Annual | SARS/BCEA/POPIA legal reference validation | New tax year or updated gazette/official guidance |
| Ad hoc | Emergency update | Critical defect, legal notice, regulator directive |

## Assumptions Register
| ID | Assumption | Default | Validation Owner |
|---|---|---|---|
| ASSUME-001 | Runtime stack remains ASP.NET Core (.NET 10) + Firestore | Fixed for v1 | Solution Architect |
| ASSUME-002 | MCP consumer is Claude Desktop | Primary | Product Manager |
| ASSUME-003 | Initial operating scale is SME (<500 employees) | Fixed for v1 | Product Manager |
| ASSUME-004 | Cloud-first deployment with SA governance controls | Fixed for v1 | CTO |
| ASSUME-005 | Single legal entity in launch scope | Fixed for v1 | CFO |
| ASSUME-006 | Payroll cadence is monthly | Fixed for v1 | Payroll Lead |
| ASSUME-007 | Currency is ZAR only in v1 | Fixed for v1 | CFO |
| ASSUME-008 | External counsel performs annual legal sign-off | Required | Compliance Officer |
| ASSUME-009 | No biometric processing in v1 | Fixed for v1 | Security Lead |
| ASSUME-010 | Statutory rate tables are configuration data, not hardcoded | Required | Engineering Manager |

## Source References and Validation Metadata
| Source | URL | Purpose | Last Validated |
|---|---|---|---|
| Foundational architecture document | Local file in Downloads | Baseline product and innovation scope | 2026-02-18 |
| SARS PAYE overview | https://www.sars.gov.za/types-of-tax/pay-as-you-earn/ | PAYE/UIF/SDL/ETI and EMP obligations context | 2026-02-18 |
| SARS employer declarations | https://www.sars.gov.za/types-of-tax/pay-as-you-earn/completing-and-submitting-employer-declarations/ | EMP201/EMP501 process requirements | 2026-02-18 |
| SARS tax certificate guide | https://www.sars.gov.za/guide-for-completion-and-submission-of-employees-tax-certificates-2025/ | IRP5/IT3(a) format and validation baseline | 2026-02-18 |
| BCEA Act page | https://www.gov.za/documents/basic-conditions-employment-act | Legal anchor for labor controls | 2026-02-18 |
| BCEA Act PDF | https://www.gov.za/sites/default/files/gcis_document/201409/a75-97.pdf | Section-level control mapping (working time, leave, records, remuneration) | 2026-02-18 |
| POPIA Act page | https://www.gov.za/documents/protection-personal-information-act | Legal anchor for privacy controls | 2026-02-18 |
| POPIA Act PDF | https://www.gov.za/sites/default/files/gcis_document/201409/3706726-11act4of2013popi.pdf | Section-level mapping for lawful processing, safeguards, breach handling, cross-border transfer | 2026-02-18 |
| Information Regulator POPIA portal | https://inforegulator.org.za/popia/ | Operational reporting guidance for security compromises | 2026-02-18 |

## Governance Rules
1. No `TBD`, `TBA`, or unresolved placeholders are permitted.
2. No implementation may proceed unless impacted `REQ-*` entries are trace-linked in `11_traceability_matrix.md`.
3. Any legal interpretation change requires:
- update to `06_compliance_sars_bcea.md`
- new/updated test in `08_testing_quality.md`
- `ADR` or `RISK` update in `12_risks_decisions.md`
4. Compliance-critical releases are blocked if any required evidence artifact is missing.

## Legal Disclaimer
This PRD package is technical implementation guidance and not legal advice. Final statutory interpretation and filing decisions require qualified legal/tax counsel approval.
