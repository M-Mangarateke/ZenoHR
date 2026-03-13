---
doc_id: OPS-DEPLOY-001
version: 1.0.0
owner: Director / SaasAdmin
classification: Internal
updated_on: 2026-03-13
applies_to: [ZenoHR Platform]
---

# ZenoHR Deployment Strategy

This document defines the deployment architecture, CI/CD pipeline, environment strategy, and operational procedures for the ZenoHR platform. It is the authoritative reference for all deployment-related decisions and processes.

**Audience**: Director, SaasAdmin, DevOps engineers, and technical stakeholders.

---

## 1. Deployment Architecture

### 1.1 Infrastructure Overview

ZenoHR is deployed on **Azure Container Apps** in the **South Africa North (Johannesburg)** region. This region selection is mandated by POPIA data residency requirements (CTL-POPIA-001) -- all personal employee data must remain within South African borders.

```
                    Internet
                       |
                 [Azure Front Door]
                       |
              [TLS Termination Layer]
                       |
          [Azure Container Apps Ingress]
                       |
           +-----------+-----------+
           |                       |
     [Replica 1]            [Replica N]
     ZenoHR API +           ZenoHR API +
     Blazor Server           Blazor Server
           |                       |
           +-----------+-----------+
                       |
              +--------+--------+
              |                 |
     [Google Firestore]  [Azure Key Vault]
     (Database)          (Secrets)
              |
     [Firebase Auth]
     (Identity)
```

### 1.2 Container Specification

| Property | Value | Reference |
|----------|-------|-----------|
| Base image | `mcr.microsoft.com/dotnet/aspnet:10.0` | `Dockerfile` |
| Build image | `mcr.microsoft.com/dotnet/sdk:10.0` | `Dockerfile` |
| Build type | Multi-stage Docker build | `Dockerfile` |
| Exposed port | 8080 (HTTP) | TLS terminated at ingress |
| Runtime user | `zenohr` (UID 1001, non-root) | REQ-SEC-001 |
| Entry point | `dotnet ZenoHR.Api.dll` | Blazor Server embedded in API host |
| Registry | `ghcr.io` (GitHub Container Registry) | `deploy-production.yml` |

### 1.3 Resource Allocation and Scaling

| Property | Value | Notes |
|----------|-------|-------|
| CPU per replica | 0.5 vCPU | Defined in `infra/container-app.bicep` |
| Memory per replica | 1 Gi | Defined in `infra/container-app.bicep` |
| Minimum replicas | 1 | Always-on for availability |
| Maximum replicas | 5 | Auto-scale ceiling |
| Scaling trigger | HTTP concurrency > 50 | Per-replica concurrent request threshold |
| Zone redundancy | Disabled | SA North has limited zone support; enable when available |

### 1.4 Health Probes

Three health probes are configured in `infra/container-app.bicep` to ensure reliable operation:

| Probe | Endpoint | Interval | Timeout | Failure Threshold | Purpose |
|-------|----------|----------|---------|-------------------|---------|
| **Startup** | `/health` | 10s | 10s | 10 (allows ~100s cold start) | Prevents traffic before app is initialized |
| **Liveness** | `/health` | 30s | 10s | 3 | Restarts unresponsive containers |
| **Readiness** | `/health/ready` | 15s | 10s | 3 | Removes container from load balancer if Firestore is unreachable |

The readiness probe at `/health/ready` verifies connectivity to Google Firestore with a configurable timeout (default 5 seconds, set in `appsettings.Production.json`).

### 1.5 Ingress Configuration

- **Transport**: HTTPS enforced (`allowInsecure: false`). TLS is terminated at the Container Apps ingress layer; containers receive HTTP on port 8080.
- **CORS policy**: Origins restricted to `https://zenohr.zenowethu.co.za` (configurable via `corsAllowedOrigins` parameter). Allowed methods: GET, POST, PUT, DELETE, OPTIONS. Max-age: 3600 seconds.
- **Security headers**: CSP, HSTS, X-Frame-Options enforced at the application middleware layer (REQ-SEC-003, VUL-001).

---

## 2. Pre-Deployment Checklist

Complete every item before initiating a production deployment. The CI/CD pipeline enforces items marked with (automated).

### 2.1 Code Quality

- [ ] All unit tests pass (automated)
- [ ] All integration tests pass against Firestore emulator (automated)
- [ ] Architecture boundary tests pass (automated)
- [ ] Build produces 0 errors in Release configuration (automated)
- [ ] No new compiler warnings introduced
- [ ] Code review approved by at least one team member

### 2.2 Security

- [ ] Trivy container scan shows no HIGH or CRITICAL CVEs (automated)
- [ ] No secrets committed to source code (automated via pre-commit hook)
- [ ] SAST scan clean (GitHub CodeQL)
- [ ] SBOM generated and attached to container image (automated)
- [ ] Vulnerability register reviewed -- no open Sev-1 findings blocking release

### 2.3 Infrastructure

- [ ] Environment variables configured in Azure Key Vault
- [ ] Firebase service account key rotated if older than 180 days
- [ ] CORS origins updated for production domain
- [ ] SSL/TLS certificate provisioned and valid (auto-managed by Container Apps)
- [ ] DNS records configured (CNAME to Container Apps FQDN)
- [ ] Log Analytics workspace retention set to 90 days

### 2.4 Data

- [ ] Statutory seed data verified (`docs/seed-data/*.json` embedded in container image)
- [ ] Firestore security rules deployed to production project
- [ ] Firestore indexes deployed
- [ ] Backup schedule confirmed (daily, 30-day retention)

### 2.5 Stakeholder

- [ ] Release notes prepared
- [ ] Deployment reason documented (required by workflow input)
- [ ] Rollback plan communicated to operations team

---

## 3. CI/CD Pipeline

### 3.1 Pipeline Architecture

The production deployment pipeline is defined in `.github/workflows/deploy-production.yml` and triggered manually via `workflow_dispatch`. This ensures human approval for every production release.

```
[Manual Trigger]
    |
    v
[Build & Test]  ──(fail)──> STOP
    |
    v
[Docker Build & Push to GHCR]
    |
    v
[Trivy Vulnerability Scan]  ──(HIGH/CRITICAL found)──> STOP
    |
    v
[GitHub Environment Approval Gate: "production"]
    |
    v
[Azure Login (OIDC)]
    |
    v
[Deploy to Container Apps]
    |
    v
[Post-Deploy Health Checks]  ──(fail)──> Alert + Rollback
    |
    v
[Deployment Summary]
```

### 3.2 Workflow Inputs

Each production deployment requires three inputs:

| Input | Required | Description |
|-------|----------|-------------|
| `image_tag` | No | Specific container image tag to deploy. If empty, builds from HEAD. |
| `skip_tests` | No | Skip test execution (emergency hotfix only -- requires justification). |
| `reason` | Yes | Deployment reason for the audit trail. |

### 3.3 Authentication

- **Azure**: OIDC-based login using a federated service principal. No stored credentials. Secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, and `AZURE_SUBSCRIPTION_ID` are configured as GitHub repository secrets.
- **GHCR**: Authenticated via `GITHUB_TOKEN` (automatic, scoped to the workflow run).

### 3.4 Concurrency Control

The workflow uses a `production-deploy` concurrency group with `cancel-in-progress: false`. This prevents parallel deployments and ensures each deployment completes before the next can begin.

### 3.5 Image Tagging Strategy

| Tag | Format | Example | Purpose |
|-----|--------|---------|---------|
| Commit SHA | `sha-<short-hash>` | `sha-abc1234` | Immutable, traceable to exact commit |
| Latest | `latest` | `latest` | Convenience pointer to most recent build |

Production deployments always reference the SHA-based tag for traceability. The `latest` tag is updated as a convenience but never used for deployment targeting.

### 3.6 Post-Deployment Verification

After deployment, the pipeline automatically:

1. Queries the Container App for its FQDN
2. Runs liveness probe (`/health`) with 5 retries, 10-second intervals
3. Runs readiness probe (`/health/ready`) with 5 retries, 10-second intervals
4. If either probe fails after all retries, the workflow fails and alerts are triggered
5. Generates a deployment summary in the GitHub Actions step summary

---

## 4. Environment Strategy

### 4.1 Environment Topology

```
+------------------+     +------------------+     +------------------+
|   Development    |     |     Staging       |     |   Production     |
+------------------+     +------------------+     +------------------+
| Local Docker     |     | Azure Container   |     | Azure Container  |
| Compose          |     | Apps              |     | Apps             |
|                  |     |                   |     |                  |
| Firestore        |     | Firestore test    |     | Firestore prod   |
| Emulator         |     | project           |     | project          |
|                  |     |                   |     |                  |
| Firebase Auth    |     | Firebase Auth     |     | Firebase Auth    |
| Emulator         |     | test project      |     | prod project     |
|                  |     |                   |     |                  |
| No Key Vault     |     | Key Vault         |     | Key Vault        |
| (appsettings)    |     | (staging)         |     | (production)     |
+------------------+     +------------------+     +------------------+
     Region: local          Region: SA North       Region: SA North
```

### 4.2 Environment Details

#### Development

- **Runtime**: Local Docker or `dotnet run` with hot reload
- **Database**: Google Firestore emulator (no cloud dependency)
- **Auth**: Firebase Auth emulator
- **Secrets**: `appsettings.Development.json` and `dotnet user-secrets`
- **Purpose**: Feature development and local testing
- **Data**: Synthetic seed data, no PII

#### Staging

- **Runtime**: Azure Container Apps (separate resource group: `zenohr-staging-rg`)
- **Database**: Firestore test project (isolated from production data)
- **Auth**: Firebase Auth test project
- **Secrets**: Azure Key Vault (staging instance)
- **Purpose**: Integration testing, UAT, pre-production validation
- **Data**: Anonymized copies of production data or synthetic test data
- **Deployment**: Automatic on merge to `main` branch

#### Production

- **Runtime**: Azure Container Apps (resource group: `zenohr-prod-rg`)
- **Database**: Firestore production project
- **Auth**: Firebase Auth production project
- **Secrets**: Azure Key Vault (production instance)
- **Region**: South Africa North (Johannesburg) -- POPIA data residency
- **Purpose**: Live system serving Zenowethu employees
- **Deployment**: Manual trigger with approval gate

### 4.3 Environment Promotion Flow

```
Feature branch --> main (PR merge) --> Staging (auto-deploy) --> Production (manual trigger)
```

Code flows strictly in one direction. No hotfixes bypass staging unless using the `skip_tests` emergency flag, which requires documented justification.

---

## 5. Secrets Management

### 5.1 Architecture

All secrets are stored in **Azure Key Vault** and accessed via managed identity. No secrets appear in source code, configuration files, environment variable definitions, or CI/CD logs.

The `appsettings.Production.json` file contains empty placeholder values for all secret-bearing configuration keys. At runtime, the Container App injects secret values via environment variables backed by Key Vault references.

### 5.2 Secrets Inventory

| Secret Name | Purpose | Rotation Period | Rotation Method |
|-------------|---------|----------------|-----------------|
| `firebase-credentials-json` | Firestore service account key (base64) | 180 days | Key Vault auto-rotation + Firebase console |
| `appinsights-connection-string` | Azure Monitor telemetry export | Static (tied to resource) | Rotate on resource recreation |
| `cors-allowed-origins` | Allowed CORS origins list | On change | Manual Key Vault update |
| `encryption-dek` | Data encryption key for PII fields | 365 days | Key Vault auto-rotation |
| `smtp-credentials` | Email notification service | 90 days | Key Vault auto-rotation |
| `firebase-api-key` | Firebase Auth client configuration | On compromise | Manual rotation |

### 5.3 Key Rotation Policy

- **Default rotation**: 180 days (Key Vault auto-rotation enabled)
- **Critical secrets** (encryption keys): 365 days with automated rotation
- **SMTP credentials**: 90 days
- **Rotation alerts**: Key Vault sends email notifications 30 days before expiry
- **Post-rotation verification**: Health check endpoints validate connectivity after rotation

### 5.4 Access Control

- Production Key Vault: accessible only to the Container App managed identity and designated SaasAdmin accounts
- No developer has direct read access to production secrets
- All secret access is logged in Key Vault audit logs (90-day retention)

---

## 6. Monitoring and Alerting

### 6.1 Observability Stack

```
[ZenoHR Application]
    |
    | (OpenTelemetry SDK)
    v
[Azure Application Insights]
    |
    +---> [Metrics]
    +---> [Traces]
    +---> [Logs]
    |
    v
[Azure Monitor Alert Rules]
    |
    v
[Email / SMS Notifications]
```

The application exports telemetry via the OpenTelemetry SDK to Azure Application Insights (connection string injected from Key Vault at runtime).

### 6.2 Health Check Endpoints

| Endpoint | Method | Purpose | Expected Response |
|----------|--------|---------|-------------------|
| `/health` | GET | Liveness -- confirms the process is running | 200 OK |
| `/health/ready` | GET | Readiness -- confirms Firestore connectivity | 200 OK (or 503 if Firestore unreachable) |

### 6.3 Custom Metrics

| Metric | Type | Description | Alert Threshold |
|--------|------|-------------|-----------------|
| `payroll_runs_total` | Counter | Total payroll runs executed | N/A (informational) |
| `payroll_run_duration` | Histogram | Duration of payroll run processing | > 120 seconds |
| `compliance_checks` | Counter | Compliance validation checks performed | N/A (informational) |
| `api_errors` | Counter | API errors by status code and endpoint | Rate > 5% of total requests |

### 6.4 Alert Rules

| Alert | Condition | Severity | Action |
|-------|-----------|----------|--------|
| High error rate | API error rate > 5% over 5-minute window | Sev-1 | Email + SMS to SaasAdmin and Director |
| Slow response | P95 response time > 2 seconds over 5-minute window | Sev-2 | Email to SaasAdmin |
| Health check failure | Liveness or readiness probe fails 3 consecutive times | Sev-1 | Email + SMS, auto-restart by Container Apps |
| Payroll run timeout | `payroll_run_duration` > 120 seconds | Sev-2 | Email to SaasAdmin |
| Key Vault access failure | Secret retrieval fails | Sev-1 | Email + SMS to SaasAdmin |
| Container restart | Unexpected container restart detected | Sev-2 | Email to SaasAdmin |

### 6.5 Log Retention

- **Application Insights**: 90 days (standard retention)
- **Log Analytics workspace**: 90 days (configured in `infra/container-app.bicep`)
- **Audit events**: Indefinite (stored in Firestore, immutable)
- **Key Vault audit logs**: 90 days

---

## 7. Rollback Procedure

### 7.1 Decision Criteria

Initiate a rollback when any of the following occur:

- Post-deployment health checks fail and do not recover within 5 minutes
- Error rate exceeds 10% after deployment
- Critical business function (payroll, leave, compliance) is broken
- Data integrity issue detected (hash-chain validation failure)

### 7.2 Rollback Steps

**Step 1: Identify and confirm the issue**

- Review Azure Monitor alerts and Application Insights
- Confirm the issue is deployment-related (not an upstream dependency failure)
- Identify the last known good container image tag from deployment history

**Step 2: Revert to the previous container image**

Using the Azure CLI or the deployment helper script:

```bash
# Via Azure CLI directly
az containerapp update \
  --name zenohr-prod-app \
  --resource-group zenohr-prod-rg \
  --image ghcr.io/<org>/zenohr:<previous-tag>

# Via deployment helper script
./infra/deploy.sh <previous-tag>
```

Alternatively, re-run the GitHub Actions workflow with the previous image tag specified in the `image_tag` input.

**Step 3: Verify health endpoints**

```bash
curl -s -o /dev/null -w "%{http_code}" https://zenohr-prod-app.<region>.azurecontainerapps.io/health
curl -s -o /dev/null -w "%{http_code}" https://zenohr-prod-app.<region>.azurecontainerapps.io/health/ready
```

Both must return HTTP 200.

**Step 4: Investigate root cause**

- Reproduce the issue in staging using the failed image tag
- Review application logs in Application Insights
- Check Firestore connectivity and security rules
- Identify the offending commit and create a fix

**Step 5: Fix, test, and redeploy**

- Apply the fix on a feature branch
- Verify in staging (full test suite + manual validation)
- Deploy to production using the standard CI/CD pipeline

### 7.3 Rollback Time Targets

| Action | Target Duration |
|--------|----------------|
| Issue detection to rollback decision | < 5 minutes |
| Rollback execution (container update) | < 3 minutes |
| Health verification | < 2 minutes |
| **Total rollback time** | **< 10 minutes** |

---

## 8. Data Backup and Recovery

### 8.1 Backup Strategy

| Component | Backup Method | Frequency | Retention | Location |
|-----------|---------------|-----------|-----------|----------|
| Firestore data | Google Cloud managed export | Daily (automated) | 30 days | Google Cloud Storage (same region) |
| Firestore security rules | Version-controlled in repository | On every change | Git history | GitHub |
| Azure Key Vault secrets | Azure managed backup | Continuous (soft-delete enabled) | 90 days | Azure (SA North) |
| Container images | Stored in GHCR | Indefinite | Tag-based | GitHub Container Registry |
| Statutory seed data | Embedded in container image | Every build | Git history | Repository + container image |

### 8.2 Recovery Objectives

| Metric | Target | Notes |
|--------|--------|-------|
| **RPO** (Recovery Point Objective) | 24 hours | Based on daily Firestore export schedule |
| **RTO** (Recovery Time Objective) | 4 hours | Includes infrastructure provisioning + data restore |

### 8.3 Restore Procedure

1. **Identify the restore point**: Select the most recent clean backup before the data loss or corruption event
2. **Provision a new Firestore database** (or restore to the existing one if safe)
3. **Import the backup**: `gcloud firestore import gs://<backup-bucket>/<export-folder>`
4. **Verify data integrity**: Run hash-chain validation on audit events, verify payroll run totals
5. **Update application configuration** if the Firestore project ID changed
6. **Redeploy the application** pointing to the restored database
7. **Run smoke tests**: Verify login, employee lookup, payroll history, leave balances

### 8.4 Backup Verification

- **Monthly**: Restore the latest backup to a staging Firestore project and run integration tests
- **Quarterly**: Full disaster recovery drill (see Section 10)
- **Verification log**: Results documented in `docs/progress/decisions.jsonl`

---

## 9. Go-Live Runbook

### Day -7: Final Staging Validation

| Step | Task | Owner | Duration |
|------|------|-------|----------|
| 9.1.1 | Deploy release candidate to staging | SaasAdmin | 15 min |
| 9.1.2 | Execute full test suite against staging | SaasAdmin | 30 min |
| 9.1.3 | Complete UAT with HR Manager and Director | HR Manager / Director | 2 hours |
| 9.1.4 | Sign off UAT results | Director | 15 min |
| 9.1.5 | Review vulnerability register -- confirm no open Sev-1 | SaasAdmin | 30 min |
| 9.1.6 | Verify POPIA control status -- all mandatory controls implemented | SaasAdmin | 30 min |
| 9.1.7 | Freeze `main` branch (no non-critical merges until go-live) | SaasAdmin | 5 min |

### Day -3: Infrastructure Provisioning

| Step | Task | Owner | Duration |
|------|------|-------|----------|
| 9.2.1 | Deploy Bicep template to production resource group | SaasAdmin | 20 min |
| 9.2.2 | Provision Azure Key Vault and populate all secrets | SaasAdmin | 30 min |
| 9.2.3 | Configure Firebase Auth production project (MFA, providers) | SaasAdmin | 30 min |
| 9.2.4 | Deploy Firestore security rules to production | SaasAdmin | 15 min |
| 9.2.5 | Deploy Firestore indexes to production | SaasAdmin | 15 min |
| 9.2.6 | Seed statutory data (PAYE, UIF, SDL, ETI, BCEA tables) | SaasAdmin | 15 min |
| 9.2.7 | Verify Log Analytics workspace and Application Insights | SaasAdmin | 15 min |
| 9.2.8 | Configure alert rules in Azure Monitor | SaasAdmin | 30 min |
| 9.2.9 | Run infrastructure smoke test (deploy test container, verify health) | SaasAdmin | 20 min |

### Day -1: DNS and SSL Preparation

| Step | Task | Owner | Duration |
|------|------|-------|----------|
| 9.3.1 | Verify SSL/TLS certificate (auto-managed by Container Apps) | SaasAdmin | 10 min |
| 9.3.2 | Prepare DNS CNAME record for `zenohr.zenowethu.co.za` | SaasAdmin | 10 min |
| 9.3.3 | Set DNS TTL to 300 seconds (5 min) for fast cutover | SaasAdmin | 5 min |
| 9.3.4 | Verify CORS configuration matches production domain | SaasAdmin | 10 min |
| 9.3.5 | Notify all stakeholders of go-live schedule | Director | 15 min |
| 9.3.6 | Prepare rollback plan and distribute to operations team | SaasAdmin | 15 min |
| 9.3.7 | Confirm on-call roster for Day 0 and Day +1 | Director | 10 min |

### Day 0: Production Deployment

| Step | Task | Owner | Duration |
|------|------|-------|----------|
| 9.4.1 | Final check: all Day -1 items complete | SaasAdmin | 5 min |
| 9.4.2 | Trigger production deployment workflow | SaasAdmin | 2 min |
| 9.4.3 | Monitor CI/CD pipeline (build, test, scan, deploy) | SaasAdmin | 15 min |
| 9.4.4 | Verify post-deployment health checks pass (automated) | SaasAdmin | 5 min |
| 9.4.5 | Apply DNS CNAME record pointing to Container App FQDN | SaasAdmin | 5 min |
| 9.4.6 | Verify HTTPS access via `https://zenohr.zenowethu.co.za` | SaasAdmin | 5 min |
| 9.4.7 | Create initial HR Manager account via Firebase Admin | SaasAdmin | 10 min |
| 9.4.8 | HR Manager login test (full authentication flow) | HR Manager | 5 min |
| 9.4.9 | Smoke tests: dashboard loads, employee list renders | HR Manager | 10 min |
| 9.4.10 | Smoke tests: create test employee, verify audit trail | HR Manager | 10 min |
| 9.4.11 | Smoke tests: verify payroll configuration visible | HR Manager | 5 min |
| 9.4.12 | Verify monitoring: check Application Insights for telemetry | SaasAdmin | 10 min |
| 9.4.13 | Verify alerts: trigger a test alert (optional) | SaasAdmin | 10 min |
| 9.4.14 | Confirm go-live success with Director | Director | 5 min |
| 9.4.15 | Announce go-live to all Zenowethu staff | Director | 10 min |

### Day +1: Post-Launch Monitoring

| Step | Task | Owner | Duration |
|------|------|-------|----------|
| 9.5.1 | Review overnight monitoring dashboards | SaasAdmin | 15 min |
| 9.5.2 | Check error rates and response times | SaasAdmin | 15 min |
| 9.5.3 | Verify Firestore backup ran successfully | SaasAdmin | 10 min |
| 9.5.4 | Address any user-reported issues | SaasAdmin / HR Manager | As needed |
| 9.5.5 | Maintain on-call availability | SaasAdmin | Full day |

### Day +7: Post-Launch Review

| Step | Task | Owner | Duration |
|------|------|-------|----------|
| 9.6.1 | Compile performance baseline (P50, P95, P99 response times) | SaasAdmin | 30 min |
| 9.6.2 | Review error log for recurring issues | SaasAdmin | 30 min |
| 9.6.3 | Gather feedback from HR Manager and Director | Director | 30 min |
| 9.6.4 | Document lessons learned | SaasAdmin | 30 min |
| 9.6.5 | Establish performance and reliability baselines | SaasAdmin | 15 min |
| 9.6.6 | Unfreeze `main` branch | SaasAdmin | 5 min |
| 9.6.7 | Schedule first monthly DR test | SaasAdmin | 10 min |

---

## 10. Disaster Recovery

### 10.1 Recovery Architecture

| Tier | Region | Purpose | Status |
|------|--------|---------|--------|
| **Primary** | Azure SA North (Johannesburg) | Production workload | Active |
| **DR** | Azure SA West (Cape Town) | Cold standby | Provisioned on-demand |

Both regions satisfy POPIA data residency requirements as they are located within South Africa.

### 10.2 Disaster Scenarios

| Scenario | Impact | Recovery Strategy |
|----------|--------|-------------------|
| Single container failure | None (auto-restart) | Container Apps automatically replaces failed replicas |
| All replicas unhealthy | Service outage | Investigate root cause; if region-wide, failover to SA West |
| Firestore outage | Service degradation | Wait for Google Cloud recovery (multi-region Firestore SLA) |
| Azure SA North outage | Full outage | Failover to SA West |
| Data corruption | Data loss risk | Restore from Firestore backup |
| Security breach | Data compromise | Activate incident response plan, rotate all secrets, rebuild from clean state |

### 10.3 Failover Procedure (SA North to SA West)

**Estimated failover time**: 2-4 hours

1. **Assess the outage**: Confirm SA North is unavailable via Azure Status page and direct probe
2. **Provision DR infrastructure**: Deploy `infra/container-app.bicep` to SA West resource group with `location` parameter overridden (requires Bicep parameter modification for one-time DR use)
3. **Configure secrets**: Replicate Key Vault secrets to SA West Key Vault instance
4. **Deploy application**: Trigger deployment workflow targeting the DR Container App
5. **Verify Firestore connectivity**: Firestore is accessed via Google Cloud SDK (region-independent), so no Firestore failover is needed
6. **Update DNS**: Change CNAME record for `zenohr.zenowethu.co.za` to the DR Container App FQDN
7. **Verify health**: Run liveness and readiness probes
8. **Notify stakeholders**: Inform Director and HR Manager of DR activation

### 10.4 Failback Procedure

Once SA North is restored:

1. Verify SA North infrastructure is healthy
2. Deploy the current container image to SA North
3. Run health checks against SA North
4. Update DNS CNAME back to SA North FQDN (5-minute TTL enables fast cutover)
5. Decommission DR resources in SA West to avoid unnecessary costs
6. Document the incident in `docs/progress/decisions.jsonl`

### 10.5 DR Testing Schedule

| Test Type | Frequency | Scope | Owner |
|-----------|-----------|-------|-------|
| Backup restore verification | Monthly | Restore latest Firestore backup to staging | SaasAdmin |
| Failover simulation | Quarterly | Provision DR in SA West, verify connectivity | SaasAdmin |
| Full DR drill | Annually | End-to-end failover including DNS cutover | SaasAdmin + Director |

---

## 11. Infrastructure as Code Reference

All infrastructure definitions are version-controlled and reproducible.

| File | Purpose |
|------|---------|
| `infra/container-app.bicep` | Azure Container Apps, Log Analytics, environment definition |
| `Dockerfile` | Multi-stage container image build |
| `.github/workflows/deploy-production.yml` | Production deployment CI/CD pipeline |
| `infra/deploy.sh` | Manual deployment helper script (Azure CLI) |
| `src/ZenoHR.Api/appsettings.Production.json` | Production application settings (no secrets) |

---

## 12. Compliance Considerations

### POPIA Data Residency (CTL-POPIA-001)

- All compute (Azure Container Apps) runs in SA North (Johannesburg)
- All persistent data (Firestore) is configured for the `africa-south1` region
- DR failover to SA West (Cape Town) maintains POPIA compliance
- No data replication to regions outside South Africa

### Audit Trail

- Every deployment is logged in GitHub Actions with the deploying user, reason, commit SHA, and image tag
- The Container App is tagged with `compliance: POPIA` for governance tracking
- All secret access is logged in Azure Key Vault audit logs

### Data Classification

| Data Type | Classification | Storage | Encryption |
|-----------|---------------|---------|------------|
| Employee PII | Confidential | Firestore (encrypted at rest) | AES-256 |
| Payroll data | Confidential | Firestore (encrypted at rest) | AES-256 |
| Audit events | Internal | Firestore (immutable, hash-chained) | AES-256 |
| Statutory rules | Public | Container image + Firestore | N/A |
| Application logs | Internal | Log Analytics | Azure managed |

---

## Appendix A: Quick Command Reference

```bash
# Deploy to production (via GitHub Actions — preferred)
# Navigate to Actions > "Deploy to Production (Manual)" > Run workflow

# Deploy to production (via CLI — emergency)
./infra/deploy.sh sha-abc1234

# Rollback to previous version
az containerapp update \
  --name zenohr-prod-app \
  --resource-group zenohr-prod-rg \
  --image ghcr.io/<org>/zenohr:<previous-tag>

# Check health
curl https://zenohr.zenowethu.co.za/health
curl https://zenohr.zenowethu.co.za/health/ready

# View container logs
az containerapp logs show \
  --name zenohr-prod-app \
  --resource-group zenohr-prod-rg \
  --follow

# View current container image
az containerapp show \
  --name zenohr-prod-app \
  --resource-group zenohr-prod-rg \
  --query "properties.template.containers[0].image" \
  --output tsv

# Provision infrastructure from Bicep
az deployment group create \
  --resource-group zenohr-prod-rg \
  --template-file infra/container-app.bicep \
  --parameters containerImage=ghcr.io/<org>/zenohr:sha-abc1234 \
               keyVaultUri=https://zenohr-prod-kv.vault.azure.net/ \
               appInsightsConnectionString=<connection-string> \
               firebaseProjectId=<project-id>
```
