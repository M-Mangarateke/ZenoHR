---
doc_id: STAKE-05-AUDIT-PREP
version: 1.0.0
updated_on: 2026-03-13
owner: Director / HRManager / Information Officer
classification: Confidential
applies_to: [Zenowethu (Pty) Ltd, ZenoHR Platform]
---

# Audit Preparation Guide

**For**: Directors, HR Managers, and Information Officers at Zenowethu (Pty) Ltd
**Purpose**: Preparing for POPIA compliance audits and SARS inspections
**Date**: 13 March 2026
**Classification**: Confidential

---

## Table of Contents

1. [Overview](#1-overview)
2. [POPIA Audit Preparation](#2-popia-audit-preparation)
3. [SARS Inspection Preparation](#3-sars-inspection-preparation)
4. [Where to Find Evidence in ZenoHR](#4-where-to-find-evidence-in-zenohr)
5. [Generating an Evidence Pack](#5-generating-an-evidence-pack)
6. [Key Documents to Have Ready](#6-key-documents-to-have-ready)
7. [Contact Information Template](#7-contact-information-template)
8. [Timeline Expectations](#8-timeline-expectations)
9. [Common Audit Findings and How ZenoHR Addresses Them](#9-common-audit-findings-and-how-zenohr-addresses-them)
10. [Pre-Audit Checklist](#10-pre-audit-checklist)

---

## 1. Overview

This guide helps you prepare for two types of external scrutiny:

1. **POPIA compliance audit**: An assessment by the Information Regulator of South Africa or a qualified assessor to verify that Zenowethu (Pty) Ltd complies with the Protection of Personal Information Act (Act 4 of 2013).

2. **SARS inspection**: A review by the South African Revenue Service to verify correct calculation and payment of employees' tax (PAYE), UIF, SDL, and ETI, and timely submission of employer declarations.

Both types of audit require documented evidence. ZenoHR is designed to produce this evidence systematically.

---

## 2. POPIA Audit Preparation

### What Auditors Will Ask For

A POPIA audit typically covers the following areas. For each, we note where ZenoHR provides evidence:

#### 2.1 Information Officer Registration

| Question | Expected Answer |
|----------|----------------|
| Has an Information Officer been appointed? | [To be confirmed -- appointment pending] |
| Is the IO registered with the Information Regulator? | [To be confirmed] |
| Is there a Deputy Information Officer? | [To be confirmed] |

**Action required**: Appoint and register an Information Officer before the audit.

#### 2.2 Lawful Basis for Processing

| Question | Expected Answer |
|----------|----------------|
| What is the lawful basis for processing employee data? | Legal obligation (BCEA, SARS) and contractual necessity (employment contracts). Documented in `docs/stakeholder/popia-compliance-report.md`. |
| Is there a register of processing activities? | Yes -- the POPIA compliance report documents all processing activities, purposes, and lawful bases. |
| Are all processing activities necessary and proportionate? | Yes -- ZenoHR processes only data required for payroll, compliance, and employment administration. |

**Evidence in ZenoHR**: Navigate to Compliance > POPIA Controls to see the processing basis for each data category.

#### 2.3 Data Subject Notification

| Question | Expected Answer |
|----------|----------------|
| Are data subjects (employees) informed about data processing? | Yes -- data processing notice is shown on first login and when the notice is updated. |
| Is there a versioned notice? | Yes -- notices are versioned with acknowledgment tracking. |
| Can you demonstrate employee acknowledgment? | Yes -- acknowledgment records are stored per employee. |

**Evidence in ZenoHR**: Employee records show notice acknowledgment status. Audit trail records each acknowledgment event.

#### 2.4 Security Safeguards

| Question | Expected Answer |
|----------|----------------|
| What encryption is used? | AES-256 for data at rest (field-level for PII), TLS 1.2+ for data in transit. |
| How is access controlled? | Firebase Authentication with MFA, 5-tier role-based access, Firestore security rules. |
| Is there an audit trail? | Yes -- SHA-256 hash-chained, tamper-evident, immutable. |
| Are there access reviews? | Yes -- monthly automated reviews with Director/HR Manager approval. |
| Is there incident response? | Yes -- automated anomaly detection, severity classification, escalation workflow. |

**Evidence in ZenoHR**: Navigate to Audit Trail for hash-chain verification. Navigate to Settings > Security for access review history. Navigate to Compliance > POPIA Controls for control status.

#### 2.5 Breach Management

| Question | Expected Answer |
|----------|----------------|
| Is there a breach register? | Yes -- maintained in ZenoHR with full lifecycle tracking. |
| What is the notification procedure? | 72-hour notification to Information Regulator for qualifying breaches. Automated deadline tracking. |
| Has there been a breach? | [Answer based on current breach register -- if no breaches, state "No breaches recorded to date."] |

**Evidence in ZenoHR**: Breach register with status history, notification records, and remediation evidence.

#### 2.6 Data Subject Rights

| Question | Expected Answer |
|----------|----------------|
| How do employees request access to their data? | Submit request via ZenoHR or email to hr@zenowethu.co.za. 30-day SLA tracked. |
| How are corrections handled? | Correction request workflow with HR approval. New document created (original preserved). |
| Can employees object to processing? | Yes, for non-essential processing. Legal obligation processing (payroll, SARS) cannot be objected to. |

**Evidence in ZenoHR**: Data subject request records with SLA tracking. Correction audit trail showing original and corrected values.

#### 2.7 Data Retention

| Question | Expected Answer |
|----------|----------------|
| What is the retention policy? | BCEA: 3 years minimum for payroll. POPIA: 5 years general. SARS: 7 years for filings. Audit: 7 years. |
| Is retention enforced automatically? | Yes -- automated archival at 3 AM SAST, HR review before destruction. |
| How is data destroyed? | Anonymisation or secure deletion with audit trail record. Legal hold prevents destruction during disputes. |

**Evidence in ZenoHR**: Navigate to Settings > Archival for retention status. Audit trail shows all data lifecycle events.

#### 2.8 Cross-Border Transfers

| Question | Expected Answer |
|----------|----------------|
| Is personal information transferred outside South Africa? | No -- all data stored in Azure SA North (Johannesburg) and Firestore africa-south1 (Johannesburg). |
| Who are the data processors? | Google Cloud (Firestore, Firebase Auth), Microsoft Azure (Container Apps, Monitor, Key Vault). |
| Are there Data Processing Agreements? | In progress -- DPAs to be formally documented. Google and Microsoft provide standard DPAs. |

**Evidence**: Data processor inventory (`docs/security/data-processor-inventory.md`), architecture documentation showing SA-region deployment.

---

## 3. SARS Inspection Preparation

### What SARS Will Ask For

#### 3.1 Payroll Records

| Requirement | ZenoHR Evidence |
|-------------|----------------|
| Payroll records for the past 3 years (BCEA Section 31) | Payroll runs stored in Firestore with full calculation breakdowns. Navigate to Payroll > select period. |
| Individual employee payslips | PDF payslips generated for every pay period. Download from Payroll > [Run] > [Employee]. |
| Gross-to-net breakdown per employee | Each payroll result shows: gross, PAYE, UIF employee, UIF employer, pension, medical, other deductions, net pay. |
| Tax calculation method used | Annual equivalent method per SARS specifications. Documented in `docs/prd/16_payroll_calculation_spec.md`. |

#### 3.2 Tax Certificates

| Requirement | ZenoHR Evidence |
|-------------|----------------|
| IRP5 certificates for all employees | Generated from Compliance > IRP5. Includes all SARS source codes. |
| IT3(a) certificates (if applicable) | Generated alongside IRP5 for non-employees. |
| Reconciliation of IRP5 totals to EMP201 submissions | Compliance module reconciles totals automatically. |

#### 3.3 Employer Declarations

| Requirement | ZenoHR Evidence |
|-------------|----------------|
| EMP201 monthly declarations | Generated from Compliance > EMP201. Status tracked (draft, submitted, accepted). |
| EMP501 mid-year and annual reconciliation | Generated from Compliance > EMP501. |
| Proof of timely submission | Submission timestamps in compliance records. Compliance schedule tracks deadlines. |

#### 3.4 UIF, SDL, and ETI

| Requirement | ZenoHR Evidence |
|-------------|----------------|
| UIF contributions (employee + employer) | Shown on each payslip. UIF ceiling (R17,712/month) enforced automatically. |
| SDL calculations and threshold | SDL at 1% of payroll. R500,000 annual payroll exemption threshold checked. |
| ETI claims and qualifying criteria | ETI calculated for employees aged 18-29 within R2,500-R7,500 remuneration band. Age and remuneration verified. |

#### 3.5 Employee Registration

| Requirement | ZenoHR Evidence |
|-------------|----------------|
| ITREG registration for new employees | ITREG workflow in Compliance module. |
| Tax reference numbers for all employees | Stored in employee records (masked by default; unmask with purpose code). |
| Proof of tax status validation | SA ID and tax reference format validation on data entry. |

---

## 4. Where to Find Evidence in ZenoHR

### Navigation Guide for Evidence

| Evidence Needed | Where to Find It |
|-----------------|-----------------|
| Employee records | Employees > [Employee Name] |
| Employment contracts | Employees > [Employee Name] > Contracts tab |
| Payroll calculations | Payroll > [Payroll Run] > [Employee] |
| Payslips (PDF) | Payroll > [Payroll Run] > [Employee] > Download PDF |
| IRP5 tax certificates | Compliance > IRP5 |
| EMP201 declarations | Compliance > EMP201 |
| EMP501 reconciliation | Compliance > EMP501 |
| Leave balances | Leave > [Employee] > Balance |
| Leave accrual ledger | Leave > [Employee] > Accrual Ledger |
| Timesheet records | Timesheets > [Week] |
| Audit trail | Audit Trail > filter by date/user/action |
| POPIA control status | Compliance > POPIA Controls |
| Security settings | Settings > Security |
| Access review history | Settings > Security > Access Reviews |
| Breach register | Compliance > Breach Register (if applicable) |
| Data subject requests | Compliance > Data Subject Requests |
| Company settings | Settings > Company |
| Department structure | Settings > Departments |
| User accounts and roles | Settings > Users |
| Role definitions | Settings > Roles |

### Audit Trail Filters

The Audit Trail page supports filtering by:
- **Date range**: e.g., "1 March 2025 to 28 February 2026" for a tax year
- **User**: specific user's actions
- **Action type**: PayrollFinalised, LeaveApproved, EmployeeCreated, ComplianceSubmitted, etc.
- **Module**: Employee, Payroll, Leave, Compliance, Audit, Settings
- **Entity ID**: specific employee or payroll run

Each audit entry shows the timestamp, user, action, entity affected, and hash-chain verification status.

---

## 5. Generating an Evidence Pack

An evidence pack is a PDF bundle containing all relevant records for a specific period, suitable for handing to auditors.

### Steps to Generate

1. Navigate to **Audit Trail**
2. Click **Generate Evidence Pack**
3. Configure the scope:
   - **Date range**: Select the period the auditor is interested in
   - **Module(s)**: Select which areas to include (e.g., Payroll + Compliance for a SARS inspection)
   - **Employees**: All employees or specific individuals
4. Click **Generate**
5. The system compiles:
   - All audit trail events for the selected scope
   - Relevant payroll calculations and payslips
   - Compliance submissions and their status
   - Hash-chain verification summary
   - System configuration at the time (statutory rule sets in effect)
6. Download the evidence pack as a PDF

### Evidence Pack Contents

A typical evidence pack includes:

| Section | Content |
|---------|---------|
| Cover page | Period, scope, generation date, generated by |
| Summary | Key metrics: employee count, total payroll, filing status |
| Audit trail | Chronological list of all events with hash verification |
| Payroll details | Per-employee calculation breakdowns |
| Payslips | Individual payslip PDFs |
| Compliance | Filing records (EMP201, IRP5, etc.) with submission status |
| Configuration | Statutory rule sets in effect during the period |
| Hash chain | Verification results confirming audit trail integrity |
| Appendix | System architecture and security controls summary |

---

## 6. Key Documents to Have Ready

Prepare the following documents before any audit:

### For POPIA Audit

| Document | Location | Notes |
|----------|----------|-------|
| POPIA compliance report | `docs/stakeholder/popia-compliance-report.md` | Comprehensive report of all 15 controls |
| Vulnerability register | `docs/security/vulnerability-register.md` | All findings with remediation status |
| POPIA control status | `docs/security/popia-control-status.md` | Detailed per-control assessment |
| Data processor inventory | `docs/security/data-processor-inventory.md` | All third-party processors with DPA status |
| Information Officer appointment letter | [Company records] | Must be provided separately |
| Data processing notice | [ZenoHR system -- notice versioning] | Current version with employee acknowledgments |
| Breach register | [ZenoHR system -- Compliance > Breach Register] | All recorded breaches (if any) |
| Access review records | [ZenoHR system -- Settings > Security] | Monthly review history |
| Evidence pack | [Generated from ZenoHR -- Audit Trail] | For the audit period |

### For SARS Inspection

| Document | Location | Notes |
|----------|----------|-------|
| IRP5 certificates | ZenoHR Compliance > IRP5 | For all employees in scope period |
| EMP201 submissions | ZenoHR Compliance > EMP201 | Monthly declarations |
| EMP501 reconciliation | ZenoHR Compliance > EMP501 | Mid-year and annual |
| Payslips | ZenoHR Payroll > [Run] > [Employee] | For all employees in scope period |
| Payroll calculations | ZenoHR Payroll > [Run] | Gross-to-net breakdowns |
| Statutory rule sets | `docs/seed-data/sars-paye-2025-2026.json` | Tax tables used |
| ETI claims evidence | ZenoHR Payroll (ETI-eligible employees) | Age and remuneration verification |
| ITREG records | ZenoHR Compliance > ITREG | New employee registrations |
| Evidence pack | [Generated from ZenoHR -- Audit Trail] | For the inspection period |

---

## 7. Contact Information Template

Prepare the following contact information for auditors:

```
COMPANY INFORMATION
Company name:        Zenowethu (Pty) Ltd
Registration number: [Company registration number]
Physical address:    [Company address]
Postal address:      [Company postal address]
Telephone:           [Company telephone]
Email:               admin@zenowethu.co.za

INFORMATION OFFICER (POPIA)
Name:                [To be appointed]
Email:               [io@zenowethu.co.za]
Telephone:           [Contact number]

DIRECTOR
Name:                [Director name]
Email:               [director@zenowethu.co.za]
Telephone:           [Contact number]

HR MANAGER
Name:                [HR Manager name]
Email:               hr@zenowethu.co.za
Telephone:           [Contact number]

TECHNICAL CONTACT
Name:                [Technical contact name]
Email:               admin@zenowethu.co.za
Telephone:           [Contact number]

TAX PRACTITIONER (if applicable)
Name:                [Tax practitioner name]
Practice number:     [SARS practice number]
Email:               [Tax practitioner email]
Telephone:           [Contact number]

EXTERNAL LEGAL COUNSEL (if applicable)
Firm:                [Law firm name]
Contact:             [Lawyer name]
Email:               [Lawyer email]
Telephone:           [Contact number]
```

---

## 8. Timeline Expectations

### POPIA Audit Timeline

| Phase | Duration | Activities |
|-------|----------|-----------|
| **Notification** | 2-4 weeks before | Information Regulator or assessor notifies of upcoming audit |
| **Preparation** | 1-2 weeks | Gather documents, generate evidence packs, brief key personnel |
| **On-site/remote audit** | 2-5 business days | Auditor reviews documentation, interviews key personnel, examines systems |
| **Draft findings** | 2-4 weeks after | Auditor shares preliminary findings |
| **Management response** | 2 weeks | Zenowethu responds to findings with remediation plan |
| **Final report** | 2-4 weeks | Auditor issues final assessment |
| **Remediation** | As specified | Implement any required changes within agreed timeline |

### SARS Inspection Timeline

| Phase | Duration | Activities |
|-------|----------|-----------|
| **Notification** | Varies (can be immediate) | SARS issues inspection notice |
| **Document submission** | 21 business days (typical) | Submit requested records |
| **Review** | 4-8 weeks | SARS reviews submitted records |
| **Queries** | Ongoing | SARS may request additional information |
| **Assessment** | 2-4 weeks after review | SARS issues assessment (if adjustments needed) |
| **Objection** | 30 business days | If disagreeing with assessment, formal objection can be lodged |

### Preparation Actions (Start Immediately on Notification)

1. Identify the scope and period of the audit/inspection
2. Generate evidence packs for the relevant period
3. Review the breach register for any incidents during the period
4. Verify that all filings for the period are complete and submitted
5. Brief the Information Officer, HR Manager, and Director
6. Engage external legal counsel if needed
7. Prepare the contact information template
8. Test evidence pack generation to ensure it works correctly

---

## 9. Common Audit Findings and How ZenoHR Addresses Them

### POPIA Common Findings

| Common Finding | How ZenoHR Addresses It |
|---------------|------------------------|
| **No Information Officer appointed** | Administrative action required. ZenoHR cannot address this -- an IO must be appointed and registered with the Information Regulator. |
| **No record of processing activities** | ZenoHR's POPIA compliance report (`docs/stakeholder/popia-compliance-report.md`) documents all processing activities, purposes, and lawful bases. |
| **No data subject notification** | ZenoHR shows a versioned data processing notice on first login and on notice updates. Employee acknowledgments are tracked. |
| **Inadequate access controls** | Five-tier RBAC with database-level enforcement. MFA on privileged operations. Monthly access reviews with Director sign-off. |
| **No encryption of sensitive data** | AES-256 field-level encryption for national IDs, tax references, and bank accounts. TLS for data in transit. |
| **No audit trail** | SHA-256 hash-chained audit trail recording every action. Tamper-evident, immutable, searchable. |
| **No breach notification procedure** | Automated 72-hour deadline tracking. Breach register with full lifecycle management. Notification text generation for Information Regulator. |
| **No data retention policy** | Automated retention enforcement: BCEA 3 years, POPIA 5 years, SARS 7 years. HR review before destruction. Audit trail for all data lifecycle events. |
| **Cross-border data without adequate protection** | All data stored in South Africa (Azure SA North, Firestore africa-south1). Google and Microsoft provide standard DPAs with SCCs. |
| **No regular security testing** | 228+ automated security tests run on every code change. Vulnerability register with monthly review cycle. |

### SARS Common Findings

| Common Finding | How ZenoHR Addresses It |
|---------------|------------------------|
| **Incorrect PAYE calculation** | Annual equivalent method per SARS spec. Property-based tests verify calculations against SARS tables. MoneyZAR ensures cent-accurate arithmetic. |
| **UIF not calculated or incorrect** | UIF automatically calculated at 1% employee + 1% employer. R17,712 monthly ceiling enforced. Cannot be skipped. |
| **SDL not paid or incorrect** | SDL at 1% employer. R500,000 annual payroll exemption threshold checked automatically. |
| **ETI claimed for non-qualifying employees** | Age (18-29), minimum wage (R2,500), and maximum remuneration (R7,500) verified automatically. Non-qualifying employees excluded. |
| **Late EMP201 submission** | Compliance schedule tracks EMP201 due dates (7th of each month). Automated reminders sent as deadlines approach. |
| **Missing or incomplete IRP5s** | IRP5 generator includes all required SARS source codes. Validation checks for missing tax references or incomplete data. |
| **Payslips missing BCEA Section 33 fields** | Payslip template includes all 9 mandatory sections per BCEA Section 33. QuestPDF template validated against requirements. |
| **Payroll records not retained for 3 years** | Automated retention enforcement. BCEA 3-year minimum prevents destruction. Legal hold available for disputes. |
| **Tax tables outdated** | Statutory rule sets loaded from configuration (not hardcoded). Current period uses SARS 2025/2026 tables. New tables can be loaded without code changes. |

---

## 10. Pre-Audit Checklist

Use this checklist when preparing for an audit or inspection:

### Administrative Preparation

- [ ] Information Officer appointed and registered with Information Regulator (POPIA audit)
- [ ] Deputy Information Officer appointed (POPIA audit)
- [ ] Contact information template completed (Section 7 of this guide)
- [ ] External legal counsel engaged (if needed)
- [ ] Key personnel briefed (Director, HR Manager, IO, technical contact)
- [ ] Audit scope and period confirmed with auditor/inspector

### Document Preparation

- [ ] POPIA compliance report reviewed and up to date
- [ ] Vulnerability register reviewed (no unresolved Sev-1 findings)
- [ ] POPIA control status reviewed (all controls at least partially implemented)
- [ ] Data processor inventory complete with DPA status
- [ ] Breach register reviewed (all breaches properly closed)

### System Preparation

- [ ] Evidence pack generated for the audit period
- [ ] Payroll records verified for the audit period
- [ ] IRP5 certificates generated and verified
- [ ] EMP201 submissions confirmed for the audit period
- [ ] Access review records available for the audit period
- [ ] Audit trail hash-chain verification run (no broken chains)
- [ ] Data subject request records reviewed (all within SLA)

### Technical Preparation

- [ ] System accessible for auditor demonstration (if required)
- [ ] Test environment available (auditors should not access production data)
- [ ] Security headers verified (CSP, HSTS, X-Frame-Options)
- [ ] Rate limiting active
- [ ] MFA enforcement verified on privileged operations
- [ ] Most recent CI/CD pipeline results available (all gates passing)

### Post-Audit Actions

- [ ] Document all findings from the auditor
- [ ] Create remediation plan with timelines
- [ ] Update vulnerability register with any new findings
- [ ] Update POPIA control status if controls need improvement
- [ ] Schedule follow-up with auditor to confirm remediation
- [ ] Brief Director and HR Manager on outcomes

---

*This guide is prepared for Zenowethu (Pty) Ltd. It should be reviewed by qualified legal counsel for completeness before any audit. For technical support, contact admin@zenowethu.co.za.*
