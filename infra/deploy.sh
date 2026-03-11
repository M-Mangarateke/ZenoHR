#!/usr/bin/env bash
# REQ-OPS-009: Deployment helper script for Azure Container Apps.
# Usage: ./infra/deploy.sh [image-tag]
# Example: ./infra/deploy.sh sha-abc1234
#
# Prerequisites:
#   - Azure CLI installed and logged in
#   - Appropriate subscription selected
#   - Container App already provisioned (via container-app.bicep)
#
# Region: southafricanorth (POPIA data residency compliance — CTL-POPIA-001)

set -euo pipefail

# ── Configuration ────────────────────────────────────────────────────────────────
APP_NAME="${AZURE_CONTAINER_APP:-zenohr-prod-app}"
RESOURCE_GROUP="${AZURE_RESOURCE_GROUP:-zenohr-prod-rg}"
REGISTRY="${CONTAINER_REGISTRY:-ghcr.io}"
IMAGE_NAME="${IMAGE_NAME:-$(git remote get-url origin | sed 's|.*github.com[:/]||;s|\.git$||')/zenohr}"
LOCATION="southafricanorth"

# Resolve image tag: argument > env var > git short SHA
IMAGE_TAG="${1:-${IMAGE_TAG:-sha-$(git rev-parse --short HEAD)}}"
FULL_IMAGE="${REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}"

# ── Pre-flight checks ───────────────────────────────────────────────────────────
echo "=== ZenoHR Production Deployment ==="
echo "Container App : ${APP_NAME}"
echo "Resource Group: ${RESOURCE_GROUP}"
echo "Image         : ${FULL_IMAGE}"
echo "Region        : ${LOCATION} (POPIA compliant)"
echo ""

# Check Azure CLI login
if ! az account show &>/dev/null; then
    echo "ERROR: Not logged in to Azure CLI. Run 'az login' first."
    exit 1
fi

SUBSCRIPTION=$(az account show --query name -o tsv)
echo "Azure Subscription: ${SUBSCRIPTION}"
echo ""

# ── Confirm deployment ───────────────────────────────────────────────────────────
read -rp "Deploy ${FULL_IMAGE} to ${APP_NAME}? [y/N] " confirm
if [[ "${confirm}" != "y" && "${confirm}" != "Y" ]]; then
    echo "Deployment cancelled."
    exit 0
fi

# ── Deploy ───────────────────────────────────────────────────────────────────────
echo ""
echo "Deploying..."

az containerapp update \
    --name "${APP_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --image "${FULL_IMAGE}" \
    --output table

echo ""
echo "=== Deployment complete ==="

# ── Post-deployment health check ─────────────────────────────────────────────────
APP_URL=$(az containerapp show \
    --name "${APP_NAME}" \
    --resource-group "${RESOURCE_GROUP}" \
    --query "properties.configuration.ingress.fqdn" \
    --output tsv)

echo "Application URL: https://${APP_URL}"
echo ""
echo "Running health checks..."

for endpoint in "/health" "/health/ready"; do
    echo -n "  ${endpoint}: "
    for i in 1 2 3 4 5; do
        STATUS=$(curl -s -o /dev/null -w "%{http_code}" "https://${APP_URL}${endpoint}" 2>/dev/null || echo "000")
        if [ "${STATUS}" = "200" ]; then
            echo "OK (HTTP 200)"
            break
        fi
        if [ "${i}" = "5" ]; then
            echo "FAILED (HTTP ${STATUS} after 5 attempts)"
            echo "WARNING: ${endpoint} health check did not pass. Investigate deployment."
        else
            sleep 5
        fi
    done
done

echo ""
echo "Done. Verify at: https://${APP_URL}"
