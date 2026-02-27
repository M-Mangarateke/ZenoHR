---
doc_id: PRD-08-TEST
version: 1.0.0
owner: QA Lead
updated_on: 2026-02-18
applies_to:
  - Test strategy, quality controls, and release gates
depends_on:
  - PRD-04-API
  - PRD-05-SEC
  - PRD-06-COMP
requirements:
  - REQ-OPS-009
  - REQ-OPS-010
  - REQ-COMP-016
---

# Testing and Quality Specification

## Quality Objective
Implement strict quality gates to operationalize the business objective of near-zero defect leakage and zero known compliance-critical failures in production.

## Test Layers
1. Unit tests
- pure domain logic, calculations, and invariants.
2. Property-based tests
- payroll arithmetic consistency and boundary ranges.
3. Integration tests
- module interactions, persistence behavior, workflow transitions.
4. Contract tests
- request/response schema compatibility and event schema evolution.
5. End-to-end tests
- full user and compliance workflows.
6. Security tests
- authz/authn abuse, privilege escalation, injection, secret exposure paths.
7. Performance tests
- load, stress, soak, and endurance profiles.
8. Resilience tests
- dependency failures, retry behavior, idempotency correctness.

## Coverage Policy
| Coverage Type | Minimum | Rule |
|---|---|---|
| Domain/business logic | 90% line coverage | Compliance-critical modules cannot drop below threshold |
| Branch coverage for calculation engines | 85% | Boundary and exception paths mandatory |
| Contract coverage | 100% of public endpoints/events | Every external contract must have tests |
| Requirement trace coverage | 100% `REQ-*` mapped | Unmapped requirements block release |

## Mandatory Test Scenarios
| Test ID | Requirement Link | Scenario | Expected Result |
|---|---|---|---|
| `TC-BCEA-001` | `REQ-COMP-007` | Weekly ordinary hours above legal policy | Block or route to policy exception workflow |
| `TC-BCEA-002` | `REQ-COMP-008` | Overtime computation for eligible employee | Overtime computed at configured legal multiplier and displayed on payslip |
| `TC-BCEA-003` | `REQ-COMP-009` | Annual leave accrual cycle | Balance accrues correctly by policy version |
| `TC-BCEA-004` | `REQ-COMP-011` | Records retention tagging | Required records tagged and non-destructive archival path validated |
| `TC-BCEA-005` | `REQ-COMP-010` | Payslip remuneration detail rendering | Payslip contains required remuneration information fields |
| `TC-BCEA-006` | `REQ-COMP-017` | Termination notice and severance calculation | Settlement output matches active policy/legal rules and blocks invalid cases |
| `TC-SARS-001` | `REQ-COMP-001` | PAYE boundary salary band cases | Calculations match active table values |
| `TC-SARS-002` | `REQ-COMP-006` | Missing/invalid tax number before export | Submission generation blocked |
| `TC-SARS-003` | `REQ-COMP-002` | UIF/SDL cap and rate checks | Statutory contribution output is correct |
| `TC-SARS-004` | `REQ-COMP-003` | EMP201 package generation cadence | Package generated with due-date metadata |
| `TC-SARS-005` | `REQ-COMP-004` | EMP501 reconciliation mismatch simulation | Mismatch flagged and release blocked |
| `TC-SARS-006` | `REQ-COMP-005` | IRP5/IT3(a) schema validation | Invalid records rejected with explicit errors |
| `TC-SARS-007` | `REQ-HR-010` | ETI eligibility edge cases | Correct eligible/ineligible classification |
| `TC-SARS-008` | `REQ-HR-009` | Travel claim with SARS rate updates | Reimbursement reflects effective rate version |
| `TC-POPIA-001` | `REQ-SEC-002` | Unauthorized role reads restricted payroll fields | Access denied and event logged |
| `TC-POPIA-002` | `REQ-COMP-012` | Missing lawful basis metadata on data operation | Operation blocked |
| `TC-POPIA-003` | `REQ-COMP-014` | Encryption/masking verification | Restricted fields encrypted and masked as policy |
| `TC-POPIA-004` | `REQ-COMP-014` | Security compromise workflow | Breach workflow and notifications start with full audit trail |
| `TC-POPIA-005` | `REQ-COMP-013` | Data subject correction request | Corrected data and immutable correction history created |
| `TC-POPIA-006` | `REQ-COMP-014` | Cross-border transfer without legal basis | Transfer blocked and exception recorded |
| `TC-POPIA-007` | `REQ-COMP-011` | Retention expiry with legal hold | Legal hold prevents destructive purge |
| `TC-AUD-001` | `REQ-SEC-006` | Payroll mutation event logging | Immutable audit event generated with hash chain |
| `TC-CACHE-001` | `REQ-OPS-003` | Tax table activation cache behavior | Dependent caches invalidated and stale data not served |
| `TC-DR-001` | `REQ-OPS-007` | Backup restore drill | Restore meets RPO/RTO and integrity checks |
| `TC-OPS-001` | `REQ-OPS-009` | CI/CD policy gate | Build fails when critical scans/tests fail |
| `TC-OPS-002` | `REQ-OPS-010` | Release gate simulation | Release blocked if any critical gate fails |

## Supplemental Test Catalog (Traceability Completion)
These tests are required by `PRD-11-TRACE` and must be implemented even when not part of the mandatory release smoke pack.

| Test ID | Scope | Scenario |
|---|---|---|
| `TC-HR-001` | HR | Employee master data validation and mandatory field enforcement |
| `TC-HR-002` | HR | Non-overlapping employment contract effective periods |
| `TC-HR-003` | HR | Monthly payroll run lifecycle from draft to calculated status |
| `TC-HR-006` | HR | Leave request approval workflow with atomic ledger update |
| `TC-HR-007` | HR | Time entry completeness and approval preconditions |
| `TC-HR-011` | HR | Risk dashboard prioritization by legal due date |
| `TC-HR-012` | HR | Compensating adjustment behavior after finalized payroll |
| `TC-HR-013` | HR | Employee self-service access boundaries |
| `TC-SEC-001` | Security | MFA enforcement on privileged endpoints |
| `TC-SEC-003` | Security | Separation-of-duties enforcement on finalization |
| `TC-SEC-005` | Security | Key rotation process and break-glass auditability |
| `TC-SEC-007` | Security | Sensitive field masking in UI and logs |
| `TC-SEC-008` | Security | Secret management policy (no plaintext secrets) |
| `TC-SEC-010` | Security | Incident-response workflow and evidence completeness |
| `TC-ARCH-001` | Architecture | Module boundary conformance validation |
| `TC-ARCH-002` | Architecture | Internal event publication and consumption reliability |
| `TC-CACHE-002` | Caching | Finalization path bypasses cache in all cases |
| `TC-DR-002` | Resilience | Quarterly DR simulation and operational recovery validation |
| `TC-OPS-003` | Operations | Tax-year rule activation governance workflow |
| `TC-OPS-004` | Operations | Evidence pack generation completeness and integrity |
| `TC-OPS-005` | Operations | Structured telemetry schema conformance |
| `TC-OPS-006` | Operations | SLO breach detection and error-budget policy enforcement |
| `TC-OPS-011` | Operations | ADR update requirement for compliance-impacting changes |
| `TC-OPS-012` | Operations | MCP metadata presence and schema validation on all PRD files |
| `TC-OPS-013` | Operations | Performance budgets validated under launch-scale load |
| `TC-OPS-014` | Operations | Capacity envelope validation for SME target |
| `TC-OPS-015` | Operations | Annual legal reference review cadence enforcement |

## Defect Severity Model
| Severity | Description | SLA |
|---|---|---|
| Sev-1 | Compliance/security/data integrity failure in production | Immediate response, fix or rollback <=4 hours |
| Sev-2 | Major functional failure with legal or payroll impact | Fix <=1 business day |
| Sev-3 | Non-critical functional defect | Fix <=1 sprint |
| Sev-4 | Cosmetic/documentation issue | Backlog prioritization |

## Release Blocking Rules
Release is blocked if any of the following is true:
1. Any mandatory test above fails.
2. Any high/critical security finding remains open without approved exception.
3. Requirement traceability <100%.
4. Evidence artifact set for impacted controls is incomplete.
5. Performance SLO tests breach thresholds on production-equivalent environment.

## Defect Prevention Controls
1. Pair review required for compliance-critical logic.
2. Mutation testing on payroll computation module.
3. Golden dataset regression tests for tax-year updates.
4. Static schema diff checks for public API and event contracts.
5. Continuous fuzzing for critical input validation endpoints.

## Test Data Management
1. Only synthetic/anonymized data in non-production environments.
2. Deterministic seed datasets for payroll calculations.
3. Test data versioning linked to rule version and tax year.
4. Reproducibility guarantee: any failed CI test can be replayed locally with provided seed.

## CI/CD Quality Pipeline
Pipeline stages:
1. Lint and static checks.
2. Unit + property tests.
3. Integration + contract tests.
4. Security scans (SAST, dependency, secrets).
5. E2E smoke pack.
6. Performance and resilience smoke checks.
7. Compliance evidence artifact generation.

Any failed mandatory stage blocks merge or release.

## Reporting and Metrics
- test pass rate by suite
- escaped defect count by severity
- mean time to detect (MTTD)
- mean time to resolve (MTTR)
- requirement coverage ratio
- flaky test ratio

Targets:
1. Flaky tests <1%.
2. Escaped Sev-1 and Sev-2 defects: 0.
3. Requirement coverage ratio: 100%.

## Evidence Artifacts
- `EV-TEST-001`: full test execution report by build.
- `EV-TEST-002`: requirement-to-test mapping snapshot.
- `EV-TEST-003`: quality gate decision log.
- `EV-TEST-004`: performance test benchmark report.
- `EV-TEST-005`: security test results and remediation status.
