@allowed([
  'dev'
  'prod'
])
param environmentName string
param location string
param tags object
param sqlAdministratorObjectId string
param sqlAdministratorLogin string
param documentIntelligenceEnabled bool

var suffix = uniqueString(subscription().subscriptionId, resourceGroup().id, environmentName)
var prefix = 'pegasus-${environmentName}'
var storageName = environmentName == 'prod' ? 'pegasusprodst${take(suffix, 10)}' : 'pegasusdevst${take(suffix, 10)}'
var keyVaultName = environmentName == 'prod' ? 'pegasusprodkv${take(suffix, 8)}' : 'pegasusdevkv${take(suffix, 8)}'
var webPlanSkuName = environmentName == 'prod' ? 'B1' : 'F1'
var webPlanSkuTier = environmentName == 'prod' ? 'Basic' : 'Free'
var sqlSkuName = environmentName == 'prod' ? 'S0' : 'Basic'
var sqlSkuTier = environmentName == 'prod' ? 'Standard' : 'Basic'
var sqlCapacity = environmentName == 'prod' ? 10 : 5
var sqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Managed Identity;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'
var workerSqlConnectionString = 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Database=${sqlDatabase.name};Authentication=Active Directory Managed Identity;User Id=${workerIdentity.properties.clientId};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${prefix}-logs-${suffix}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
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
  }
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  tags: tags
  properties: {
    tenantId: tenant().tenantId
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enablePurgeProtection: true
    publicNetworkAccess: 'Enabled'
  }
}

resource storage 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageName
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Enabled'
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: storage
  name: 'default'
  properties: {
    deleteRetentionPolicy: {
      enabled: true
      days: 7
    }
  }
}

resource packageContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'app-package'
  properties: {
    publicAccess: 'None'
  }
}

resource intakeContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'intake-temporary'
  properties: {
    publicAccess: 'None'
  }
}

resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-05-01' = {
  parent: storage
  name: 'default'
}

resource intakeQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-05-01' = {
  parent: queueService
  name: 'intake-work'
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
  sku: {
    name: sqlSkuName
    tier: sqlSkuTier
    capacity: sqlCapacity
  }
  properties: {
    maxSizeBytes: environmentName == 'prod' ? 268435456000 : 2147483648
    zoneRedundant: false
  }
}

resource sqlAzureServicesFirewall 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAllWindowsAzureIps'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

resource webPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${prefix}-web-plan-${suffix}'
  location: location
  kind: 'linux'
  tags: tags
  sku: {
    name: webPlanSkuName
    tier: webPlanSkuTier
    capacity: 1
  }
  properties: {
    reserved: true
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${prefix}-web-${suffix}'
  location: location
  kind: 'app,linux'
  tags: union(tags, {
    'azd-service-name': 'web'
  })
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: webPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    siteConfig: {
      alwaysOn: environmentName == 'prod'
      ftpsState: 'Disabled'
      healthCheckPath: '/health/ready'
      http20Enabled: true
      linuxFxVersion: 'DOTNETCORE|10.0'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: environmentName == 'prod' ? 'Production' : 'Development'
        }
        {
          // ASP.NET Core maps the Linux-safe double underscore to
          // ConnectionStrings:Pegasus for GetConnectionString().
          name: 'ConnectionStrings__Pegasus'
          value: sqlConnectionString
        }
        {
          name: 'KEY_VAULT_URI'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storage.name
        }
        {
          name: 'SCM_DO_BUILD_DURING_DEPLOYMENT'
          value: 'true'
        }
      ]
    }
  }
}

resource workerIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${prefix}-worker-id-${suffix}'
  location: location
  tags: tags
}

resource workerBlobOwner 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, workerIdentity.id, 'Storage Blob Data Owner')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerQueueContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, workerIdentity.id, 'Storage Queue Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '974c5e8b-45b9-4653-ba55-5f855dd0fb88')
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerTableContributor 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storage.id, workerIdentity.id, 'Storage Table Data Contributor')
  scope: storage
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource functionPlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: '${prefix}-worker-plan-${suffix}'
  location: location
  kind: 'functionapp'
  tags: tags
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true
  }
}

resource workerApp 'Microsoft.Web/sites@2024-04-01' = {
  name: '${prefix}-worker-${suffix}'
  location: location
  kind: 'functionapp,linux'
  tags: union(tags, {
    'azd-service-name': 'worker'
  })
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${workerIdentity.id}': {}
    }
  }
  properties: {
    serverFarmId: functionPlan.id
    httpsOnly: true
    publicNetworkAccess: 'Enabled'
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${storage.properties.primaryEndpoints.blob}${packageContainer.name}'
          authentication: {
            type: 'UserAssignedIdentity'
            userAssignedIdentityResourceId: workerIdentity.id
          }
        }
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '10.0'
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 20
        instanceMemoryMB: 2048
      }
    }
    siteConfig: {
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'FUNCTIONS_WORKER_RUNTIME'
          value: 'dotnet-isolated'
        }
        {
          name: 'AzureWebJobsStorage__accountName'
          value: storage.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'AzureWebJobsStorage__clientId'
          value: workerIdentity.properties.clientId
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsights.properties.ConnectionString
        }
        {
          name: 'AZURE_SQL_CONNECTION_STRING'
          value: workerSqlConnectionString
        }
        {
          name: 'KEY_VAULT_URI'
          value: keyVault.properties.vaultUri
        }
        {
          name: 'STORAGE_ACCOUNT_NAME'
          value: storage.name
        }
      ]
    }
  }
  dependsOn: [
    workerBlobOwner
    workerQueueContributor
    workerTableContributor
  ]
}

resource webKeyVaultReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webApp.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource workerKeyVaultReader 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, workerIdentity.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6')
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

resource documentIntelligence 'Microsoft.CognitiveServices/accounts@2024-10-01' = if (documentIntelligenceEnabled) {
  name: '${prefix}-docintel-${suffix}'
  location: location
  kind: 'FormRecognizer'
  tags: tags
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: '${prefix}-docintel-${suffix}'
    disableLocalAuth: true
    publicNetworkAccess: 'Enabled'
  }
}

resource workerDocumentIntelligenceUser 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (documentIntelligenceEnabled) {
  name: guid(documentIntelligence.id, workerIdentity.id, 'Cognitive Services User')
  scope: documentIntelligence
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908')
    principalId: workerIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

output webAppName string = webApp.name
output webAppPrincipalId string = webApp.identity.principalId
output workerAppName string = workerApp.name
output workerPrincipalId string = workerIdentity.properties.principalId
output workerIdentityName string = workerIdentity.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabase.name
output storageAccountName string = storage.name
output keyVaultName string = keyVault.name
