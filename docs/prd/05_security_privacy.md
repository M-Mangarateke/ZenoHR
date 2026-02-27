---
doc_id: PRD-05-SEC
version: 1.0.0
owner: Security Lead
updated_on: 2026-02-18
applies_to:
  - Application security and privacy controls
  - POPIA-aligned safeguards
depends_on:
  - PRD-03-ARCH
  - PRD-04-API
requirements:
  - REQ-SEC-001
  - REQ-SEC-010
  - REQ-COMP-012
  - REQ-COMP-014
---

# Security and Privacy Specification

## Security Objectives
1. Prevent unauthorized access to payroll, identity, and statutory data.
2. Preserve integrity of compliance-critical records and evidence.
3. Provide forensic-grade auditability for regulator and internal investigations.
4. Enforce privacy-by-design for POPIA obligations.

## Threat Model (STRIDE)
| Threat Class | Example Threat | Primary Controls | Residual Risk Handling |
|---|---|---|---|
| Spoofing | Stolen session token used for payroll finalization | MFA, short token lifetime, device/session anomaly checks | Forced re-auth and session revocation |
| Tampering | Altering finalized payroll records | Immutable storage pattern, append-only adjustments, hash-chain audit | Automated integrity scans and incident workflow |
| Repudiation | User denies performing approval | Signed audit events with actor identity and trace IDs | Evidence bundle export and legal hold |
| Information Disclosure | Exposure of tax IDs or bank details | Field-level access controls, masking, encryption at rest | Access anomaly alerts and data compromise playbook |
| Denial of Service | API flooding during payroll run | Rate limiting, queue backpressure, autoscaling guardrails | Graceful degradation and priority scheduling |
| Elevation of Privilege | HR role escalates to compliance approver | Strict RBAC, separation of duties, approval dual control | Privilege change alerts and periodic access review |

## Identity and Access Management

### Role Model
- `HRAdmin`
- `PayrollOfficer`
- `ComplianceOfficer`
- `Manager`
- `Employee`
- `Auditor`
- `SecurityAdmin`

### Access Rules
1. Least privilege default (`REQ-SEC-002`).
2. Privileged actions require MFA (`REQ-SEC-001`).
3. Separation of duties (`REQ-SEC-003`):
- `PayrollOfficer` can calculate payroll but cannot single-handedly finalize.
- `ComplianceOfficer` must co-approve finalization and filing package release.
- `SecurityAdmin` cannot edit payroll amounts.

### Access Control Matrix (minimum)
| Operation | HRAdmin | PayrollOfficer | ComplianceOfficer | Manager | Employee | Auditor |
|---|---|---|---|---|---|---|
| Edit employee profile | Allow | Read | Read | Limited | Self only | Read |
| Run payroll calculation | Deny | Allow | Read | Deny | Deny | Read |
| Finalize payroll | Deny | Co-Approve | Co-Approve | Deny | Deny | Read |
| Generate EMP201/EMP501 | Deny | Prepare | Approve/Release | Deny | Deny | Read |
| View own payslip | Deny | Deny | Deny | Deny | Allow | Deny |
| Export audit evidence | Deny | Read | Allow | Deny | Deny | Allow |

## Cryptography and Key Management
1. Data in transit: TLS 1.2+ only.
2. Data at rest: provider-managed encryption plus application-level protection for highly sensitive fields.
3. Key rotation interval:
- Standard keys: every 180 days.
- High-sensitivity keys: every 90 days.
4. Break-glass process:
- ticketed approval
- time-bound privileged grant
- mandatory post-event review with evidence
5. Integrity requirements:
- audit hash chain verification daily
- signed rule-set and tax-table artifacts before activation

## Sensitive Data Handling
Data classes:
- `Restricted`: tax reference, bank account references, identity numbers.
- `Confidential`: salary components, disciplinary/legal notes.
- `Internal`: operational logs without PII.
- `Public`: release notes and non-sensitive metadata.

Rules:
1. Restricted data is masked in UI by default.
2. Logs cannot include plaintext restricted fields.
3. Export endpoints require purpose code and authorization scope.
4. Test fixtures must use synthetic data only.

## POPIA Control Set

### Lawful Processing and Purpose Limitation
- `CTL-POPIA-001`: Personal information processing must be linked to lawful basis metadata.
- `CTL-POPIA-002`: Purpose code required for data capture and use.
- `CTL-POPIA-003`: Secondary use outside registered purpose is blocked pending approval.

### Information Quality and Transparency
- `CTL-POPIA-004`: Mandatory validation for identity, tax, and banking fields before payroll finalization.
- `CTL-POPIA-005`: Data subject notices must be versioned and traceable.

### Security Safeguards
- `CTL-POPIA-006`: Security safeguards aligned to POPIA security condition.
- `CTL-POPIA-007`: Access reviews performed monthly for privileged roles.
- `CTL-POPIA-008`: Compromise detection linked to incident response workflow.

### Data Subject Participation
- `CTL-POPIA-009`: Subject access request workflow with SLA tracking.
- `CTL-POPIA-010`: Correction request workflow with immutable amendment history.

### Security Compromise Response
- `CTL-POPIA-011`: Breach register and notification workflow aligned to legal obligations.
- `CTL-POPIA-012`: Incident record must contain impact scope, timeline, and mitigation actions.

### Cross-Border Transfer Governance
- `CTL-POPIA-013`: Cross-border transfer requires legal basis check and approval.
- `CTL-POPIA-014`: Data flow inventory must identify all cross-border processors/sub-processors.

## Security Logging and Alerting
1. All privileged actions generate `AUDIT_HIGH` events.
2. Alerting thresholds:
- 5 failed privileged auth attempts in 10 minutes -> `SEV-2`.
- Audit chain integrity failure -> `SEV-1`.
- Unauthorized export attempt of restricted data -> `SEV-2`.
3. Logs retained with tamper-evident controls and legal hold capability.

## Secure SDLC Requirements
1. SAST on every pull request.
2. Dependency vulnerability scanning on every build.
3. Secret scanning pre-merge and pre-release.
4. DAST/API abuse tests before production cutover.
5. High/critical findings are release blockers unless explicit risk acceptance is approved by CISO and Compliance Officer.

## Incident Response Requirements
1. Security incident severity model: `SEV-1` to `SEV-4`.
2. MTTA target:
- `SEV-1`: <=15 minutes
- `SEV-2`: <=30 minutes
3. Forensic readiness:
- trace IDs in all security-relevant requests
- immutable timeline of containment actions
- evidence export in regulator-friendly format

## Evidence Artifacts
- `EV-SEC-001`: IAM policy export and monthly access review sign-off.
- `EV-SEC-002`: MFA enforcement logs.
- `EV-SEC-003`: Key rotation records.
- `EV-SEC-004`: Vulnerability scan reports and remediation status.
- `EV-SEC-005`: Incident drill and postmortem records.
- `EV-SEC-006`: Audit chain integrity verification report.

## Validation Metadata
| Reference | Last Validated | Next Review |
|---|---|---|
| POPIA Act references | 2026-02-18 | 2027-02-18 |
| Information Regulator guidance links | 2026-02-18 | 2026-08-18 |
| Security standard baselines | 2026-02-18 | 2026-05-18 |

## Legal Disclaimer
This document encodes technical controls for privacy and security implementation and does not replace legal advice.
