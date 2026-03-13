// VUL-026: Network isolation for ZenoHR Azure deployment.
// CTL-POPIA-001: All infrastructure deployed in South Africa North for POPIA data residency.
//
// Architecture rationale:
// ─────────────────────────────────────────────────────────────────────────────────
// POPIA §19 requires appropriate technical measures to protect personal information.
// Network isolation via VNet + NSG provides defence-in-depth:
//   1. app-subnet: Container Apps — only accepts HTTPS inbound (port 443).
//   2. data-subnet: Private endpoints for Key Vault and any future Azure PaaS services.
//      No public internet access — all traffic stays within the Azure backbone.
//   3. mgmt-subnet: Management/jump-box access for emergency operations (VUL-006 break-glass).
//
// Firestore (Google Cloud) traffic egresses via the app-subnet NSG allow rules.
// Azure Key Vault is accessed via private endpoint in data-subnet (no public exposure).
// ─────────────────────────────────────────────────────────────────────────────────

// ── Parameters ──────────────────────────────────────────────────────────────────

@description('Base name prefix for all network resources')
param namePrefix string = 'zenohr-prod'

@description('Azure region — must be southafricanorth for POPIA data residency (CTL-POPIA-001)')
@allowed([
  'southafricanorth'
])
param location string = 'southafricanorth'

@description('VNet address space (RFC 1918)')
param vnetAddressPrefix string = '10.0.0.0/16'

@description('App subnet address range — hosts Container Apps environment')
param appSubnetPrefix string = '10.0.1.0/24'

@description('Data subnet address range — private endpoints for Key Vault, storage')
param dataSubnetPrefix string = '10.0.2.0/24'

@description('Management subnet address range — jump-box / break-glass access')
param mgmtSubnetPrefix string = '10.0.3.0/24'

@description('Resource ID of the Key Vault to create a private endpoint for (leave empty to skip)')
param keyVaultResourceId string = ''

// ── NSG: App Subnet ─────────────────────────────────────────────────────────────
// VUL-026: Only HTTPS inbound allowed. All other inbound denied.

resource appNsg 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: '${namePrefix}-app-nsg'
  location: location
  properties: {
    securityRules: [
      {
        // Allow HTTPS inbound from the internet (Container Apps ingress)
        name: 'Allow-HTTPS-Inbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '443'
        }
      }
      {
        // Deny all other inbound traffic
        name: 'Deny-All-Other-Inbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        // Allow outbound to Firestore (Google Cloud) — required for database access
        // Google Cloud IP ranges for firestore.googleapis.com
        name: 'Allow-Outbound-Firestore'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: appSubnetPrefix
          sourcePortRange: '*'
          destinationAddressPrefix: 'Internet'
          destinationPortRange: '443'
        }
      }
      {
        // Allow outbound to data-subnet (private endpoints for Key Vault)
        name: 'Allow-Outbound-DataSubnet'
        properties: {
          priority: 200
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: appSubnetPrefix
          sourcePortRange: '*'
          destinationAddressPrefix: dataSubnetPrefix
          destinationPortRange: '443'
        }
      }
    ]
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    compliance: 'POPIA'
    security: 'VUL-026'
  }
}

// ── NSG: Data Subnet ────────────────────────────────────────────────────────────
// VUL-026: No public inbound. Only accepts traffic from app-subnet.

resource dataNsg 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: '${namePrefix}-data-nsg'
  location: location
  properties: {
    securityRules: [
      {
        // Allow inbound from app-subnet only (private endpoint traffic)
        name: 'Allow-AppSubnet-Inbound'
        properties: {
          priority: 100
          direction: 'Inbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: appSubnetPrefix
          sourcePortRange: '*'
          destinationAddressPrefix: dataSubnetPrefix
          destinationPortRange: '443'
        }
      }
      {
        // Deny all other inbound
        name: 'Deny-All-Other-Inbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
    ]
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    compliance: 'POPIA'
    security: 'VUL-026'
  }
}

// ── NSG: Management Subnet ──────────────────────────────────────────────────────
// VUL-026, VUL-006: Restricted access for break-glass / emergency operations.

resource mgmtNsg 'Microsoft.Network/networkSecurityGroups@2024-01-01' = {
  name: '${namePrefix}-mgmt-nsg'
  location: location
  properties: {
    securityRules: [
      {
        // Deny all inbound by default — break-glass access is granted via
        // just-in-time (JIT) VM access or Azure Bastion, not permanent NSG rules.
        name: 'Deny-All-Inbound'
        properties: {
          priority: 4096
          direction: 'Inbound'
          access: 'Deny'
          protocol: '*'
          sourceAddressPrefix: '*'
          sourcePortRange: '*'
          destinationAddressPrefix: '*'
          destinationPortRange: '*'
        }
      }
      {
        // Allow outbound to app and data subnets for management tasks
        name: 'Allow-Outbound-Internal'
        properties: {
          priority: 100
          direction: 'Outbound'
          access: 'Allow'
          protocol: 'Tcp'
          sourceAddressPrefix: mgmtSubnetPrefix
          sourcePortRange: '*'
          destinationAddressPrefix: vnetAddressPrefix
          destinationPortRange: '*'
        }
      }
    ]
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    compliance: 'POPIA'
    security: 'VUL-026'
  }
}

// ── Virtual Network ─────────────────────────────────────────────────────────────
// CTL-POPIA-001: Deployed in South Africa North for data residency.
// VUL-026: Three-subnet architecture for network isolation.

resource vnet 'Microsoft.Network/virtualNetworks@2024-01-01' = {
  name: '${namePrefix}-vnet'
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetAddressPrefix
      ]
    }
    subnets: [
      {
        // App subnet — Container Apps environment
        name: 'app-subnet'
        properties: {
          addressPrefix: appSubnetPrefix
          networkSecurityGroup: {
            id: appNsg.id
          }
          // Container Apps requires delegation
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
        }
      }
      {
        // Data subnet — private endpoints (Key Vault, future Azure services)
        name: 'data-subnet'
        properties: {
          addressPrefix: dataSubnetPrefix
          networkSecurityGroup: {
            id: dataNsg.id
          }
          // Private endpoints require this setting
          privateEndpointNetworkPolicies: 'Disabled'
        }
      }
      {
        // Management subnet — break-glass / jump-box access
        name: 'mgmt-subnet'
        properties: {
          addressPrefix: mgmtSubnetPrefix
          networkSecurityGroup: {
            id: mgmtNsg.id
          }
        }
      }
    ]
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    company: 'Zenowethu Pty Ltd'
    compliance: 'POPIA-data-residency'
    security: 'VUL-026'
  }
}

// ── Private Endpoint: Key Vault ─────────────────────────────────────────────────
// VUL-026, REQ-SEC-001: Key Vault accessed via private endpoint only.
// This ensures encryption keys and secrets never traverse the public internet.

resource keyVaultPrivateEndpoint 'Microsoft.Network/privateEndpoints@2024-01-01' = if (!empty(keyVaultResourceId)) {
  name: '${namePrefix}-kv-pe'
  location: location
  properties: {
    subnet: {
      id: vnet.properties.subnets[1].id // data-subnet
    }
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-kv-plsc'
        properties: {
          privateLinkServiceId: keyVaultResourceId
          groupIds: [
            'vault'
          ]
        }
      }
    ]
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    compliance: 'POPIA'
    security: 'VUL-026'
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────────

@description('Resource ID of the Virtual Network')
output vnetId string = vnet.id

@description('Resource ID of the app-subnet (for Container Apps environment integration)')
output appSubnetId string = vnet.properties.subnets[0].id

@description('Resource ID of the data-subnet (for private endpoints)')
output dataSubnetId string = vnet.properties.subnets[1].id

@description('Resource ID of the mgmt-subnet (for management access)')
output mgmtSubnetId string = vnet.properties.subnets[2].id

@description('Resource ID of the Key Vault private endpoint (empty if not created)')
output keyVaultPrivateEndpointId string = !empty(keyVaultResourceId) ? keyVaultPrivateEndpoint.id : ''
