targetScope = 'subscription'

@allowed([
  'prod'
])
@description('The only Azure target. Local development is not represented in Azure.')
param environmentName string = 'prod'

@allowed([
  'approved-live-deployment'
])
@description('Explicit fail-closed release mode required for a production preview or provision.')
param deploymentMode string

@description('Primary Azure region.')
param location string = 'uksouth'

@description('Object ID for the Microsoft Entra administrator of the Azure SQL logical server.')
param sqlAdministratorObjectId string

@description('Display name or UPN for the Microsoft Entra administrator of the Azure SQL logical server.')
param sqlAdministratorLogin string

@description('Email address for production platform and budget notifications.')
param alertEmailAddress string

@description('Exact Microsoft Graph mailbox object ID for instructions@collisionengineers.co.uk.')
param graphMailboxId string
@description('Exact immutable Microsoft Graph Inbox folder ID.')
param graphInboxFolderId string
@description('Exact immutable Microsoft Graph Sent Items folder ID.')
param graphSentFolderId string
@description('Versioned Key Vault secret URI containing the Box access credential.')
param boxAccessTokenSecretUri string
@description('Versioned Key Vault secret URI containing the DVLA VES API key.')
param dvlaApiKeySecretUri string
@description('Versioned Key Vault secret URI containing the DVSA OAuth client ID.')
param dvsaClientIdSecretUri string
@description('Versioned Key Vault secret URI containing the DVSA OAuth client secret.')
param dvsaClientSecretSecretUri string
@description('Versioned Key Vault secret URI containing the DVSA API key.')
param dvsaApiKeySecretUri string
@description('Approved DVSA OAuth token endpoint.')
param dvsaTokenUri string
@description('Approved DVSA OAuth scope.')
param dvsaScope string

var activationAllowed = environmentName == 'prod' && deploymentMode == 'approved-live-deployment'
var resourceGroupName = 'rg-pegasus-prod'
var commonTags = {
  app: 'pegasus'
  environment: 'prod'
  managedBy: 'azd-bicep'
  release: '0.1.0-alpha.1'
}

resource resourceGroup 'Microsoft.Resources/resourceGroups@2024-11-01' = if (activationAllowed) {
  name: resourceGroupName
  location: location
  tags: commonTags
}

module platform 'modules/platform.bicep' = if (activationAllowed) {
  name: 'pegasus-prod'
  scope: resourceGroup
  params: {
    location: location
    tags: commonTags
    sqlAdministratorObjectId: sqlAdministratorObjectId
    sqlAdministratorLogin: sqlAdministratorLogin
    alertEmailAddress: alertEmailAddress
    graphMailboxId: graphMailboxId
    graphInboxFolderId: graphInboxFolderId
    graphSentFolderId: graphSentFolderId
    boxAccessTokenSecretUri: boxAccessTokenSecretUri
    dvlaApiKeySecretUri: dvlaApiKeySecretUri
    dvsaClientIdSecretUri: dvsaClientIdSecretUri
    dvsaClientSecretSecretUri: dvsaClientSecretSecretUri
    dvsaApiKeySecretUri: dvsaApiKeySecretUri
    dvsaTokenUri: dvsaTokenUri
    dvsaScope: dvsaScope
  }
}

resource productionBudget 'Microsoft.Consumption/budgets@2023-11-01' = if (activationAllowed) {
  name: 'pegasus-prod-monthly'
  properties: {
    amount: 75
    category: 'Cost'
    timeGrain: 'Monthly'
    timePeriod: {
      startDate: '2026-08-01T00:00:00Z'
      endDate: '2036-08-01T00:00:00Z'
    }
    filter: {
      dimensions: {
        name: 'ResourceGroupName'
        operator: 'In'
        values: [
          resourceGroupName
        ]
      }
    }
    notifications: {
      Actual50: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 50
        thresholdType: 'Actual'
        contactEmails: [alertEmailAddress]
        contactGroups: []
        contactRoles: []
      }
      Actual80: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 80
        thresholdType: 'Actual'
        contactEmails: [alertEmailAddress]
        contactGroups: []
        contactRoles: []
      }
      Actual100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Actual'
        contactEmails: [alertEmailAddress]
        contactGroups: []
        contactRoles: []
      }
      Forecast100: {
        enabled: true
        operator: 'GreaterThan'
        threshold: 100
        thresholdType: 'Forecasted'
        contactEmails: [alertEmailAddress]
        contactGroups: []
        contactRoles: []
      }
    }
  }
}

output DEPLOYMENT_MODE string = deploymentMode
output AZURE_RESOURCE_GROUP string = activationAllowed ? resourceGroup!.name : ''
output AZURE_LOCATION string = location
output WEB_APP_NAME string = activationAllowed ? platform!.outputs.webAppName : ''
output WEB_IDENTITY_NAME string = activationAllowed ? platform!.outputs.webIdentityName : ''
output WEB_IDENTITY_CLIENT_ID string = activationAllowed ? platform!.outputs.webIdentityClientId : ''
output WORKER_APP_NAME string = activationAllowed ? platform!.outputs.workerAppName : ''
output WORKER_IDENTITY_NAME string = activationAllowed ? platform!.outputs.workerIdentityName : ''
output WORKER_IDENTITY_CLIENT_ID string = activationAllowed ? platform!.outputs.workerIdentityClientId : ''
output AZURE_SQL_SERVER_FQDN string = activationAllowed ? platform!.outputs.sqlServerFqdn : ''
output AZURE_SQL_DATABASE_NAME string = activationAllowed ? platform!.outputs.sqlDatabaseName : ''
output TRANSPORT_STORAGE_ACCOUNT_NAME string = activationAllowed ? platform!.outputs.transportStorageAccountName : ''
output CUSTODY_STORAGE_ACCOUNT_NAME string = activationAllowed ? platform!.outputs.custodyStorageAccountName : ''
output AZURE_KEY_VAULT_NAME string = activationAllowed ? platform!.outputs.keyVaultName : ''
output APPLICATION_INSIGHTS_NAME string = activationAllowed ? platform!.outputs.applicationInsightsName : ''
output LOG_ANALYTICS_WORKSPACE_NAME string = activationAllowed ? platform!.outputs.logAnalyticsWorkspaceName : ''
output ACTION_GROUP_NAME string = activationAllowed ? platform!.outputs.actionGroupName : ''
