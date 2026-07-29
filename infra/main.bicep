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
@allowed([
  'offline-replay'
])
@description('Fail-closed release mode. This revision permits only offline artifact replay validation and cannot provision Azure resources.')
param deploymentMode string = 'offline-replay'

var activationAllowed = deploymentMode == 'approved-live-deployment'


var resourceGroupName = 'rg-pegasus-${environmentName}'
var commonTags = {
  app: 'pegasus'
  environment: environmentName
  managedBy: 'azd-bicep'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = if (activationAllowed) {
  name: resourceGroupName
  location: location
  tags: commonTags
}

module platform 'modules/platform.bicep' = if (activationAllowed) {
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

output DEPLOYMENT_MODE string = deploymentMode
output AZURE_RESOURCE_GROUP string = activationAllowed ? resourceGroup.name : ''
output AZURE_LOCATION string = location
output WEB_APP_NAME string = activationAllowed ? platform.outputs.webAppName : ''
output WEB_IDENTITY_NAME string = activationAllowed ? platform.outputs.webIdentityName : ''
output WEB_IDENTITY_CLIENT_ID string = activationAllowed ? platform.outputs.webIdentityClientId : ''
output WEB_SQL_USER_NAME string = activationAllowed ? platform.outputs.webSqlUserName : ''
output WORKER_APP_NAME string = activationAllowed ? platform.outputs.workerAppName : ''
output WORKER_IDENTITY_NAME string = activationAllowed ? platform.outputs.workerIdentityName : ''
output WORKER_IDENTITY_CLIENT_ID string = activationAllowed ? platform.outputs.workerIdentityClientId : ''
output WORKER_SQL_USER_NAME string = activationAllowed ? platform.outputs.workerSqlUserName : ''
output AZURE_SQL_SERVER_FQDN string = activationAllowed ? platform.outputs.sqlServerFqdn : ''
output AZURE_SQL_DATABASE_NAME string = activationAllowed ? platform.outputs.sqlDatabaseName : ''
output AZURE_STORAGE_ACCOUNT_NAME string = activationAllowed ? platform.outputs.storageAccountName : ''
output AZURE_KEY_VAULT_NAME string = activationAllowed ? platform.outputs.keyVaultName : ''
