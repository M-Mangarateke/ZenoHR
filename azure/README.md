# ZenoHR Azure Deployment Guide

REQ-OPS-009 | POPIA data residency: all resources in **southafricanorth**.

---

## Architecture

```
GitHub Actions (deploy.yml)
  └─ Docker build + Trivy scan
  └─ Push to GHCR (ghcr.io/org/zenohr:sha-<hash>)
  └─ az containerapp update → Azure Container Apps (southafricanorth)
                                  ├─ Managed Identity → Azure Key Vault (southafricanorth)
                                  └─ Logs → Log Analytics Workspace (southafricanorth)

Google Cloud Firestore (nam5) ← no PII stored in Azure; Firestore accessed over HTTPS
```

---

## Prerequisites

- Azure CLI with Bicep extension: `az bicep install`
- Azure subscription with `Microsoft.App` and `Microsoft.KeyVault` providers registered
- GitHub repository secrets configured (see below)
- Firebase project `zenohr-a7ccf` service account JSON ready for Key Vault upload

---

## Initial Deployment (One-Time)

### 1. Create resource group in SA North

```bash
az group create \
  --name zenohr-prod-rg \
  --location southafricanorth
```

### 2. Deploy Key Vault (before main infra — Container App needs KV URI)

```bash
# First deploy — no containerAppPrincipalId yet; deploy KV standalone then update after step 3
az deployment group create \
  --resource-group zenohr-prod-rg \
  --template-file azure/keyvault.bicep \
  --parameters \
      keyVaultName=zenohr-prod-kv \
      adminGroupObjectId=<your-aad-group-object-id>
```

### 3. Upload secrets to Key Vault

```bash
KV=zenohr-prod-kv

# Firebase service account (the full JSON content)
az keyvault secret set \
  --vault-name $KV \
  --name firebase-service-account \
  --file /path/to/firebase-service-account.json

# Azure Monitor connection string (from Application Insights resource)
az keyvault secret set \
  --vault-name $KV \
  --name azure-monitor-connection-string \
  --value "InstrumentationKey=...;IngestionEndpoint=..."

# CORS allowed origins (comma-separated production URL(s))
az keyvault secret set \
  --vault-name $KV \
  --name cors-allowed-origins \
  --value "https://zenohr.zenowethu.co.za"
```

### 4. Deploy Container Apps infrastructure

```bash
az deployment group create \
  --resource-group zenohr-prod-rg \
  --template-file azure/main.bicep \
  --parameters \
      environmentName=prod \
      containerImage=ghcr.io/<org>/zenohr:latest \
      firebaseProjectId=zenohr-a7ccf \
      keyVaultName=zenohr-prod-kv

# Capture Container App principal ID from output
PRINCIPAL_ID=$(az deployment group show \
  --resource-group zenohr-prod-rg \
  --name main \
  --query properties.outputs.containerAppPrincipalId.value \
  --output tsv)
```

### 5. Grant Container App access to Key Vault

```bash
az deployment group create \
  --resource-group zenohr-prod-rg \
  --template-file azure/keyvault.bicep \
  --parameters \
      keyVaultName=zenohr-prod-kv \
      containerAppPrincipalId=$PRINCIPAL_ID \
      adminGroupObjectId=<your-aad-group-object-id>
```

---

## Ongoing Deployments

Handled automatically by `.github/workflows/deploy.yml` on every push to `main`/`master`.

The pipeline:
1. Builds Docker image (multi-stage, non-root, .NET 10 ASP.NET runtime)
2. Scans with Trivy — fails on HIGH/CRITICAL CVEs (VUL-017)
3. Pushes `sha-<commit>` tagged image to GHCR
4. Runs `az containerapp update` with the immutable sha-tagged image
5. Verifies `/health` endpoint returns 200 OK

---

## GitHub Secrets Required

| Secret | Description |
|--------|-------------|
| `AZURE_CLIENT_ID` | Service principal / app registration client ID (OIDC) |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_SUBSCRIPTION_ID` | Azure subscription ID |

Configure OIDC federated credentials on the Azure AD app registration for the GitHub repository
(Actions → Settings → Environments → production). No long-lived secrets needed.

---

## Bicep Files

| File | Purpose |
|------|---------|
| `azure/main.bicep` | Log Analytics, Container Apps Environment, Container App (with auto-scaling 1–3 replicas) |
| `azure/keyvault.bicep` | Key Vault + RBAC role assignments (Secrets User for Container App, Admin for ops team) |

---

## POPIA Data Residency

All Azure resources are constrained to `southafricanorth` via the `@allowed` parameter decorator
in both Bicep files. Any attempt to deploy to another region will fail at parameter validation.

Google Cloud Firestore uses `nam5` (US multi-region) per project design decision. No PII is
stored in Azure — Azure hosts the compute layer only. Firestore connection is outbound HTTPS.
