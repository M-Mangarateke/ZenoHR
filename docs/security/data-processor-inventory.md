---
doc_id: SEC-DPI-001
version: 1.0.0
owner: Director
classification: Confidential
updated_on: 2026-03-13
applies_to: [ZenoHR Platform]
controls: [CTL-POPIA-013, CTL-POPIA-014]
---

# Data Processor Inventory & Cross-Border Transfer Register

## Executive Summary

This document maintains a complete inventory of all third-party data processors that process personal information on behalf of Zenowethu (Pty) Ltd through the ZenoHR platform, as required by POPIA Section 21 (processing by operator) and Section 72 (transborder information flows).

POPIA Section 21 requires that any operator (data processor) processing personal information on behalf of a responsible party must do so under a written contract that establishes the conditions for processing. POPIA Section 72 restricts the transfer of personal information outside the Republic of South Africa unless the recipient country provides an adequate level of protection or the data subject consents.

This inventory is reviewed annually and updated whenever a new processor is onboarded or an existing processor's data handling changes.

---

## Processor Inventory

| Processor | Service Used | Data Types Processed | Region | DPA Status | POPIA S72 Basis |
|-----------|-------------|---------------------|--------|-----------|----------------|
| Google Cloud (Firebase) | Firebase Authentication | Email, password hash, MFA tokens, login history | Multi-region (user metadata) | Required — Google Cloud DPA | Adequate protection (EU adequacy) |
| Google Cloud (Firestore) | Cloud Firestore | All employee PII, payroll data, audit events | africa-south1 (Johannesburg) | Required — Google Cloud DPA | SA-resident data (no cross-border) |
| Microsoft Azure | Container Apps (hosting) | Application runtime, no PII at rest | South Africa North (Johannesburg) | Required — Microsoft DPA | SA-resident hosting (no cross-border) |
| Microsoft Azure | Key Vault | Encryption keys, connection strings | South Africa North | Required — Microsoft DPA | SA-resident (no cross-border) |
| Microsoft Azure | Application Insights / Monitor | Telemetry, sanitised logs (PII redacted by LogRedactionProcessor) | South Africa North | Required — Microsoft DPA | SA-resident (no cross-border) |
| QuestPDF | PDF generation library | Employee data for payslips (in-process only) | N/A (in-process, no data transfer) | N/A (no operator relationship) | N/A |
| MediatR | In-process event bus | Domain events (in-process only) | N/A (in-process) | N/A | N/A |

---

## Cross-Border Transfer Analysis

### Processors with SA-Resident Data (No Cross-Border Transfer)

The following processors store and process data exclusively within South African data centres. No POPIA Section 72 cross-border transfer analysis is required:

- **Google Cloud Firestore (africa-south1)** — All employee PII, payroll records, audit events, and compliance data reside in the Johannesburg region. Firestore location is locked at project creation and cannot be changed.
- **Microsoft Azure Container Apps (South Africa North)** — Application hosting only. No PII is persisted at this layer; all data flows through to Firestore.
- **Microsoft Azure Key Vault (South Africa North)** — Encryption keys and connection strings. No employee PII stored.
- **Microsoft Azure Application Insights (South Africa North)** — Telemetry and logs. The `LogRedactionProcessor` middleware strips all PII before data reaches Application Insights, ensuring no personal information is stored in telemetry.

### Processors with Potential Cross-Border Data Flow

| Processor | Cross-Border Risk | Mitigation |
|-----------|------------------|------------|
| **Google Cloud — Firebase Authentication** | **Medium** — Firebase Auth user metadata (email, password hash, MFA tokens, login timestamps) may be stored in Google's global infrastructure outside South Africa. Google does not guarantee Firebase Auth data residency in a specific region. | 1. Google Cloud DPA provides GDPR-equivalent protection. 2. EU adequacy decision applies (POPIA S72(1)(a)). 3. Data is limited to authentication metadata only — no salary, employment, or sensitive PII. 4. Consider migration to Azure AD B2C (SA North) for full data residency if risk is deemed unacceptable. |

### In-Process Libraries (No Transfer)

QuestPDF and MediatR are .NET libraries that execute entirely within the application process boundary. No data leaves the application container. These are not data processors under POPIA and require no DPA.

---

## DPA Action Items

| # | Processor | Action Required | Responsible | Target Date | Status |
|---|-----------|----------------|-------------|-------------|--------|
| 1 | Google Cloud (Firebase Auth + Firestore) | Obtain and file Google Cloud Data Processing Addendum (DPA) | Director | 2026-04-15 | Pending |
| 2 | Microsoft Azure (Container Apps, Key Vault, App Insights) | Obtain and file Microsoft Online Services DPA | Director | 2026-04-15 | Pending |
| 3 | Google Cloud (Firebase Auth) | Document Firebase Auth data residency risk acceptance or migration plan | Director | 2026-05-01 | Pending |

---

## Firebase Auth Cross-Border Risk Analysis

### Risk Description

Firebase Authentication is a globally distributed service. While Firestore allows region selection (we use `africa-south1`), Firebase Auth does not provide the same regional data residency guarantees. User metadata — including email addresses, password hashes, MFA configuration, and login history — may be stored and replicated across Google's global infrastructure, potentially including data centres outside South Africa.

### POPIA Section 72 Assessment

POPIA Section 72(1) prohibits transferring personal information to a foreign country unless:

- **(a)** The recipient country provides an adequate level of protection — Google operates under EU GDPR, which provides adequate protection.
- **(b)** The data subject consents — Possible but operationally burdensome.
- **(c)** Transfer is necessary for contract performance — Authentication is necessary for system access.
- **(d)** Transfer is in the legitimate interest of the data subject — System security protects the data subject.

**Assessment**: The transfer of authentication metadata to Google's global infrastructure is defensible under POPIA S72(1)(a) (adequate protection via EU GDPR) and S72(1)(c) (necessary for contract performance). However, this represents the only cross-border data flow in the ZenoHR platform and should be monitored.

### Mitigation Options

1. **Accept risk with documentation** (recommended for v1.0) — Document the risk, obtain Google Cloud DPA, and include Firebase Auth data flow in the POPIA Section 18 notice to employees.
2. **Migrate to Azure AD B2C** (future consideration) — Azure AD B2C supports South Africa North region, which would eliminate all cross-border data flows. This is a significant architectural change and should be evaluated for v2.0.

---

## Annual Review Schedule

| Review Activity | Frequency | Next Review | Responsible |
|----------------|-----------|-------------|-------------|
| Full processor inventory review | Annual | 2027-03-13 | Director |
| DPA expiry / renewal check | Annual | 2027-03-13 | Director |
| Cross-border transfer risk assessment | Annual | 2027-03-13 | Director |
| Firebase Auth data residency review | Semi-annual | 2026-09-13 | Director |
| New processor onboarding check | On each new vendor | Ongoing | Director |

---

## POPIA Section 72 Compliance Checklist

- [ ] All processors with access to personal information identified and documented
- [ ] Data types processed by each processor catalogued
- [ ] Data residency region confirmed for each processor
- [ ] Cross-border transfers identified and S72 legal basis documented
- [ ] Google Cloud Data Processing Addendum obtained and filed
- [ ] Microsoft Online Services DPA obtained and filed
- [ ] Firebase Auth cross-border risk formally accepted or migration planned
- [ ] Employee POPIA Section 18 notice updated to include processor disclosures
- [ ] Annual review schedule established and calendar reminders set
- [ ] Processor inventory integrated into ZenoHR compliance module (DataProcessorRegistryService)

---

## Revision History

| Version | Date | Author | Change |
|---------|------|--------|--------|
| 1.0.0 | 2026-03-13 | System | Initial processor inventory and cross-border analysis |
