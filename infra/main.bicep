// REQ-OPS-009: Azure Container Apps deployment — South Africa North region.
// POPIA compliance: all resources MUST remain in southafricanorth for data residency.
// Resources: Log Analytics Workspace, Container Apps Environment, Container App.
// Secrets sourced from Azure Key Vault via managed identity (no secrets in template).

@description('Environment name (dev, staging, prod)')
param environmentName string = 'prod'

@description('Azure region. Must be southafricanorth for POPIA data residency (REQ-OPS-009).')
@allowed(['southafricanorth'])
param location string = 'southafricanorth'

@description('Container image to deploy (e.g. ghcr.io/org/zenohr:sha-abc1234)')
param containerImage string

@description('Firebase project ID (not a secret — used as an env var in the container)')
param firebaseProjectId string

@description('Azure Key Vault name containing firebase-service-account and other secrets')
param keyVaultName string

@description('Minimum replicas (1 for production — always-on; 0 for dev to save cost)')
@minValue(0)
@maxValue(3)
param minReplicas int = 1

@description('Maximum replicas for scale-out under load')
@minValue(1)
@maxValue(10)
param maxReplicas int = 3

var resourcePrefix = 'zenohr-${environmentName}'

// ── Log Analytics Workspace ────────────────────────────────────────────────
// Required by Container Apps Environment for structured log ingestion.
// REQ-OPS-005: Centralised observability — logs flow to Azure Monitor via OTel.
// POPIA: retentionInDays=90 satisfies POPIA audit log retention (minimum 3 years needs archival
//        policy on top — extend via Diagnostic Settings → Storage Account in prod).
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${resourcePrefix}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
  }
}

// ── Container Apps Environment ─────────────────────────────────────────────
// Hosts the Container App and manages networking, ingress TLS, and observability.
// southafricanorth — POPIA data residency enforced by @allowed constraint on location param.
resource containerEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${resourcePrefix}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    // Workload profiles: Consumption plan (no dedicated nodes — cost effective for v1)
    workloadProfiles: [
      {
        name: 'Consumption'
        workloadProfileType: 'Consumption'
      }
    ]
  }
}

// ── Container App ─────────────────────────────────────────────────────────
// Hosts the ZenoHR.Api ASP.NET Core + Blazor Server image.
// System-assigned managed identity used for Key Vault secret access (VUL-002 remediation).
resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${resourcePrefix}-app'
  location: location
  identity: {
    type: 'SystemAssigned'  // Key Vault Secrets User role assigned in keyvault.bicep
  }
  properties: {
    managedEnvironmentId: containerEnv.id
    workloadProfileName: 'Consumption'
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
        allowInsecure: false  // HTTPS enforced at ingress — VUL-001/VUL-023 (HSTS, CSP applied in app)
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
        // Sticky sessions off — stateless API; Blazor Server SignalR handled by Azure SignalR Service
        stickySessions: {
          affinity: 'none'
        }
      }
      // Secrets reference Key Vault via managed identity (no plaintext secrets in bicep)
      secrets: [
        {
          name: 'firebase-credentials-json'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/firebase-service-account'
          identity: 'system'
        }
        {
          name: 'azure-monitor-connection-string'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/azure-monitor-connection-string'
          identity: 'system'
        }
        {
          name: 'cors-allowed-origins'
          keyVaultUrl: 'https://${keyVaultName}.vault.azure.net/secrets/cors-allowed-origins'
          identity: 'system'
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'zenohr'
          image: containerImage
          env: [
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'Firebase__ProjectId'
              value: firebaseProjectId
            }
            // GOOGLE_APPLICATION_CREDENTIALS points to the firebase service account JSON.
            // The JSON is written to /app/secrets/ by an init container or volume mount.
            // For Container Apps, the secret value is surfaced as an env var (base64 JSON).
            {
              name: 'Firebase__CredentialsJson'
              secretRef: 'firebase-credentials-json'
            }
            {
              name: 'AzureMonitor__ConnectionString'
              secretRef: 'azure-monitor-connection-string'
            }
            {
              name: 'Cors__AllowedOrigins'
              secretRef: 'cors-allowed-origins'
            }
            // Dotnet diagnostic: disable telemetry opt-in prompt in container
            {
              name: 'DOTNET_CLI_TELEMETRY_OPTOUT'
              value: '1'
            }
            {
              name: 'DOTNET_NOLOGO'
              value: '1'
            }
          ]
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          // Liveness + Readiness probes map to /health and /health/ready (REQ-OPS-007)
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 30
              periodSeconds: 30
              failureThreshold: 3
              timeoutSeconds: 10
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              failureThreshold: 3
              timeoutSeconds: 5
            }
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
              }
              initialDelaySeconds: 10
              periodSeconds: 10
              failureThreshold: 10
              timeoutSeconds: 5
            }
          ]
        }
      ]
      // Scale: minimum 1 replica (always-on for prod), max 3 — scales on HTTP concurrency.
      // minReplicas=0 allowed for dev/staging environments to minimise cost.
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '50'  // Scale out when >50 concurrent requests per replica
              }
            }
          }
        ]
      }
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────
output containerAppUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output containerAppPrincipalId string = containerApp.identity.principalId
output containerAppName string = containerApp.name
output logAnalyticsWorkspaceId string = logAnalytics.id
