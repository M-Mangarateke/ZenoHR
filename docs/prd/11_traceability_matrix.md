---
doc_id: PRD-11-TRACE
version: 1.0.0
owner: QA Lead; Solution Architect
updated_on: 2026-02-18
applies_to:
  - End-to-end traceability across requirements, controls, tests, and evidence
depends_on:
  - PRD-00-MANIFEST
  - PRD-08-TEST
requirements:
  - REQ-HR-001
  - REQ-COMP-001
  - REQ-SEC-001
  - REQ-OPS-001
---

# Traceability Matrix

## Traceability Rules
1. Every `REQ-*` entry must map to at least one test and one evidence artifact.
2. Compliance-critical requirements must also map to at least one control ID.
3. Missing mapping is a release blocker.

## Matrix
| Requirement | Summary | Primary Spec Refs | Control IDs | Test IDs | Evidence IDs |
|---|---|---|---|---|---|
| `REQ-HR-001` | Employee master data | `02_domain_model.md`, `04_api_contracts.md` | `CTL-POPIA-001`, `CTL-POPIA-004` | `TC-HR-001` | `EV-HR-001` |
| `REQ-HR-002` | Contract lifecycle | `02_domain_model.md` | `CTL-POPIA-001` | `TC-HR-002` | `EV-HR-002` |
| `REQ-HR-003` | Monthly payroll run | `03_architecture.md`, `04_api_contracts.md` | `CTL-SARS-001` | `TC-HR-003` | `EV-HR-003` |
| `REQ-HR-004` | Payslip generation | `04_api_contracts.md` | `CTL-BCEA-005` | `TC-BCEA-005` | `EV-BCEA-005` |
| `REQ-HR-005` | Leave accrual | `02_domain_model.md` | `CTL-BCEA-003` | `TC-BCEA-003` | `EV-BCEA-003` |
| `REQ-HR-006` | Leave workflow | `02_domain_model.md`, `04_api_contracts.md` | `CTL-BCEA-003` | `TC-HR-006` | `EV-HR-006` |
| `REQ-HR-007` | Time capture quality | `02_domain_model.md`, `04_api_contracts.md` | `CTL-BCEA-001` | `TC-HR-007`, `TC-BCEA-001` | `EV-BCEA-001` |
| `REQ-HR-008` | Overtime computation | `02_domain_model.md`, `04_api_contracts.md` | `CTL-BCEA-002` | `TC-BCEA-002` | `EV-BCEA-002` |
| `REQ-HR-009` | Travel reimbursement | `04_api_contracts.md`, `06_compliance_sars_bcea.md` | `CTL-SARS-008` | `TC-SARS-008` | `EV-SARS-008` |
| `REQ-HR-010` | ETI validator | `06_compliance_sars_bcea.md` | `CTL-SARS-007` | `TC-SARS-007` | `EV-SARS-007` |
| `REQ-HR-011` | Compliance risk dashboard | `03_architecture.md`, `04_api_contracts.md` | `CTL-SARS-003` | `TC-HR-011` | `EV-HR-011` |
| `REQ-HR-012` | Compensating adjustments | `02_domain_model.md` | `CTL-SARS-001`, `CTL-BCEA-004` | `TC-HR-012` | `EV-HR-012` |
| `REQ-HR-013` | Employee self-service | `01_executive_prd.md`, `04_api_contracts.md` | `CTL-POPIA-002` | `TC-HR-013` | `EV-HR-013` |
| `REQ-HR-014` | Termination notice and severance calculations | `02_domain_model.md`, `04_api_contracts.md` | `CTL-BCEA-006` | `TC-BCEA-006` | `EV-BCEA-006` |
| `REQ-COMP-001` | PAYE calculation | `06_compliance_sars_bcea.md` | `CTL-SARS-001` | `TC-SARS-001` | `EV-SARS-001` |
| `REQ-COMP-002` | UIF/SDL contribution calculation | `06_compliance_sars_bcea.md` | `CTL-SARS-002` | `TC-SARS-003` | `EV-SARS-002` |
| `REQ-COMP-003` | EMP201 generation | `04_api_contracts.md`, `06_compliance_sars_bcea.md` | `CTL-SARS-003` | `TC-SARS-004` | `EV-SARS-003` |
| `REQ-COMP-004` | EMP501 reconciliation | `04_api_contracts.md`, `06_compliance_sars_bcea.md` | `CTL-SARS-004` | `TC-SARS-005` | `EV-SARS-004` |
| `REQ-COMP-005` | IRP5/IT3(a) generation | `04_api_contracts.md`, `06_compliance_sars_bcea.md` | `CTL-SARS-005` | `TC-SARS-006` | `EV-SARS-005` |
| `REQ-COMP-006` | Mandatory tax reference validation | `04_api_contracts.md`, `06_compliance_sars_bcea.md` | `CTL-SARS-006` | `TC-SARS-002` | `EV-SARS-006` |
| `REQ-COMP-007` | BCEA ordinary-hours checks | `06_compliance_sars_bcea.md` | `CTL-BCEA-001` | `TC-BCEA-001` | `EV-BCEA-001` |
| `REQ-COMP-008` | BCEA overtime rules | `06_compliance_sars_bcea.md` | `CTL-BCEA-002` | `TC-BCEA-002` | `EV-BCEA-002` |
| `REQ-COMP-009` | BCEA leave entitlement | `06_compliance_sars_bcea.md` | `CTL-BCEA-003` | `TC-BCEA-003` | `EV-BCEA-003` |
| `REQ-COMP-010` | BCEA remuneration information | `06_compliance_sars_bcea.md` | `CTL-BCEA-005` | `TC-BCEA-005` | `EV-BCEA-005` |
| `REQ-COMP-011` | Record keeping and retention | `02_domain_model.md`, `06_compliance_sars_bcea.md` | `CTL-BCEA-004`, `CTL-POPIA-006` | `TC-BCEA-004`, `TC-POPIA-007` | `EV-BCEA-004`, `EV-POPIA-006` |
| `REQ-COMP-012` | POPIA lawful processing | `05_security_privacy.md`, `06_compliance_sars_bcea.md` | `CTL-POPIA-001` | `TC-POPIA-002` | `EV-POPIA-001` |
| `REQ-COMP-013` | POPIA data subject rights | `05_security_privacy.md`, `06_compliance_sars_bcea.md` | `CTL-POPIA-004` | `TC-POPIA-005` | `EV-POPIA-004` |
| `REQ-COMP-014` | POPIA safeguards and breach response | `05_security_privacy.md`, `06_compliance_sars_bcea.md` | `CTL-POPIA-002`, `CTL-POPIA-003` | `TC-POPIA-003`, `TC-POPIA-004` | `EV-POPIA-002`, `EV-POPIA-003` |
| `REQ-COMP-015` | Tax-year update governance | `04_api_contracts.md`, `06_compliance_sars_bcea.md` | `CTL-SARS-001` | `TC-CACHE-001`, `TC-OPS-003` | `EV-COMP-004` |
| `REQ-COMP-016` | Evidence pack generation | `06_compliance_sars_bcea.md`, `08_testing_quality.md` | `CTL-SARS-003`, `CTL-BCEA-004`, `CTL-POPIA-003` | `TC-OPS-004` | `EV-COMP-016` |
| `REQ-COMP-017` | Notice and severance compliance checks | `04_api_contracts.md`, `06_compliance_sars_bcea.md` | `CTL-BCEA-006` | `TC-BCEA-006` | `EV-BCEA-006` |
| `REQ-SEC-001` | Authentication and MFA | `04_api_contracts.md`, `05_security_privacy.md` | `CTL-POPIA-002` | `TC-SEC-001` | `EV-SEC-002` |
| `REQ-SEC-002` | RBAC least privilege | `05_security_privacy.md` | `CTL-POPIA-002` | `TC-POPIA-001` | `EV-SEC-001` |
| `REQ-SEC-003` | Separation of duties | `02_domain_model.md`, `05_security_privacy.md` | `CTL-POPIA-002` | `TC-SEC-003` | `EV-SEC-001` |
| `REQ-SEC-004` | Encryption in transit/at rest | `03_architecture.md`, `05_security_privacy.md` | `CTL-POPIA-002` | `TC-POPIA-003` | `EV-SEC-003` |
| `REQ-SEC-005` | Key lifecycle and rotation | `05_security_privacy.md` | `CTL-POPIA-002` | `TC-SEC-005` | `EV-SEC-003` |
| `REQ-SEC-006` | Immutable audit logging | `02_domain_model.md`, `05_security_privacy.md` | `CTL-POPIA-002` | `TC-AUD-001` | `EV-SEC-006` |
| `REQ-SEC-007` | Sensitive data masking | `05_security_privacy.md` | `CTL-POPIA-002` | `TC-SEC-007` | `EV-SEC-007` |
| `REQ-SEC-008` | Secret/config management | `03_architecture.md`, `05_security_privacy.md` | `CTL-POPIA-002` | `TC-SEC-008` | `EV-SEC-004` |
| `REQ-SEC-009` | Secure SDLC scanning | `05_security_privacy.md`, `08_testing_quality.md` | `CTL-POPIA-002` | `TC-OPS-001` | `EV-TEST-005` |
| `REQ-SEC-010` | Incident response readiness | `05_security_privacy.md`, `09_observability_ops.md` | `CTL-POPIA-003` | `TC-SEC-010` | `EV-SEC-005`, `EV-OPS-004` |
| `REQ-OPS-001` | Modular monolith boundaries | `03_architecture.md` | N/A | `TC-ARCH-001` | `EV-ADR-001` |
| `REQ-OPS-002` | Internal event-driven workflows | `03_architecture.md`, `04_api_contracts.md` | N/A | `TC-ARCH-002` | `EV-OPS-002` |
| `REQ-OPS-003` | Cache strategy with TTL/invalidation | `07_caching_performance.md` | N/A | `TC-CACHE-001` | `EV-CACHE-001`, `EV-CACHE-002` |
| `REQ-OPS-004` | No cache in finalization path | `07_caching_performance.md` | N/A | `TC-CACHE-002` | `EV-CACHE-003` |
| `REQ-OPS-005` | Structured telemetry | `09_observability_ops.md` | N/A | `TC-OPS-005` | `EV-OPS-001` |
| `REQ-OPS-006` | SLO and error budget controls | `07_caching_performance.md`, `09_observability_ops.md` | N/A | `TC-OPS-006` | `EV-OPS-003` |
| `REQ-OPS-007` | Backup and restore | `09_observability_ops.md` | N/A | `TC-DR-001` | `EV-OPS-005` |
| `REQ-OPS-008` | DR drills | `09_observability_ops.md` | N/A | `TC-DR-002` | `EV-OPS-006` |
| `REQ-OPS-009` | CI/CD quality gates | `08_testing_quality.md` | N/A | `TC-OPS-001` | `EV-TEST-003` |
| `REQ-OPS-010` | Phased release gating | `01_executive_prd.md`, `10_rollout_change_mgmt.md` | N/A | `TC-OPS-002` | `EV-REL-001` |
| `REQ-OPS-011` | ADR-governed change management | `10_rollout_change_mgmt.md`, `12_risks_decisions.md` | N/A | `TC-OPS-011` | `EV-REL-004` |
| `REQ-OPS-012` | MCP metadata and doc integrity | `00_manifest.md` | N/A | `TC-OPS-012` | `EV-OPS-012` |
| `REQ-OPS-013` | Performance budgets | `07_caching_performance.md` | N/A | `TC-OPS-013` | `EV-CACHE-004` |
| `REQ-OPS-014` | Capacity envelope and scaling path | `03_architecture.md`, `07_caching_performance.md` | N/A | `TC-OPS-014` | `EV-CACHE-004` |
| `REQ-OPS-015` | Annual legal review cadence | `00_manifest.md`, `06_compliance_sars_bcea.md` | N/A | `TC-OPS-015` | `EV-COMP-015` |

## Supplemental Evidence Catalog
| Evidence ID | Definition | Producing Owner |
|---|---|---|
| `EV-HR-001` | Employee data validation run report | HR Systems Team |
| `EV-HR-002` | Contract lifecycle integrity audit | HR Systems Team |
| `EV-HR-003` | Payroll run initiation and completion report | Payroll Team |
| `EV-HR-006` | Leave workflow audit report | HR Systems Team |
| `EV-HR-011` | Risk dashboard rule evaluation output | Compliance Analytics |
| `EV-HR-012` | Compensating adjustment integrity report | Payroll Team |
| `EV-HR-013` | Self-service access verification report | Security + QA |
| `EV-COMP-015` | Annual legal validation sign-off record | Compliance Officer + Legal Counsel |
| `EV-COMP-016` | Full compliance evidence pack manifest | Compliance Analyst |
| `EV-OPS-012` | MCP metadata conformance report for PRD pack | Architecture Office |
| `EV-SEC-007` | Masking and redaction verification report | Security Team |

## Traceability Audit Procedure
1. Run traceability validation script (or manual review checklist) on each release candidate.
2. Verify every changed `REQ-*` appears in:
- at least one updated test report
- at least one evidence artifact
3. Compliance Officer sign-off is mandatory if any `REQ-COMP-*` changed.
