---
doc_id: OPS-GOLIVE-001
version: 1.0.0
owner: Director / SaasAdmin
classification: Internal
updated_on: 2026-03-13
applies_to: [ZenoHR Platform]
---

# ZenoHR Go-Live Checklist

Printable checklist for the ZenoHR production go-live. Each item must be completed and signed off before proceeding to the next phase. Reference: `docs/deployment/deployment-strategy.md` for full details.

**Target domain**: `https://zenohr.zenowethu.co.za`
**Region**: Azure South Africa North (Johannesburg)
**Database**: Google Cloud Firestore (production project)

---

## Phase 1: Pre-Go-Live Validation (Day -7)

**Goal**: Confirm the release candidate is production-ready.

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 1.1 | Deploy release candidate to staging environment | SaasAdmin | 15 min | [ ] | ________ |
| 1.2 | Run full test suite (unit, integration, architecture) | SaasAdmin | 30 min | [ ] | ________ |
| 1.3 | Verify Trivy scan: no HIGH or CRITICAL CVEs | SaasAdmin | 10 min | [ ] | ________ |
| 1.4 | Review vulnerability register: no open Sev-1 findings | SaasAdmin | 30 min | [ ] | ________ |
| 1.5 | Review POPIA control status: all mandatory controls green | SaasAdmin | 30 min | [ ] | ________ |
| 1.6 | UAT: HR Manager tests core workflows (employee CRUD, leave, payroll view) | HR Manager | 1 hour | [ ] | ________ |
| 1.7 | UAT: Director tests dashboard and compliance screens | Director | 30 min | [ ] | ________ |
| 1.8 | UAT: Verify mobile responsiveness on phone and tablet | HR Manager | 30 min | [ ] | ________ |
| 1.9 | UAT sign-off from Director | Director | 15 min | [ ] | ________ |
| 1.10 | Freeze `main` branch — no non-critical merges until go-live | SaasAdmin | 5 min | [ ] | ________ |

**Phase 1 Total**: ~3 hours 55 minutes

**Phase 1 Sign-off**: __________________ Date: __________

---

## Phase 2: Infrastructure Provisioning (Day -3)

**Goal**: Production Azure and Google Cloud infrastructure is fully provisioned and tested.

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 2.1 | Create production resource group (`zenohr-prod-rg`) in SA North | SaasAdmin | 5 min | [ ] | ________ |
| 2.2 | Deploy Bicep template (`infra/container-app.bicep`) | SaasAdmin | 20 min | [ ] | ________ |
| 2.3 | Verify Log Analytics workspace created (90-day retention) | SaasAdmin | 5 min | [ ] | ________ |
| 2.4 | Verify Container App Environment created | SaasAdmin | 5 min | [ ] | ________ |
| 2.5 | Provision Azure Key Vault (`zenohr-prod-kv`) | SaasAdmin | 10 min | [ ] | ________ |
| 2.6 | Populate Key Vault: Firebase service account credentials | SaasAdmin | 10 min | [ ] | ________ |
| 2.7 | Populate Key Vault: Application Insights connection string | SaasAdmin | 5 min | [ ] | ________ |
| 2.8 | Populate Key Vault: CORS allowed origins | SaasAdmin | 5 min | [ ] | ________ |
| 2.9 | Populate Key Vault: Encryption DEK | SaasAdmin | 5 min | [ ] | ________ |
| 2.10 | Populate Key Vault: SMTP credentials | SaasAdmin | 5 min | [ ] | ________ |
| 2.11 | Configure Firebase Auth production project (email/password + MFA) | SaasAdmin | 30 min | [ ] | ________ |
| 2.12 | Deploy Firestore security rules to production project | SaasAdmin | 15 min | [ ] | ________ |
| 2.13 | Deploy Firestore indexes to production project | SaasAdmin | 15 min | [ ] | ________ |
| 2.14 | Verify statutory seed data embedded in container image | SaasAdmin | 10 min | [ ] | ________ |
| 2.15 | Configure Azure Monitor alert rules (error rate, response time, health) | SaasAdmin | 30 min | [ ] | ________ |
| 2.16 | Deploy test container to verify infrastructure end-to-end | SaasAdmin | 15 min | [ ] | ________ |
| 2.17 | Verify health probes return 200 from test container | SaasAdmin | 5 min | [ ] | ________ |
| 2.18 | Remove test container | SaasAdmin | 5 min | [ ] | ________ |

**Phase 2 Total**: ~3 hours 20 minutes

**Phase 2 Sign-off**: __________________ Date: __________

---

## Phase 3: DNS and SSL Preparation (Day -1)

**Goal**: Networking is ready for instant cutover on Day 0.

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 3.1 | Verify Container Apps managed SSL certificate is issued | SaasAdmin | 10 min | [ ] | ________ |
| 3.2 | Prepare DNS CNAME record: `zenohr.zenowethu.co.za` -> Container App FQDN | SaasAdmin | 10 min | [ ] | ________ |
| 3.3 | Reduce DNS TTL to 300 seconds (if not already) | SaasAdmin | 5 min | [ ] | ________ |
| 3.4 | Verify CORS origins in Key Vault match `https://zenohr.zenowethu.co.za` | SaasAdmin | 5 min | [ ] | ________ |
| 3.5 | Prepare rollback plan document and distribute to operations team | SaasAdmin | 15 min | [ ] | ________ |
| 3.6 | Confirm on-call roster for Day 0 and Day +1 | Director | 10 min | [ ] | ________ |
| 3.7 | Notify stakeholders of go-live schedule and expected timeline | Director | 15 min | [ ] | ________ |
| 3.8 | Verify GitHub Actions workflow is ready (secrets configured, environment approved) | SaasAdmin | 10 min | [ ] | ________ |

**Phase 3 Total**: ~1 hour 20 minutes

**Phase 3 Sign-off**: __________________ Date: __________

---

## Phase 4: Production Deployment (Day 0)

**Goal**: Application is live, accessible, and verified.

### 4A. Deployment (SaasAdmin)

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 4.1 | Confirm all Day -1 items are complete | SaasAdmin | 5 min | [ ] | ________ |
| 4.2 | Open GitHub Actions: "Deploy to Production (Manual)" | SaasAdmin | 2 min | [ ] | ________ |
| 4.3 | Enter deployment reason: "Initial production go-live" | SaasAdmin | 1 min | [ ] | ________ |
| 4.4 | Trigger workflow and monitor build stage | SaasAdmin | 5 min | [ ] | ________ |
| 4.5 | Monitor test execution stage | SaasAdmin | 10 min | [ ] | ________ |
| 4.6 | Monitor Docker build and Trivy scan stage | SaasAdmin | 5 min | [ ] | ________ |
| 4.7 | Approve production environment deployment gate | SaasAdmin | 2 min | [ ] | ________ |
| 4.8 | Monitor container deployment to Azure | SaasAdmin | 5 min | [ ] | ________ |
| 4.9 | Verify automated health checks pass (liveness + readiness) | SaasAdmin | 5 min | [ ] | ________ |
| 4.10 | Record deployed image tag: `sha-________________` | SaasAdmin | 1 min | [ ] | ________ |

### 4B. DNS Cutover

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 4.11 | Apply DNS CNAME record for `zenohr.zenowethu.co.za` | SaasAdmin | 5 min | [ ] | ________ |
| 4.12 | Verify HTTPS access: `https://zenohr.zenowethu.co.za` loads login page | SaasAdmin | 5 min | [ ] | ________ |
| 4.13 | Verify no mixed content warnings (all resources over HTTPS) | SaasAdmin | 5 min | [ ] | ________ |

### 4C. Account Setup

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 4.14 | Create HR Manager account in Firebase Auth (with MFA) | SaasAdmin | 10 min | [ ] | ________ |
| 4.15 | Create Director account in Firebase Auth (with MFA) | SaasAdmin | 10 min | [ ] | ________ |
| 4.16 | Create Zenowethu company settings document in Firestore | SaasAdmin | 5 min | [ ] | ________ |

### 4D. Smoke Tests

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 4.17 | HR Manager logs in successfully | HR Manager | 5 min | [ ] | ________ |
| 4.18 | Dashboard loads with correct company name (Zenowethu) | HR Manager | 2 min | [ ] | ________ |
| 4.19 | Navigate to Employees page — list renders | HR Manager | 2 min | [ ] | ________ |
| 4.20 | Create a test employee record | HR Manager | 5 min | [ ] | ________ |
| 4.21 | Verify audit trail entry created for employee creation | HR Manager | 3 min | [ ] | ________ |
| 4.22 | Navigate to Payroll page — configuration visible | HR Manager | 2 min | [ ] | ________ |
| 4.23 | Navigate to Leave page — calendar renders | HR Manager | 2 min | [ ] | ________ |
| 4.24 | Navigate to Compliance page — scores render | HR Manager | 2 min | [ ] | ________ |
| 4.25 | Director logs in and verifies full access | Director | 5 min | [ ] | ________ |
| 4.26 | Delete test employee record (or mark inactive) | HR Manager | 2 min | [ ] | ________ |

### 4E. Monitoring Verification

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 4.27 | Open Application Insights — verify telemetry data arriving | SaasAdmin | 5 min | [ ] | ________ |
| 4.28 | Verify request traces visible in Application Insights | SaasAdmin | 5 min | [ ] | ________ |
| 4.29 | Check Log Analytics — application logs flowing | SaasAdmin | 5 min | [ ] | ________ |
| 4.30 | Verify Container Apps metrics dashboard (CPU, memory, replicas) | SaasAdmin | 5 min | [ ] | ________ |

### 4F. Go-Live Confirmation

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 4.31 | SaasAdmin confirms: deployment healthy, monitoring active | SaasAdmin | 2 min | [ ] | ________ |
| 4.32 | HR Manager confirms: core workflows functional | HR Manager | 2 min | [ ] | ________ |
| 4.33 | Director approves go-live | Director | 5 min | [ ] | ________ |
| 4.34 | Announce go-live to all Zenowethu staff via email | Director | 10 min | [ ] | ________ |

**Phase 4 Total**: ~2 hours 30 minutes

**Phase 4 Sign-off**: __________________ Date: __________

---

## Phase 5: Post-Launch Monitoring (Day +1)

**Goal**: Verify stability over the first 24 hours.

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 5.1 | Review overnight Application Insights dashboards | SaasAdmin | 15 min | [ ] | ________ |
| 5.2 | Check error rate (target: < 1%) | SaasAdmin | 10 min | [ ] | ________ |
| 5.3 | Check P95 response time (target: < 2 seconds) | SaasAdmin | 10 min | [ ] | ________ |
| 5.4 | Verify Firestore daily backup ran successfully | SaasAdmin | 10 min | [ ] | ________ |
| 5.5 | Review any user-reported issues | SaasAdmin | As needed | [ ] | ________ |
| 5.6 | Verify no unexpected container restarts | SaasAdmin | 5 min | [ ] | ________ |
| 5.7 | Confirm on-call availability maintained | SaasAdmin | — | [ ] | ________ |

**Phase 5 Total**: ~1 hour + as needed

**Phase 5 Sign-off**: __________________ Date: __________

---

## Phase 6: Post-Launch Review (Day +7)

**Goal**: Establish baselines and close the go-live cycle.

| # | Task | Owner | Est. Time | Status | Sign-off |
|---|------|-------|-----------|--------|----------|
| 6.1 | Compile 7-day performance report (P50, P95, P99 response times) | SaasAdmin | 30 min | [ ] | ________ |
| 6.2 | Review error log for recurring patterns | SaasAdmin | 30 min | [ ] | ________ |
| 6.3 | Gather feedback from HR Manager | HR Manager | 15 min | [ ] | ________ |
| 6.4 | Gather feedback from Director | Director | 15 min | [ ] | ________ |
| 6.5 | Document lessons learned | SaasAdmin | 30 min | [ ] | ________ |
| 6.6 | Establish performance and reliability baselines | SaasAdmin | 15 min | [ ] | ________ |
| 6.7 | Unfreeze `main` branch | SaasAdmin | 5 min | [ ] | ________ |
| 6.8 | Schedule first monthly backup restore test | SaasAdmin | 5 min | [ ] | ________ |
| 6.9 | Schedule first quarterly DR drill | SaasAdmin | 5 min | [ ] | ________ |

**Phase 6 Total**: ~2 hours 30 minutes

**Phase 6 Sign-off**: __________________ Date: __________

---

## Emergency Rollback Procedure

If any critical issue is detected during go-live, execute this procedure immediately:

| # | Task | Owner | Est. Time |
|---|------|-------|-----------|
| R.1 | **DECISION**: Confirm rollback is necessary (SaasAdmin + Director) | SaasAdmin | 2 min |
| R.2 | Identify the previous known-good image tag from deployment history | SaasAdmin | 2 min |
| R.3 | Run rollback command (see below) | SaasAdmin | 3 min |
| R.4 | Verify health endpoints return 200 | SaasAdmin | 2 min |
| R.5 | Notify Director and HR Manager of rollback | SaasAdmin | 2 min |
| R.6 | Investigate root cause in staging | SaasAdmin | As needed |

**Rollback command**:

```bash
az containerapp update \
  --name zenohr-prod-app \
  --resource-group zenohr-prod-rg \
  --image ghcr.io/<org>/zenohr:<previous-tag>
```

**Total rollback time**: < 10 minutes

---

## Summary Timeline

| Day | Phase | Duration | Key Milestone |
|-----|-------|----------|---------------|
| Day -7 | Pre-Go-Live Validation | ~4 hours | UAT signed off |
| Day -3 | Infrastructure Provisioning | ~3.5 hours | Azure + Google Cloud ready |
| Day -1 | DNS and SSL Preparation | ~1.5 hours | Networking ready for cutover |
| **Day 0** | **Production Deployment** | **~2.5 hours** | **Application live** |
| Day +1 | Post-Launch Monitoring | ~1 hour | Stability confirmed |
| Day +7 | Post-Launch Review | ~2.5 hours | Baselines established, cycle closed |

**Total effort across all phases**: ~15 hours

---

## Approvals

| Role | Name | Signature | Date |
|------|------|-----------|------|
| Director | | | |
| SaasAdmin | | | |
| HR Manager | | | |
