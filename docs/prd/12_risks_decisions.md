---
doc_id: PRD-12-RISK-ADR
version: 1.0.0
owner: Solution Architect; Compliance Officer
updated_on: 2026-02-18
applies_to:
  - Architectural decisions and risk governance
depends_on:
  - PRD-03-ARCH
  - PRD-06-COMP
  - PRD-10-ROLLOUT
requirements:
  - REQ-OPS-011
  - REQ-OPS-014
  - REQ-COMP-015
---

# Architecture Decisions and Risk Register

## ADR Log

### ADR-001: Stack Lock (.NET 10 + Firestore)
- Status: Accepted
- Decision date: 2026-02-18
- Context: Foundational architecture document specifies ASP.NET Core (.NET 10) with Firebase Firestore.
- Decision: Keep stack fixed for v1 to reduce delivery risk and assumption surface.
- Consequences:
1. Faster implementation alignment.
2. Need strong data-model governance for document DB integrity.
3. Future relational migration remains optional and out of scope for v1.
- Revisit date: 2027-02-18

### ADR-002: Modular Monolith First
- Status: Accepted
- Decision date: 2026-02-18
- Context: SME scale target and strict compliance needs favor reduced distributed complexity.
- Decision: Implement modular monolith with explicit internal boundaries.
- Consequences:
1. Lower operational overhead.
2. Requires strong module interface discipline.
3. Future extraction path should be designed, not assumed.
- Revisit date: 2027-02-18

### ADR-003: Immutable Compliance Records
- Status: Accepted
- Decision date: 2026-02-18
- Context: Payroll/compliance auditability requires tamper-evident history.
- Decision: Finalized payroll and filing records are append-only; corrections are compensating entries.
- Consequences:
1. Strong audit posture.
2. Added complexity for correction workflows.
- Revisit date: 2027-02-18

### ADR-004: Signed Configuration-as-Data for Statutory Rules
- Status: Accepted
- Decision date: 2026-02-18
- Context: Tax and legal rules change over time and must be traceable.
- Decision: Rule/tax table changes use signed, versioned artifacts and controlled activation.
- Consequences:
1. Safer updates without full redeploy.
2. Requires robust activation pipeline and regression suite.
- Revisit date: 2026-08-18

### ADR-005: Dual Authorization for Payroll Finalization
- Status: Accepted
- Decision date: 2026-02-18
- Context: Separation of duties and fraud prevention needs.
- Decision: Finalization requires `PayrollOfficer` + `ComplianceOfficer`.
- Consequences:
1. Stronger governance.
2. Requires fallback staffing plan for absences.
- Revisit date: 2027-02-18

### ADR-006: Cloud-first Deployment with SA Governance Controls
- Status: Accepted
- Decision date: 2026-02-18
- Context: Need scalable operations while complying with South African legal controls.
- Decision: Cloud deployment with explicit data-governance and cross-border restrictions.
- Consequences:
1. Faster scaling.
2. Ongoing governance required for data transfer and providers.
- Revisit date: 2026-11-18

## Risk Register
| Risk ID | Risk Description | Likelihood | Impact | Mitigation | Owner | Status | Review Date |
|---|---|---|---|---|---|---|---|
| `RISK-001` | Incorrect tax table activation causes payroll miscalculation | Medium | Critical | Signed artifacts, checksum validation, mandatory regression pack, staged rollout | Payroll Lead | Open | 2026-03-18 |
| `RISK-002` | Legal interpretation drift on BCEA/POPIA obligations | Medium | High | Annual counsel review, monthly regulatory monitoring, change-impact workflow | Compliance Officer | Open | 2026-03-18 |
| `RISK-003` | Privilege escalation in approval workflows | Low | Critical | MFA, RBAC hardening, SoD checks, anomaly alerts | Security Lead | Open | 2026-03-18 |
| `RISK-004` | Audit log integrity corruption | Low | Critical | Hash-chain validation, immutable storage policy, Sev-1 incident trigger | SRE Lead | Open | 2026-03-18 |
| `RISK-005` | Missed EMP201/EMP501 filing due to operational failure | Medium | High | Deadline alerts, readiness dashboards, backup approver workflow | Compliance Officer | Open | 2026-03-18 |
| `RISK-006` | Cache staleness impacts legal-reference reads | Medium | High | Strict no-cache on finalization path, staleness monitoring, forced refresh | Performance Engineer | Open | 2026-03-18 |
| `RISK-007` | Data breach involving payroll PII | Low | Critical | Encryption, masking, DLP controls, breach response runbook | CISO | Open | 2026-03-18 |
| `RISK-008` | Single-point dependency on key personnel during approvals | Medium | Medium | Delegation matrix, cross-training, backup approvers | Program Manager | Open | 2026-03-18 |
| `RISK-009` | Scope expansion introduces hidden technical debt | Medium | High | Requirement gatekeeping, ADR discipline, phased scope control | Product Manager | Open | 2026-03-18 |
| `RISK-010` | DR capabilities untested before incident | Low | High | Quarterly DR drills with sign-off and remediation tracking | SRE Lead | Open | 2026-03-18 |

## Risk Scoring Method
- Likelihood: `Low`, `Medium`, `High`
- Impact: `Medium`, `High`, `Critical`
- Escalation rules:
1. Any `Critical` impact risk requires named mitigation owner and due date.
2. Any overdue mitigation with `High/Critical` impact blocks relevant release.

## Risk Acceptance Policy
1. High/Critical risks cannot be silently accepted.
2. Accepted risks require:
- documented rationale
- compensating controls
- expiration date
- accountable executive sign-off
3. Expired risk acceptance without renewal triggers release block.

## Decision Change Protocol
When an accepted ADR changes:
1. Create new ADR entry with supersedes link.
2. Update impacted requirements and tests.
3. Update traceability matrix and evidence expectations.
4. Communicate change in release notes and governance review.

## Evidence Artifacts
- `EV-ADR-001`: ADR index and approval records.
- `EV-RISK-001`: monthly risk review minutes.
- `EV-RISK-002`: mitigation completion proofs.
- `EV-RISK-003`: risk acceptance approvals and expiries.
