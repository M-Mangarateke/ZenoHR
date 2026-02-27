---
doc_id: PRD-03-ARCH
version: 1.0.0
owner: Solution Architect
updated_on: 2026-02-18
applies_to:
  - System architecture and deployment design
depends_on:
  - PRD-02-DOMAIN
  - PRD-05-SEC
requirements:
  - REQ-OPS-001
  - REQ-OPS-002
  - REQ-OPS-014
  - REQ-SEC-004
---

# Architecture Specification

## Locked Decisions
1. Runtime stack is fixed: ASP.NET Core (.NET 10) + Firebase Firestore.
2. Architecture style is modular monolith for v1 with strict internal module boundaries.
3. Internal notifications are event-driven; transactional operations remain synchronous APIs.
4. Compliance-relevant records are immutable; corrections use compensating records.
5. Tax and compliance rules are configuration-as-data with signed versioning.

## C4 Level 1: System Context
- Primary users: HR Admin, Payroll Officer, Compliance Officer, Manager, Employee, Auditor.
- External systems:
- SARS filing channels (manual/assisted operational submission path).
- Optional e-mail/SMS provider for notifications.
- Identity provider for MFA and enterprise auth controls.

## C4 Level 2: Container View
1. `Web Portal` (ASP.NET Core frontend + API gateway endpoints)
2. `Application API` (ASP.NET Core modular services)
3. `Domain Modules` (Payroll, Leave, Employee, Compliance, Audit, Risk)
4. `Rules Engine` (tax/BCEA/POPIA rule evaluation against signed config versions)
5. `Firestore` (source-of-truth storage)
6. `Cache Layer` (in-memory/distributed cache for reference and derived reads)
7. `Background Workers` (accrual, reminders, scheduled validations, evidence generation)
8. `Observability Stack` (logs, metrics, traces, alerts)

## C4 Level 3: Module Boundaries

### Employee Module
Responsibilities:
- Employee lifecycle, contract history, profile validation.

Owned data collections:
- `employees`
- `employment_contracts`

### Time Module
Responsibilities:
- Weekly time aggregation, overtime validation, approval workflow.

Owned collections:
- `timesheets`
- `time_entries`

### Leave Module
Responsibilities:
- Leave policy application, accrual postings, request lifecycle.

Owned collections:
- `leave_balances`
- `leave_requests`
- `leave_accrual_ledger`

### Payroll Module
Responsibilities:
- Payroll run lifecycle, statutory deductions, payslip composition.

Owned collections:
- `payroll_runs`
- `payroll_results`
- `payroll_adjustments`

### Compliance Module
Responsibilities:
- Control checks, filing package generation, blocking invalid submissions.

Owned collections:
- `statutory_rule_sets`
- `submission_packages`
- `compliance_results`

### Audit Module
Responsibilities:
- Immutable audit chain, evidence bundle construction, integrity verification.

Owned collections:
- `audit_events`
- `evidence_bundles`

### Risk Module
Responsibilities:
- Deadline risk scoring, fine exposure estimation, action prioritization.

Owned collections:
- `risk_alerts`
- `risk_scores`

## Integration Contracts
1. Module-to-module calls must go through published internal interfaces.
2. No direct cross-module writes to another module's collections.
3. Domain events are immutable and versioned.
4. Any integration failure with compliance impact must emit alert with `SEV-2` or higher.

## Deployment Architecture (Cloud-first, SA controls)
1. Regional deployment aligned to South African data-governance requirements.
2. Network segmentation:
- Public ingress: API gateway and static web assets.
- Private compute: application services and workers.
- Data plane: Firestore and key management services.
3. Secrets and keys stored in managed secret store; no plaintext in code or CI logs.
4. Backup snapshots and encrypted archives with tested restore path.

## Data Flow (Payroll Finalization)
1. Collect approved timesheets and leave adjustments.
2. Load active statutory rule set and tax tables by effective date.
3. Execute payroll calculation with deterministic rounding.
4. Run compliance validators (BCEA/SARS/POPIA checks).
5. If all critical checks pass: approve and finalize payroll.
6. Write immutable audit records and generate payslip outputs.
7. Queue compliance package generation tasks (EMP201/EMP501 as applicable).

## Failure Modes and Architectural Responses
| Failure Mode | Detection | Response | Evidence |
|---|---|---|---|
| Stale tax table version | Rule checksum mismatch | Block run, require signed update | `EV-COMP-004` |
| Firestore write conflict | Transaction failure metrics | Retry with idempotency token; on exhaustion fail safely | `EV-OPS-005` |
| Missing tax references | Validation error in submission prep | Block submission generation | `EV-SARS-006` |
| Audit chain break | Hash continuity check | Raise `SEV-1`, freeze affected workflow | `EV-SEC-006` |
| Cache staleness over budget | Cache age metric breach | Force refresh and degrade to direct read | `EV-CACHE-003` |

## Capacity Envelope (Launch)
- Employee count: up to 500 active employees.
- Payroll finalization SLA: <=15 minutes for full monthly run at launch scale.
- Report generation SLA: <=5 minutes for EMP package generation.
- Concurrent users: 100 interactive users peak.

## Architecture Constraints for Implementation Agents
1. No runtime bypass of compliance validators.
2. No mutable updates for finalized payroll records.
3. No undocumented external dependency introduction.
4. Any module boundary change requires new ADR and updated traceability entries.
