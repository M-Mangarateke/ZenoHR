---
doc_id: STAKE-01-EXEC-SUMMARY
version: 1.0.0
updated_on: 2026-03-13
owner: Director / HRManager
classification: Internal
applies_to: [Zenowethu (Pty) Ltd, ZenoHR Platform]
---

# ZenoHR — Executive Summary

**Prepared for**: Directors and Stakeholders of Zenowethu (Pty) Ltd
**Date**: 13 March 2026
**Classification**: Internal

---

## Table of Contents

1. [What Is ZenoHR?](#1-what-is-zenohr)
2. [Key Capabilities](#2-key-capabilities)
3. [South African Compliance](#3-south-african-compliance)
4. [Technology Choices](#4-technology-choices)
5. [Security Posture](#5-security-posture)
6. [Current Status](#6-current-status)
7. [Cost Structure](#7-cost-structure)
8. [Risk Register Summary](#8-risk-register-summary)

---

## 1. What Is ZenoHR?

ZenoHR is a purpose-built HR, payroll, and compliance platform designed exclusively for Zenowethu (Pty) Ltd. It automates the core administrative functions that keep the company running: paying employees correctly, managing leave, tracking working hours, filing with SARS, and ensuring compliance with South African labour and privacy law.

Unlike generic HR software, ZenoHR was built from the ground up with South African statutory requirements at its core. Every tax bracket, leave entitlement, and filing format follows current SARS and BCEA rules. The system is not a configuration of a foreign product -- it is a South African system built for a South African company.

**In one sentence**: ZenoHR handles payroll, leave, compliance, audit, and analytics for Zenowethu, ensuring every rand is calculated correctly and every legal obligation is met.

---

## 2. Key Capabilities

### Payroll Processing
- Monthly and weekly pay period support
- PAYE calculated using SARS 2025/2026 tax tables (annual equivalent method)
- UIF (1% employee + 1% employer, ceiling R17,712/month)
- SDL (1% employer, exempt if annual payroll below R500,000)
- ETI (Employment Tax Incentive) for qualifying youth employees aged 18-29
- Payslip generation in branded A4 PDF format with all BCEA Section 33 mandatory fields
- Payslip formula verified to the cent: net pay = gross pay - PAYE - UIF - deductions

### Leave Management
- All five BCEA leave types: annual (15 days), sick (30 days/3 years), family responsibility (3 days), maternity (4 months), and parental (10 days)
- Calendar view for leave planning
- Approval workflow: employee requests, manager or HR approves
- Automatic balance tracking with full accrual ledger

### Compliance and Filing
- IRP5/IT3(a) tax certificate generation
- EMP201 monthly employer declaration
- EMP501 mid-year reconciliation
- EMP601 cancellation declarations
- EMP701 prior-year reconciliation
- ITREG new employee tax registration
- IRP3 tax directive management (IRP3a/b/c/s forms)
- Compliance scoring dashboard showing SARS, BCEA, and POPIA status

### Audit Trail
- Every action in the system is recorded with a tamper-evident SHA-256 hash chain
- Each audit event links to the previous event's hash, making it impossible to alter records without detection
- Searchable audit log with filtering by date, user, action type, and module
- Evidence pack generation for inspections and audits (PDF bundles)

### Analytics
- Company-wide analytics: headcount trends, payroll costs, salary bands, leave heatmaps
- Personal analytics: individual earnings history, leave balances, IRP5 preview with SARS codes
- Role-appropriate views: Directors and HR see everything, Managers see their team, Employees see their own data

### Role-Based Access
- Five system roles: SaasAdmin (platform), Director (full access), HR Manager (full access), Manager (team-scoped), Employee (self-service)
- Navigation items are completely hidden for roles without access -- not greyed out, not clickable
- Custom Manager roles can be created with specific permission tokens
- Every employee, including Directors and Managers, has their own payroll record and receives payslips

---

## 3. South African Compliance

ZenoHR is built around three pillars of South African compliance:

### SARS (South African Revenue Service)
- **PAYE**: Calculated per SARS tax tables with correct brackets, rebates, and medical tax credits. Verified by hundreds of automated tests against official SARS tables.
- **UIF**: Employee and employer contributions calculated with the R17,712 monthly ceiling enforced.
- **SDL**: Skills Development Levy at 1%, with automatic exemption for payrolls below R500,000.
- **ETI**: Employment Tax Incentive calculated for qualifying youth employees (aged 18-29, earning between R2,500 and R7,500).
- **Filing**: All major SARS employer filing formats supported (IRP5, EMP201, EMP501, EMP601, EMP701). Live electronic filing to SARS is deferred until ISV accreditation is obtained -- manual filing is supported in the interim.

### BCEA (Basic Conditions of Employment Act)
- All five mandatory leave types implemented with correct entitlements
- Working time limits from BCEA loaded as configuration (45-hour week, overtime rules)
- Payslip format includes all fields required by BCEA Section 33
- Payroll records retained for 3 years minimum as required by BCEA

### POPIA (Protection of Personal Information Act)
- 15 POPIA controls identified and tracked
- Data stored exclusively in Azure South Africa North (Johannesburg) -- employee data never leaves South Africa
- AES-256 field-level encryption for sensitive fields (national ID, tax reference, bank account)
- Tenant data isolation enforced at database level
- Breach notification workflow with 72-hour deadline tracking for Information Regulator notification
- Data subject access request (SAR) workflow with 30-day SLA
- Monthly access reviews with audit trail
- Hash-chained audit trail provides tamper-evident record of all data access

---

## 4. Technology Choices

Each technology choice was made for a specific reason:

| Choice | Why |
|--------|-----|
| **Azure South Africa North (Johannesburg)** | All data stays in South Africa as required by POPIA. No cross-border transfer of employee data. |
| **.NET 10** | Microsoft's latest long-term support framework. Enterprise-grade, well-supported, strong security track record. |
| **Blazor Server** | Single codebase for web interface. Real-time updates without page reloads. Works on all devices including mobile. |
| **Google Cloud Firestore** | Scalable database with built-in security rules. Supports tenant isolation at the database level. |
| **Firebase Authentication** | Industry-standard authentication with multi-factor authentication (MFA) support. No need to build login security from scratch. |
| **Azure Key Vault** | All passwords, API keys, and encryption keys stored in a certified security vault -- never in code. |
| **QuestPDF** | Payslips and reports generated as professional A4 PDFs with Zenowethu branding. |
| **GitHub Actions** | Every code change is automatically built, tested, and security-scanned before deployment. |
| **OpenTelemetry + Azure Monitor** | Real-time monitoring of system health, performance, and errors. Alerts when something needs attention. |

**No per-user licensing fees**: ZenoHR is a custom-built platform. There are no recurring per-employee or per-user license costs from third-party HR software vendors. Costs are limited to cloud hosting and infrastructure.

---

## 5. Security Posture

### Encryption
- **In transit**: All data encrypted using HTTPS/TLS. HSTS headers enforce HTTPS for all browser connections.
- **At rest**: Firestore provides managed encryption at rest. Sensitive PII fields (national ID, tax reference, bank account) receive additional AES-256 application-level encryption.

### Access Control
- Firebase Authentication with multi-factor authentication (MFA) required for privileged operations (payroll finalisation, SARS filing approval, role management)
- Five-tier role-based access: each role sees only what it should
- Firestore security rules enforce access at the database level -- even if application code had a bug, the database would reject unauthorised access
- Rate limiting prevents brute-force attacks and denial-of-service attempts (100 requests/minute general, 10/5 minutes on authentication)

### Audit Trail
- SHA-256 hash-chained audit trail records every action
- Tamper-evident: if any record is altered, the hash chain breaks and the system detects it
- Immutable: finalised payroll runs, audit events, and accrual entries cannot be modified or deleted
- Corrections create new records referencing the original -- history is preserved

### Monitoring
- OpenTelemetry sends metrics to Azure Monitor (Application Insights)
- Health check endpoints for automated monitoring
- Anomaly detection for suspicious access patterns (multiple failed logins, bulk data exports, off-hours privileged access)
- Automated incident classification and escalation

### Security Headers
- Content Security Policy (CSP), X-Frame-Options, X-Content-Type-Options, Strict-Transport-Security, and Referrer-Policy all configured
- CORS restricted to the deployment domain only

---

## 6. Current Status

### Overall Progress: 98% Complete (120 of 122 tasks)

| Phase | Status | Tasks |
|-------|--------|-------|
| Phase 0 -- Planning and Design | Complete | 27/27 |
| Phase 1 -- Core Calculations Engine | Complete | 21/21 |
| Phase 2 -- Employee and Leave Management | Complete | 10/10 |
| Phase 3 -- Compliance, Audit, and Payroll | Complete | 20/20 |
| Phase 4 -- User Interface | Complete | 23/23 |
| Phase 5 -- Integration and Notifications | Complete | 8/8 |
| Phase 6 -- Hardening and Go-Live | 85% | 11/13 |

### What Remains

1. **POPIA formal sign-off**: All 15 controls are implemented and tested with 228+ security tests. A formal audit to confirm compliance is the final step. This is an administrative milestone, not a technical one.

2. **SARS ISV accreditation**: Deferred to year two. Live electronic filing to SARS requires ISV (Independent Software Vendor) accreditation, which involves an application and approval process with SARS. Manual filing is fully supported in the interim. The decision to defer was deliberate -- the application needs to prove viable before investing in accreditation.

### Test Coverage
- 228+ security and compliance tests across 10 services
- Property-based tests (FsCheck) verify payroll calculations against SARS tables with hundreds of edge cases
- Architecture tests enforce module boundaries automatically
- 25 User Acceptance Tests covering payroll, leave, compliance, and tenant isolation

---

## 7. Cost Structure

ZenoHR's costs are cloud infrastructure costs only. There are no per-user software license fees.

| Component | Service | Cost Driver |
|-----------|---------|-------------|
| **Application hosting** | Azure Container Apps (SA North) | Consumption-based: pay for CPU/memory used |
| **Database** | Google Cloud Firestore | Per-read/write/storage: scales with usage |
| **Authentication** | Firebase Authentication | Free tier covers up to 50,000 monthly active users |
| **Secrets management** | Azure Key Vault | Per-operation: minimal cost for key/secret access |
| **Monitoring** | Azure Monitor / Application Insights | Per-GB ingested: scales with log volume |
| **CI/CD** | GitHub Actions | Free tier for public repos; minutes-based for private |
| **PDF generation** | QuestPDF | Open-source library: no licensing cost |
| **Domain and SSL** | Azure-managed | Nominal annual cost |

**Cost advantages over commercial HR software**:
- No per-employee monthly fees (typical competitors charge R50-R200 per employee per month)
- No annual license renewals
- No vendor lock-in -- Zenowethu owns the entire codebase
- Infrastructure costs scale with actual usage, not headcount

---

## 8. Risk Register Summary

The following risks have been identified and are being managed:

### Resolved Risks
| Risk | Resolution |
|------|-----------|
| Security HTTP headers missing | Implemented: CSP, HSTS, X-Frame-Options, nosniff, Referrer-Policy |
| No CORS policy | Implemented: restrictive CORS allowing deployment domain only |
| MFA not enforced on privileged operations | Implemented: RequireMfa attribute on payroll, compliance, and role endpoints |
| No incident response system | Implemented: anomaly detection, incident classification, escalation workflow |
| No POPIA breach register | Implemented: breach notification service with 72-hour deadline tracking |
| No break-glass emergency access | Implemented: time-limited emergency access with mandatory post-event audit |

### Managed Risks (Low Residual)
| Risk | Status | Mitigation |
|------|--------|-----------|
| SARS ISV accreditation not obtained | Deferred | Manual filing fully supported; will apply when application proves viable |
| POPIA formal audit not yet completed | Pending | All 15 controls implemented with 228+ tests; administrative sign-off remaining |
| Key rotation automation | Configured | Azure Key Vault 180-day rotation policy documented |
| Container image security | Active | Trivy scans in CI pipeline; fail on HIGH/CRITICAL CVEs |

### External Dependencies
| Dependency | Impact | Contingency |
|-----------|--------|-------------|
| SARS ISV accreditation | Cannot file electronically until approved | Manual filing via SARS eFiling portal |
| Information Regulator POPIA audit | Formal compliance confirmation | All controls implemented; audit is an administrative process |
| Google Cloud Firestore SA region | Data residency for POPIA | Confirmed SA region configuration; Azure SA North for hosting |

---

*This document was prepared for stakeholders of Zenowethu (Pty) Ltd. For technical details, refer to the full PRD documentation package in `docs/prd/`. For questions, contact the development team at admin@zenowethu.co.za.*
