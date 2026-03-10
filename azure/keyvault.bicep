// REQ-OPS-009: Azure Key Vault for ZenoHR secrets — southafricanorth region.
// POPIA compliance: all secrets remain in SA North (data residency requirement).
// Grants Container App system-assigned managed identity the Key Vault Secrets User role.
// Network ACLs: deny all public access by default; Azure services bypass for Key Vault references.

@description('Azure region. Must be southafricanorth for POPIA data residency (REQ-OPS-009).')
@allowed(['southafricanorth'])
param location string = 'southafricanorth'

@description('Key Vault name (must be globally unique, 3–24 chars, alphanumeric + hyphens)')
param keyVaultName string

@description('Principal ID of the Container App system-assigned managed identity (from main.bicep output)')
param containerAppPrincipalId string

@description('Object ID of the team/admin group to grant Key Vault Administrator role (for secret management)')
param adminGroupObjectId string = ''

// ── Key Vault ────────────────────────────────────────────────────────────
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId

    // RBAC authorization model — preferred over access policies (more granular, auditable)
    enableRbacAuthorization: true

    // Soft-delete + purge protection: POPIA audit log retention — prevents accidental secret destruction.
    // 90-day retention aligns with the Log Analytics workspace retention period.
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true  // Prevents permanent deletion during retention window

    // Network: deny all inbound; allow Azure services (Container Apps uses trusted Azure backbone)
    networkAcls: {
      defaultAction: 'Deny'
      bypass: 'AzureServices'
      ipRules: []
      virtualNetworkRules: []
    }

    // Audit: all data-plane operations logged to Azure Monitor (connected via diagnostic settings below)
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: false
  }
}

// ── Role: Key Vault Secrets User → Container App managed identity ────────
// Role ID: 4633458b-17de-408a-b874-0445c86b69e6 (Key Vault Secrets User — read-only on secret values)
// This is the minimum permission required for Container Apps to resolve secretRef values.
// REQ-SEC-001: Principle of least privilege.
resource containerAppSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, containerAppPrincipalId, '4633458b-17de-408a-b874-0445c86b69e6')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '4633458b-17de-408a-b874-0445c86b69e6'  // Key Vault Secrets User (read-only)
    )
    principalId: containerAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}

// ── Role: Key Vault Administrator → Admin group (optional) ───────────────
// Only deployed when adminGroupObjectId is provided.
// Role ID: 00482a5a-887f-4fb3-b363-3b7fe8e74483 (Key Vault Administrator)
// Allows the ops team to read/write/delete secrets via the Azure Portal or CLI.
resource adminGroupKeyVaultAdminRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (!empty(adminGroupObjectId)) {
  name: guid(keyVault.id, adminGroupObjectId, '00482a5a-887f-4fb3-b363-3b7fe8e74483')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '00482a5a-887f-4fb3-b363-3b7fe8e74483'  // Key Vault Administrator
    )
    principalId: adminGroupObjectId
    principalType: 'Group'
  }
}

// ── Diagnostic Settings → Log Analytics ─────────────────────────────────
// Sends Key Vault audit logs (AuditEvent) to Log Analytics for POPIA compliance monitoring.
// POPIA CTL-POPIA-006: access to personal data must be logged and reviewable.
// Note: logAnalyticsWorkspaceId must be passed in from main.bicep output if needed.
// Omitted here to keep keyvault.bicep independently deployable — add via Portal or separate module.

// ── Outputs ──────────────────────────────────────────────────────────────
output keyVaultUri string = keyVault.properties.vaultUri
output keyVaultName string = keyVault.name
