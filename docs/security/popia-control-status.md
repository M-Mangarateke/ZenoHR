---
doc_id: SEC-POPIA-001
version: 1.0.0
owner: HRManager / Director
classification: Confidential
updated_on: 2026-03-09
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
| **Implemented** | 2 | CTL-POPIA-003, partial CTL-POPIA-006 |
| **Documented Only** (no code) | 4 | CTL-POPIA-001, CTL-POPIA-002, CTL-POPIA-004, CTL-POPIA-007 |
| **Not Started** | 9 | CTL-POPIA-005, CTL-POPIA-008–015 |
| **Total Controls** | **15** | — |

**POPIA Readiness**: ~13% implemented
**Target**: 100% before v1.0 release (regulatory requirement)
**Regulatory authority**: Information Regulator of South Africa
**Relevant legislation**: Protection of Personal Information Act 4 of 2013

---

## Control-by-Control Status

### CTL-POPIA-001 — Lawful Processing Basis Enforcement
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-001 |
| **POPIA Section** | §11 — Conditions for lawful processing |
| **Status** | DOCUMENTED ONLY |
| **VUL Reference** | — |
| **PRD Ref** | PRD-05 §6, PRD-15 §8 |
| **Description** | Every data processing operation must have a documented lawful basis (consent, contract, legal obligation, legitimate interest). |
| **Current State** | Purpose codes mentioned in PRD-15 §8. No API-level validation that processing has an approved lawful basis. No workflow to validate before data access. |
| **Required Implementation** | `LawfulBasisService` validates purpose code against `lawful_basis_registry` Firestore collection before any processing operation. Reject if no valid basis. |
| **Test Placeholder** | TC-POPIA-001: Attempt to process employee data without valid lawful basis → rejected with POPIA error. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-002 — Purpose Limitation (Unmask Validation)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-002 |
| **POPIA Section** | §13 — Purpose specification |
| **Status** | DOCUMENTED ONLY |
| **VUL Reference** | VUL-020 |
| **PRD Ref** | PRD-15 §8 lines 402-410 |
| **Description** | Sensitive fields (`national_id`, `tax_reference`, `bank_account_ref`) may only be unmasked with a documented purpose code from an approved list. |
| **Current State** | Masking requirement documented. No API endpoint validates `purpose_code` on unmask request. AuditEvent records unmask event but not purpose. |
| **Required Implementation** | `PATCH /api/employees/{id}/unmask` requires `purpose_code` in body; validates against `approved_purposes` in `statutory_rule_sets`; rejects unauthorized codes; logs purpose in AuditEvent.Metadata. |
| **Test Placeholder** | TC-POPIA-002: Unmask request without purpose_code → 422. Unmask with invalid code → 403. Valid code → 200 + audit logged with purpose. |
| **Phase Target** | Phase 4 |

---

### CTL-POPIA-003 — Tenant Data Isolation
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-003 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | IMPLEMENTED |
| **VUL Reference** | — |
| **PRD Ref** | PRD-05 §5, Critical Rule #5 |
| **Description** | Each tenant's data must be completely isolated. Cross-tenant access is a Sev-1 vulnerability. |
| **Current State** | **Fully implemented**: `tenant_id` on every Firestore document; all queries filter by tenant; Firestore security rules enforce tenant scoping; `BaseFirestoreRepository` validates tenant on every read. |
| **Evidence** | `src/ZenoHR.Infrastructure/Firestore/BaseFirestoreRepository.cs:56-71`, `firestore.rules` (full tenant isolation rules) |
| **Test Coverage** | TC-SEC-001: Cross-tenant read attempt blocked. |
| **Phase Target** | Complete |

---

### CTL-POPIA-004 — Information Quality (Input Validation)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-004 |
| **POPIA Section** | §16 — Information quality |
| **Status** | PARTIAL |
| **VUL Reference** | — |
| **PRD Ref** | PRD-05 §6 |
| **Description** | Personal information must be complete, accurate, and up to date. Validation must prevent incorrect data from entering the system. |
| **Current State** | FluentValidation pipeline validates all MediatR commands. SA ID number validation seeded (`sa-id-validation.json`). Tax reference format seeded (`sars-tax-ref-format.json`). No field-to-POPIA-004 traceability. |
| **Gap** | Validators not explicitly tagged with CTL-POPIA-004. No pre-payroll validation that employee data is complete (e.g., SA ID verified, tax number format valid). |
| **Required Implementation** | Add `DataQualityCheckService` to payroll pre-flight: verify SA ID, tax ref, banking details format before allowing payroll finalization. |
| **Test Placeholder** | TC-POPIA-004: Payroll finalization with invalid SA ID format → blocked. |
| **Phase Target** | Phase 3 |

---

### CTL-POPIA-005 — Data Subject Notice Versioning
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-005 |
| **POPIA Section** | §18 — Notification to data subject |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-021 |
| **PRD Ref** | PRD-05 §6 |
| **Description** | Data subjects must be notified of processing activities before or at time of collection. Notifications must be versioned and acknowledgment tracked. |
| **Required Implementation** | `data_processing_notices` Firestore collection with versioned notice content; `employee_notice_acknowledgments` subcollection; notice shown on first login + on version change; withdrawal mechanism for consent-based processing. |
| **Test Placeholder** | TC-POPIA-005: New employee login → notice shown → acknowledgment recorded. Notice update → re-acknowledgment required. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-006 — Security Safeguards (Auth + Encryption)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-006 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | PARTIAL |
| **VUL Reference** | VUL-003, VUL-010, VUL-019 |
| **PRD Ref** | PRD-05 §2 |
| **Description** | Appropriate technical and organisational measures to prevent unauthorised access, disclosure, or loss of personal information. |
| **Implemented** | Firebase JWT auth, Firestore security rules, HTTPS enforcement, hash-chained audit trail, immutable finalized records. |
| **Gaps** | (a) MFA not enforced on privileged ops (VUL-003). (b) No application-level encryption of PII fields (VUL-019). (c) No key rotation automation (VUL-010). (d) No rate limiting (VUL-007). |
| **Test Placeholder** | TC-POPIA-006: Verify all 4 safeguard gaps are closed before marking complete. |
| **Phase Target** | Phases 4 and 5 |

---

### CTL-POPIA-007 — Access Reviews
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-007 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | DOCUMENTED ONLY |
| **VUL Reference** | VUL-016 |
| **PRD Ref** | PRD-15 §9 line 107 |
| **Description** | Monthly review of all user role assignments. Approval sign-off by Director or HRManager. Audit trail of each review. |
| **Required Implementation** | BackgroundService triggers monthly review generation; produces snapshot of `user_role_assignments`; routes to Director for approval; records outcome in `access_review_records` collection; creates AuditEvent. |
| **Test Placeholder** | TC-POPIA-007: Monthly cron generates review; Director approves; AuditEvent created with hash-chain entry. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-008 — Breach Detection & Anomaly Monitoring
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-008 |
| **POPIA Section** | §22 — Notification of security compromises |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-004 |
| **PRD Ref** | PRD-05 §7 lines 123-144 |
| **Description** | Automated detection of anomalous access patterns that may indicate a breach or compromise. |
| **Required Implementation** | BackgroundService monitors AuditEvent stream for: 5 failed auths in 10 min (SEV-2), unusual bulk export (SEV-2), off-hours privileged access (SEV-3). Alert dispatched to Director/HRManager. |
| **Test Placeholder** | TC-POPIA-008: Simulate 5 failed auth events → SEV-2 incident created in <60s. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-009 — Data Subject Access Requests (SAR)
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-009 |
| **POPIA Section** | §23 — Right of access to information |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-012 |
| **PRD Ref** | PRD-06 §5 |
| **Description** | Employees have the right to request a copy of all personal information ZenoHR holds about them. Responses required within 30 calendar days. |
| **Required Implementation** | `POST /api/data-subject-requests` endpoint; `data_subject_requests` Firestore collection; 30-day SLA countdown; HRManager reviews and approves data export; QuestPDF generates redacted data package. |
| **Test Placeholder** | TC-POPIA-009: Employee submits SAR → HR notified → approved within 30 days → data package generated and delivered. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-010 — Correction of Personal Information
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-010 |
| **POPIA Section** | §24 — Correction of personal information |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-012 |
| **PRD Ref** | PRD-06 §5 |
| **Description** | Data subjects may request correction or deletion of inaccurate/incomplete information. HRManager reviews and implements corrections with audit trail. |
| **Required Implementation** | Correction request workflow; `correction_requests` collection; HR approval; correction creates new document (immutability preserved via new record + correction reference); AuditEvent for each correction. |
| **Test Placeholder** | TC-POPIA-010: Employee requests tax number correction → HR approves → new contract document created → original preserved → AuditEvent logged. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-011 — Breach Register & Notification
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-011 |
| **POPIA Section** | §22 — Notification of security compromises |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-005 |
| **PRD Ref** | PRD-05 §7 |
| **Description** | POPIA §22: Notify Information Regulator and affected data subjects when breach affects >100 persons, within 25 business days of discovery. Maintain breach register. |
| **Required Implementation** | `breach_register` Firestore collection; breach classification (scope, affected count, data types); SLA timer (25 business days); notification templates (Information Regulator + employees); evidence pack (QuestPDF). |
| **Test Placeholder** | TC-POPIA-011: Create breach affecting 150 employees → 25-day countdown starts → notification generated → SLA tracking visible in Security Ops dashboard. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-012 — Compromise Response & Containment
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-012 |
| **POPIA Section** | §19 — Security safeguards |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-004 |
| **PRD Ref** | PRD-05 §7 |
| **Description** | Defined incident response procedure: classify → contain → investigate → recover → review. Each step requires documented evidence. |
| **Required Implementation** | Incident lifecycle state machine in `ZenoHR.Module.SecurityOps`; containment actions (revoke user token, freeze payroll run, export evidence pack); post-incident review record. |
| **Test Placeholder** | TC-POPIA-012: Create incident → classify SEV-1 → revoke affected user tokens → freeze in-progress payroll run → generate evidence pack. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-013 — Cross-Border Transfer Governance
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-013 |
| **POPIA Section** | §72 — Transfer of personal information outside Republic |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-018 |
| **PRD Ref** | PRD-05 §5 |
| **Description** | Personal information may not be transferred outside SA unless recipient country provides adequate protection or standard contractual clauses are in place. |
| **Required Implementation** | Document all cross-border data flows; confirm Firestore SA-region pinning; obtain Google Cloud and Microsoft Azure DPAs; record DPA status in `docs/security/data-processor-inventory.md`. |
| **Test Placeholder** | TC-POPIA-013: Verify Firestore writes go to `africa-south1` region. |
| **Phase Target** | Phase 6 |

---

### CTL-POPIA-014 — Data Processor Inventory
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-014 |
| **POPIA Section** | §21 — Operator |
| **Status** | NOT STARTED |
| **VUL Reference** | VUL-018 |
| **Description** | Zenowethu must have written agreements with all operators (processors) who process personal information on its behalf. |
| **Known Processors** | Google Cloud (Firebase Auth + Firestore), Microsoft Azure (Container Apps + Monitor + Key Vault), QuestPDF (in-process, no data transfer), MediatR (in-process). |
| **Required Implementation** | `docs/security/data-processor-inventory.md` listing all processors, regions, DPA status, and contact. Firestore `operator_agreements` collection with signed DPA references. |
| **Test Placeholder** | TC-POPIA-014: Verify all processors have recorded DPA agreement reference. |
| **Phase Target** | Phase 5 |

---

### CTL-POPIA-015 — Employee Data Retention Policy
| Field | Value |
|-------|-------|
| **Control ID** | CTL-POPIA-015 |
| **POPIA Section** | §14 — Retention and restriction of records |
| **Status** | NOT STARTED |
| **PRD Ref** | PRD-02 §6, PRD-06 §4 |
| **Description** | Personal information must not be retained longer than necessary for its purpose. BCEA requires payroll records for 3 years; POPIA requires justification for retention beyond that. |
| **Required Implementation** | Retention schedule in `statutory_rule_sets`; BackgroundService identifies records past retention date; HRManager review workflow for deletion or anonymisation; AuditEvent on data destruction. |
| **Test Placeholder** | TC-POPIA-015: Employee terminated 3+ years ago → retention review triggered → anonymisation applied → AuditEvent logged. |
| **Phase Target** | Phase 6 |

---

## POPIA Compliance Roadmap

| Phase | Controls Targeted | Completion Target |
|-------|------------------|------------------|
| Phase 3 | CTL-POPIA-004 (data quality), CTL-POPIA-003 (confirmed) | Phase 3 end |
| Phase 4 | CTL-POPIA-002 (purpose limitation), CTL-POPIA-006 partial | Phase 4 end |
| Phase 5 | CTL-POPIA-001, 005, 007, 008, 009, 010, 011, 012, 014 | Phase 5 end |
| Phase 6 | CTL-POPIA-013, CTL-POPIA-015 | Phase 6 end |

---

## Version History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0.0 | 2026-03-09 | Claude Agent (Security Audit) | Initial status tracker — 15 controls assessed |
