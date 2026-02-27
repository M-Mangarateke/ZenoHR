---
doc_id: PRD-10-ROLLOUT
version: 1.0.0
owner: Program Manager
updated_on: 2026-02-18
applies_to:
  - Release sequencing, rollout controls, and change management
depends_on:
  - PRD-01-EXECUTIVE
  - PRD-08-TEST
  - PRD-09-OPS
requirements:
  - REQ-OPS-010
  - REQ-OPS-011
---

# Rollout and Change Management

## Rollout Strategy
Delivery follows phased gates to minimize compliance and operational risk:
1. Phase 0: Foundation
2. Phase 1: Internal MVP
3. Phase 2: Controlled Production Rollout
4. Phase 3: Full Production

## Phase Definitions

### Phase 0: Foundation
Objectives:
1. Finalize contracts, controls, and test harness.
2. Establish observability and quality gates.
3. Validate baseline legal mappings with Compliance Officer.

Exit criteria:
- `REQ-*` and `TC-*` baseline published.
- CI/CD policy gates active.
- Security and compliance runbooks available.

### Phase 1: Internal MVP
Scope:
1. Employee records
2. Payroll calculation
3. Payslips
4. Leave management
5. Core BCEA and SARS validations

Entry criteria:
- phase 0 exit criteria met.

Exit criteria:
- mandatory MVP test pack pass
- no open Sev-1/Sev-2 defects
- payroll reconciliation sign-off in pilot environment

### Phase 2: Controlled Production Rollout
Scope additions:
1. EMP501 and IRP5/IT3(a) flows
2. ETI validator
3. Travel reimbursement
4. Compliance dashboard and risk estimator

Controls:
1. Limited user cohort with explicit onboarding plan.
2. Daily compliance and telemetry review during first filing cycle.

Exit criteria:
- successful filing-cycle rehearsal
- backup/restore test pass in production-like environment
- compliance evidence pack generated successfully

### Phase 3: Full Production
Scope:
1. Full operational ownership and normal release cadence.
2. Quarterly DR and annual legal validation cycles enforced.

Entry criteria:
- all phase 2 exit criteria met
- executive go-live approval

## Migration and Data Change Rules
1. Schema changes must be backward compatible within a release window.
2. Compliance-critical data migrations require:
- dry run report
- rollback script
- post-migration integrity verification
3. Finalized payroll records are never rewritten by migration scripts.
4. Any migration touching regulated fields requires Compliance Officer sign-off.

## Change Control Policy
1. Every compliance-impacting change requires:
- updated requirement references
- updated tests
- updated traceability entries
- ADR or risk register update
2. Emergency changes follow expedited path but require retrospective review within 2 business days.
3. Feature flags required for high-impact logic releases (tax engine, submission formatting, payroll finalization flow).

## Communication Plan
Stakeholder communications by phase:
1. Weekly implementation checkpoint to Product, Compliance, and Engineering.
2. Filing-cycle readiness review before each major release.
3. Release notes must include:
- changed controls
- changed requirement IDs
- known risks and mitigations

## Training Plan
Role-based training:
1. HR Admin: profile and leave workflows.
2. Payroll Officer: payroll run, approvals, exception handling.
3. Compliance Officer: control dashboard, submission pack, evidence export.
4. Auditor: evidence retrieval and audit trail navigation.
5. Operations: incident handling, backup restore, and DR response.

Training artifacts:
- quick reference guides
- workflow videos
- sandbox exercises
- competency sign-off checklist

## Rollback Triggers
Automatic rollback trigger conditions:
1. Sev-1 incident linked to payroll correctness or data integrity.
2. Compliance control false-negative in production.
3. Security compromise in privileged workflow.
4. Filing artifact generation corruption or validation failure.

Rollback procedure requirements:
1. Preserve forensic evidence.
2. Revert via approved deployment mechanism.
3. Execute post-rollback validation suite.
4. Communicate status and impact within 30 minutes to critical stakeholders.

## Release Governance Board
Members:
- Product Manager
- Engineering Manager
- Compliance Officer
- Security Lead
- QA Lead

Decision inputs:
1. quality gate report
2. unresolved risk profile
3. legal-control status
4. operations readiness status

## Evidence Artifacts
- `EV-REL-001`: phase gate checklist sign-offs.
- `EV-REL-002`: migration dry run and rollback validation reports.
- `EV-REL-003`: training completion records.
- `EV-REL-004`: release governance board decision logs.
- `EV-REL-005`: rollback drill reports.
