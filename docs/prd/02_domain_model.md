---
doc_id: PRD-02-DOMAIN
version: 1.0.0
owner: Solution Architect
updated_on: 2026-02-18
applies_to:
  - Domain model and invariants
  - Data design and validation rules
depends_on:
  - PRD-01-EXECUTIVE
requirements:
  - REQ-HR-001
  - REQ-HR-012
  - REQ-COMP-011
  - REQ-OPS-001
---

# Domain Model

## Bounded Contexts
1. Employee Context
- Owns employee profile, tax identifiers, bank details, contract metadata, and employment status.

2. Time and Attendance Context
- Owns timesheets, weekly totals, overtime requests, and approval flow.

3. Leave Context
- Owns leave policy setup, accrual ledger, leave requests, and balance snapshots.

4. Payroll Context
- Owns payroll calendar, calculation runs, line items, deductions, net pay, and compensating adjustments.

5. Compliance Context
- Owns statutory rules, validation outcomes, filing packages, and compliance control status.

6. Audit and Evidence Context
- Owns immutable audit events, evidence bundles, and integrity proofs.

7. Risk and Insights Context
- Owns compliance risk indicators, deadline alerts, and fine estimation outputs.

## Core Aggregates and Entities

### Employee Aggregate
- `Employee`: employee_id, legal_name, national_id/passport, tax_reference, bank_account_ref, employment_status.
- `EmploymentContract`: contract_id, start_date, end_date, grade, salary_basis, ordinary_hours_policy.
- `TerminationCase`: termination_case_id, termination_reason, notice_period_days, severance_amount_zar, effective_termination_date.
- `EmployeeAddress`, `EmergencyContact`.

Invariants:
1. `employee_id` is immutable after creation.
2. `tax_reference` format must pass validation before payroll finalization.
3. Contract effective periods may not overlap for the same employee.
4. Termination calculations must reference active policy and legal-rule versions.

### Timesheet Aggregate
- `TimesheetWeek`: employee_id, week_start, entries[], total_ordinary_hours, total_overtime_hours, status.
- `TimeEntry`: date, hours, category, source, approval_state.

Invariants:
1. Ordinary hours cannot exceed configured legal policy unless explicit approved exception flow exists.
2. Overtime entries require explicit employee or manager agreement metadata.
3. Approved timesheets are immutable; corrections require adjustment entries.

### Leave Aggregate
- `LeaveBalance`: employee_id, leave_type, accrued_hours, consumed_hours, available_hours, cycle_id.
- `LeaveRequest`: leave_request_id, requested_range, reason_code, status, approver.
- `LeaveAccrualLedger`: append-only records per accrual event.

Invariants:
1. Leave balance cannot become negative unless approved policy exception with recorded reason.
2. Accrual must follow policy version active for accrual date.
3. Approved leave must reduce available balance atomically.

### Payroll Aggregate
- `PayrollRun`: payroll_run_id, period, status, rule_version, tax_table_version, checksum.
- `PayrollResult`: employee_id, gross, deductions[], net, compliance_flags[].
- `PayrollAdjustment`: adjustment_id, linked_payroll_result, reason, amount, created_by.
- `TerminationSettlement`: settlement_id, employee_id, notice_pay_zar, severance_pay_zar, policy_version, legal_check_status.

Invariants:
1. Finalized payroll runs are immutable.
2. Any post-finalization correction must create a compensating adjustment record.
3. Net pay equation must hold: `gross - sum(deductions) + sum(additions) = net`.
4. Rounding is deterministic and documented (bankers rounding not allowed unless explicitly configured).
5. Termination settlement cannot finalize without notice/severance compliance validation.

### Compliance Aggregate
- `StatutoryRuleSet`: rule_set_id, jurisdiction, effective_from, effective_to, signature_hash.
- `SubmissionPackage`: type (EMP201/EMP501/IRP5/IT3a), period, validation_status, artifact_refs.
- `ComplianceControlResult`: control_id, period, status, evidence_ref.

Invariants:
1. Only one active rule set per rule type and date window.
2. Submission package generation blocked when critical validation fails.
3. Evidence references are required for all pass/fail compliance controls.

### Audit Aggregate
- `AuditEvent`: event_id, actor_id, action, entity_ref, before_hash, after_hash, timestamp_utc, trace_id.
- `EvidenceBundle`: bundle_id, period, control_scope, document_refs, signature.

Invariants:
1. Audit events are append-only and hash-linked.
2. Deletion is forbidden; retention expiry is archive-only with legal hold support.

## Canonical Value Objects
- `MoneyZAR(amount, currency="ZAR", scale=2)`
- `TaxYear(start_date, end_date)`
- `EffectivePeriod(start, end)`
- `RuleVersion(version_id, checksum, signed_at)`
- `DataClassification(public/internal/confidential/restricted)`

## State Machines

### PayrollRun State Machine
`Draft -> Calculated -> Validated -> Approved -> Finalized -> Filed`

Rules:
1. `Calculated -> Validated` requires all mandatory validations complete.
2. `Validated -> Approved` requires role `PayrollOfficer` and no critical flags.
3. `Approved -> Finalized` requires dual control (`PayrollOfficer` + `ComplianceOfficer`) per `REQ-SEC-003`.
4. `Finalized` cannot transition backward.

### LeaveRequest State Machine
`Submitted -> ManagerReview -> Approved | Rejected | Cancelled`

Rules:
1. Approval checks available balance and blackout constraints.
2. Approved request writes immutable leave-consumption ledger entry.

### SubmissionPackage State Machine
`Prepared -> Validated -> Signed -> Submitted -> Acknowledged | Rejected`

Rules:
1. Validation failure cannot proceed to `Signed`.
2. Rejected package requires corrected successor package; original remains immutable.

## Data Retention and Archival
1. Compliance-critical payroll and filing records: minimum retention per active legal obligations.
2. Employee and payroll operational data: retained according to policy schedule and legal holds.
3. Audit events: retained for entire legal evidence horizon, then archived with hash verification.

## Error Taxonomy
- `VALIDATION_ERROR`: input or business rule breach.
- `COMPLIANCE_BLOCK`: legal control violated, operation blocked.
- `SECURITY_VIOLATION`: access or integrity breach attempt.
- `INTEGRATION_FAILURE`: external dependency unavailable.
- `DATA_INTEGRITY_ERROR`: invariant or checksum failure.

## Domain Events
- `EmployeeCreated`
- `ContractActivated`
- `TimesheetApproved`
- `LeaveAccrued`
- `PayrollCalculated`
- `PayrollFinalized`
- `SubmissionGenerated`
- `SubmissionRejected`
- `TaxTableVersionActivated`
- `SecurityCompromiseReported`

Each event must include `event_id`, `event_time_utc`, `actor_id`, `correlation_id`, and `schema_version`.

## Domain Constraints for AI-Agent Implementation
1. Any automation that modifies payroll-affecting entities must produce an `AuditEvent`.
2. Any workflow that may violate legal controls must return `COMPLIANCE_BLOCK` with control IDs.
3. Agents must not infer missing policy values; required policy fields are mandatory and validated.
