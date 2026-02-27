---
doc_id: PRD-01-EXECUTIVE
version: 1.0.0
owner: Product Manager
updated_on: 2026-02-18
applies_to:
  - Zenowethu HR System product and delivery
  - Claude Desktop local MCP planning context
depends_on:
  - PRD-00-MANIFEST
  - PRD-02-DOMAIN
  - PRD-03-ARCH
requirements:
  - REQ-HR-001
  - REQ-HR-013
  - REQ-COMP-001
  - REQ-COMP-016
  - REQ-OPS-010
---

# Executive PRD

## Product Goal
Deliver an HR and payroll platform for South African SMEs that is audit-ready by design, legally aligned to SARS/BCEA/POPIA, and deterministic enough for autonomous implementation agents to build with minimal assumptions.

## Vision Statement
Zenowethu HR System is a compliance-first HR platform where statutory rules are first-class system controls, not afterthoughts. The platform should prevent unlawful operations before submission rather than detect them after failure.

## In Scope
1. Employee master data and employment record lifecycle.
2. Payroll processing with statutory components: PAYE, UIF, SDL, ETI.
3. Payslip generation with legally required remuneration details.
4. Leave accrual, leave balances, and leave approval workflows.
5. Working-time and overtime compliance controls.
6. SARS compliance outputs: EMP201, EMP501, IRP5, IT3(a).
7. Compliance risk dashboard and regulatory fine exposure estimator.
8. Automatic travel reimbursement using configurable SARS rates.
9. Immutable audit trail for compliance-sensitive changes.
10. Security and privacy controls aligned to POPIA.
11. Structured markdown context pack for local MCP ingestion.
12. Notice-period and severance compliance handling for employee termination workflows.

## Out of Scope (v1)
1. Multi-country payroll tax logic.
2. Multi-currency payroll settlement.
3. Biometric attendance ingestion.
4. Advanced predictive analytics requiring external ML pipelines.
5. Native mobile applications (web-first delivery).

## User Personas
1. HR Administrator: manages employee records, contracts, leave, and onboarding data quality.
2. Payroll Officer: runs payroll, validates statutory deductions, and publishes payslips.
3. Compliance Officer: monitors BCEA/SARS/POPIA controls and evidence packs.
4. Manager: approves leave, reviews overtime exceptions, and receives compliance alerts.
5. Employee: views profile, leave, and payslips via self-service portal.
6. Auditor (internal/external): inspects immutable change history and filing evidence.

## Business Outcomes and KPIs
| Outcome | KPI | Target |
|---|---|---|
| Accurate payroll | Payroll statutory calculation defect leakage | 0 Sev-1 and 0 Sev-2 to production |
| Compliance timeliness | On-time EMP201 submissions | 100% on-time |
| Reconciliation integrity | EMP501 reconciliation mismatches | 0 unresolved mismatches at period close |
| Employee trust | Payroll dispute rate | <1% of monthly payslips |
| Security posture | Unauthorized sensitive-data access incidents | 0 tolerated |
| Operability | Production availability | >=99.9% monthly |

## Product Principles
1. Compliance by construction: block invalid operations early.
2. Deterministic calculations: identical input always yields identical output.
3. Immutable accountability: no destructive update for compliance records.
4. Configurable statutory data: tax tables and thresholds are signed/versioned data.
5. Machine-readable specification: every requirement is uniquely addressable.

## Functional Requirements (Public Summary)
### HR requirements
- `REQ-HR-001`: Capture and maintain complete employee master data with validation.
- `REQ-HR-002`: Manage contract lifecycle with effective-date tracking.
- `REQ-HR-003`: Execute monthly payroll run with deterministic result set.
- `REQ-HR-004`: Generate payslips with detailed remuneration breakdown.
- `REQ-HR-005`: Accrue leave monthly and expose balance history.
- `REQ-HR-006`: Run leave request/approval/rejection workflow.
- `REQ-HR-007`: Enforce time capture quality and completeness.
- `REQ-HR-008`: Compute overtime at required multiplier.
- `REQ-HR-009`: Calculate travel reimbursement from maintained SARS rates.
- `REQ-HR-010`: Validate ETI eligibility per configured rules.
- `REQ-HR-011`: Produce compliance risk dashboard with actionable alerts.
- `REQ-HR-012`: Apply payroll corrections via compensating entries only.
- `REQ-HR-013`: Provide employee self-service for payslips and leave.
- `REQ-HR-014`: Calculate termination notice and severance outcomes using active policy rules.

### Compliance requirements
- `REQ-COMP-001`: PAYE calculations use active statutory table version.
- `REQ-COMP-002`: UIF and SDL calculations enforce legal caps/rates.
- `REQ-COMP-003`: Produce EMP201-ready monthly declarations and reminders.
- `REQ-COMP-004`: Produce EMP501 reconciliation packs for interim/annual cycles.
- `REQ-COMP-005`: Generate IRP5/IT3(a) certificates in valid schema.
- `REQ-COMP-006`: Block submissions when tax references are invalid/missing.
- `REQ-COMP-007`: Enforce BCEA ordinary hours limit checks.
- `REQ-COMP-008`: Enforce BCEA overtime remuneration rules.
- `REQ-COMP-009`: Enforce BCEA annual leave entitlement logic.
- `REQ-COMP-010`: Provide remuneration information per BCEA requirements.
- `REQ-COMP-011`: Retain legally required records and preserve integrity.
- `REQ-COMP-012`: Enforce POPIA lawful processing and purpose metadata.
- `REQ-COMP-013`: Enable POPIA data-subject participation workflows.
- `REQ-COMP-014`: Enforce POPIA security safeguards and breach workflow.
- `REQ-COMP-015`: Govern annual tax table update with pre-release regression.
- `REQ-COMP-016`: Generate evidence packs for audits and regulator reviews.
- `REQ-COMP-017`: Enforce BCEA-aligned notice period and severance compliance checks.

### Security requirements
- `REQ-SEC-001`: Strong authentication with MFA for privileged roles.
- `REQ-SEC-002`: Role-based access with least-privilege defaults.
- `REQ-SEC-003`: Separation of duties across payroll and compliance approvals.
- `REQ-SEC-004`: Encryption in transit and at rest for sensitive data.
- `REQ-SEC-005`: Key lifecycle management with rotation and break-glass control.
- `REQ-SEC-006`: Tamper-evident immutable audit logging.
- `REQ-SEC-007`: Sensitive data masking in UI/logs/reports.
- `REQ-SEC-008`: Secrets/config managed outside source code.
- `REQ-SEC-009`: Secure SDLC scanning and remediation SLA.
- `REQ-SEC-010`: Incident response and forensic evidence readiness.

### Operational requirements
- `REQ-OPS-001`: Modular monolith boundaries must be explicit.
- `REQ-OPS-002`: Internal event-driven notifications for compliance/audit.
- `REQ-OPS-003`: Cache tiers with explicit TTL and invalidation logic.
- `REQ-OPS-004`: No cache for source-of-truth payroll commit path.
- `REQ-OPS-005`: Structured logs, metrics, and traces for key workflows.
- `REQ-OPS-006`: Defined SLOs and breach response playbooks.
- `REQ-OPS-007`: Backup/restore with measurable RPO/RTO.
- `REQ-OPS-008`: Quarterly DR drills with evidence artifacts.
- `REQ-OPS-009`: CI/CD gates enforce tests/security/compliance checks.
- `REQ-OPS-010`: Phased release gates with rollback criteria.
- `REQ-OPS-011`: ADR-governed change management for compliance-critical changes.
- `REQ-OPS-012`: MCP-ready docs metadata and stable cross-linking.
- `REQ-OPS-013`: Performance budgets for payroll/reporting/dashboard.
- `REQ-OPS-014`: Capacity envelope for SME launch and growth path.
- `REQ-OPS-015`: Annual legal reference validation cadence.

## UX and Design System Requirements
1. Design language must communicate compliance confidence: clear status states, explicit warnings, and immutable action history visibility.
2. Accessibility baseline: WCAG 2.1 AA for all user-facing portals.
3. Critical actions (payroll finalization, SARS export, policy override) require deliberate confirmation with visible legal impact notes.
4. Every numeric statutory output must surface provenance: formula ID, rule version, effective date, and last synced date.
5. Risk dashboard must prioritize legally time-bound actions over informational alerts.

## Non-Functional Requirements
1. Availability: >=99.9% monthly for production API.
2. Data integrity: 0 tolerated silent corruption; all writes have integrity checks.
3. Auditability: 100% of payroll-affecting mutations linked to immutable audit events.
4. Performance: see `PRD-07-CACHE` for workflow budgets.
5. Security: no unresolved high or critical vulnerabilities at release.
6. Traceability: 100% requirement-to-test-to-evidence mapping.

## Roadmap and Phases
### Phase 0: Foundation
- Baseline domain model, IAM, audit schema, requirement traceability framework.

### Phase 1: Compliance MVP
- Employee records, payroll core, payslips, leave, BCEA working-time checks, EMP201 basics.

### Phase 2: SARS and Risk Intelligence
- EMP501 pipeline, IRP5/IT3(a), ETI validator, travel compensation calculator, risk estimator.

### Phase 3: Hardening and Scale
- Advanced observability, DR maturity, compliance evidence automation, performance tuning.

## Release Gates (Blocking)
1. Gate A (Design): no unresolved assumptions for in-scope features.
2. Gate B (Build): all contracts and invariants implemented with passing unit/integration tests.
3. Gate C (Compliance): SARS/BCEA/POPIA control tests pass with evidence generated.
4. Gate D (Security): SAST/DAST/dependency/secret scans clear policy thresholds.
5. Gate E (Production): rollback rehearsal and backup restore test completed successfully.

## Explicit Default Decisions
- Stack fixed to ASP.NET Core (.NET 10) + Firebase Firestore.
- Deployment is cloud-first with South Africa governance controls.
- SME launch target (<500 employees).
- Release approach is phased, not big-bang.
- Documentation is machine-oriented for local MCP usage.

## Legal Statement
This PRD is a technical implementation specification. It does not replace legal, tax, or labor-law counsel.
