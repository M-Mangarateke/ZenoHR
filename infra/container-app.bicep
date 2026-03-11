// REQ-OPS-009: Azure Container Apps infrastructure definition for ZenoHR production deployment.
// Region: southafricanorth — POPIA data residency compliance (CTL-POPIA-001).
// This Bicep template provisions: Log Analytics workspace, Container App Environment, and Container App.

// ── Parameters ──────────────────────────────────────────────────────────────────

@description('Base name for all resources (e.g., zenohr-prod)')
param appName string = 'zenohr-prod'

@description('Azure region — must be southafricanorth for POPIA data residency (REQ-OPS-009)')
@allowed([
  'southafricanorth'
])
param location string = 'southafricanorth'

@description('Container image reference (e.g., ghcr.io/org/zenohr:sha-abc1234)')
param containerImage string

@description('Azure Container Registry login server (e.g., zenohr.azurecr.io)')
param acrLoginServer string = ''

@description('Azure Container Registry username (empty if using managed identity)')
param acrUsername string = ''

@secure()
@description('Azure Container Registry password (empty if using managed identity)')
param acrPassword string = ''

@secure()
@description('Azure Key Vault connection string for secrets resolution')
param keyVaultUri string

@secure()
@description('Application Insights connection string for OpenTelemetry export (REQ-OPS-005)')
param appInsightsConnectionString string

@description('Firebase/Firestore project ID')
param firebaseProjectId string

@secure()
@description('Google Cloud service account key JSON (base64-encoded) for Firestore access')
param firestoreCredentials string = ''

@description('Minimum number of replicas (REQ-OPS-009: at least 1 for availability)')
@minValue(1)
@maxValue(10)
param minReplicas int = 1

@description('Maximum number of replicas for auto-scaling')
@minValue(1)
@maxValue(10)
param maxReplicas int = 5

@description('Allowed CORS origins (comma-separated)')
param corsAllowedOrigins string = 'https://zenohr.zenowethu.co.za'

// ── Log Analytics Workspace ─────────────────────────────────────────────────────
// REQ-OPS-005: Centralized logging for observability.

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${appName}-logs'
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 90 // REQ-OPS-008: Audit log retention
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    company: 'Zenowethu Pty Ltd'
    compliance: 'POPIA'
  }
}

// ── Container App Environment ───────────────────────────────────────────────────
// REQ-OPS-009: Managed environment with Log Analytics integration.

resource containerAppEnv 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${appName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
    zoneRedundant: false // SA North has limited zone support; enable when available
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    compliance: 'POPIA'
  }
}

// ── Container App ───────────────────────────────────────────────────────────────
// REQ-OPS-009: ZenoHR API + Blazor Server application container.
// REQ-OPS-007: Health probes for liveness, readiness, and startup.

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${appName}-app'
  location: location
  properties: {
    managedEnvironmentId: containerAppEnv.id
    configuration: {
      // ── Ingress ──
      // REQ-OPS-009: External HTTPS ingress on port 8080.
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http' // TLS terminated at Container Apps ingress layer
        allowInsecure: false // Force HTTPS (REQ-SEC-003)
        corsPolicy: {
          allowedOrigins: split(corsAllowedOrigins, ',')
          allowedMethods: [ 'GET', 'POST', 'PUT', 'DELETE', 'OPTIONS' ]
          allowedHeaders: [ 'Authorization', 'Content-Type', 'X-Correlation-Id' ]
          maxAge: 3600
        }
      }

      // ── Container Registry credentials ──
      registries: !empty(acrLoginServer) ? [
        {
          server: acrLoginServer
          username: acrUsername
          passwordSecretRef: 'acr-password'
        }
      ] : []

      // ── Secrets ──
      // REQ-SEC-001: All secrets sourced from Azure Key Vault or GitHub Actions secrets.
      // No hardcoded values in infrastructure code.
      secrets: concat(
        [
          {
            name: 'appinsights-connection-string'
            value: appInsightsConnectionString
          }
          {
            name: 'key-vault-uri'
            value: keyVaultUri
          }
          {
            name: 'firebase-project-id'
            value: firebaseProjectId
          }
        ],
        !empty(acrPassword) ? [
          {
            name: 'acr-password'
            value: acrPassword
          }
        ] : [],
        !empty(firestoreCredentials) ? [
          {
            name: 'firestore-credentials'
            value: firestoreCredentials
          }
        ] : []
      )
    }

    template: {
      containers: [
        {
          name: 'zenohr-api'
          image: containerImage
          // REQ-OPS-009: Resource allocation — 0.5 vCPU, 1Gi memory.
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }
          env: [
            // REQ-OPS-005: OpenTelemetry export to Azure Monitor
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              secretRef: 'appinsights-connection-string'
            }
            {
              name: 'AzureKeyVault__Uri'
              secretRef: 'key-vault-uri'
            }
            {
              name: 'Firebase__ProjectId'
              secretRef: 'firebase-project-id'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://+:8080'
            }
            {
              name: 'Cors__AllowedOrigins'
              value: corsAllowedOrigins
            }
          ]

          // ── Health Probes ──
          // REQ-OPS-007: Liveness, readiness, and startup probes for Azure Container Apps.
          probes: [
            {
              type: 'Liveness'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 30
              periodSeconds: 30
              timeoutSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: {
                path: '/health/ready'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 15
              periodSeconds: 15
              timeoutSeconds: 10
              failureThreshold: 3
            }
            {
              type: 'Startup'
              httpGet: {
                path: '/health'
                port: 8080
                scheme: 'HTTP'
              }
              initialDelaySeconds: 5
              periodSeconds: 10
              timeoutSeconds: 10
              failureThreshold: 10 // Allow up to ~100s for cold start
            }
          ]
        }
      ]

      // ── Scaling ──
      // REQ-OPS-009: Auto-scale between 1 and 5 replicas based on HTTP concurrency.
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '50' // Scale up when > 50 concurrent requests per replica
              }
            }
          }
        ]
      }
    }
  }
  tags: {
    application: 'ZenoHR'
    environment: 'production'
    company: 'Zenowethu Pty Ltd'
    compliance: 'POPIA'
    // REQ-OPS-009: Tag for cost tracking and resource identification
  }
}

// ── Outputs ─────────────────────────────────────────────────────────────────────

@description('The FQDN of the Container App')
output appFqdn string = containerApp.properties.configuration.ingress.fqdn

@description('The full HTTPS URL of the Container App')
output appUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'

@description('The resource ID of the Container App')
output appResourceId string = containerApp.id

@description('The name of the Container App Environment')
output environmentName string = containerAppEnv.name

@description('The Log Analytics workspace ID')
output logAnalyticsWorkspaceId string = logAnalytics.id
