param location string
param tags object
param sqlAdministratorObjectId string
param sqlAdministratorLogin string
param alertEmailAddress string
@allowed([
  'disabled'
  'approved'
])
param webActivation string
param workerActivation string
param webImageDigest string
param webRevisionSuffix string
param graphMailboxId string
param graphInboxFolderId string
param graphSentFolderId string
param graphChangeNotificationClientStateSecretUri string
param boxConfigJsonSecretUri string
param boxClientSecretSecretUri string
param boxHoldingFolderId string
param automationMcpClientSecretUri string
param automationMcpSigningCertificateSecretUris string
param automationMcpEncryptionCertificateSecretUris string
var automationMcpSigningCertificateEnvironment = [for (uri, index) in split(automationMcpSigningCertificateSecretUris, ','): {
  name: 'AutomationMcp__SigningCertificateSecretUris__${index}'
  value: trim(uri)
}]
var automationMcpEncryptionCertificateEnvironment = [for (uri, index) in split(automationMcpEncryptionCertificateSecretUris, ','): {
  name: 'AutomationMcp__EncryptionCertificateSecretUris__${index}'
  value: trim(uri)
}]
param automationMcpRedirectUris string
param dvlaApiKeySecretUri string
param dvsaClientIdSecretUri string
param dvsaClientSecretSecretUri string
param dvsaApiKeySecretUri string
param dvsaTokenUri string
param dvsaScope string
// EXT-04. EVA serves test and live from one host, so the environment is
// decided entirely by which credential pair these two URIs resolve to.
param evaClientIdSecretUri string
param evaClientSecretSecretUri string
param evaBaseUri string
param evaRequestFrom string
param evaInspectionType string
param evaInstructionEmail string

var suffix = take(uniqueString(subscription().subscriptionId, resourceGroup().id, 'prod'), 10)
var prefix = 'pegasus-prod'
var telemetryDailyCapGb = json('0.5')
var transportStorageName = 'pegtrans${suffix}'
var custodyStorageName = 'pegcustody${suffix}'
var keyVaultName = 'pegasusprodkv${take(suffix, 8)}'
var containerRegistryName = 'pegasusprodacr${suffix}'
var webImageReference = '${containerRegistryName}.azurecr.io/pegasus/web@${webImageDigest}'
var webActivationApproved = webActivation == 'approved' && startsWith(webImageDigest, 'sha256:') && length(webImageDigest) == 71 && length(webRevisionSuffix) == 12
var workerActivationApproved = workerActivation == 'approved-live-worker'
var blobDataOwnerRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var blobDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var queueDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var queueDataMessageSenderRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'c6a89b2d-59bc-44d0-9896-0f6e12d7b80a')
var tableDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var acrPullRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '7f951dda-4ed3-4680-a7ca-43fe172d538d')
var monitoringMetricsPublisherRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')
var webSqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Managed Identity;User Id=${webIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var workerSqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Managed Identity;User Id=${workerIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 31
    workspaceCapping: { dailyQuotaGb: telemetryDailyCapGb }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${prefix}-appi-${suffix}'
  location: location
  kind: 'web'
  tags: tags
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    DisableLocalAuth: true
  }
}

// MAIL-020: one number governs the component and workspace daily caps.
resource applicationInsightsDailyCap 'Microsoft.Insights/components/pricingPlans@2017-10-01' = {
  parent: applicationInsights
  name: 'current'
  properties: {
    planType: 'Basic'
    cap: telemetryDailyCapGb
    warningThreshold: 90
    stopSendNotificationWhenHitCap: false
  }
}

resource actionGroup 'Microsoft.Insights/actionGroups@2023-01-01' = {
  name: '${prefix}-operations'
  location: 'global'
  tags: tags
  properties: {
    groupShortName: 'PegProd'
    enabled: true
    emailReceivers: [
      {
        name: 'Pegasus production operator'
        emailAddress: alertEmailAddress
        useCommonAlertSchema: true
      }
    ]
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource transportStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: transportStorageName
  location: location
  tags: union(tags, { purpose: 'transport-deployment' })
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

resource transportBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: transportStorage
  name: 'default'
  properties: { deleteRetentionPolicy: { enabled: true, days: 7 } }
}

resource packageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: transportBlobService
  name: 'app-package'
  properties: { publicAccess: 'None' }
}

resource transportQueueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: transportStorage
  name: 'default'
}

resource intakeQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: transportQueueService
  name: 'intake-work'
}

resource intakePoisonQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: transportQueueService
  name: 'intake-work-poison'
}

resource custodyStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: custodyStorageName
  location: location
  tags: union(tags, { purpose: 'custody-protection' })
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    defaultToOAuthAuthentication: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

resource custodyBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: custodyStorage
  name: 'default'
  properties: { deleteRetentionPolicy: { enabled: true, days: 7 } }
}

resource transientIntakeContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: custodyBlobService
  name: 'transient-intake'
  properties: { publicAccess: 'None' }
}

resource authenticationRingContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: custodyBlobService
  name: 'authentication-ring'
  properties: { publicAccess: 'None' }
}

resource boxLinkContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: custodyBlobService
  name: 'box-links'
  properties: { publicAccess: 'None' }
}

resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: '${prefix}-sql-${suffix}'
  location: location
  tags: tags
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      azureADOnlyAuthentication: true
      login: sqlAdministratorLogin
      principalType: 'User'
      sid: sqlAdministratorObjectId
      tenantId: tenant().tenantId
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: 'Disabled'
  }
}

resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: 'pegasus'
  location: location
  tags: tags
  sku: { name: 'S0', tier: 'Standard', capacity: 10 }
  properties: { maxSizeBytes: 268435456000, zoneRedundant: false }
}

resource sqlAzureServicesFirewall 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource containerRegistry 'Microsoft.ContainerRegistry/registries@2023-07-01' = {
  name: containerRegistryName
  location: location
  tags: union(tags, { purpose: 'web-image-custody' })
  sku: { name: 'Basic' }
  properties: {
    adminUserEnabled: false
    dataEndpointEnabled: false
    publicNetworkAccess: 'Enabled'
  }
}

resource containerEnvironment 'Microsoft.App/managedEnvironments@2025-01-01' = {
  name: '${prefix}-aca-env-${suffix}'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'azure-monitor'
    }
  }
}

resource containerEnvironmentDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: '${prefix}-aca-diagnostics'
  scope: containerEnvironment
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { category: 'ContainerAppConsoleLogs', enabled: true }
      { category: 'ContainerAppSystemLogs', enabled: true }
    ]
  }
}

resource webIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-web-id-${suffix}'
  location: location
  tags: tags
}

resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-worker-id-${suffix}'
  location: location
  tags: tags
}

resource webRegistryPull 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(containerRegistry.id, webIdentity.id, acrPullRole)
  scope: containerRegistry
  properties: {
    roleDefinitionId: acrPullRole
    principalId: webIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource webTelemetryPublisher 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(applicationInsights.id, webIdentity.id, monitoringMetricsPublisherRole)
  scope: applicationInsights
  properties: {
    roleDefinitionId: monitoringMetricsPublisherRole
    principalId: webIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerTelemetryPublisher 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(applicationInsights.id, workerIdentity.id, monitoringMetricsPublisherRole)
  scope: applicationInsights
  properties: {
    roleDefinitionId: monitoringMetricsPublisherRole
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerTransportBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transportStorage.id, workerIdentity.id, blobDataOwnerRole)
  scope: transportStorage
  properties: { roleDefinitionId: blobDataOwnerRole, principalId: workerIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource workerTransportTableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transportStorage.id, workerIdentity.id, tableDataContributorRole)
  scope: transportStorage
  properties: { roleDefinitionId: tableDataContributorRole, principalId: workerIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource workerTransportQueueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transportStorage.id, workerIdentity.id, queueDataContributorRole)
  scope: transportStorage
  properties: { roleDefinitionId: queueDataContributorRole, principalId: workerIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource webIntakeQueueSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(intakeQueue.id, webIdentity.id, queueDataMessageSenderRole)
  scope: intakeQueue
  properties: { roleDefinitionId: queueDataMessageSenderRole, principalId: webIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource workerTransientCustodyOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transientIntakeContainer.id, workerIdentity.id, blobDataOwnerRole)
  scope: transientIntakeContainer
  properties: { roleDefinitionId: blobDataOwnerRole, principalId: workerIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource workerBoxLinkContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(boxLinkContainer.id, workerIdentity.id, blobDataContributorRole)
  scope: boxLinkContainer
  properties: { roleDefinitionId: blobDataContributorRole, principalId: workerIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource webTransientCustodyOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transientIntakeContainer.id, webIdentity.id, blobDataOwnerRole)
  scope: transientIntakeContainer
  properties: { roleDefinitionId: blobDataOwnerRole, principalId: webIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource webAuthenticationRingContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(authenticationRingContainer.id, webIdentity.id, blobDataContributorRole)
  scope: authenticationRingContainer
  properties: { roleDefinitionId: blobDataContributorRole, principalId: webIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource webBoxLinkContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(boxLinkContainer.id, webIdentity.id, blobDataContributorRole)
  scope: boxLinkContainer
  properties: { roleDefinitionId: blobDataContributorRole, principalId: webIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource webContainerApp 'Microsoft.App/containerApps@2025-01-01' = if (webActivationApproved) {
  name: '${prefix}-web-${suffix}'
  location: location
  tags: union(tags, { 'azd-service-name': 'web' })
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${webIdentity.id}': {} } }
  properties: {
    managedEnvironmentId: containerEnvironment.id
    configuration: {
      activeRevisionsMode: 'Single'
      ingress: {
        external: true
        allowInsecure: false
        targetPort: 8080
        transport: 'auto'
        traffic: [
          { latestRevision: true, weight: 100 }
        ]
      }
      registries: [
        {
          server: containerRegistry.properties.loginServer
          identity: webIdentity.id
        }
      ]
      // Web composes Box-backed case custody and managed document content, so it
      // needs the same Box credentials the Worker uses. Container Apps resolves
      // these through the web identity, which needs Key Vault Secrets User on the
      // vault holding the referenced secrets.
      secrets: [
        {
          name: 'box-config-json'
          keyVaultUrl: boxConfigJsonSecretUri
          identity: webIdentity.id
        }
        {
          name: 'box-client-secret'
          keyVaultUrl: boxClientSecretSecretUri
          identity: webIdentity.id
        }
        {
          name: 'automation-mcp-client-secret'
          keyVaultUrl: automationMcpClientSecretUri
          identity: webIdentity.id
        }
        {
          name: 'graph-change-notification-client-state'
          keyVaultUrl: graphChangeNotificationClientStateSecretUri
          identity: webIdentity.id
        }
        {
          name: 'eva-client-id'
          keyVaultUrl: evaClientIdSecretUri
          identity: webIdentity.id
        }
        {
          name: 'eva-client-secret'
          keyVaultUrl: evaClientSecretSecretUri
          identity: webIdentity.id
        }
      ]
    }
    template: {
      revisionSuffix: webRevisionSuffix
      containers: [
        {
          name: 'web'
          image: webImageReference
          env: concat([
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsights.properties.ConnectionString }
            { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'Authorization=AAD;ClientId=${webIdentity.properties.clientId}' }
            { name: 'APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING', value: 'true' }
            { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
            { name: 'ASPNETCORE_HTTP_PORTS', value: '8080' }
            { name: 'Runtime__Profile', value: 'Production' }
            { name: 'ConnectionStrings__Pegasus', value: webSqlConnectionString }
            { name: 'KEY_VAULT_URI', value: keyVault.properties.vaultUri }
            { name: 'TransportStorage__AccountName', value: transportStorage.name }
            { name: 'CustodyStorage__AccountName', value: custodyStorage.name }
            { name: 'CustodyStorage__ServiceUri', value: custodyStorage.properties.primaryEndpoints.blob }
            { name: 'IntakeQueue__ServiceUri', value: transportStorage.properties.primaryEndpoints.queue }
            { name: 'AZURE_CLIENT_ID', value: webIdentity.properties.clientId }
            { name: 'AzureIdentity__WebClientId', value: webIdentity.properties.clientId }
            // Mailbox administration's "add an address" resolve port alone (MAIL-002):
            // Web never polls a mailbox, so no Graph__MailboxId/InboxFolderId/SentFolderId
            // here — only the base URI, matching the Worker's Graph__BaseUri exactly.
            { name: 'Graph__BaseUri', value: 'https://graph.microsoft.com/v1.0/' }
            { name: 'Graph__TenantId', value: tenant().tenantId }
            { name: 'Graph__ChangeNotificationClientState', secretRef: 'graph-change-notification-client-state' }
            { name: 'Box__BaseUri', value: 'https://api.box.com/2.0/' }
            { name: 'Box__UploadUri', value: 'https://upload.box.com/api/2.0/' }
            { name: 'Box__RootFolderId', value: '405543781910' }
            { name: 'Box__HoldingFolderId', value: boxHoldingFolderId }
            { name: 'Box__ConfigJson', secretRef: 'box-config-json' }
            { name: 'Box__ClientSecret', secretRef: 'box-client-secret' }
            { name: 'Features__AutomationMcp', value: 'true' }
            { name: 'Features__ProviderApi', value: 'true' }
            { name: 'AutomationMcp__ClientId', value: 'pegasus-automation' }
            { name: 'AutomationMcp__KeyVaultUri', value: keyVault.properties.vaultUri }
            { name: 'AutomationMcp__ClientSecret', secretRef: 'automation-mcp-client-secret' }
            { name: 'Eva__ClientId', secretRef: 'eva-client-id' }
            { name: 'Eva__ClientSecret', secretRef: 'eva-client-secret' }
            { name: 'Eva__BaseUri', value: evaBaseUri }
            { name: 'Eva__RequestFrom', value: evaRequestFrom }
            { name: 'Eva__InspectionType', value: evaInspectionType }
            { name: 'Eva__InstructionEmail', value: evaInstructionEmail }
            { name: 'AutomationMcp__PublicOrigin', value: 'https://${prefix}-web-${suffix}.${containerEnvironment.properties.defaultDomain}/' }
            { name: 'AutomationMcp__RedirectUris', value: automationMcpRedirectUris }
            // INT-31 upload links. Program.cs:247-250 composes the upload-link
            // services only when AcceptedLimitsVersion is non-empty, so before
            // this block production had no /Uploads surface at all.
            //
            // All FIFTEEN entries are required together, the seven media-type
            // entries no less than the eight scalars: Program.cs:266-268 throws
            // when the array binds to null. Note the failure is NOT a startup
            // crash-loop -- RequestUploadLimits is a lazily resolved factory
            // singleton (DependencyInjection.cs:475) and ValidateOnBuild follows
            // IsDevelopment(), false here -- so a missing or misspelled key
            // surfaces as a 500 on the first request that touches /Uploads, a
            // case's documents, or the Operations page. There is no fail-fast
            // net behind this block; the values were verified by binding them
            // directly (INTK-051 scratch).
            //
            // These are the interim limits accepted 2026-08-29 (INTK-051);
            // INT-31 itself stays open on one-time-vs-reuse and the
            // revocation/expiry error contract.
            { name: 'DocumentRequests__LimitsVersion', value: 'int-31-interim-v1' }
            { name: 'DocumentRequests__AcceptedLimitsVersion', value: 'int-31-interim-v1' }
            // The recorded interim bound is the existing aggregate 10 MB intake
            // limit, which is IntakeEnvelopeLimits.MaximumContentLength exactly
            // (IntakeContracts.cs:13). DurableIntake bounds the ManualUpload
            // channel by that same constant, so raising these two alone would
            // accept a larger file and then lose it downstream -- see INTK-052.
            { name: 'DocumentRequests__MaximumRequestBytes', value: '10485760' }
            { name: 'DocumentRequests__MaximumFileBytes', value: '10485760' }
            { name: 'DocumentRequests__MaximumFileCount', value: '10' }
            // 7 days, matching the existing chase cadence (CASE-17/18, MAIL-18).
            { name: 'DocumentRequests__LifetimeHours', value: '168' }
            // Bounds a caller who HOLDS a token. A caller who holds none is
            // bounded by PublicUploadLink's per-address policy instead, because
            // RequestUploadAttemptLimiter partitions on the token digest and is
            // never reached for an unknown token.
            { name: 'DocumentRequests__RateLimit', value: '20' }
            { name: 'DocumentRequests__RateLimitWindowMinutes', value: '10' }
            // Exactly the seven media types MimeKitPdfPigOpenXmlIntakeSourceReader
            // resolves to a SourceFormat (DetectFormat, :971-1014), so an upload link
            // admits nothing the estate cannot read and refuses nothing it can.
            // text/plain is deliberately absent: the reader has no handler for it
            // and would classify it Unsupported.
            { name: 'DocumentRequests__AllowedMediaTypes__0', value: 'application/pdf' }
            { name: 'DocumentRequests__AllowedMediaTypes__1', value: 'image/jpeg' }
            { name: 'DocumentRequests__AllowedMediaTypes__2', value: 'image/png' }
            { name: 'DocumentRequests__AllowedMediaTypes__3', value: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document' }
            { name: 'DocumentRequests__AllowedMediaTypes__4', value: 'application/msword' }
            { name: 'DocumentRequests__AllowedMediaTypes__5', value: 'message/rfc822' }
            { name: 'DocumentRequests__AllowedMediaTypes__6', value: 'application/vnd.ms-outlook' }
          ], automationMcpSigningCertificateEnvironment, automationMcpEncryptionCertificateEnvironment)
          // ADR-0028: the report renderer runs in process in this container,
          // so headless Chromium shares the app's CPU and memory. Container
          // Apps hard-OOM-kills rather than throttling, and this app runs a
          // single always-warm replica, so a render that exceeded 1Gi would
          // take the site down until it restarted. Raised to the next valid
          // Consumption combination on the operator's decision (2026-08-19,
          // DELIV-012); CPU and memory must stay a supported pair.
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          probes: [
            {
              type: 'Startup'
              httpGet: { path: '/health/live', port: 8080, scheme: 'HTTP' }
              periodSeconds: 5
              timeoutSeconds: 5
              failureThreshold: 24
            }
            {
              type: 'Liveness'
              httpGet: { path: '/health/live', port: 8080, scheme: 'HTTP' }
              periodSeconds: 10
              timeoutSeconds: 5
              failureThreshold: 3
            }
            {
              type: 'Readiness'
              httpGet: { path: '/health/ready', port: 8080, scheme: 'HTTP' }
              periodSeconds: 5
              timeoutSeconds: 5
              failureThreshold: 6
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 1
      }
    }
  }
  dependsOn: [webRegistryPull, webTelemetryPublisher, webIntakeQueueSender]
}

resource functionPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${prefix}-worker-plan-${suffix}'
  location: location
  kind: 'functionapp'
  tags: tags
  sku: { name: 'FC1', tier: 'FlexConsumption' }
  properties: { reserved: true }
}

resource workerApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${prefix}-worker-${suffix}'
  location: location
  kind: 'functionapp,linux'
  tags: union(tags, { 'azd-service-name': 'worker' })
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${workerIdentity.id}': {} } }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    keyVaultReferenceIdentity: workerIdentity.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${transportStorage.properties.primaryEndpoints.blob}${packageContainer.name}'
          authentication: { type: 'UserAssignedIdentity', userAssignedIdentityResourceId: workerIdentity.id }
        }
      }
      runtime: { name: 'dotnet-isolated', version: '10.0' }
      scaleAndConcurrency: {
        maximumInstanceCount: 20
        instanceMemoryMB: 2048
        alwaysReady: [
          {
            name: 'function:UnifiedWorkFunction'
            instanceCount: 1
          }
        ]
      }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'Runtime__Profile', value: 'Production' }
        { name: 'AzureWebJobsStorage__accountName', value: transportStorage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: workerIdentity.properties.clientId }
        { name: 'AzureIdentity__WorkerClientId', value: workerIdentity.properties.clientId }
        { name: 'IntakeStorage__ServiceUri', value: custodyStorage.properties.primaryEndpoints.blob }
        { name: 'IntakeQueue__ServiceUri', value: transportStorage.properties.primaryEndpoints.queue }
        // Recovery only: every committing caller attempts exact-ID publication.
        { name: 'PendingWorkRecoverySchedule', value: '0 * * * * *' }
        { name: 'IntakeStagedArtifactReconciliationSchedule', value: '*/10 * * * * *' }
        { name: 'ApprovedInboxPollSchedule', value: '0 */5 * * * *' }
        { name: 'SentEvidencePollSchedule', value: '15 * * * * *' }
        { name: 'DueWorkSweepSchedule', value: '0 */5 * * * *' }
        { name: 'AzureWebJobs.PendingWorkRecoveryFunction.Disabled', value: workerActivationApproved ? 'false' : 'true' }
        { name: 'AzureWebJobs.UnifiedWorkFunction.Disabled', value: workerActivationApproved ? 'false' : 'true' }
        { name: 'AzureWebJobs.UnifiedWorkPoisonFunction.Disabled', value: workerActivationApproved ? 'false' : 'true' }
        { name: 'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled', value: workerActivationApproved ? 'false' : 'true' }
        { name: 'AzureWebJobs.InboxRecoveryFunction.Disabled', value: workerActivationApproved ? 'false' : 'true' }
        { name: 'AzureWebJobs.SentEvidencePollFunction.Disabled', value: workerActivationApproved ? 'false' : 'true' }
        { name: 'AzureWebJobs.DueWorkSweepFunction.Disabled', value: workerActivationApproved ? 'false' : 'true' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsights.properties.ConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'Authorization=AAD;ClientId=${workerIdentity.properties.clientId}' }
        { name: 'APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING', value: 'true' }
        { name: 'ConnectionStrings__Pegasus', value: workerSqlConnectionString }
        { name: 'KEY_VAULT_URI', value: keyVault.properties.vaultUri }
        { name: 'TransportStorage__AccountName', value: transportStorage.name }
        { name: 'CustodyStorage__AccountName', value: custodyStorage.name }
        { name: 'Graph__BaseUri', value: 'https://graph.microsoft.com/v1.0/' }
        { name: 'Graph__MailboxId', value: graphMailboxId }
        { name: 'Graph__MailboxAddress', value: 'instructions@collisionengineers.co.uk' }
        { name: 'Graph__InboxFolderId', value: graphInboxFolderId }
        { name: 'Graph__SentFolderId', value: graphSentFolderId }
        { name: 'Graph__TenantId', value: tenant().tenantId }
        { name: 'Graph__ChangeNotificationUrl', value: 'https://${prefix}-web-${suffix}.${containerEnvironment.properties.defaultDomain}/hooks/microsoft-graph/mail' }
        { name: 'Graph__ChangeNotificationClientState', value: '@Microsoft.KeyVault(SecretUri=${graphChangeNotificationClientStateSecretUri})' }
        { name: 'Box__BaseUri', value: 'https://api.box.com/2.0/' }
        { name: 'Box__UploadUri', value: 'https://upload.box.com/api/2.0/' }
        { name: 'Box__RootFolderId', value: '405543781910' }
        { name: 'Box__HoldingFolderId', value: boxHoldingFolderId }
        { name: 'Box__ConfigJson', value: '@Microsoft.KeyVault(SecretUri=${boxConfigJsonSecretUri})' }
        { name: 'Box__ClientSecret', value: '@Microsoft.KeyVault(SecretUri=${boxClientSecretSecretUri})' }
        { name: 'Dvla__BaseUri', value: 'https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/' }
        { name: 'Dvla__ApiKey', value: '@Microsoft.KeyVault(SecretUri=${dvlaApiKeySecretUri})' }
        { name: 'Dvsa__BaseUri', value: 'https://history.mot.api.gov.uk/v1/trade/vehicles/registration/' }
        { name: 'Dvsa__TokenUri', value: dvsaTokenUri }
        { name: 'Dvsa__ClientId', value: '@Microsoft.KeyVault(SecretUri=${dvsaClientIdSecretUri})' }
        { name: 'Dvsa__ClientSecret', value: '@Microsoft.KeyVault(SecretUri=${dvsaClientSecretSecretUri})' }
        { name: 'Eva__ClientId', value: '@Microsoft.KeyVault(SecretUri=${evaClientIdSecretUri})' }
        { name: 'Eva__ClientSecret', value: '@Microsoft.KeyVault(SecretUri=${evaClientSecretSecretUri})' }
        { name: 'Eva__BaseUri', value: evaBaseUri }
        { name: 'Eva__RequestFrom', value: evaRequestFrom }
        { name: 'Eva__InspectionType', value: evaInspectionType }
        { name: 'Eva__InstructionEmail', value: evaInstructionEmail }
        { name: 'Dvsa__ApiKey', value: '@Microsoft.KeyVault(SecretUri=${dvsaApiKeySecretUri})' }
        { name: 'Dvsa__Scope', value: dvsaScope }
      ]
    }
  }
  dependsOn: [
    workerTransportBlobOwner
    workerTransportQueueContributor
    workerTransportTableContributor
    workerTelemetryPublisher
  ]
}

resource webHttp5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = if (webActivationApproved) {
  name: '${prefix}-web-http5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'Pegasus production Web returned an HTTP 5xx response.'
    severity: 1
    enabled: true
    scopes: [webContainerApp.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    autoMitigate: true
    targetResourceType: 'Microsoft.App/containerApps'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'WebHttp5xx'
          metricNamespace: 'Microsoft.App/containerapps'
          metricName: 'Requests'
          operator: 'GreaterThan'
          timeAggregation: 'Total'
          threshold: 0
          criterionType: 'StaticThresholdCriterion'
          dimensions: [
            {
              name: 'StatusCodeCategory'
              operator: 'Include'
              values: ['5xx']
            }
          ]
        }
      ]
    }
    actions: [
      { actionGroupId: actionGroup.id }
    ]
  }
}

resource applicationExceptionAlert 'Microsoft.Insights/scheduledQueryRules@2023-12-01' = {
  name: '${prefix}-application-exceptions'
  location: location
  tags: tags
  properties: {
    displayName: 'Pegasus production application exceptions'
    description: 'One or more correlated Web or Worker exceptions were recorded.'
    severity: 1
    enabled: true
    evaluationFrequency: 'PT5M'
    scopes: [logAnalytics.id]
    targetResourceTypes: ['Microsoft.OperationalInsights/workspaces']
    windowSize: 'PT15M'
    criteria: {
      allOf: [
        {
          query: '''
            let CorrelatedExceptions = AppExceptions
              | where TimeGenerated > ago(15m)
              | extend ExceptionSignature = strcat(
                  coalesce(ExceptionType, 'UnknownException'),
                  '|',
                  substring(replace_regex(coalesce(OuterMessage, InnermostMessage, Message, ''), @'[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}', '<id>'), 0, 512)),
                  CorrelationId = tostring(OperationId)
              | where isnotempty(CorrelationId)
              | summarize LastSeen=max(TimeGenerated), ExceptionRows=count(), AppRole=take_any(AppRoleName)
                  by ExceptionSignature, CorrelationId;
            let FailedRecentOperations = AppRequests
              | where TimeGenerated > ago(5m) and Success == false
              | where isnotempty(OperationId)
              | summarize by CorrelationId=tostring(OperationId);
            let FailedRecent = CorrelatedExceptions
              | where LastSeen > ago(5m)
              | join kind=inner FailedRecentOperations on CorrelationId
              | project ExceptionSignature, AppRole, LastSeen, Reason='failed_operation';
            let PersistentCorrelated = CorrelatedExceptions
              | summarize DistinctOperations=dcount(CorrelationId), LastSeen=max(LastSeen), AppRole=take_any(AppRole)
                  by ExceptionSignature
              | where DistinctOperations >= 3
              | project ExceptionSignature, AppRole, LastSeen, Reason='repeated_operations';
            let PersistentUncorrelated = AppExceptions
              | where TimeGenerated > ago(15m) and isempty(OperationId)
              | extend ExceptionSignature = strcat(
                  coalesce(ExceptionType, 'UnknownException'),
                  '|',
                  substring(replace_regex(coalesce(OuterMessage, InnermostMessage, Message, ''), @'[0-9a-fA-F]{8}-[0-9a-fA-F-]{27,}', '<id>'), 0, 512)),
                  MinuteBucket=bin(TimeGenerated, 1m)
              | summarize AppRole=take_any(AppRoleName), LastSeen=max(TimeGenerated)
                  by ExceptionSignature, MinuteBucket
              | summarize DistinctMinuteBuckets=count(), LastSeen=max(LastSeen), AppRole=take_any(AppRole)
                  by ExceptionSignature
              | where DistinctMinuteBuckets >= 3
              | project ExceptionSignature, AppRole, LastSeen, Reason='repeated_uncorrelated';
            union FailedRecent, PersistentCorrelated, PersistentUncorrelated
              | summarize ExceptionCount=count()
            '''
          timeAggregation: 'Total'
          metricMeasureColumn: 'ExceptionCount'
          operator: 'GreaterThan'
          threshold: 0
          failingPeriods: {
            numberOfEvaluationPeriods: 1
            minFailingPeriodsToAlert: 1
          }
        }
      ]
    }
    autoMitigate: true
    actions: {
      actionGroups: [actionGroup.id]
    }
  }
}

output webContainerAppName string = webActivationApproved ? webContainerApp!.name : ''
output webContainerAppFqdn string = webActivationApproved ? webContainerApp!.properties.configuration.ingress.fqdn : ''
output webContainerAppRevision string = webActivationApproved ? '${webContainerApp!.name}--${webRevisionSuffix}' : ''
output containerRegistryName string = containerRegistry.name
output containerRegistryLoginServer string = containerRegistry.properties.loginServer
output webImageReference string = webActivationApproved ? webImageReference : ''
output webIdentityName string = webIdentity.name
output webIdentityClientId string = webIdentity.properties.clientId
output workerAppName string = workerApp.name
output workerIdentityName string = workerIdentity.name
output workerIdentityClientId string = workerIdentity.properties.clientId
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output transportStorageAccountName string = transportStorage.name
output custodyStorageAccountName string = custodyStorage.name
output keyVaultName string = keyVault.name
output applicationInsightsName string = applicationInsights.name
output logAnalyticsWorkspaceName string = logAnalytics.name
output actionGroupName string = actionGroup.name
