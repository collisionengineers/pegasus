targetScope = 'subscription'

@allowed([
  'dev'
  'prod'
])
@description('Deployment environment. Development and production use separate resource groups.')
param environmentName string

@description('Primary Azure region.')
param location string = 'uksouth'

@description('Object ID for the Microsoft Entra administrator of the Azure SQL logical server.')
param sqlAdministratorObjectId string

@description('Display name or UPN for the Microsoft Entra administrator of the Azure SQL logical server.')
param sqlAdministratorLogin string

@description('Deploy Document Intelligence only after the PDF benchmark and old F0 ownership decision.')
param documentIntelligenceEnabled bool = false

var resourceGroupName = 'rg-pegasus-${environmentName}'
var commonTags = {
  app: 'pegasus'
  environment: environmentName
  managedBy: 'azd-bicep'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = {
  name: resourceGroupName
  location: location
  tags: commonTags
}

module platform 'modules/platform.bicep' = {
  name: 'pegasus-${environmentName}'
  scope: resourceGroup
  params: {
    environmentName: environmentName
    location: location
    tags: commonTags
    sqlAdministratorObjectId: sqlAdministratorObjectId
    sqlAdministratorLogin: sqlAdministratorLogin
    documentIntelligenceEnabled: documentIntelligenceEnabled
  }
}

output AZURE_RESOURCE_GROUP string = resourceGroup.name
output AZURE_LOCATION string = location
output WEB_APP_NAME string = platform.outputs.webAppName
output WEB_APP_PRINCIPAL_ID string = platform.outputs.webAppPrincipalId
output WORKER_APP_NAME string = platform.outputs.workerAppName
output WORKER_PRINCIPAL_ID string = platform.outputs.workerPrincipalId
output WORKER_IDENTITY_NAME string = platform.outputs.workerIdentityName
output AZURE_SQL_SERVER_FQDN string = platform.outputs.sqlServerFqdn
output AZURE_SQL_DATABASE_NAME string = platform.outputs.sqlDatabaseName
output AZURE_STORAGE_ACCOUNT_NAME string = platform.outputs.storageAccountName
output AZURE_KEY_VAULT_NAME string = platform.outputs.keyVaultName
