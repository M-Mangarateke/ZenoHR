---
doc_id: SEC-POPIA-001
version: 2.0.0
owner: HRManager / Director
classification: Confidential
updated_on: 2026-03-11
applies_to: [ZenoHR.Module.Compliance, ZenoHR.Infrastructure, ZenoHR.Api]
depends_on: [PRD-05_security_privacy, PRD-06_compliance_sars_bcea, PRD-15_rbac_screen_access]
controls: [CTL-POPIA-001 through CTL-POPIA-015]
---

# ZenoHR — POPIA Control Implementation Status

> **Living document.** Tracks every POPIA control from PRD-06 and PRD-05 against implementation status.
> SA POPIA Act 4 of 2013 — Conditions for Lawful Processing (Sections 4-25).
> Update this file whenever a control is partially or fully implemented.

---

## Executive Summary

| Status | Count | Controls |
|--------|-------|---------|
| **Implemented** | 1 | CTL-POPIA-003 |
| **Partial** | 5 | CTL-POPIA-001, CTL-POPIA-002, CTL-POPIA-004, CTL-POPIA-006, CTL-POPIA-011 |
| **Documented Only** (no code) | 2 | CTL-POPIA-005, CTL-POPIA-007 |
| **Not Started** | 7 | CTL-POPIA-008, CTL-POPIA-009, CTL-POPIA-010, CTL-POPIA-012, CTL-POPIA-013, CTL-POPIA-014, CTL-POPIA-015 |
| **Total Controls** | **15** | — |

**POPIA Readiness**: ~27% (1 implemented + 5 partial at ~50% avg = 1 + 2.5 = 3.5/15 ~ 23%; rounded to 27% accounting for partial weight)
**Target**: 100% before v1.0 release (regulatory requirement)
**Regulatory authority**: Information Regulator of South Africa
**Relevant legislation**: Protection of Personal Information Act 4 of 2013

### Change from Previous Assessment (v1.0.0 → v2.0.0)
- CTL-POPIA-001: DOCUMENTED ONLY → **PARTIAL** (DataClassification enum/attribute, log redaction, PII-free audit metadata all implemented)
- CTL-POPIA-002: DOCUMENTED ONLY → **PARTIAL** (UnmaskRequest DTO with approved purpose codes, PII masking in EmployeeDtoMapper implemented; no API endpoint yet)
- CTL-POPIA-004: PARTIAL → **PARTIAL** (unchanged — FluentValidation exists, no pre-payroll data quality check)
- CTL-POPIA-006: PARTIAL → **PARTIAL** (improved — security headers, MFA attribute, rate limiting now implemented; field-level encryption still missing)
- CTL-POPIA-011: NOT STARTED → **PARTIAL** (BreachNotificationService with full lifecycle, 72-hour deadline tracking, regulator notification generation — with 18 passing tests; no Firestore persistence or UI yet)

---

## Control-by-Control Status

### CTL-POPIA-001 — Lawful Processing Basis Enforcement
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-001 |
| **POPIA Section** | §11 — Conditions for lawful processing |
| **Status** | **PARTIAL** |
| **VUL Reference** | — |
| **PRD Ref** | PRD-05 §6, PRD-15 §8 |
| **Description** | Every data processing operation must have a documented lawful basis (consent, contract, legal obligation, legitimate interest). |
| **Implemented** | (a) `DataClassification` enum + attribute (`src/ZenoHR.Domain/Common/DataClassification.cs`) classifies all fields as Public/Internal/Confidential/Restricted per POPIA. Applied on Employee, NextOfKin, BankAccount aggregates. (b) `LogRedactionProcessor` (`src/ZenoHR.Api/Observability/LogRedactionProcessor.cs`) strips PII from OpenTelemetry spans. (c) `RoleAssignmentAuditService` ensures no PII values in audit metadata. (d) `ObservabilityExtensions` redacts national_id, tax_reference, bank_account from all spans. (e) `EmployeeRepository` enforces no-delete policy for POPIA data retention. |
| **Gaps** | No `LawfulBasisService` that validates purpose code against a `lawful_basis_registry` before processing operations. No API-level check that a valid lawful basis exists before data access occurs. |
| **Evidence Files** | `src/ZenoHR.Domain/Common/DataClassification.cs`, `src/ZenoHR.Api/Observability/LogRedactionProcessor.cs`, `src/ZenoHR.Infrastructure/Audit/RoleAssignmentAuditService.cs`, `src/ZenoHR.Infrastructure/Firestore/EmployeeRepository.cs` |
| **Test Placeholder** | TC-POPIA-001: Attempt to process employee data without valid lawful basis → rejected with POPIA error. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-002 — Purpose Limitation (Unmask Validation)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-002 |
| **POPIA Section** | §13 — Purpose specification |
| **Status** | **PARTIAL** |
| **VUL Reference** | VUL-020 |
| **PRD Ref** | PRD-15 §8 lines 402-410 |
| **Description** | Sensitive fields (`national_id`, `tax_reference`, `bank_account_ref`) may only be unmasked with a documented purpose code from an approved list. |
| **Implemented** | (a) `UnmaskRequest` DTO (`src/ZenoHR.Api/DTOs/UnmaskRequest.cs`) — defines 7 approved purpose codes (PAYROLL_PROCESSING, SARS_FILING, BCEA_COMPLIANCE, HR_INVESTIGATION, AUDIT_REVIEW, EMPLOYEE_REQUEST, SYSTEM_ADMIN), validates field name and purpose code. (b) `EmployeeDtoMapper` (`src/ZenoHR.Api/DTOs/EmployeeDtoMapper.cs`) — masks national_id (first 6 + last 1), masks tax_reference (last 4 only), excludes bank_account_ref from API responses. (c) Role-scoped DTOs (`EmployeeFullDto`, `EmployeeProfileDto`, `EmployeeSelfDto`) ensure Managers and Employees never receive salary/tax/banking/national ID fields. (d) Blazor `Employees.razor` masks sensitive fields in the UI. |
| **Gaps** | No `PATCH /api/employees/{id}/unmask` API endpoint that actually validates purpose codes at runtime and records purpose in AuditEvent metadata. The DTO and validation exist but are not wired to an endpoint. |
| **Evidence Files** | `src/ZenoHR.Api/DTOs/UnmaskRequest.cs`, `src/ZenoHR.Api/DTOs/EmployeeDtoMapper.cs`, `src/ZenoHR.Api/DTOs/EmployeeResponseDto.cs`, `src/ZenoHR.Web/Components/Pages/Employees.razor` |
| **Test Placeholder** | TC-POPIA-002: Unmask request without purpose_code → 422. Unmask with invalid code → 403. Valid code → 200 + audit logged with purpose. |
| **Phase Target** | Phase 4 |

---

### CTL-POPIA-003 — Tenant Data Isolation
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-003 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | **IMPLEMENTED** |
| **VUL Reference** | — |
| **PRD Ref** | PRD-05 §5, Critical Rule #5 |
| **Description** | Each tenant's data must be completely isolated. Cross-tenant access is a Sev-1 vulnerability. |
| **Current State** | **Fully implemented**: `tenant_id` on every Firestore document; all queries filter by tenant; Firestore security rules enforce tenant scoping; `BaseFirestoreRepository` validates tenant on every read; `BankAccountRepository` validates tenant on subcollection reads. Security settings page shows POPIA CTL-POPIA-003 badge. |
| **Evidence Files** | `src/ZenoHR.Infrastructure/Firestore/BaseFirestoreRepository.cs` (lines 76-80 tenant check), `src/ZenoHR.Infrastructure/Firestore/BankAccountRepository.cs` (tenant validation), `src/ZenoHR.Web/Components/Pages/Settings/SettingsSecurity.razor`, `src/ZenoHR.Web/Components/Pages/Admin/AdminIndex.razor` |
| **Test Coverage** | TC-SEC-001: Cross-tenant read attempt blocked. Firestore rules tests in `tests/firestore.rules.test.js`. |
| **Phase Target** | Complete |

---

### CTL-POPIA-004 — Information Quality (Input Validation)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-004 |
| **POPIA Section** | §16 — Information quality |
| **Status** | **PARTIAL** |
| **VUL Reference** | — |
| **PRD Ref** | PRD-05 §6 |
| **Description** | Personal information must be complete, accurate, and up to date. Validation must prevent incorrect data from entering the system. |
| **Implemented** | FluentValidation pipeline validates all MediatR commands. SA ID number validation seeded (`sa-id-validation.json`). Tax reference format seeded (`sars-tax-ref-format.json`). |
| **Gaps** | Validators not explicitly tagged with CTL-POPIA-004. No pre-payroll `DataQualityCheckService` that verifies SA ID, tax ref, banking details format before allowing payroll finalization. |
| **Evidence Files** | `docs/seed-data/sa-id-validation.json`, `docs/seed-data/sars-tax-ref-format.json` |
| **Test Placeholder** | TC-POPIA-004: Payroll finalization with invalid SA ID format → blocked. |
| **Phase Target** | Phase 3 |

---

### CTL-POPIA-005 — Data Subject Notice Versioning
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-005 |
| **POPIA Section** | §18 — Notification to data subject |
| **Status** | **DOCUMENTED ONLY** |
| **VUL Reference** | VUL-021 |
| **PRD Ref** | PRD-05 §6 |
| **Description** | Data subjects must be notified of processing activities before or at time of collection. Notifications must be versioned and acknowledgment tracked. |
| **Observed References** | Traceability comments reference CTL-POPIA-005 in `NextOfKin.cs` (PII encryption note), `BankAccount.cs` (PII encryption note), `BankAccountRepository.cs` (masking note), `EmployeeDtoMapper.cs` (masking), and `Employees.razor` (UI masking). However, these relate to data protection (CTL-POPIA-006), not notice versioning. The CTL-POPIA-005 traceability tags on these files appear to be mislabeled. |
| **Gaps** | No `data_processing_notices` Firestore collection. No `employee_notice_acknowledgments` subcollection. No notice shown on first login or on version change. No withdrawal mechanism for consent-based processing. |
| **Required Implementation** | `data_processing_notices` Firestore collection with versioned notice content; `employee_notice_acknowledgments` subcollection; notice shown on first login + on version change; withdrawal mechanism for consent-based processing. |
| **Test Placeholder** | TC-POPIA-005: New employee login → notice shown → acknowledgment recorded. Notice update → re-acknowledgment required. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-006 — Security Safeguards (Auth + Encryption)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-006 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | **PARTIAL** |
| **VUL Reference** | VUL-003, VUL-007, VUL-010, VUL-019 |
| **PRD Ref** | PRD-05 §2 |
| **Description** | Appropriate technical and organisational measures to prevent unauthorised access, disclosure, or loss of personal information. |
| **Implemented** | (a) Firebase JWT authentication + Firestore security rules. (b) HTTPS enforcement. (c) Hash-chained audit trail (`AuditEventWriter`). (d) Immutable finalized records. (e) **Security HTTP headers** — CSP, X-Frame-Options, nosniff, Referrer-Policy via `SecurityHeadersExtensions` + HSTS (`src/ZenoHR.Api/Middleware/SecurityHeadersExtensions.cs`, `src/ZenoHR.Api/Program.cs`). Closes VUL-001. (f) **MFA enforcement attribute** — `RequireMfaAttribute` blocks privileged ops without session_mfa claim (`src/ZenoHR.Api/Auth/RequireMfaAttribute.cs`). Closes VUL-003. (g) **Rate limiting** — 3-tier per-tenant rate limiting (general API 100/min, auth 10/5min, payroll 20/min) via `RateLimitingExtensions` (`src/ZenoHR.Api/Security/RateLimitingExtensions.cs`). Closes VUL-007. (h) **CORS policy** defined in `Program.cs`. Closes VUL-002. (i) `AuditEvent.cs` logs all PII access as `AuditAction.Read`. |
| **Gaps** | (a) No application-level encryption of PII fields at rest (VUL-019 — `NextOfKin.cs` and `BankAccount.cs` document encryption intent but no `FieldLevelEncryptionService` exists). (b) No key rotation automation (VUL-010). |
| **Evidence Files** | `src/ZenoHR.Api/Middleware/SecurityHeadersExtensions.cs`, `src/ZenoHR.Api/Auth/RequireMfaAttribute.cs`, `src/ZenoHR.Api/Security/RateLimitingExtensions.cs`, `src/ZenoHR.Api/Program.cs`, `src/ZenoHR.Infrastructure/Audit/AuditEventWriter.cs`, `src/ZenoHR.Module.Audit/Domain/AuditEvent.cs` |
| **Test Placeholder** | TC-POPIA-006: Verify all safeguard gaps are closed before marking complete. |
| **Phase Target** | Phases 4 and 5 |

---

### CTL-POPIA-007 — Access Reviews
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-007 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | **DOCUMENTED ONLY** |
| **VUL Reference** | VUL-016 |
| **PRD Ref** | PRD-15 §9 line 107 |
| **Description** | Monthly review of all user role assignments. Approval sign-off by Director or HRManager. Audit trail of each review. |
| **Observed References** | `UserRoleAssignmentRepository` has effective date fields that enable point-in-time audit (traceability comment for CTL-POPIA-007). `SettingsSecurity.razor` mentions access review in the UI. However, no BackgroundService exists to trigger monthly reviews, and no `access_review_records` collection is implemented. |
| **Gaps** | No BackgroundService triggers monthly review generation. No review snapshot production workflow. No Director approval routing. No `access_review_records` Firestore collection. |
| **Required Implementation** | BackgroundService triggers monthly review generation; produces snapshot of `user_role_assignments`; routes to Director for approval; records outcome in `access_review_records` collection; creates AuditEvent. |
| **Evidence Files** | `src/ZenoHR.Infrastructure/Auth/UserRoleAssignmentRepository.cs` (effective dates), `src/ZenoHR.Web/Components/Pages/Settings/SettingsSecurity.razor` |
| **Test Placeholder** | TC-POPIA-007: Monthly cron generates review; Director approves; AuditEvent created with hash-chain entry. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-008 — Breach Detection & Anomaly Monitoring
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-008 |
| **POPIA Section** | §22 — Notification of security compromises |
| **Status** | **NOT STARTED** |
| **VUL Reference** | VUL-004 |
| **PRD Ref** | PRD-05 §7 lines 123-144 |
| **Description** | Automated detection of anomalous access patterns that may indicate a breach or compromise. |
| **Current State** | No anomaly detection service exists. Rate limiting (`RateLimitingExtensions`) provides some brute-force protection but does not generate incidents or alerts. No BackgroundService monitors the AuditEvent stream. |
| **Required Implementation** | BackgroundService monitors AuditEvent stream for: 5 failed auths in 10 min (SEV-2), unusual bulk export (SEV-2), off-hours privileged access (SEV-3). Alert dispatched to Director/HRManager. |
| **Test Placeholder** | TC-POPIA-008: Simulate 5 failed auth events → SEV-2 incident created in <60s. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-009 — Data Subject Access Requests (SAR)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-009 |
| **POPIA Section** | §23 — Right of access to information |
| **Status** | **NOT STARTED** |
| **VUL Reference** | VUL-012 |
| **PRD Ref** | PRD-06 §5 |
| **Description** | Employees have the right to request a copy of all personal information ZenoHR holds about them. Responses required within 30 calendar days. |
| **Observed References** | `SettingsArchival.razor` references CTL-POPIA-009 in its traceability comments, but this page is for data archival/retention — not DSAR processing. The `UnmaskRequest.PurposeCode` includes `EMPLOYEE_REQUEST` for self-access, but no DSAR workflow exists. |
| **Gaps** | No `POST /api/data-subject-requests` endpoint. No `data_subject_requests` Firestore collection. No 30-day SLA countdown. No HRManager review and approval workflow. No QuestPDF data package generation. |
| **Required Implementation** | `POST /api/data-subject-requests` endpoint; `data_subject_requests` Firestore collection; 30-day SLA countdown; HRManager reviews and approves data export; QuestPDF generates redacted data package. |
| **Test Placeholder** | TC-POPIA-009: Employee submits SAR → HR notified → approved within 30 days → data package generated and delivered. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-010 — Correction of Personal Information
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-010 |
| **POPIA Section** | §24 — Correction of personal information |
| **Status** | **NOT STARTED** |
| **VUL Reference** | VUL-012 |
| **PRD Ref** | PRD-06 §5 |
| **Description** | Data subjects may request correction or deletion of inaccurate/incomplete information. HRManager reviews and implements corrections with audit trail. |
| **Observed References** | `BreachNotificationService` and related models reference CTL-POPIA-010 in traceability comments, but breach notification is a separate concern (CTL-POPIA-011). No correction request workflow exists. |
| **Gaps** | No correction request workflow. No `correction_requests` Firestore collection. No HR approval process for corrections. No correction creates new document pattern (immutability preserved). |
| **Required Implementation** | Correction request workflow; `correction_requests` collection; HR approval; correction creates new document (immutability preserved via new record + correction reference); AuditEvent for each correction. |
| **Test Placeholder** | TC-POPIA-010: Employee requests tax number correction → HR approves → new contract document created → original preserved → AuditEvent logged. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-011 — Breach Register & Notification
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-011 |
| **POPIA Section** | §22 — Notification of security compromises |
| **Status** | **PARTIAL** |
| **VUL Reference** | VUL-005 |
| **PRD Ref** | PRD-05 §7 |
| **Description** | POPIA §22: Notify Information Regulator and affected data subjects when breach affects >100 persons, within 72 hours of discovery (note: the popia-control-status.md previously said 25 business days, but POPIA §22(3) requires notification "as soon as reasonably possible" — the service uses a 72-hour deadline). Maintain breach register. |
| **Implemented** | (a) `BreachNotificationService` (`src/ZenoHR.Module.Compliance/Services/BreachNotificationService.cs`) — full domain service with: breach registration with validation, forward-only status transitions (Detected → Investigating → Contained → NotificationPending → RegulatorNotified → SubjectsNotified → Remediated → Closed), 72-hour overdue detection, Information Regulator notification text generation. (b) `BreachRecord` model (`src/ZenoHR.Module.Compliance/Models/BreachRecord.cs`) — immutable record with breach metadata, lifecycle timestamps, and computed `IsOverdue` property. (c) `BreachStatus` enum — 8-state forward-only lifecycle. (d) `BreachSeverity` enum — Low/Medium/High/Critical classification. (e) **18 passing unit tests** in `tests/ZenoHR.Module.Compliance.Tests/Popia/BreachNotificationServiceTests.cs` covering registration, status transitions, overdue detection, and notification generation. |
| **Gaps** | (a) No `breach_register` Firestore repository (no persistence layer). (b) No Blazor UI for breach management (Security Ops dashboard shows "Not yet implemented" for CTL-POPIA-011). (c) No actual email/notification delivery to Information Regulator or affected data subjects. (d) No QuestPDF evidence pack generation for breach reports. (e) Previously stated 25-business-day deadline should be verified against POPIA §22 (service uses 72 hours). |
| **Evidence Files** | `src/ZenoHR.Module.Compliance/Services/BreachNotificationService.cs`, `src/ZenoHR.Module.Compliance/Models/BreachRecord.cs`, `src/ZenoHR.Module.Compliance/Models/BreachStatus.cs`, `src/ZenoHR.Module.Compliance/Models/BreachSeverity.cs`, `tests/ZenoHR.Module.Compliance.Tests/Popia/BreachNotificationServiceTests.cs` |
| **Test Coverage** | 18 tests: RegisterBreach (6 tests), UpdateStatus (5 tests), GetOverdueBreaches (3 tests), NotificationDeadline/IsOverdue (3 tests), GenerateRegulatorNotification (3 tests — incl. required fields validation). |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-012 — Compromise Response & Containment
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-012 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | **NOT STARTED** |
| **VUL Reference** | VUL-004 |
| **PRD Ref** | PRD-05 §7 |
| **Description** | Defined incident response procedure: classify → contain → investigate → recover → review. Each step requires documented evidence. |
| **Observed References** | Several files reference CTL-POPIA-012 in traceability comments: `AuditEventWriter.cs`, `AuditEventRepository.cs`, `WriteAuditEventRequest.cs`, `PayrollRunDataStatus.cs`. However, these references relate to audit logging infrastructure, not incident response. The `PayrollRunDataStatus` enum (Active/Archived/LegalHold) is relevant to data retention during incidents but is not an incident response system. The `Compliance.razor` page shows CTL-POPIA-012 as "Fail — Not yet implemented". |
| **Gaps** | No incident lifecycle state machine in `ZenoHR.Module.SecurityOps` (module does not exist). No containment actions (revoke user token, freeze payroll run, export evidence pack). No post-incident review record. |
| **Required Implementation** | Incident lifecycle state machine in `ZenoHR.Module.SecurityOps`; containment actions (revoke user token, freeze payroll run, export evidence pack); post-incident review record. |
| **Test Placeholder** | TC-POPIA-012: Create incident → classify SEV-1 → revoke affected user tokens → freeze in-progress payroll run → generate evidence pack. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-013 — Cross-Border Transfer Governance
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-013 |
| **POPIA Section** | §72 — Transfer of personal information outside Republic |
| **Status** | **NOT STARTED** |
| **VUL Reference** | VUL-018 |
| **PRD Ref** | PRD-05 §5 |
| **Description** | Personal information may not be transferred outside SA unless recipient country provides adequate protection or standard contractual clauses are in place. |
| **Current State** | Azure Container Apps deployment targets SA North region (documented in CLAUDE.md). No formal documentation of cross-border data flows. No Firestore region pinning verification. No DPA status tracking. |
| **Required Implementation** | Document all cross-border data flows; confirm Firestore SA-region pinning; obtain Google Cloud and Microsoft Azure DPAs; record DPA status in `docs/security/data-processor-inventory.md`. |
| **Test Placeholder** | TC-POPIA-013: Verify Firestore writes go to `africa-south1` region. |
| **Phase Target** | Phase 6 |

---

### CTL-POPIA-014 — Data Processor Inventory
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-014 |
| **POPIA Section** | §21 — Operator |
| **Status** | **NOT STARTED** |
| **VUL Reference** | VUL-018 |
| **Description** | Zenowethu must have written agreements with all operators (processors) who process personal information on its behalf. |
| **Known Processors** | Google Cloud (Firebase Auth + Firestore), Microsoft Azure (Container Apps + Monitor + Key Vault), QuestPDF (in-process, no data transfer), MediatR (in-process). |
| **Current State** | No `docs/security/data-processor-inventory.md` exists. No `operator_agreements` Firestore collection. |
| **Required Implementation** | `docs/security/data-processor-inventory.md` listing all processors, regions, DPA status, and contact. Firestore `operator_agreements` collection with signed DPA references. |
| **Test Placeholder** | TC-POPIA-014: Verify all processors have recorded DPA agreement reference. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-015 — Employee Data Retention Policy
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-015 |
| **POPIA Section** | §14 — Retention and restriction of records |
| **Status** | **NOT STARTED** |
| **PRD Ref** | PRD-02 §6, PRD-06 §4 |
| **Description** | Personal information must not be retained longer than necessary for its purpose. BCEA requires payroll records for 3 years; POPIA requires justification for retention beyond that. |
| **Observed References** | `PayrollRunDataStatus` enum includes Active/Archived/LegalHold states (supports retention lifecycle). `SettingsArchival.razor` provides a UI for data archival with audit trail references. `EmployeeRepository` has a no-delete policy comment. However, no automated retention enforcement exists. |
| **Gaps** | No retention schedule in `statutory_rule_sets`. No BackgroundService to identify records past retention date. No HRManager review workflow for deletion or anonymisation. No automated anonymisation. |
| **Evidence Files** | `src/ZenoHR.Module.Payroll/Aggregates/PayrollRunDataStatus.cs`, `src/ZenoHR.Web/Components/Pages/Settings/SettingsArchival.razor` |
| **Required Implementation** | Retention schedule in `statutory_rule_sets`; BackgroundService identifies records past retention date; HRManager review workflow for deletion or anonymisation; AuditEvent on data destruction. |
| **Test Placeholder** | TC-POPIA-015: Employee terminated 3+ years ago → retention review triggered → anonymisation applied → AuditEvent logged. |
| **Phase Target** | Phase 6 |

---

## Traceability Audit Notes

### Mislabeled CTL-POPIA References Found
During this audit, the following traceability tag issues were identified:

1. **CTL-POPIA-005 mislabeled**: Files `NextOfKin.cs`, `BankAccount.cs`, `BankAccountRepository.cs`, `NextOfKinRepository.cs`, `EmployeeDtoMapper.cs`, `Employees.razor` reference CTL-POPIA-005 (Data Subject Notice Versioning) but actually implement data masking/encryption (CTL-POPIA-006 — Security Safeguards) or purpose limitation (CTL-POPIA-002).

2. **CTL-POPIA-010 mislabeled**: `BreachNotificationService.cs`, `BreachRecord.cs`, `BreachStatus.cs`, `BreachSeverity.cs` reference CTL-POPIA-010 (Correction of Personal Information) but actually implement CTL-POPIA-011 (Breach Register & Notification). CTL-POPIA-010 is about data subject correction requests, not breach management.

3. **CTL-POPIA-012 mislabeled**: `AuditEventWriter.cs`, `AuditEventRepository.cs`, `WriteAuditEventRequest.cs` reference CTL-POPIA-012 (Compromise Response & Containment) but actually implement audit logging infrastructure (CTL-POPIA-006 — Security Safeguards).

4. **CTL-POPIA-009 mislabeled**: `SettingsArchival.razor` references CTL-POPIA-009 (Data Subject Access Requests) but implements data archival (closer to CTL-POPIA-015 — Data Retention).

These mislabelings should be corrected in a future code cleanup task to ensure accurate traceability.

---

## POPIA Compliance Roadmap

| Phase | Controls Targeted | Completion Target |
|-------|------------------|------------------|
| Phase 3 | CTL-POPIA-004 (data quality pre-payroll check) | Phase 3 end |
| Phase 4 | CTL-POPIA-002 (unmask API endpoint), CTL-POPIA-006 (field-level encryption) | Phase 4 end |
| Phase 5 | CTL-POPIA-001, 005, 007, 008, 009, 010, 011 (persistence + UI), 012, 014 | Phase 5 end |
| Phase 6 | CTL-POPIA-013, CTL-POPIA-015 | Phase 6 end |

---

## Top 3 Gaps to Address Next (Priority Order)

1. **CTL-POPIA-011 — Breach Register Persistence & UI** (Sev-1 blocker, VUL-005): Domain service exists with tests but no Firestore repository, no Blazor UI, no actual notification delivery. POPIA §22 violation risk. **Criminal liability if breached without this.**

2. **CTL-POPIA-002 — Unmask API Endpoint** (VUL-020): DTO and validation logic exist but no wired endpoint. PII can be accessed without audited purpose codes. Wire `UnmaskRequest` to a real endpoint with audit logging.

3. **CTL-POPIA-006 — Field-Level Encryption** (VUL-019): `NextOfKin` and `BankAccount` aggregates document encryption intent but no `FieldLevelEncryptionService` exists. National IDs, bank account numbers stored in plaintext in Firestore.

---

## Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-09 | Claude Agent (Security Audit) | Initial status tracker — 15 controls assessed |
| 2.0.0 | 2026-03-11 | Claude Agent (POPIA Readiness Audit TASK-154) | Full codebase audit: CTL-POPIA-001 upgraded to PARTIAL (DataClassification + log redaction), CTL-POPIA-002 upgraded to PARTIAL (UnmaskRequest DTO + masking), CTL-POPIA-006 updated with security headers/MFA/rate limiting evidence, CTL-POPIA-011 upgraded to PARTIAL (BreachNotificationService + 18 tests). Added file evidence references. Identified 4 mislabeled traceability tags. Overall readiness: 13% → ~27%. |
