param location string
param tags object
param sqlAdministratorObjectId string
param sqlAdministratorLogin string
param alertEmailAddress string
param graphMailboxId string
param graphInboxFolderId string
param graphSentFolderId string
param boxConfigJsonSecretUri string
param boxClientSecretSecretUri string
param dvlaApiKeySecretUri string
param dvsaClientIdSecretUri string
param dvsaClientSecretSecretUri string
param dvsaApiKeySecretUri string
param dvsaTokenUri string
param dvsaScope string

var suffix = take(uniqueString(subscription().subscriptionId, resourceGroup().id, 'prod'), 10)
var prefix = 'pegasus-prod'
var transportStorageName = 'pegtrans${suffix}'
var custodyStorageName = 'pegcustody${suffix}'
var keyVaultName = 'pegasusprodkv${take(suffix, 8)}'
var blobDataOwnerRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var blobDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
var queueDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
var queueMessageSenderRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
var tableDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var webSqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Managed Identity;User Id=${webIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var workerSqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Managed Identity;User Id=${workerIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 31
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

resource externalQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: transportQueueService
  name: 'external-work'
}

resource externalPoisonQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: transportQueueService
  name: 'external-work-poison'
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
  name: 'AllowAllWindowsAzureIps'
  properties: { startIpAddress: '0.0.0.0', endIpAddress: '0.0.0.0' }
}

resource webPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${prefix}-web-plan-${suffix}'
  location: location
  kind: 'linux'
  tags: tags
  sku: { name: 'B1', tier: 'Basic', capacity: 1 }
  properties: { reserved: true }
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

resource workerTransientCustodyContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transientIntakeContainer.id, workerIdentity.id, blobDataContributorRole)
  scope: transientIntakeContainer
  properties: { roleDefinitionId: blobDataContributorRole, principalId: workerIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource workerBoxLinkContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(boxLinkContainer.id, workerIdentity.id, blobDataContributorRole)
  scope: boxLinkContainer
  properties: { roleDefinitionId: blobDataContributorRole, principalId: workerIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource webTransientCustodyContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(transientIntakeContainer.id, webIdentity.id, blobDataContributorRole)
  scope: transientIntakeContainer
  properties: { roleDefinitionId: blobDataContributorRole, principalId: webIdentity.properties.principalId, principalType: 'ServicePrincipal' }
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

resource webIntakeQueueSender 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(intakeQueue.id, webIdentity.id, queueMessageSenderRole)
  scope: intakeQueue
  properties: { roleDefinitionId: queueMessageSenderRole, principalId: webIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${prefix}-web-${suffix}'
  location: location
  kind: 'app,linux'
  tags: union(tags, { 'azd-service-name': 'web' })
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${webIdentity.id}': {} } }
  properties: {
    serverFarmId: webPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      alwaysOn: true
      ftpsState: 'Disabled'
      healthCheckPath: '/health/ready'
      http20Enabled: true
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsights.properties.ConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'Authorization=AAD;ClientId=${webIdentity.properties.clientId}' }
        { name: 'ApplicationInsightsAgent_EXTENSION_VERSION', value: '~3' }
        { name: 'APPLICATIONINSIGHTS_ENABLEADAPTIVESAMPLING', value: 'true' }
        { name: 'ASPNETCORE_ENVIRONMENT', value: 'Production' }
        { name: 'Runtime__Profile', value: 'Production' }
        { name: 'ConnectionStrings__Pegasus', value: webSqlConnectionString }
        { name: 'KEY_VAULT_URI', value: keyVault.properties.vaultUri }
        { name: 'TransportStorage__AccountName', value: transportStorage.name }
        { name: 'CustodyStorage__AccountName', value: custodyStorage.name }
        { name: 'CustodyStorage__ServiceUri', value: custodyStorage.properties.primaryEndpoints.blob }
        { name: 'AZURE_CLIENT_ID', value: webIdentity.properties.clientId }
        { name: 'AzureIdentity__WebClientId', value: webIdentity.properties.clientId }
      ]
    }
  }
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
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${transportStorage.properties.primaryEndpoints.blob}${packageContainer.name}'
          authentication: { type: 'UserAssignedIdentity', userAssignedIdentityResourceId: workerIdentity.id }
        }
      }
      runtime: { name: 'dotnet-isolated', version: '10.0' }
      scaleAndConcurrency: { maximumInstanceCount: 20, instanceMemoryMB: 2048 }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'Runtime__Profile', value: 'Production' }
        { name: 'FUNCTIONS_WORKER_RUNTIME', value: 'dotnet-isolated' }
        { name: 'AzureWebJobsStorage__accountName', value: transportStorage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: workerIdentity.properties.clientId }
        { name: 'AzureIdentity__WorkerClientId', value: workerIdentity.properties.clientId }
        { name: 'IntakeStorage__ServiceUri', value: custodyStorage.properties.primaryEndpoints.blob }
        { name: 'IntakeQueue__ServiceUri', value: transportStorage.properties.primaryEndpoints.queue }
        { name: 'ExternalWorkQueue__ServiceUri', value: transportStorage.properties.primaryEndpoints.queue }
        { name: 'PendingWorkDispatchSchedule', value: '0 * * * * *' }
        { name: 'IntakeStagedArtifactReconciliationSchedule', value: '30 * * * * *' }
        { name: 'ApprovedInboxPollSchedule', value: '45 * * * * *' }
        { name: 'SentEvidencePollSchedule', value: '15 * * * * *' }
        { name: 'DueWorkSweepSchedule', value: '0 */5 * * * *' }
        { name: 'AzureWebJobs.PendingWorkDispatchFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.IntakeWorkFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.IntakePoisonFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.InboxPollFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.SentEvidencePollFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.DueWorkSweepFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.ExternalWorkFunction.Disabled', value: 'true' }
        { name: 'AzureWebJobs.ExternalPoisonFunction.Disabled', value: 'true' }
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
        { name: 'Box__BaseUri', value: 'https://api.box.com/2.0/' }
        { name: 'Box__UploadUri', value: 'https://upload.box.com/api/2.0/' }
        { name: 'Box__RootFolderId', value: '392761581105' }
        { name: 'Box__ConfigJson', value: '@Microsoft.KeyVault(SecretUri=${boxConfigJsonSecretUri})' }
        { name: 'Box__ClientSecret', value: '@Microsoft.KeyVault(SecretUri=${boxClientSecretSecretUri})' }
        { name: 'Dvla__BaseUri', value: 'https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/' }
        { name: 'Dvla__ApiKey', value: '@Microsoft.KeyVault(SecretUri=${dvlaApiKeySecretUri})' }
        { name: 'Dvsa__BaseUri', value: 'https://history.mot.api.gov.uk/v1/trade/vehicles/registration/' }
        { name: 'Dvsa__TokenUri', value: dvsaTokenUri }
        { name: 'Dvsa__ClientId', value: '@Microsoft.KeyVault(SecretUri=${dvsaClientIdSecretUri})' }
        { name: 'Dvsa__ClientSecret', value: '@Microsoft.KeyVault(SecretUri=${dvsaClientSecretSecretUri})' }
        { name: 'Dvsa__ApiKey', value: '@Microsoft.KeyVault(SecretUri=${dvsaApiKeySecretUri})' }
        { name: 'Dvsa__Scope', value: dvsaScope }
      ]
    }
  }
  dependsOn: [
    workerTransportBlobOwner
    workerTransportQueueContributor
    workerTransportTableContributor
  ]
}

resource webHttp5xxAlert 'Microsoft.Insights/metricAlerts@2018-03-01' = {
  name: '${prefix}-web-http5xx'
  location: 'global'
  tags: tags
  properties: {
    description: 'Pegasus production Web returned an HTTP 5xx response.'
    severity: 1
    enabled: true
    scopes: [webApp.id]
    evaluationFrequency: 'PT5M'
    windowSize: 'PT5M'
    autoMitigate: true
    targetResourceType: 'Microsoft.Web/sites'
    targetResourceRegion: location
    criteria: {
      'odata.type': 'Microsoft.Azure.Monitor.SingleResourceMultipleMetricCriteria'
      allOf: [
        {
          name: 'WebHttp5xx'
          metricNamespace: 'Microsoft.Web/sites'
          metricName: 'Http5xx'
          operator: 'GreaterThan'
          timeAggregation: 'Total'
          threshold: 0
          criterionType: 'StaticThresholdCriterion'
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
    windowSize: 'PT5M'
    criteria: {
      allOf: [
        {
          query: 'AppExceptions | where TimeGenerated > ago(5m) | summarize ExceptionCount=count()'
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

output webAppName string = webApp.name
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
