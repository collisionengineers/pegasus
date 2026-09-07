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

@allowed([
  'disabled'
  'approved'
])
@description('Fail-closed Web activation. Base provisioning leaves the public Container App absent.')
param webActivation string = 'disabled'

@description('Fail-closed Worker activation. Only approved-live-worker enables the nine production functions.')
param workerActivation string = 'disabled'

@description('Exact sha256 OCI manifest digest. Required only when Web activation is approved; the registry and repository are template-owned.')
param webImageDigest string = ''

@description('Twelve-character source revision suffix for the immutable Container App revision.')
param webRevisionSuffix string = ''

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
@description('Versioned Key Vault secret URI containing the Microsoft Graph notification clientState.')
param graphChangeNotificationClientStateSecretUri string
@description('Versioned Key Vault secret URI containing the Box JWT configuration JSON.')
param boxConfigJsonSecretUri string
@description('Versioned Key Vault secret URI containing the Box client secret.')
param boxClientSecretSecretUri string
@description('Operator-created Box holding folder below the approved Pegasus root.')
@minLength(1)
param boxHoldingFolderId string
@description('Versioned Key Vault secret URI containing the Automation MCP OAuth client secret.')
param automationMcpClientSecretUri string
@description('Comma-separated versioned Key Vault secret URIs for current and retained OAuth signing certificates (passwordless PFX).')
@minLength(1)
param automationMcpSigningCertificateSecretUris string
@description('Comma-separated versioned Key Vault secret URIs for current and retained OAuth encryption certificates (passwordless PFX).')
@minLength(1)
param automationMcpEncryptionCertificateSecretUris string
@description('Exact redirect URIs (comma separated) of the external MCP connectors allowed to use the authorization-code flow; empty disables that flow.')
param automationMcpRedirectUris string = ''
@description('Versioned Key Vault secret URI containing the DVLA VES API key.')
param dvlaApiKeySecretUri string
@description('Versioned Key Vault secret URI containing the DVSA OAuth client ID.')
param dvsaClientIdSecretUri string
@description('Versioned Key Vault secret URI containing the DVSA OAuth client secret.')
param dvsaClientSecretSecretUri string
@description('Versioned Key Vault secret URI containing the EVA API client ID. The credential pair alone decides whether this is EVA test or live.')
param evaClientIdSecretUri string
@description('Versioned Key Vault secret URI containing the EVA API client secret.')
param evaClientSecretSecretUri string
@description('Approved EVA Sentry API base URI, serving both test and live.')
param evaBaseUri string
@description('EVA contact code this deployment submits instructions as.')
param evaRequestFrom string
@description('EVA inspection type sent on every instruction.')
param evaInspectionType string
@description('Instruction contact address sent to EVA.')
param evaInstructionEmail string
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
    webActivation: webActivation
    workerActivation: workerActivation
    webImageDigest: webImageDigest
    webRevisionSuffix: webRevisionSuffix
    graphMailboxId: graphMailboxId
    graphInboxFolderId: graphInboxFolderId
    graphSentFolderId: graphSentFolderId
    graphChangeNotificationClientStateSecretUri: graphChangeNotificationClientStateSecretUri
    boxConfigJsonSecretUri: boxConfigJsonSecretUri
    boxHoldingFolderId: boxHoldingFolderId
    boxClientSecretSecretUri: boxClientSecretSecretUri
    automationMcpClientSecretUri: automationMcpClientSecretUri
    automationMcpSigningCertificateSecretUris: automationMcpSigningCertificateSecretUris
    automationMcpEncryptionCertificateSecretUris: automationMcpEncryptionCertificateSecretUris
    automationMcpRedirectUris: automationMcpRedirectUris
    dvlaApiKeySecretUri: dvlaApiKeySecretUri
    dvsaClientIdSecretUri: dvsaClientIdSecretUri
    dvsaClientSecretSecretUri: dvsaClientSecretSecretUri
    evaClientIdSecretUri: evaClientIdSecretUri
    evaClientSecretSecretUri: evaClientSecretSecretUri
    evaBaseUri: evaBaseUri
    evaRequestFrom: evaRequestFrom
    evaInspectionType: evaInspectionType
    evaInstructionEmail: evaInstructionEmail
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
output WEB_CONTAINER_APP_NAME string = activationAllowed ? platform!.outputs.webContainerAppName : ''
output WEB_CONTAINER_APP_FQDN string = activationAllowed ? platform!.outputs.webContainerAppFqdn : ''
output WEB_CONTAINER_APP_REVISION string = activationAllowed ? platform!.outputs.webContainerAppRevision : ''
output CONTAINER_REGISTRY_NAME string = activationAllowed ? platform!.outputs.containerRegistryName : ''
output CONTAINER_REGISTRY_LOGIN_SERVER string = activationAllowed ? platform!.outputs.containerRegistryLoginServer : ''
output WEB_IMAGE_REFERENCE string = activationAllowed ? platform!.outputs.webImageReference : ''
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
