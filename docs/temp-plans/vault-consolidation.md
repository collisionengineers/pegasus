# Vault consolidation

## Purpose and boundary

Move only the six live Box, DVLA, and DVSA secret references from the two
adopted predecessor vaults into the Pegasus production Key Vault. Repoint the
production Worker and Web to versioned target-vault URIs, prove configuration
resolution, then retire only those two vaults and their now-empty predecessor
resource group.

This plan is **Planned**. No Azure command below has been run. It does not
authorise a deployment, a live provider call, an Azure read, a secret-value
retrieval, an access broadening, a vault purge, or a deletion. Before each
external phase, the operator must approve the exact subscription, resource
IDs, identities, secret names, cost scope, and command group produced by the
preceding read-only phase. Stop on any drift.

The immutable `.azure/deployment-plan.md` remains the executed 2026-08-02
release record. This task plan is the repository-required transient plan; it
does not replace that record.

## Expected end state

- `rg-pegasus-prod` contains the one Pegasus Key Vault and no new resource.
- The Worker references versioned target-vault URIs for `Box__ConfigJson`,
  `Box__ClientSecret`, `Dvla__ApiKey`, `Dvsa__ClientId`,
  `Dvsa__ClientSecret`, and `Dvsa__ApiKey` through its assigned identity.
- The Web's `box-config-json` and `box-client-secret` Container Apps secrets
  refer to the corresponding versioned target-vault URIs through its assigned
  identity.
- `cespkboxkvv76a47`, `cespkenrichkvgi62sd`, and then
  `rg-collisionspike-dev` are soft-deleted only after independent readback
  proves that no live Pegasus reference points to either predecessor vault.
- No secret value is printed, put into a command line, stored in the
  repository, or included in telemetry. The Key Vault backup blobs are kept
  outside the repository and removed only after the approved retirement
  succeeds.

## Exact command sequence

Run the following in PowerShell 7 from an approved operator terminal. The
variables deliberately derive mutable resource names from the current Azure
inventory and then fail closed if an expected singleton is not found.

### 1. Read-only target preflight

```powershell
$ErrorActionPreference = 'Stop'
$PSNativeCommandArgumentPassing = 'Standard'
$PegasusSubscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$PegasusProductionResourceGroup = 'rg-pegasus-prod'
$PredecessorResourceGroup = 'rg-collisionspike-dev'
$SourceVaultNames = @('cespkboxkvv76a47', 'cespkenrichkvgi62sd')
$WorkerSettingNames = @(
  'Box__ConfigJson',
  'Box__ClientSecret',
  'Dvla__ApiKey',
  'Dvsa__ClientId',
  'Dvsa__ClientSecret',
  'Dvsa__ApiKey'
)
$WebSecretNames = @('box-config-json', 'box-client-secret')
$KeyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

az account set --subscription $PegasusSubscription
$Account = az account show --output json | ConvertFrom-Json
if ($Account.id -ne $PegasusSubscription -or $Account.tenantId -ne
    '858cf5b3-aa0a-47a6-9b40-4851fd0afa94') {
  throw 'Unexpected Azure subscription or tenant.'
}

$TargetVaults = @(az keyvault list `
  --resource-group $PegasusProductionResourceGroup `
  --query "[?tags.app=='pegasus'].{name:name,id:id,uri:properties.vaultUri}" `
  --output json | ConvertFrom-Json)
if ($TargetVaults.Count -ne 1) {
  throw 'Expected exactly one Pegasus production Key Vault.'
}
$TargetVault = $TargetVaults[0]

$WorkerApps = @(az functionapp list `
  --resource-group $PegasusProductionResourceGroup `
  --query '[?tags."azd-service-name"==''worker''].{name:name,id:id,state:state}' `
  --output json | ConvertFrom-Json)
$WebApps = @(az containerapp list `
  --resource-group $PegasusProductionResourceGroup `
  --query '[?tags."azd-service-name"==''web''].{name:name,id:id}' `
  --output json | ConvertFrom-Json)
if ($WorkerApps.Count -ne 1 -or $WebApps.Count -ne 1) {
  throw 'Expected exactly one tagged Worker and one tagged Web application.'
}
$Worker = $WorkerApps[0]
$Web = $WebApps[0]

$WorkerIdentity = az functionapp identity show `
  --resource-group $PegasusProductionResourceGroup --name $Worker.name `
  --output json | ConvertFrom-Json
$WebIdentity = az containerapp identity show `
  --resource-group $PegasusProductionResourceGroup --name $Web.name `
  --output json | ConvertFrom-Json
$WorkerIdentityIds = @($WorkerIdentity.userAssignedIdentities.PSObject.Properties.Name)
$WebIdentityIds = @($WebIdentity.userAssignedIdentities.PSObject.Properties.Name)
if ($WorkerIdentityIds.Count -ne 1 -or $WebIdentityIds.Count -ne 1) {
  throw 'Each runtime must have exactly one assigned user-managed identity.'
}
$WorkerIdentityResourceId = $WorkerIdentityIds[0]
$WebIdentityResourceId = $WebIdentityIds[0]
$WorkerPrincipalId = $WorkerIdentity.userAssignedIdentities.PSObject.Properties[$WorkerIdentityResourceId].Value.principalId
$WebPrincipalId = $WebIdentity.userAssignedIdentities.PSObject.Properties[$WebIdentityResourceId].Value.principalId
if ([string]::IsNullOrWhiteSpace($WorkerPrincipalId) -or
    [string]::IsNullOrWhiteSpace($WebPrincipalId)) {
  throw 'A runtime managed-identity principal ID is missing.'
}

$WorkerKeyVaultReferenceIdentity = az resource show --ids $Worker.id `
  --api-version '2024-04-01' `
  --query 'properties.keyVaultReferenceIdentity' --output tsv
if ($WorkerKeyVaultReferenceIdentity -ne $WorkerIdentityResourceId) {
  throw 'Worker Key Vault reference identity is not its assigned identity.'
}

$SourceVaults = @($SourceVaultNames | ForEach-Object {
  az keyvault show --name $_ `
    --query '{name:name,id:id,location:location,rbac:properties.enableRbacAuthorization}' `
    --output json | ConvertFrom-Json
})
if (($SourceVaults | Where-Object { -not $_.rbac }).Count -ne 0) {
  throw 'A source vault is not RBAC-authorised; stop rather than change policy.'
}
$TargetVaultProperties = az keyvault show --name $TargetVault.name `
  --query '{id:id,location:location,rbac:properties.enableRbacAuthorization}' `
  --output json | ConvertFrom-Json
if (-not $TargetVaultProperties.rbac) {
  throw 'The Pegasus Key Vault is not RBAC-authorised.'
}
if (($SourceVaults | Where-Object {
  $_.location -ne $TargetVaultProperties.location -or
  $_.id -notlike "*/resourceGroups/$PredecessorResourceGroup/*"
}).Count -ne 0) {
  throw 'A source vault is outside the approved predecessor group or region.'
}
```

Read back the generated resource and identity values. Obtain an explicit
approval naming those values and the two source-vault IDs before continuing.

### 2. Read metadata only and derive the approved six bindings

```powershell
$WorkerSettings = @(az functionapp config appsettings list `
  --resource-group $PegasusProductionResourceGroup --name $Worker.name `
  --query "[?name=='Box__ConfigJson' || name=='Box__ClientSecret' || name=='Dvla__ApiKey' || name=='Dvsa__ClientId' || name=='Dvsa__ClientSecret' || name=='Dvsa__ApiKey']" `
  --output json | ConvertFrom-Json | Select-Object name,value)
if ($WorkerSettings.Count -ne $WorkerSettingNames.Count) {
  throw 'The Worker does not expose exactly the six approved references.'
}

$WebSecrets = @(az containerapp secret list `
  --resource-group $PegasusProductionResourceGroup --name $Web.name `
  --query "[?name=='box-config-json' || name=='box-client-secret']" `
  --output json | ConvertFrom-Json | Select-Object name,keyVaultUrl,identity)
if ($WebSecrets.Count -ne $WebSecretNames.Count) {
  throw 'The Web does not expose exactly its two approved Key Vault secrets.'
}

$Bindings = foreach ($WorkerSetting in $WorkerSettings) {
  if ($WorkerSetting.value -notmatch '^@Microsoft\.KeyVault\(SecretUri=(https://[^/]+\.vault\.azure\.net/secrets/[^/)]+/[^/)]+)\)$') {
    throw "Worker setting $($WorkerSetting.name) is not a versioned Key Vault reference."
  }
  $SourceUri = [Uri]$Matches[1]
  $SourceVaultName = $SourceUri.Host.Split('.')[0]
  $PathParts = $SourceUri.AbsolutePath.Trim('/').Split('/')
  [pscustomobject]@{
    SettingName = $WorkerSetting.name
    SourceUri = $SourceUri.AbsoluteUri
    SourceVaultName = $SourceVaultName
    SecretName = $PathParts[1]
    SourceVersion = $PathParts[2]
  }
}

if (($Bindings.SourceVaultName | Sort-Object -Unique | Compare-Object $SourceVaultNames) -or
    ($Bindings.SettingName | Sort-Object -Unique | Compare-Object $WorkerSettingNames)) {
  throw 'The Worker reference inventory does not match the approved sources or settings.'
}
if (@($Bindings.SecretName | Sort-Object -Unique).Count -ne $WorkerSettingNames.Count) {
  throw 'The approved Worker settings do not resolve to six distinct secrets.'
}
foreach ($WebSecret in $WebSecrets) {
  if ($WebSecret.keyVaultUrl -notmatch '^https://[^/]+\.vault\.azure\.net/secrets/[^/]+/[^/]+$' -or
      $WebSecret.identity -ne $WebIdentityResourceId) {
    throw "Web secret $($WebSecret.name) is not a versioned reference through the Web identity."
  }
}
$BoxBindings = @($Bindings | Where-Object {
  $_.SettingName -in @('Box__ConfigJson', 'Box__ClientSecret')
})
if ($BoxBindings.Count -ne $WebSecretNames.Count) {
  throw 'The Worker does not expose exactly the two approved Box bindings.'
}
foreach ($BoxBinding in $BoxBindings) {
  $ExpectedWebSecretName = if ($BoxBinding.SettingName -eq 'Box__ConfigJson') {
    'box-config-json'
  } else {
    'box-client-secret'
  }
  $WebSecret = $WebSecrets | Where-Object { $_.name -eq $ExpectedWebSecretName }
  if ($WebSecret.keyVaultUrl -ne $BoxBinding.SourceUri) {
    throw "Web and Worker do not agree on $($BoxBinding.SettingName)."
  }
}

foreach ($Binding in $Bindings) {
  $SourceVersionMetadata = @(az keyvault secret list-versions `
    --vault-name $Binding.SourceVaultName --name $Binding.SecretName `
    --query "[?id=='$($Binding.SourceUri)'].{id:id,enabled:attributes.enabled}" `
    --output json | ConvertFrom-Json)
  if ($SourceVersionMetadata.Count -ne 1 -or -not $SourceVersionMetadata[0].enabled) {
    throw "The specifically referenced source version for $($Binding.SettingName) is absent or disabled."
  }
}

$ExistingTargetNames = @(az keyvault secret list --vault-name $TargetVault.name `
  --query '[].name' --output json | ConvertFrom-Json)
$DeletedTargetNames = @(az keyvault secret list-deleted --vault-name $TargetVault.name `
  --query '[].name' --output json | ConvertFrom-Json)
$DeletedApprovedTargetNames = @($Bindings.SecretName | Where-Object {
  $_ -in $DeletedTargetNames
} | Sort-Object -Unique)
if ($DeletedApprovedTargetNames.Count -ne 0) {
  throw "Target vault has soft-deleted approved names: $($DeletedApprovedTargetNames -join ', '). Do not recover or purge them."
}
$ExistingApprovedTargetNames = @($Bindings.SecretName | Where-Object {
  $_ -in $ExistingTargetNames
} | Sort-Object -Unique)
foreach ($Binding in $Bindings) {
  $CopyRequired = $Binding.SecretName -notin $ExistingApprovedTargetNames
  $Binding | Add-Member -NotePropertyName CopyRequired -NotePropertyValue $CopyRequired
  if (-not $CopyRequired) {
    $ExpectedTargetUri = "$($TargetVault.uri)secrets/$($Binding.SecretName)/$($Binding.SourceVersion)"
    $TargetVersionMetadata = @(az keyvault secret list-versions `
      --vault-name $TargetVault.name --name $Binding.SecretName `
      --query "[?id=='$ExpectedTargetUri'].{id:id,enabled:attributes.enabled}" `
      --output json | ConvertFrom-Json)
    if ($TargetVersionMetadata.Count -ne 1 -or -not $TargetVersionMetadata[0].enabled) {
      throw "Existing target secret $($Binding.SecretName) is not an exact, enabled prior copy; do not overwrite it."
    }
  }
}
```

The commands only handle Key Vault reference strings and metadata. They do
not query a secret value. Review the six derived source URI/name/version pairs,
the two Web URI/name pairs, and the target-name state. An exact, enabled copy
of a previously restored source version is the only permitted pre-existing
approved target secret is allowed. A soft-deleted approved name, an unverified
copy, or an unrelated existing target-vault secret remains out of scope. This
lets a stopped run resume without overwriting, recovering, or purging a secret. Obtain a
second, specific approval before copying any secret. That approval must also confirm
that the executing principal already has `secrets/backup` on each source vault
and `secrets/restore` on the target vault; the plan never grants those highly
privileged data-plane permissions.

### 3. Copy each complete secret history without exposing a value

```powershell
$BackupRoot = Join-Path $env:TEMP "pegasus-vault-consolidation-$(Get-Date -AsUTC -Format 'yyyyMMddTHHmmssZ')"
New-Item -ItemType Directory -Path $BackupRoot -ErrorAction Stop | Out-Null

foreach ($Binding in @($Bindings | Where-Object { $_.CopyRequired })) {
  $BackupPath = Join-Path $BackupRoot "$($Binding.SecretName).backup"
  az keyvault secret backup --vault-name $Binding.SourceVaultName `
    --name $Binding.SecretName --file $BackupPath --only-show-errors
  az keyvault secret restore --vault-name $TargetVault.name `
    --file $BackupPath --only-show-errors --output none
}

foreach ($Binding in $Bindings) {
  $ExpectedTargetUri = "$($TargetVault.uri)secrets/$($Binding.SecretName)/$($Binding.SourceVersion)"
  $TargetVersionMetadata = @(az keyvault secret list-versions `
    --vault-name $TargetVault.name --name $Binding.SecretName `
    --query "[?id=='$ExpectedTargetUri'].{id:id,enabled:attributes.enabled}" `
    --output json | ConvertFrom-Json)
  if ($TargetVersionMetadata.Count -ne 1 -or -not $TargetVersionMetadata[0].enabled) {
    throw "The referenced source version was not restored as an enabled target version for $($Binding.SecretName)."
  }
  $Binding | Add-Member -NotePropertyName TargetUri -NotePropertyValue $ExpectedTargetUri
}
```

Stop if any backup or restore fails, a restored secret is disabled, an existing
target secret causes a conflict, the specifically referenced source version is
not present and enabled in the target, or the source and target vault locations
are not compatible for backup/restore. The service permits restore only within
the same Azure subscription and geography; this plan's same-region check is a
deliberately stricter gate. Do not fall back to `az keyvault secret show`,
`--value`, or a pipeline that exposes a secret value.

### 4. Grant only the necessary secret-level reads

```powershell
function Ensure-SecretUserRole {
  param(
    [Parameter(Mandatory)] [string] $PrincipalId,
    [Parameter(Mandatory)] [string] $SecretScope
  )

  $Existing = @(az role assignment list --assignee-object-id $PrincipalId `
    --scope $SecretScope --role $KeyVaultSecretsUserRoleId `
    --fill-principal-name false --fill-role-definition-name false `
    --query '[].id' --output json | ConvertFrom-Json)
  if ($Existing.Count -eq 0) {
    az role assignment create --assignee-object-id $PrincipalId `
      --assignee-principal-type ServicePrincipal `
      --role $KeyVaultSecretsUserRoleId --scope $SecretScope `
      --only-show-errors --output none
  } elseif ($Existing.Count -ne 1) {
    throw "Unexpected Key Vault Secrets User role count at $SecretScope."
  }
}

foreach ($Binding in $Bindings) {
  $SecretScope = "$($TargetVaultProperties.id)/secrets/$($Binding.SecretName)"
  Ensure-SecretUserRole -PrincipalId $WorkerPrincipalId -SecretScope $SecretScope
}
foreach ($BoxBinding in $BoxBindings) {
  $SecretScope = "$($TargetVaultProperties.id)/secrets/$($BoxBinding.SecretName)"
  Ensure-SecretUserRole -PrincipalId $WebPrincipalId -SecretScope $SecretScope
}

$ExpectedWorkerSecretScopes = @($Bindings | ForEach-Object {
  "$($TargetVaultProperties.id)/secrets/$($_.SecretName)"
} | Sort-Object -Unique)
$ExpectedWebSecretScopes = @($BoxBindings | ForEach-Object {
  "$($TargetVaultProperties.id)/secrets/$($_.SecretName)"
} | Sort-Object -Unique)
function Assert-ExactSecretUserScopes {
  param(
    [Parameter(Mandatory)] [string] $PrincipalId,
    [Parameter(Mandatory)] [string[]] $ExpectedScopes
  )

  $AllSecretUserScopes = @(az role assignment list --assignee-object-id $PrincipalId `
    --all --role $KeyVaultSecretsUserRoleId `
    --fill-principal-name false --fill-role-definition-name false `
    --query '[].scope' --output json | ConvertFrom-Json)
  $TargetSecretUserScopes = @($AllSecretUserScopes | Where-Object {
    $_ -eq $TargetVaultProperties.id -or
    $TargetVaultProperties.id -like "$_/*" -or
    $_ -like "$($TargetVaultProperties.id)/*"
  } | Sort-Object -Unique)
  if (Compare-Object $ExpectedScopes $TargetSecretUserScopes) {
    throw "Key Vault Secrets User scopes for $PrincipalId are not exactly the approved secret scopes."
  }
}
Assert-ExactSecretUserScopes -PrincipalId $WorkerPrincipalId `
  -ExpectedScopes $ExpectedWorkerSecretScopes
Assert-ExactSecretUserScopes -PrincipalId $WebPrincipalId `
  -ExpectedScopes $ExpectedWebSecretScopes
```

The readback requires exactly six target-vault secret scopes for the Worker and
two for the Web. It fails on a `Key Vault Secrets User` grant at the vault,
resource-group, or subscription scope. Review any other assignment applying to
the target vault before proceeding; this task must not be used to broaden
runtime access.

### 5. Repoint the Worker and prove all six references resolve

```powershell
$WorkerUpdates = @($Bindings | ForEach-Object {
  "$($_.SettingName)=@Microsoft.KeyVault(SecretUri=$($_.TargetUri))"
})
az functionapp config appsettings set `
  --resource-group $PegasusProductionResourceGroup --name $Worker.name `
  --settings $WorkerUpdates --only-show-errors --output none

function Get-UnresolvedWorkerKeyVaultReferences {
  $CurrentWorkerSettings = @(az functionapp config appsettings list `
    --resource-group $PegasusProductionResourceGroup --name $Worker.name `
    --query "[?name=='Box__ConfigJson' || name=='Box__ClientSecret' || name=='Dvla__ApiKey' || name=='Dvsa__ClientId' || name=='Dvsa__ClientSecret' || name=='Dvsa__ApiKey']" `
    --output json | ConvertFrom-Json | Select-Object name,value)
  $WorkerReferenceStatus = az rest --method GET --uri (
    "https://management.azure.com$($Worker.id)/config/configreferences/appsettings?api-version=2025-03-01"
  ) --output json | ConvertFrom-Json
  $Unresolved = @()
  foreach ($WorkerSettingName in $WorkerSettingNames) {
    $CurrentWorkerSetting = @($CurrentWorkerSettings | Where-Object {
      $_.name -eq $WorkerSettingName
    })
    $WorkerReference = @($WorkerReferenceStatus.value | Where-Object {
      $_.name -eq $WorkerSettingName
    })
    $Binding = $Bindings | Where-Object { $_.SettingName -eq $WorkerSettingName }
    $ExpectedReference = "@Microsoft.KeyVault(SecretUri=$($Binding.TargetUri))"
    $TargetUriParts = ([Uri]$Binding.TargetUri).AbsolutePath.Trim('/').Split('/')
    if ($CurrentWorkerSetting.Count -ne 1 -or
        $CurrentWorkerSetting[0].value -ne $ExpectedReference -or
        $WorkerReference.Count -ne 1 -or
        $WorkerReference[0].properties.status -ne 'Resolved' -or
        $WorkerReference[0].properties.vaultName -ne $TargetVault.name -or
        $WorkerReference[0].properties.secretName -ne $Binding.SecretName -or
        $WorkerReference[0].properties.secretVersion -ne $TargetUriParts[2]) {
      $Unresolved += $WorkerSettingName
    }
  }
  return @($Unresolved | Sort-Object -Unique)
}

$WorkerReferenceDeadline = (Get-Date).AddMinutes(10)
do {
  az rest --method POST --uri (
    "https://management.azure.com$($Worker.id)/config/configreferences/appsettings/refresh?api-version=2022-03-01"
  ) --only-show-errors --output none
  $UnresolvedWorkerSettings = @(Get-UnresolvedWorkerKeyVaultReferences)
  if ($UnresolvedWorkerSettings.Count -eq 0) {
    break
  }
  if ((Get-Date) -ge $WorkerReferenceDeadline) {
    throw "Worker Key Vault reference resolution timed out: $($UnresolvedWorkerSettings -join ', ')."
  }
  Start-Sleep -Seconds 15
} while ($true)
```

The loop reads the six setting strings as references (not secret values) and
their status collection until all six are `Resolved`, or ten minutes passes.
The `Resolved` statuses prove reference resolution only; they do not prove a
Box, DVLA, or DVSA business outcome.

### 6. Repoint the Web and prove the revision becomes healthy

```powershell
$WebConfigUri = ($Bindings | Where-Object {
  $_.SettingName -eq 'Box__ConfigJson'
}).TargetUri
$WebClientSecretUri = ($Bindings | Where-Object {
  $_.SettingName -eq 'Box__ClientSecret'
}).TargetUri

az containerapp secret set `
  --resource-group $PegasusProductionResourceGroup --name $Web.name `
  --secrets (
    "box-config-json=keyvaultref:$WebConfigUri,identityref:$WebIdentityResourceId",
    "box-client-secret=keyvaultref:$WebClientSecretUri,identityref:$WebIdentityResourceId"
  ) --only-show-errors --output none

$CurrentWebSecrets = @(az containerapp secret list `
  --resource-group $PegasusProductionResourceGroup --name $Web.name `
  --query "[?name=='box-config-json' || name=='box-client-secret']" `
  --output json | ConvertFrom-Json | Select-Object name,keyVaultUrl,identity)
if ($CurrentWebSecrets.Count -ne $WebSecretNames.Count) {
  throw 'The Web does not expose exactly its two approved Key Vault secrets after update.'
}
foreach ($ExpectedWebSecret in @(
  [pscustomobject]@{ name = 'box-config-json'; keyVaultUrl = $WebConfigUri },
  [pscustomobject]@{ name = 'box-client-secret'; keyVaultUrl = $WebClientSecretUri }
)) {
  $CurrentWebSecret = @($CurrentWebSecrets | Where-Object {
    $_.name -eq $ExpectedWebSecret.name
  })
  if ($CurrentWebSecret.Count -ne 1 -or
      $CurrentWebSecret[0].keyVaultUrl -ne $ExpectedWebSecret.keyVaultUrl -or
      $CurrentWebSecret[0].identity -ne $WebIdentityResourceId) {
    throw "Web secret $($ExpectedWebSecret.name) was not saved as the approved target-vault reference."
  }
}

$ActiveWebRevisions = @(az containerapp revision list `
  --resource-group $PegasusProductionResourceGroup --name $Web.name `
  --query '[?properties.active].name' --output json | ConvertFrom-Json)
if ($ActiveWebRevisions.Count -eq 0) {
  throw 'No active Web revisions exist to restart.'
}
foreach ($ActiveWebRevision in $ActiveWebRevisions) {
  az containerapp revision restart `
    --resource-group $PegasusProductionResourceGroup --name $Web.name `
    --revision $ActiveWebRevision --only-show-errors --output none
}

$WebRevisionDeadline = (Get-Date).AddMinutes(10)
do {
  $UnhealthyWebRevisions = @()
  foreach ($ActiveWebRevision in $ActiveWebRevisions) {
    $WebRevision = az containerapp revision show `
      --resource-group $PegasusProductionResourceGroup --name $Web.name `
      --revision $ActiveWebRevision --output json | ConvertFrom-Json
    if ($WebRevision.properties.provisioningState -ne 'Provisioned' -or
        $WebRevision.properties.runningState -ne 'Running' -or
        $WebRevision.properties.healthState -ne 'Healthy') {
      $UnhealthyWebRevisions += $ActiveWebRevision
    }
  }
  if ($UnhealthyWebRevisions.Count -eq 0) {
    break
  }
  if ((Get-Date) -ge $WebRevisionDeadline) {
    throw "Web revisions did not become healthy: $($UnhealthyWebRevisions -join ', ')."
  }
  Start-Sleep -Seconds 15
} while ($true)

$WebFqdn = az containerapp show --resource-group $PegasusProductionResourceGroup `
  --name $Web.name --query 'properties.configuration.ingress.fqdn' --output tsv
if ([string]::IsNullOrWhiteSpace($WebFqdn)) {
  throw 'The Web ingress FQDN is absent.'
}
$WebReady = Invoke-WebRequest -Uri "https://$WebFqdn/health/ready" `
  -MaximumRedirection 0 -TimeoutSec 30
if ($WebReady.StatusCode -ne 200) {
  throw "Web readiness returned HTTP $($WebReady.StatusCode)."
}
```

The metadata readback requires both target-vault URLs and the Web identity. It
then restarts every active revision, not an arbitrary first revision, and polls
for `Provisioned`, `Running`, and `Healthy` before an unauthenticated
`/health/ready` HTTP 200 check. A healthy restarted revision is required before
the predecessor vaults can be considered for retirement. Stop and restore the
saved predecessor reference metadata if any revision does not become healthy.

### 7. Independent readback, retirement, and final cleanup

First obtain a final destructive-operation approval naming the exact two source
vault IDs and `rg-collisionspike-dev`. Then run the following checks; do not
delete anything if they produce another resource, reference, identity, role,
or unresolved status.

```powershell
$PredecessorResources = @(az resource list `
  --resource-group $PredecessorResourceGroup `
  --query '[].{id:id,name:name,type:type}' --output json | ConvertFrom-Json)
$ExpectedPredecessorIds = @($SourceVaults.id | Sort-Object)
$ActualPredecessorIds = @($PredecessorResources.id | Sort-Object)
if (($PredecessorResources.Count -ne 2) -or
    ($PredecessorResources.type | Where-Object {
      $_ -ne 'Microsoft.KeyVault/vaults'
    }).Count -ne 0 -or
    (Compare-Object $ExpectedPredecessorIds $ActualPredecessorIds)) {
  throw 'Predecessor group is not exactly the two approved source vaults.'
}

$UnresolvedWorkerSettings = @(Get-UnresolvedWorkerKeyVaultReferences)
if ($UnresolvedWorkerSettings.Count -ne 0) {
  throw 'Worker references are no longer all resolved.'
}
Assert-ExactSecretUserScopes -PrincipalId $WorkerPrincipalId `
  -ExpectedScopes $ExpectedWorkerSecretScopes
Assert-ExactSecretUserScopes -PrincipalId $WebPrincipalId `
  -ExpectedScopes $ExpectedWebSecretScopes

$CurrentWebSecrets = @(az containerapp secret list `
  --resource-group $PegasusProductionResourceGroup --name $Web.name `
  --query "[?name=='box-config-json' || name=='box-client-secret']" `
  --output json | ConvertFrom-Json | Select-Object name,keyVaultUrl,identity)
if ($CurrentWebSecrets.Count -ne $WebSecretNames.Count) {
  throw 'Web does not expose exactly the two approved Key Vault secrets at final readback.'
}
foreach ($ExpectedWebSecret in @(
  [pscustomobject]@{ name = 'box-config-json'; keyVaultUrl = $WebConfigUri },
  [pscustomobject]@{ name = 'box-client-secret'; keyVaultUrl = $WebClientSecretUri }
)) {
  $CurrentWebSecret = @($CurrentWebSecrets | Where-Object {
    $_.name -eq $ExpectedWebSecret.name
  })
  if ($CurrentWebSecret.Count -ne 1 -or
      $CurrentWebSecret[0].keyVaultUrl -ne $ExpectedWebSecret.keyVaultUrl -or
      $CurrentWebSecret[0].identity -ne $WebIdentityResourceId) {
    throw "Web secret $($ExpectedWebSecret.name) is not the approved target-vault reference at final readback."
  }
}

foreach ($SourceVault in $SourceVaults) {
  az resource delete --ids $SourceVault.id --only-show-errors
  az resource wait --ids $SourceVault.id --deleted --timeout 300
}

$RemainingPredecessorResources = @(az resource list `
  --resource-group $PredecessorResourceGroup --output json | ConvertFrom-Json)
if ($RemainingPredecessorResources.Count -ne 0) {
  throw 'The predecessor group still contains resources; do not delete the group.'
}
az group delete --name $PredecessorResourceGroup --yes --no-wait
az group wait --name $PredecessorResourceGroup --deleted --timeout 900

foreach ($SourceVaultName in $SourceVaultNames) {
  az keyvault show-deleted --name $SourceVaultName `
    --query '{name:name,id:id,properties:properties}' --output json
}
```

Only after those commands succeed, remove the exact temporary backup directory
created in step 3, not its parent and not a repository path:

```powershell
Remove-Item -LiteralPath $BackupRoot -Recurse -Force
```

## Verification and handoff

Record only the date, approved target IDs, resource/identity names, six
setting names, two Web secret names, reference-resolution statuses, revision
health, role-scope counts, soft-delete IDs, and the group-deletion result.
Do not record secret values, backup paths, access tokens, connection strings,
or provider responses.

Classify the result precisely:

- **Implemented:** target-vault copies, scoped RBAC, and references were made.
- **Live verified:** the six Worker reference statuses are `Resolved`, the two
  Web references use the Web identity, and the restarted Web revision is
  healthy.
- **Not proved by this task:** Box/DVLA/DVSA business behavior, Worker caller
  traffic, the production spine, deployment acceptance, or operator
  acceptance.

The implementation PR must remove its own `NOW.md` claim and receive an
independent review against this plan before merging into `dev`. After merge, a
maintenance push deletes this transient plan.
