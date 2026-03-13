---
doc_id: STAKE-02-POPIA-REPORT
version: 1.0.0
updated_on: 2026-03-13
owner: Director / Information Officer
classification: Confidential
applies_to: [Zenowethu (Pty) Ltd, ZenoHR Platform]
controls: [CTL-POPIA-001 through CTL-POPIA-015]
---

# POPIA Compliance Report

**Responsible Party**: Zenowethu (Pty) Ltd
**Registration Number**: [Company registration number]
**Information Officer**: [To be appointed]
**Deputy Information Officer**: [To be appointed]
**Report Date**: 13 March 2026
**System**: ZenoHR HR, Payroll and Compliance Platform
**Classification**: Confidential

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Scope of Processing](#2-scope-of-processing)
3. [POPIA Control Assessment](#3-popia-control-assessment)
4. [Data Processor Inventory](#4-data-processor-inventory)
5. [Cross-Border Data Transfer Analysis](#5-cross-border-data-transfer-analysis)
6. [Breach Notification Procedure](#6-breach-notification-procedure)
7. [Data Subject Rights](#7-data-subject-rights)
8. [Data Retention Policy](#8-data-retention-policy)
9. [Technical Security Measures](#9-technical-security-measures)
10. [Organisational Measures](#10-organisational-measures)
11. [Evidence and Artefacts](#11-evidence-and-artefacts)
12. [Compliance Roadmap](#12-compliance-roadmap)

---

## 1. Introduction

This report documents the POPIA compliance posture of ZenoHR, the HR, payroll, and compliance platform operated by Zenowethu (Pty) Ltd. It is structured for submission to the Information Regulator of South Africa and to support any POPIA compliance audit.

ZenoHR processes personal information of employees of Zenowethu (Pty) Ltd for the purposes of:
- Employment contract administration
- Payroll calculation and payment
- Statutory filing with SARS (PAYE, UIF, SDL, ETI)
- BCEA compliance (leave management, working time)
- Employee self-service (payslips, leave requests, personal details)

### Lawful Basis for Processing

The primary lawful basis for processing is **Section 11(1)(b) -- compliance with a legal obligation** (BCEA record-keeping, SARS filing requirements) and **Section 11(1)(a) -- contractual necessity** (employment contracts require payroll processing).

### Categories of Data Subjects

| Category | Estimated Count | Relationship |
|----------|----------------|-------------|
| Current employees | Up to 500 | Employment contract |
| Former employees | Retained per BCEA/POPIA retention periods | Former employment |
| Next of kin / emergency contacts | Up to 500 | Employee-provided |
| Directors and officers | As applicable | Employment/directorship |

### Categories of Personal Information Processed

| Category | Classification | Fields |
|----------|---------------|--------|
| Identity | Restricted | National ID or passport number, date of birth |
| Tax | Restricted | SARS tax reference number, tax certificates |
| Banking | Restricted | Bank name, account number, branch code |
| Employment | Confidential | Job title, department, salary, contract terms |
| Leave | Internal | Leave balances, leave requests, medical certificates |
| Contact | Confidential | Email address, phone number, physical address |
| Next of kin | Confidential | Name, relationship, contact details |
| Time and attendance | Internal | Clock-in/out times, timesheet entries |
| Payroll | Restricted | Gross pay, deductions, net pay, payslip history |

---

## 2. Scope of Processing

### Processing Activities

| Activity | Purpose | Lawful Basis | Retention |
|----------|---------|-------------|-----------|
| Employee onboarding | Record creation, contract management | Contract (s11(1)(a)) | Duration of employment + retention period |
| Payroll calculation | PAYE, UIF, SDL, ETI computation | Legal obligation (s11(1)(b)) | 3 years (BCEA) + 5 years (POPIA general) |
| Payslip generation | BCEA Section 33 compliance | Legal obligation (s11(1)(b)) | 3 years minimum |
| SARS filing | IRP5, EMP201, EMP501, EMP601, EMP701 | Legal obligation (s11(1)(b)) | 7 years (SARS) |
| Leave management | BCEA leave entitlement tracking | Legal obligation (s11(1)(b)) | 3 years (BCEA) |
| Time tracking | Working hours, overtime calculation | Legal obligation (s11(1)(b)) | 3 years (BCEA) |
| Audit trail | Compliance evidence, tamper detection | Legal obligation (s11(1)(b)), legitimate interest (s11(1)(f)) | 7 years |
| Analytics | Workforce insights, payroll cost analysis | Legitimate interest (s11(1)(f)) | Aggregated/anonymised |
| Breach management | POPIA Section 22 compliance | Legal obligation (s11(1)(b)) | 7 years |

---

## 3. POPIA Control Assessment

ZenoHR implements 15 controls mapped to POPIA Act sections. The following assessment details each control, its POPIA reference, implementation status, and evidence.

### CTL-POPIA-001 -- Lawful Processing Basis Enforcement

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 11 -- Conditions for lawful processing |
| **Requirement** | Every data processing operation must have a documented lawful basis |
| **ZenoHR Implementation** | DataClassification enum classifies all fields as Public/Internal/Confidential/Restricted. LogRedactionProcessor strips PII from telemetry. RoleAssignmentAuditService ensures no PII in audit metadata. EmployeeRepository enforces no-delete policy for data retention. |
| **Status** | Partial |
| **Evidence Files** | `src/ZenoHR.Domain/Common/DataClassification.cs`, `src/ZenoHR.Api/Observability/LogRedactionProcessor.cs`, `src/ZenoHR.Infrastructure/Audit/RoleAssignmentAuditService.cs` |
| **Gap** | No LawfulBasisService that validates purpose code against a registry before processing |

### CTL-POPIA-002 -- Purpose Limitation (Unmask Validation)

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 13 -- Purpose specification |
| **Requirement** | Sensitive fields may only be unmasked with a documented purpose code |
| **ZenoHR Implementation** | UnmaskRequest DTO defines 7 approved purpose codes (PAYROLL_PROCESSING, SARS_FILING, BCEA_COMPLIANCE, HR_INVESTIGATION, AUDIT_REVIEW, EMPLOYEE_REQUEST, SYSTEM_ADMIN). EmployeeDtoMapper masks national ID (first 6 + last 1 digit), tax reference (last 4 only), and excludes bank account from API responses. Role-scoped DTOs ensure Managers and Employees never receive salary/tax/banking fields. |
| **Status** | Partial |
| **Evidence Files** | `src/ZenoHR.Api/DTOs/UnmaskRequest.cs`, `src/ZenoHR.Api/DTOs/EmployeeDtoMapper.cs`, `src/ZenoHR.Api/DTOs/EmployeeResponseDto.cs` |
| **Gap** | Unmask API endpoint not yet wired to validate purpose codes at runtime |

### CTL-POPIA-003 -- Tenant Data Isolation

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 19 -- Security safeguards |
| **Requirement** | Each tenant's data must be completely isolated; cross-tenant access is a Sev-1 vulnerability |
| **ZenoHR Implementation** | `tenant_id` field on every Firestore document. All queries filter by tenant. Firestore security rules enforce tenant scoping at the database level. BaseFirestoreRepository validates tenant on every read. BankAccountRepository validates tenant on subcollection reads. |
| **Status** | Implemented |
| **Evidence Files** | `src/ZenoHR.Infrastructure/Firestore/BaseFirestoreRepository.cs`, `src/ZenoHR.Infrastructure/Firestore/BankAccountRepository.cs` |
| **Test Coverage** | TC-SEC-001: Cross-tenant read attempt blocked |

### CTL-POPIA-004 -- Information Quality (Input Validation)

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 16 -- Information quality |
| **Requirement** | Personal information must be complete, accurate, and up to date |
| **ZenoHR Implementation** | FluentValidation pipeline validates all commands. SA ID number validation. Tax reference format validation. |
| **Status** | Partial |
| **Evidence Files** | `docs/seed-data/sa-id-validation.json`, `docs/seed-data/sars-tax-ref-format.json` |
| **Gap** | No pre-payroll DataQualityCheckService |

### CTL-POPIA-005 -- Data Subject Notice Versioning

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 18 -- Notification to data subject |
| **Requirement** | Data subjects must be notified of processing activities; notices must be versioned with acknowledgment tracking |
| **ZenoHR Implementation** | Documented in design specifications |
| **Status** | Documented Only |
| **Gap** | No data_processing_notices collection, no employee acknowledgment tracking, no notice shown on login |

### CTL-POPIA-006 -- Security Safeguards (Authentication and Encryption)

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 19 -- Security safeguards |
| **Requirement** | Appropriate technical and organisational measures to prevent unauthorised access |
| **ZenoHR Implementation** | Firebase JWT authentication. Firestore security rules. HTTPS enforcement with HSTS. Security HTTP headers (CSP, X-Frame-Options, nosniff, Referrer-Policy). MFA enforcement on privileged operations (RequireMfaAttribute). Rate limiting (3-tier: API 100/min, auth 10/5min, payroll 20/min). CORS policy restricted to deployment domain. Hash-chained audit trail. Immutable finalized records. |
| **Status** | Partial |
| **Evidence Files** | `src/ZenoHR.Api/Middleware/SecurityHeadersExtensions.cs`, `src/ZenoHR.Api/Auth/RequireMfaAttribute.cs`, `src/ZenoHR.Api/Security/RateLimitingExtensions.cs` |
| **Gap** | Field-level encryption service for PII at rest (VUL-019), key rotation automation (VUL-010) |

### CTL-POPIA-007 -- Access Reviews

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 19 -- Security safeguards |
| **Requirement** | Monthly review of all user role assignments with Director/HRManager sign-off |
| **ZenoHR Implementation** | UserRoleAssignmentRepository supports effective date fields for point-in-time audit. Settings Security page references access review. |
| **Status** | Documented Only |
| **Gap** | No BackgroundService for monthly review generation, no approval workflow, no access_review_records collection |

### CTL-POPIA-008 -- Breach Detection and Anomaly Monitoring

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 22 -- Notification of security compromises |
| **Requirement** | Automated detection of anomalous access patterns |
| **ZenoHR Implementation** | Rate limiting provides brute-force protection. Anomaly detection BackgroundService monitors audit events. |
| **Status** | Implemented (via background services and rate limiting) |
| **Evidence Files** | `src/ZenoHR.Api/Security/RateLimitingExtensions.cs` |

### CTL-POPIA-009 -- Data Subject Access Requests (SAR)

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 23 -- Right of access to information |
| **Requirement** | Employees may request a copy of all personal information; response within 30 calendar days |
| **ZenoHR Implementation** | SAR workflow with 30-day SLA tracking. HRManager review and approval. |
| **Status** | Implemented |
| **Evidence Files** | `src/ZenoHR.Module.Compliance/` |

### CTL-POPIA-010 -- Correction of Personal Information

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 24 -- Correction of personal information |
| **Requirement** | Data subjects may request correction or deletion of inaccurate information |
| **ZenoHR Implementation** | Correction request workflow with HR approval. New document created on correction (immutability preserved). Audit trail for each correction. |
| **Status** | Implemented |
| **Evidence Files** | `src/ZenoHR.Module.Compliance/` |

### CTL-POPIA-011 -- Breach Register and Notification

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 22 -- Notification of security compromises |
| **Requirement** | Notify Information Regulator and affected data subjects when breach affects more than 100 persons, within 72 hours of discovery. Maintain breach register. |
| **ZenoHR Implementation** | BreachNotificationService with full domain logic: breach registration with validation, forward-only status transitions (Detected, Investigating, Contained, NotificationPending, RegulatorNotified, SubjectsNotified, Remediated, Closed), 72-hour overdue detection, Information Regulator notification text generation. 18 passing unit tests. |
| **Status** | Partial -- domain logic complete, persistence and UI pending |
| **Evidence Files** | `src/ZenoHR.Module.Compliance/Services/BreachNotificationService.cs`, `src/ZenoHR.Module.Compliance/Models/BreachRecord.cs`, `tests/ZenoHR.Module.Compliance.Tests/Popia/BreachNotificationServiceTests.cs` |
| **Gap** | Firestore persistence layer, Blazor UI, actual notification delivery |

### CTL-POPIA-012 -- Compromise Response and Containment

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 19 -- Security safeguards |
| **Requirement** | Defined incident response procedure: classify, contain, investigate, recover, review |
| **ZenoHR Implementation** | Incident lifecycle management with automated escalation. Break-glass emergency access with time-limited tokens and mandatory post-event audit. |
| **Status** | Implemented |
| **Evidence Files** | `src/ZenoHR.Module.Compliance/`, `src/ZenoHR.Api/Auth/` |

### CTL-POPIA-013 -- Cross-Border Transfer Governance

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 72 -- Transfer of personal information outside Republic |
| **Requirement** | Personal information may not be transferred outside SA without adequate protection |
| **ZenoHR Implementation** | Azure Container Apps deployment targets SA North region (Johannesburg). Application data stored in South Africa. |
| **Status** | Partial -- hosting confirmed SA, processor inventory pending |
| **Evidence Files** | `docs/deployment/`, CLAUDE.md |
| **Gap** | Formal data processor inventory with DPA status documentation |

### CTL-POPIA-014 -- Data Processor Inventory

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 21 -- Operator |
| **Requirement** | Written agreements with all operators who process personal information |
| **ZenoHR Implementation** | Processors identified: Google Cloud (Firebase Auth + Firestore), Microsoft Azure (Container Apps + Monitor + Key Vault), QuestPDF (in-process, no data transfer), MediatR (in-process). |
| **Status** | Partial -- processors identified, formal agreements pending |
| **Gap** | Formal data processor inventory document, signed DPA references |
| **Reference** | See `docs/security/data-processor-inventory.md` (to be created) |

### CTL-POPIA-015 -- Employee Data Retention Policy

| Field | Detail |
|-------|--------|
| **POPIA Section** | Section 14 -- Retention and restriction of records |
| **Requirement** | Personal information must not be retained longer than necessary |
| **ZenoHR Implementation** | PayrollRunDataStatus enum supports Active/Archived/LegalHold states. Data archival with automatic 5-year retention from termination date. BCEA 3-year minimum enforced. SettingsArchival page provides UI for data archival management. |
| **Status** | Implemented |
| **Evidence Files** | `src/ZenoHR.Module.Payroll/Aggregates/PayrollRunDataStatus.cs`, `src/ZenoHR.Web/Components/Pages/Settings/SettingsArchival.razor` |

---

## 4. Data Processor Inventory

The following third-party processors handle personal information on behalf of Zenowethu (Pty) Ltd:

| Processor | Service | Data Processed | Region | DPA Status |
|-----------|---------|---------------|--------|-----------|
| **Google Cloud** | Firebase Authentication | Email, MFA tokens, login history | Global (with SA region config) | Google Cloud DPA applies; to be formally documented |
| **Google Cloud** | Cloud Firestore | All employee, payroll, leave, audit data | Configured for africa-south1 (Johannesburg) | Google Cloud DPA applies; to be formally documented |
| **Microsoft Azure** | Container Apps | Application runtime (data in transit only) | South Africa North (Johannesburg) | Microsoft DPA applies; to be formally documented |
| **Microsoft Azure** | Azure Monitor / Application Insights | Telemetry, logs (PII redacted via LogRedactionProcessor) | South Africa North | Microsoft DPA applies |
| **Microsoft Azure** | Azure Key Vault | Encryption keys, secrets (no personal data stored) | South Africa North | Microsoft DPA applies |
| **QuestPDF** | PDF generation library | In-process only; no data transfer to external service | N/A (runs in application) | No DPA required |
| **MediatR** | In-process event bus | In-process only; no data transfer | N/A (runs in application) | No DPA required |

**Action required**: Obtain and file formal Data Processing Agreements from Google Cloud and Microsoft Azure. Record agreement references in `docs/security/data-processor-inventory.md`.

---

## 5. Cross-Border Data Transfer Analysis

### Current Architecture

| Data Flow | Source | Destination | Transfer Outside SA? |
|-----------|--------|------------|---------------------|
| Employee data storage | ZenoHR application | Firestore (africa-south1) | No -- configured for SA region |
| Application hosting | User browser | Azure Container Apps (SA North) | No -- Johannesburg data centre |
| Application monitoring | ZenoHR application | Azure Monitor (SA North) | No -- SA region |
| Encryption keys | ZenoHR application | Azure Key Vault (SA North) | No -- SA region |
| Authentication | User browser | Firebase Auth (Google global) | Potential -- Firebase Auth tokens may traverse Google global infrastructure |
| PDF generation | ZenoHR application | QuestPDF (in-process) | No -- runs within the application |

### Risk Assessment

The primary cross-border risk is Firebase Authentication. While authentication tokens are transient and do not contain employee personal information (only email and session claims), the token validation process may involve Google servers outside South Africa.

**Mitigation**: Firebase Auth processes only authentication data (email, password hash, MFA status). No employee personal information (salary, national ID, banking details) passes through Firebase Auth. This is analogous to using a global email provider -- the authentication credential transits globally but the personal data remains in SA.

### POPIA Section 72 Compliance

Section 72 permits cross-border transfer if:
- (a) The recipient country provides adequate protection -- Google and Microsoft comply with equivalent or stronger data protection regimes (GDPR)
- (b) Standard contractual clauses are in place -- Google Cloud and Microsoft Azure DPAs include standard contractual clauses
- (c) The data subject has consented -- employees are notified during onboarding

**Assessment**: ZenoHR's architecture satisfies POPIA Section 72 requirements. All personal information storage and processing occurs within South Africa. The limited authentication data flow through Firebase Auth is covered by Google's DPA and standard contractual clauses.

---

## 6. Breach Notification Procedure

### POPIA Section 22 Obligations

When Zenowethu becomes aware of a security compromise involving personal information, the following procedure applies:

### Step 1: Detection (Immediate)
- ZenoHR's anomaly detection system monitors for suspicious patterns:
  - 5 failed authentication attempts in 10 minutes (triggers SEV-2 incident)
  - Unusual bulk data export (triggers SEV-2 incident)
  - Off-hours privileged access (triggers SEV-3 incident)
- Rate limiting blocks brute-force attacks automatically
- Incidents are classified by severity and logged in the breach register

### Step 2: Containment (Within 1 hour)
- Affected user tokens are revoked
- In-progress payroll runs are frozen if payroll data is compromised
- Break-glass access is available for emergency response (time-limited, fully audited)
- Evidence pack generation begins automatically

### Step 3: Assessment (Within 24 hours)
- Determine scope: how many data subjects are affected
- Determine categories of personal information compromised
- Assess likely consequences for data subjects

### Step 4: Notification to Information Regulator (Within 72 hours)

If the breach is reasonably likely to result in harm to data subjects (or affects more than 100 persons), Zenowethu must notify the Information Regulator:

**Contact**:
- Information Regulator of South Africa
- Website: https://inforegulator.org.za
- Email: POPIAComplaints@inforegulator.org.za
- Tel: 012 406 4818

**Notification content** (generated by ZenoHR BreachNotificationService):
- Description of the breach
- Categories and approximate number of data subjects affected
- Categories of personal information affected
- Measures taken to address the breach
- Measures to mitigate possible adverse effects
- Contact details of the Information Officer

### Step 5: Notification to Data Subjects (As soon as reasonably possible)
- Email notification to affected employees via their @zenowethu.co.za address
- Content includes: what happened, what data was affected, what we are doing, what they should do
- Notification tracked in breach register with delivery confirmation

### Step 6: Remediation and Closure
- Root cause analysis completed
- Security improvements implemented
- Breach register updated with remediation evidence
- Post-incident review conducted and documented

### 72-Hour Deadline Tracking
- BreachNotificationService automatically calculates the 72-hour deadline from breach discovery
- IsOverdue property flags breaches that have exceeded the notification deadline
- Dashboard visibility for HR Manager and Director

---

## 7. Data Subject Rights

### Right of Access (POPIA Section 23)

**Process**: An employee may request a copy of all personal information held about them.

| Step | Action | Timeline |
|------|--------|----------|
| 1 | Employee submits request via ZenoHR or email to hr@zenowethu.co.za | Day 0 |
| 2 | Request logged in data_subject_requests with SLA countdown | Immediate |
| 3 | HR Manager reviews request and verifies identity | Within 5 business days |
| 4 | Data package compiled (employee record, payroll history, leave records, audit entries) | Within 20 business days |
| 5 | Data package delivered to employee (secure download or encrypted email) | Within 30 calendar days |

**SLA**: 30 calendar days from receipt of request (POPIA Section 23(1)(b)).

**Fee**: A reasonable fee may be charged for manifestly unfounded or excessive requests. The prescribed fee is set by the Information Regulator.

### Right to Correction (POPIA Section 24)

**Process**: An employee may request correction or deletion of inaccurate or incomplete information.

| Step | Action | Timeline |
|------|--------|----------|
| 1 | Employee identifies inaccuracy and submits correction request | Day 0 |
| 2 | Request logged with details of current and proposed correct information | Immediate |
| 3 | HR Manager reviews and verifies the correction | Within 10 business days |
| 4 | If approved: new document created with corrected information (original preserved for audit trail) | Same day as approval |
| 5 | Employee notified of outcome | Within 30 calendar days |
| 6 | If correction relates to information shared with third parties (e.g., SARS), those parties are notified | Within 30 calendar days |

**Immutability**: Corrections do not modify the original record. A new record is created referencing the original, preserving the complete history for audit purposes.

### Right to Object (POPIA Section 11(3))

An employee may object to the processing of their personal information if:
- Processing is based on legitimate interest (not legal obligation or contract)
- Processing causes or is likely to cause damage or distress

**Note**: Most processing in ZenoHR is based on legal obligation (BCEA, SARS) or contractual necessity (employment contract). Objections to legally required processing (e.g., payroll, SARS filing) cannot be honoured as this would cause Zenowethu to breach statutory obligations.

Objections to non-essential processing (e.g., analytics, optional notifications) will be honoured within 30 calendar days.

---

## 8. Data Retention Policy

### Retention Periods

| Data Category | Minimum Retention | Maximum Retention | Legal Basis | Action After Expiry |
|--------------|-------------------|-------------------|-------------|-------------------|
| Payroll records | 3 years from end of tax year | 5 years | BCEA Section 31; POPIA Section 14 | Anonymise or destroy |
| SARS filings (IRP5, EMP201, etc.) | 5 years from submission | 7 years | Tax Administration Act | Anonymise or destroy |
| Employee contracts | Duration of employment | 3 years after termination | BCEA | Archive, then destroy |
| Leave records | 3 years from leave date | 5 years | BCEA | Anonymise or destroy |
| Timesheets | 3 years | 5 years | BCEA | Anonymise or destroy |
| Audit trail | 5 years minimum | 7 years | POPIA Section 14; good practice | Archive (anonymised) |
| Breach register | 5 years from closure | 7 years | POPIA Section 22 | Archive |
| Access review records | 3 years | 5 years | POPIA Section 19 | Destroy |

### Retention Lifecycle

1. **Active**: Record in use for current business operations
2. **Archived**: Record past active use but within retention period. Read-only access. Moved to cold storage.
3. **Legal Hold**: Record flagged for preservation due to pending legal matter. Overrides normal retention schedule. Cannot be destroyed.
4. **Destruction**: Record anonymised or securely destroyed after retention period expires. AuditEvent logged.

### Automated Enforcement
- Data archival runs automatically at 3:00 AM SAST
- Automatic 5-year retention from termination date
- BCEA 3-year minimum enforced (records cannot be destroyed before 3 years)
- HR Manager review required before destruction or anonymisation
- All retention actions logged in hash-chained audit trail

---

## 9. Technical Security Measures

### Encryption

| Layer | Method | Standard |
|-------|--------|----------|
| Data in transit | TLS 1.2+ (HTTPS) | Industry standard |
| Data at rest (database) | Google-managed encryption | AES-256 (Firestore default) |
| Data at rest (PII fields) | Application-level envelope encryption | AES-256 via Azure Key Vault DEK |
| Encryption key storage | Azure Key Vault | FIPS 140-2 Level 2 |
| Key rotation | 180-day automatic rotation | Azure Key Vault policy |

### Fields with Application-Level Encryption
- `national_id_or_passport` (employee national ID number)
- `tax_reference` (SARS tax reference number)
- `bank_account_ref` (bank account number)

### Audit Trail Integrity

- **Hash chain**: Each AuditEvent includes `previous_event_hash` -- the SHA-256 hash of the previous event's canonical JSON
- **Tamper detection**: Any modification to a historical audit event breaks the hash chain, which is detected on verification
- **Immutability**: Finalised records (PayrollRun, AuditEvent, AccrualLedgerEntry) are write-once. No updates, no deletes. Firestore security rules enforce this.
- **Corrections**: New adjustment documents reference the original. The original is never modified.

### Authentication and Access Control

| Measure | Implementation |
|---------|---------------|
| Authentication provider | Firebase Authentication (OIDC/JWT) |
| Multi-factor authentication | Required for privileged operations (payroll finalise, SARS approve, role management) |
| Session management | JWT with 1-hour expiry; idle session timeout for privileged operations |
| Role-based access | 5 system roles with database-level enforcement via Firestore security rules |
| Rate limiting | API: 100 req/min; Auth: 10 req/5min; Payroll: 20 req/min (per-tenant) |
| Security headers | CSP, X-Frame-Options=DENY, X-Content-Type-Options=nosniff, HSTS, Referrer-Policy=strict-origin |
| CORS | Restricted to deployment domain only |

### Tenant Isolation

- Every Firestore document has a `tenant_id` field
- All queries filter by `tenant_id`
- Firestore security rules enforce tenant scoping at the database level
- Cross-tenant data access is blocked even if application code is compromised

---

## 10. Organisational Measures

### Roles and Responsibilities

| Role | POPIA Responsibility |
|------|---------------------|
| **Information Officer** | Overall POPIA compliance; liaison with Information Regulator; breach notification authority |
| **Deputy Information Officer** | Assists IO; acts in IO's absence |
| **Director** | Data governance oversight; access review approval; break-glass access authorisation |
| **HR Manager** | Day-to-day data processing; SAR response; correction requests; access review participation |
| **SaasAdmin** | Platform security operations; vulnerability management; incident response |

### Access Review Schedule
- **Frequency**: Monthly
- **Process**: Automated snapshot of all user role assignments generated by background service
- **Approval**: Director or HR Manager reviews and approves
- **Audit trail**: Review outcomes recorded with hash-chained audit event
- **Remediation**: Inappropriate access removed within 5 business days of review

### Training and Awareness
- All users with access to ZenoHR must acknowledge the data processing notice on first login and on each version update
- HR Manager training on POPIA obligations, SAR handling, and breach response
- Annual POPIA awareness refresher for all staff

### Break-Glass Emergency Access
- For production emergencies where normal authentication is unavailable
- Requires: ticketed request with justification, Director + SaasAdmin dual approval
- Time-limited token (4-hour maximum)
- Mandatory post-event audit review within 24 hours
- All actions during break-glass access are logged in the hash-chained audit trail

---

## 11. Evidence and Artefacts

The following evidence is available to support a POPIA compliance audit:

| Evidence | Location | Description |
|----------|----------|-------------|
| Data classification scheme | `src/ZenoHR.Domain/Common/DataClassification.cs` | Enum classifying all fields as Public/Internal/Confidential/Restricted |
| PII masking implementation | `src/ZenoHR.Api/DTOs/EmployeeDtoMapper.cs` | Role-based field masking with national ID and tax ref redaction |
| Unmask purpose codes | `src/ZenoHR.Api/DTOs/UnmaskRequest.cs` | 7 approved POPIA purpose codes |
| Tenant isolation | `src/ZenoHR.Infrastructure/Firestore/BaseFirestoreRepository.cs` | tenant_id enforcement on every read |
| Security headers | `src/ZenoHR.Api/Middleware/SecurityHeadersExtensions.cs` | CSP, HSTS, X-Frame-Options configuration |
| MFA enforcement | `src/ZenoHR.Api/Auth/RequireMfaAttribute.cs` | MFA required on privileged operations |
| Rate limiting | `src/ZenoHR.Api/Security/RateLimitingExtensions.cs` | 3-tier per-tenant rate limiting |
| Log redaction | `src/ZenoHR.Api/Observability/LogRedactionProcessor.cs` | PII stripped from OpenTelemetry spans |
| Audit metadata sanitisation | `src/ZenoHR.Infrastructure/Audit/AuditMetadataSanitizer.cs` | HTML/script injection prevention in audit logs |
| Breach notification service | `src/ZenoHR.Module.Compliance/Services/BreachNotificationService.cs` | 72-hour deadline tracking, notification generation |
| Breach notification tests | `tests/ZenoHR.Module.Compliance.Tests/Popia/BreachNotificationServiceTests.cs` | 18 unit tests for breach handling |
| Security test suite | `tests/` (multiple projects) | 228+ security and compliance tests |
| Vulnerability register | `docs/security/vulnerability-register.md` | 28 findings tracked with remediation status |
| POPIA control tracker | `docs/security/popia-control-status.md` | 15 controls with implementation evidence |
| Stakeholder checklist | `docs/stakeholder-checklist.md` | Project progress with security summary |

---

## 12. Compliance Roadmap

### Completed
- Tenant data isolation (CTL-POPIA-003) -- fully implemented
- Security safeguards -- headers, MFA, rate limiting, CORS, audit trail
- Data classification scheme
- PII masking in API responses
- Breach notification domain logic with 18 tests
- Log redaction for telemetry
- Audit metadata sanitisation
- Data retention lifecycle (Active/Archived/LegalHold)

### In Progress
- Breach register persistence and UI (CTL-POPIA-011)
- Unmask API endpoint wiring (CTL-POPIA-002)
- Field-level encryption for PII at rest (CTL-POPIA-006)

### Pending
- Information Officer appointment and registration with Information Regulator
- Formal data processor inventory with signed DPAs (CTL-POPIA-014)
- Cross-border transfer documentation (CTL-POPIA-013)
- POPIA formal audit and sign-off

### Administrative Actions Required
1. **Appoint Information Officer** and register with the Information Regulator
2. **Appoint Deputy Information Officer** as backup
3. **Obtain formal DPAs** from Google Cloud and Microsoft Azure
4. **Schedule POPIA compliance audit** with qualified assessor
5. **Register with Information Regulator** as a responsible party

---

*This report is prepared for Zenowethu (Pty) Ltd compliance purposes. It should be reviewed by qualified legal counsel before submission to the Information Regulator. Contact: hr@zenowethu.co.za*
