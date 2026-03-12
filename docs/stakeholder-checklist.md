# ZenoHR — Stakeholder Progress Checklist

**Project**: ZenoHR HR, Payroll & Compliance Platform
**Company**: Zenowethu (Pty) Ltd
**Last Updated**: 12 March 2026

---

## How to Read This Document

Each phase below represents a major milestone in building ZenoHR. After each phase is completed, the checkboxes will be ticked and a brief summary provided. This gives you a clear view of what's been done and how far we are from going live.

**Progress Bar**:
`[████████████████░░░░] ~80% to Production`

---

## Phase 0 — Project Setup & Planning ✅ COMPLETE

*What this means: We laid the groundwork — project structure, design documents, data models, and all the rules the system needs to follow (tax tables, leave rules, compliance requirements).*

- [x] Project repository and version control set up
- [x] All design documents written (17 specification documents)
- [x] South African tax tables loaded (PAYE 2025/2026, UIF, SDL, ETI)
- [x] BCEA rules documented (leave entitlements, working time, notice periods)
- [x] Database structure designed (employee records, payroll, leave, compliance, audit)
- [x] User interface mockups created (16 screens — login, dashboard, payroll, leave, etc.)
- [x] Brand identity applied (Zenowethu logo, colours, typography)
- [x] Role-based access designed (who can see and do what: Director, HR Manager, Manager, Employee)
- [x] Security and privacy requirements mapped (POPIA compliance controls)
- [x] Analytics dashboards designed (company-wide + personal views)

**Summary**: All planning and design work is complete. Every screen, calculation, and business rule has been specified before any code was written.

---

## Phase 1 — Core Calculations Engine ✅ COMPLETE

*What this means: We built the "brain" of the system — the engine that handles all payroll maths, tax calculations, and money handling.*

- [x] Money handling system built (cent-accurate, no rounding errors)
- [x] PAYE tax calculator (correct brackets, rebates, medical credits)
- [x] UIF calculation (1% employee + 1% employer, R17,712 ceiling)
- [x] SDL calculation (1% employer, R500k threshold exemption)
- [x] ETI (Employment Tax Incentive) calculator for youth employees
- [x] Support for both monthly and weekly pay periods
- [x] Payslip formula verified: net pay = gross - PAYE - UIF - deductions (to the cent)
- [x] Employee data model (personal details, contracts, banking, tax info)
- [x] Leave management system (annual, sick, family responsibility, maternity, parental)
- [x] Timesheet and attendance tracking structure
- [x] Automated test suite (hundreds of calculations verified against SARS tables)

**Summary**: The payroll engine is built and verified. Every rand and cent is calculated correctly according to SARS rules. All 5 BCEA leave types are supported.

---

## Phase 2 — Employee & Leave Management ✅ COMPLETE

*What this means: We built the systems for managing employee records and handling leave — storing data in the database, creating/updating/retrieving records.*

- [x] Employee database operations (create, read, update employee records)
- [x] Contract management (employment contracts linked to employees)
- [x] Leave balance tracking (how many days each employee has remaining)
- [x] Leave request workflow (apply → approve/reject → update balance)
- [x] Leave accrual ledger (audit trail of all leave balance changes)
- [x] Timesheet storage and retrieval
- [x] Clock-in/clock-out entry recording
- [x] API endpoints for employee and leave operations
- [x] Data validation (SA ID numbers, tax reference formats)
- [x] All operations respect tenant isolation (company data kept separate)

**Summary**: Employee and leave management is fully functional at the data layer. Records can be created, queried, and updated with full audit tracking.

---

## Phase 3 — Compliance, Audit & Payroll Processing ✅ COMPLETE

*What this means: We built the compliance filing systems (SARS submissions), the tamper-proof audit trail, and connected the payroll engine to actual payroll runs.*

- [x] Payroll run processing (calculate pay for all employees in a period)
- [x] Payroll results storage (individual employee payslip data)
- [x] Statutory rule engine (tax tables loaded from configuration, never hardcoded)
- [x] IRP5/IT3a tax certificate generator
- [x] EMP201 monthly filing generator
- [x] EMP501 mid-year reconciliation generator
- [x] EMP601 cancellation declaration generator
- [x] EMP701 prior-year reconciliation generator
- [x] Compliance submission tracking (draft → submitted → accepted)
- [x] Hash-chained audit trail (tamper-evident, every action recorded with SHA-256 chain)
- [x] Audit event repository with integrity verification
- [x] Risk assessment scoring engine
- [x] Payslip PDF generation (A4 format, all BCEA Section 33 mandatory fields)
- [x] Compliance schedule management
- [x] Payroll API endpoints (run payroll, view results, generate payslips)
- [x] Tax registration workflow (ITREG new employee registration)
- [x] Company settings management (5-tab settings: company, departments, users, roles, security)
- [x] Security hardening (CI gates, Firestore rules, role-scoped data, audit sanitisation)
- [x] EMP201 eFiling submission workflow (stub client — live SARS API deferred to ISV accreditation)
- [x] Tax directive repository (IRP3a/b/c/s forms, status tracking, directive number validation)

**Summary**: All compliance and audit functionality is built. SARS eFiling uses a stub client until ISV accreditation is obtained (deferred — not applying in year one until the application proves viable).

---

## Phase 4 — User Interface (Screens & Pages) ✅ COMPLETE

*What this means: We built all the screens that users interact with — every page, dashboard, and responsive layout.*

- [x] Login page (email/password authentication)
- [x] Navigation menu (role-aware — each role sees only their permitted pages)
- [x] Authentication and session management
- [x] Role-based access control (pages hidden for unauthorised roles)
- [x] Dashboard page (KPI widgets, quick stats, recent activity)
- [x] Employee management page (employee list, search, add/edit)
- [x] Payroll management page (run payroll, view results, payslip access)
- [x] Leave management page (calendar view, request/approve leave)
- [x] Compliance page (SARS filing status, BCEA checks, POPIA controls)
- [x] Timesheet page (weekly time entries, approval workflow)
- [x] Audit trail page (searchable event log with hash verification)
- [x] Settings pages (company info, departments, users, roles, security)
- [x] Admin console (platform operations — SaasAdmin only)
- [x] Clock-in/out page (employee self-service + manager team view)
- [x] Payslip PDF template (branded A4 layout matching design)
- [x] Role management page (create custom manager roles, 6 permission tokens)
- [x] Company analytics page (headcount trends, payroll costs, salary bands, leave heatmap)
- [x] My analytics page (personal earnings, leave rings, IRP5 preview with SARS codes)
- [x] Security operations page (OWASP Top 10, vulnerability tracker, POPIA controls — SaasAdmin only)
- [x] Dark mode / light mode toggle (localStorage persistence, flash prevention)
- [x] Mobile responsive: bottom navigation for phones (role-specific 5-item nav)
- [x] Mobile responsive: compact sidebar for tablets (icon-only drawer, CSS tooltips)
- [x] Mobile responsive: tables convert to card lists on small screens

**Summary**: All 23 UI tasks complete. Every screen from the mockups is implemented — login through security ops, including analytics, role management, dark mode, and full mobile/tablet responsiveness.

---

## Phase 5 — Integration, Filing & Notifications ✅ COMPLETE

*What this means: We connected ZenoHR to external systems, set up automated background jobs, notifications, and data lifecycle management.*

- [x] EMP201 filing workflow (generate, review, submit to SARS)
- [x] Filing export in SARS-required formats
- [x] SARS eFiling stub client (live API deferred to ISV accreditation — not applying year one)
- [x] POPIA breach notification workflow (72-hour regulator notification, employee alerts)
- [x] Evidence pack PDF generator (audit bundles for inspections)
- [x] Automated background jobs (nightly analytics, filing reminders, ETI expiry alerts, monthly access review)
- [x] Email notifications (payslip ready, leave approved, compliance deadlines)
- [x] Data archival (automatic 5-year retention from termination date, BCEA 3-year minimum)

**Summary**: All integration and notification tasks complete. Background jobs run on schedule (analytics nightly, access reviews monthly, archival at 3AM SAST). SARS live eFiling deferred until application proves viable.

---

## Phase 6 — Hardening, Testing & Go-Live 🔶 ~85% COMPLETE

*What this means: Final security hardening, performance testing, compliance verification, and production deployment.*

- [x] CI/CD pipeline (automated build, test, security scan on every change)
- [x] Firestore security rules (database-level access control)
- [x] Role-scoped data transfer objects (API never leaks unauthorised data)
- [x] Audit log sanitisation (no sensitive data in logs)
- [x] Architecture boundary enforcement (automated tests prevent module coupling)
- [x] Security HTTP headers and CORS policy
- [x] Code coverage enforcement (90% domain, 85% branch — CI gate active)
- [x] Application monitoring setup (OpenTelemetry + Azure Monitor, 4 custom metrics, health checks)
- [x] POPIA controls implemented (unmask audit, notice versioning, access reviews, anomaly detection, SAR workflow, correction requests, break-glass access, session timeout, field encryption)
- [x] User Acceptance Testing with pilot tenant (25 UAT tests: payroll, leave, compliance, tenant isolation)
- [x] Azure Container Apps deployment (Bicep IaC, SA North, health probes, CORS, Trivy scan)
- [ ] SARS ISV accreditation submission (deferred — not applying year one)
- [ ] Final POPIA readiness sign-off (15/15 controls implemented, formal audit pending)

**Summary**: Production infrastructure is deployed on Azure SA North. All POPIA controls are implemented with 228+ security tests. UAT complete. Only formal POPIA sign-off and SARS accreditation remain (both deferred to year two).

---

## Overall Progress to Production

| Phase | Status | Progress |
|-------|--------|----------|
| Phase 0 — Planning & Design | ✅ Complete | 27/27 tasks |
| Phase 1 — Core Calculations | ✅ Complete | 21/21 tasks |
| Phase 2 — Employee & Leave | ✅ Complete | 10/10 tasks |
| Phase 3 — Compliance & Audit | ✅ Complete | 20/20 tasks |
| Phase 4 — User Interface | ✅ Complete | 23/23 tasks |
| Phase 5 — Integration & Notifications | ✅ Complete | 8/8 tasks |
| Phase 6 — Hardening & Go-Live | 🔶 Almost done | 11/13 tasks |

**Overall: 120 of 122 tasks complete (98%)**

### What's Left Before Go-Live (Key Milestones)

1. **POPIA formal sign-off** — all 15 controls are implemented and tested; formal audit to confirm compliance
2. **SARS ISV accreditation** — deferred to year two (manual filing works as interim solution)

### External Dependencies (Outside Our Control)

| Item | Status | Impact |
|------|--------|--------|
| SARS ISV Accreditation | Deferred to year two | Manual SARS filing in the interim. Will apply once application proves viable. |
| POPIA Formal Audit | Pending | All 15 controls implemented. Formal sign-off with Information Regulator outstanding. |

### Security & Compliance Summary (Added 12 March 2026)

| Area | Status | Detail |
|------|--------|--------|
| POPIA Controls | 15/15 implemented | Unmask audit, notice versioning, access reviews, anomaly detection, SAR, corrections, break-glass, session timeout, field encryption, breach notification |
| Vulnerability Remediation | 6 Sev-1 closed | HTTP headers, CORS, MFA, incident response, breach register, break-glass |
| Test Coverage | 228+ security tests | Across 10 POPIA/security services |
| Infrastructure | Azure SA North | POPIA data residency compliant, Bicep IaC, Trivy scans |
| Encryption | AES-256 field-level | Envelope encryption for PII (national ID, tax ref, bank account) |
| Audit Trail | Hash-chained (SHA-256) | Tamper-evident, immutable, per-tenant |

---

*This document is updated after each phase milestone. Contact the development team for questions.*
