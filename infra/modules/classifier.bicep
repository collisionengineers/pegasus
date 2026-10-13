// The mail classifier agent: a TypeScript Flex Consumption Function App that
// calls the Pegasus Automation MCP server on the Web App. It is its own module
// because Flex Consumption allows one app per plan and platform.bicep's Worker
// settings are a fixed census (WorkerTimerContractTests).
param location string
param tags object
@allowed([
  'disabled'
  'approved'
])
param classifierActivation string
@description('The Web App public origin, ending in a slash; the MCP endpoint and token endpoint are under it.')
param webPublicOrigin string
param applicationInsightsName string
param automationMcpClientSecretUri string
param anthropicApiKeySecretUri string

// The same suffix platform.bicep derives in this resource group.
var suffix = take(uniqueString(subscription().subscriptionId, resourceGroup().id, 'prod'), 10)
var prefix = 'pegasus-prod'
var classifierStorageName = 'pegclass${suffix}'
var classifierActivationApproved = classifierActivation == 'approved'
var blobDataOwnerRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
var tableDataContributorRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
var monitoringMetricsPublisherRole = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '3913510d-42f4-4e42-8a64-420c390055eb')

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' existing = {
  name: applicationInsightsName
}

// The identity exists before activation so its exact-secret Key Vault grants
// can be made by hand before the app first resolves its references.
resource classifierIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-classifier-id-${suffix}'
  location: location
  tags: tags
}

resource classifierStorage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: classifierStorageName
  location: location
  tags: union(tags, { purpose: 'classifier-host' })
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

resource classifierBlobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: classifierStorage
  name: 'default'
  properties: { deleteRetentionPolicy: { enabled: true, days: 7 } }
}

resource classifierPackageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: classifierBlobService
  name: 'app-package'
  properties: { publicAccess: 'None' }
}

resource classifierTableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: classifierStorage
  name: 'default'
}

resource reviewedTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-05-01' = {
  parent: classifierTableService
  name: 'reviewed'
}

resource classifierTelemetryPublisher 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(applicationInsights.id, classifierIdentity.id, monitoringMetricsPublisherRole)
  scope: applicationInsights
  properties: {
    roleDefinitionId: monitoringMetricsPublisherRole
    principalId: classifierIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Host storage (timer locks, deployment package) and the reviewed table. The
// classifier has no queue trigger, so it holds no queue role.
resource classifierStorageBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(classifierStorage.id, classifierIdentity.id, blobDataOwnerRole)
  scope: classifierStorage
  properties: { roleDefinitionId: blobDataOwnerRole, principalId: classifierIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource classifierStorageTableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(classifierStorage.id, classifierIdentity.id, tableDataContributorRole)
  scope: classifierStorage
  properties: { roleDefinitionId: tableDataContributorRole, principalId: classifierIdentity.properties.principalId, principalType: 'ServicePrincipal' }
}

resource classifierPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${prefix}-classifier-plan-${suffix}'
  location: location
  kind: 'functionapp'
  tags: tags
  sku: { name: 'FC1', tier: 'FlexConsumption' }
  properties: { reserved: true }
}

resource classifierApp 'Microsoft.Web/sites@2024-04-01' = if (classifierActivationApproved) {
  name: '${prefix}-classifier-${suffix}'
  location: location
  kind: 'functionapp,linux'
  tags: union(tags, { 'azd-service-name': 'classifier' })
  identity: { type: 'UserAssigned', userAssignedIdentities: { '${classifierIdentity.id}': {} } }
  properties: {
    serverFarmId: classifierPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    keyVaultReferenceIdentity: classifierIdentity.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${classifierStorage.properties.primaryEndpoints.blob}${classifierPackageContainer.name}'
          authentication: { type: 'UserAssignedIdentity', userAssignedIdentityResourceId: classifierIdentity.id }
        }
      }
      runtime: { name: 'node', version: '22' }
      scaleAndConcurrency: {
        // One timer function; a single instance is all it ever needs.
        maximumInstanceCount: 1
        instanceMemoryMB: 2048
      }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: classifierStorage.name }
        { name: 'AzureWebJobsStorage__credential', value: 'managedidentity' }
        { name: 'AzureWebJobsStorage__clientId', value: classifierIdentity.properties.clientId }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: applicationInsights.properties.ConnectionString }
        { name: 'APPLICATIONINSIGHTS_AUTHENTICATION_STRING', value: 'Authorization=AAD;ClientId=${classifierIdentity.properties.clientId}' }
        { name: 'AZURE_CLIENT_ID', value: classifierIdentity.properties.clientId }
        { name: 'CLASSIFIER_STORAGE_ACCOUNT', value: classifierStorage.name }
        { name: 'PEGASUS_MCP_URL', value: '${webPublicOrigin}mcp' }
        { name: 'PEGASUS_TOKEN_URL', value: '${webPublicOrigin}connect/token' }
        { name: 'PEGASUS_CLIENT_ID', value: 'pegasus-automation' }
        { name: 'PEGASUS_CLIENT_SECRET', value: '@Microsoft.KeyVault(SecretUri=${automationMcpClientSecretUri})' }
        { name: 'ANTHROPIC_API_KEY', value: '@Microsoft.KeyVault(SecretUri=${anthropicApiKeySecretUri})' }
        { name: 'CLASSIFIER_MODEL', value: 'claude-sonnet-5-5' }
        { name: 'CLASSIFIER_CRON', value: '0 */15 * * * *' }
        { name: 'CLASSIFIER_DRY_RUN', value: 'false' }
      ]
    }
  }
  dependsOn: [
    classifierStorageBlobOwner
    classifierStorageTableContributor
    classifierTelemetryPublisher
    reviewedTable
  ]
}

// Kudu and FTP basic publishing credentials stay off; config-zip uses the
// caller's Entra token.
resource classifierAppScmBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = if (classifierActivationApproved) {
  parent: classifierApp
  name: 'scm'
  properties: { allow: false }
}

resource classifierAppFtpBasicAuth 'Microsoft.Web/sites/basicPublishingCredentialsPolicies@2024-04-01' = if (classifierActivationApproved) {
  parent: classifierApp
  name: 'ftp'
  properties: { allow: false }
}

output classifierAppName string = classifierActivationApproved ? classifierApp!.name : ''
output classifierIdentityName string = classifierIdentity.name
output classifierIdentityPrincipalId string = classifierIdentity.properties.principalId
output classifierIdentityClientId string = classifierIdentity.properties.clientId
output classifierStorageAccountName string = classifierStorage.name
