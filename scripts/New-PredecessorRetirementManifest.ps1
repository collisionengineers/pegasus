[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ArchiveRoot,
    [string] $RoleDispositionPath,
    [switch] $RequireRoleDisposition
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$subscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$allowedGroups = @('rg-collisionspike-dev', 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117')
$retainedVaultNames = @('cespkboxkvv76a47', 'cespkenrichkvgi62sd')
$resolvedRoot = [IO.Path]::GetFullPath($ArchiveRoot)
$repositoryRoot = [IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
if ($resolvedRoot.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The predecessor retirement manifest must be generated from an archive outside the repository.'
}

$archiveManifestPath = Join-Path $resolvedRoot 'archive-manifest.json'
$retirementManifestPath = Join-Path $resolvedRoot 'retirement-manifest.json'
if (-not (Test-Path -LiteralPath $archiveManifestPath -PathType Leaf)) {
    throw 'The fresh predecessor archive has no archive-manifest.json.'
}
if (Test-Path -LiteralPath $retirementManifestPath) {
    throw 'The fresh predecessor archive already contains a retirement-manifest.json.'
}

$archiveManifest = @(Get-Content -LiteralPath $archiveManifestPath -Raw | ConvertFrom-Json)
$manifestedPaths = @{}
foreach ($entry in $archiveManifest) {
    $path = [IO.Path]::GetFullPath((Join-Path $resolvedRoot ([string]$entry.path)))
    if (-not $path.StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Archive manifest entry escaped the archive root: $($entry.path)"
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Archive manifest entry is missing: $($entry.path)"
    }
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($file.Length -ne $entry.sizeBytes -or $hash -ne $entry.sha256) {
        throw "Archive manifest identity mismatch: $($entry.path)"
    }
    $manifestedPaths[[IO.Path]::GetRelativePath($resolvedRoot, $path).Replace('\','/')] = $true
}
$unmanifested = @(Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse |
    Where-Object FullName -ne $archiveManifestPath |
    Where-Object { -not $manifestedPaths.ContainsKey([IO.Path]::GetRelativePath($resolvedRoot, $_.FullName).Replace('\','/')) })
if ($unmanifested.Count -ne 0) {
    throw "The archive contains unmanifested files: $($unmanifested.FullName -join ', ')"
}

$groupEvidence = @{}
$resources = foreach ($group in $allowedGroups) {
    $groupPath = Join-Path $resolvedRoot "$group-group.json"
    if (-not (Test-Path -LiteralPath $groupPath -PathType Leaf)) {
        throw "Missing fresh resource-group evidence: $groupPath"
    }
    $groupEvidence[$group] = Get-Content -LiteralPath $groupPath -Raw | ConvertFrom-Json
    $inventoryPath = Join-Path $resolvedRoot "$group-resources.json"
    if (-not (Test-Path -LiteralPath $inventoryPath -PathType Leaf)) {
        throw "Missing fresh resource inventory: $inventoryPath"
    }
    @(Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json)
}
$resources = @($resources)
if ($resources.Count -eq 0) { throw 'The fresh predecessor inventory is empty.' }

foreach ($resource in $resources) {
    if ($resource.id -notmatch '^/subscriptions/([^/]+)/resourceGroups/([^/]+)/providers/.+$') {
        throw "Invalid predecessor resource ID: $($resource.id)"
    }
    if ($Matches[1] -ne $subscription -or $Matches[2] -notin $allowedGroups) {
        throw "Predecessor resource escaped the approved subscription/groups: $($resource.id)"
    }
}
if ($resources.Count -ne @($resources.id | Select-Object -Unique).Count) {
    throw 'The fresh predecessor inventory contains duplicate resource IDs.'
}

$retainedVaults = @($resources | Where-Object {
    $_.type -ieq 'Microsoft.KeyVault/vaults' -and $_.name -in $retainedVaultNames
})
$actualRetainedNames = @($retainedVaults.name | Sort-Object) -join '|'
$expectedRetainedNames = @($retainedVaultNames | Sort-Object) -join '|'
if ($retainedVaults.Count -ne 2 -or $actualRetainedNames -ne $expectedRetainedNames) {
    throw 'The fresh inventory does not contain exactly the two retained production dependency vaults.'
}
$retainedPrefixes = @($retainedVaults.id | ForEach-Object { "$($_.TrimEnd('/'))/" })
$retainedResources = @($resources | Where-Object {
    $id = [string]$_.id
    $id -in $retainedVaults.id -or @($retainedPrefixes | Where-Object { $id.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }).Count -gt 0
})
$managedChildResources = @($resources | Where-Object {
    $_.id -match '/resourceGroups/cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117/'
})
$mainGroupId = "/subscriptions/$subscription/resourceGroups/rg-collisionspike-dev"
$childGroupId = "/subscriptions/$subscription/resourceGroups/cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117"
$mainGroupEvidence = $groupEvidence['rg-collisionspike-dev']
$childGroupEvidence = $groupEvidence['cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117']
if (-not ([string]$mainGroupEvidence.id).Equals($mainGroupId, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The main predecessor resource-group evidence has an unexpected ID.'
}
if (-not ([string]$childGroupEvidence.id).Equals($childGroupId, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The managed child resource-group evidence has an unexpected ID.'
}
$managedByProperty = $childGroupEvidence.PSObject.Properties['managedBy']
$managedBy = if ($null -ne $managedByProperty) { [string]$managedByProperty.Value } else { $null }
$expectedManagedBy = "$childGroupId/providers/Microsoft.Web/sites"
if (-not $managedBy.Equals($expectedManagedBy, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The child resource group does not have the exact expected Function Apps platform owner.'
}
if ($managedChildResources.Count -ne 1 -or $managedChildResources[0].type -ine 'Microsoft.App/containerApps') {
    throw 'The Function Apps managed child group must contain exactly one platform-owned Container App.'
}
$managedParentCandidates = @($resources | Where-Object {
    $_.id.StartsWith("$mainGroupId/providers/", [StringComparison]::OrdinalIgnoreCase) -and
    $_.type -ieq 'Microsoft.Web/sites' -and
    $_.name -ceq $managedChildResources[0].name
})
if ($managedParentCandidates.Count -ne 1) {
    throw 'The platform-owned Container App does not map to exactly one same-named Function App parent.'
}
$managedParentId = [string]$managedParentCandidates[0].id
$deleteResources = @($resources | Where-Object {
    $_.id -notin $retainedResources.id -and $_.id -notin $managedChildResources.id
})
if ($resources.Count -ne ($retainedResources.Count + $managedChildResources.Count + $deleteResources.Count)) {
    throw 'Every inventoried resource must be retained, managed-child-owned, or deletion-batched exactly once.'
}

$rolesPath = Join-Path $resolvedRoot 'subscription-role-assignments.json'
if (-not (Test-Path -LiteralPath $rolesPath -PathType Leaf)) {
    throw "Missing complete subscription role-assignment inventory: $rolesPath"
}
$allRoleAssignments = @(Get-Content -LiteralPath $rolesPath -Raw | ConvertFrom-Json)
$roleAssignmentCandidates = foreach ($group in $allowedGroups) {
    $groupScope = "/subscriptions/$subscription/resourceGroups/$group"
    $allRoleAssignments |
        Where-Object {
            ([string]$_.scope).Equals($groupScope, [StringComparison]::OrdinalIgnoreCase) -or
            ([string]$_.scope).StartsWith("$groupScope/", [StringComparison]::OrdinalIgnoreCase)
        } |
        ForEach-Object {
            [ordered]@{
                id = $_.id
                scope = $_.scope
                principalId = $_.principalId
                principalName = $_.principalName
                roleDefinitionName = $_.roleDefinitionName
            }
        }
}
$roleAssignmentCandidates = @(
    $roleAssignmentCandidates |
        Sort-Object { [string]$_['id'] } -Unique
)
$roleDispositionSha256 = $null
if ([string]::IsNullOrWhiteSpace($RoleDispositionPath)) {
    if ($RequireRoleDisposition -and $roleAssignmentCandidates.Count -ne 0) {
        throw '-RoleDispositionPath is required when role-assignment candidates exist and -RequireRoleDisposition is set.'
    }
    $roleAssignments = @($roleAssignmentCandidates | ForEach-Object {
        [ordered]@{ id=$_.id; scope=$_.scope; principalId=$_.principalId; principalName=$_.principalName; roleDefinitionName=$_.roleDefinitionName; disposition='pending'; rationale='' }
    })
} else {
    $resolvedRoleDisposition = Resolve-Path -LiteralPath $RoleDispositionPath
    $roleDispositionSha256 = (Get-FileHash -LiteralPath $resolvedRoleDisposition -Algorithm SHA256).Hash
    $dispositions = @(Get-Content -LiteralPath $resolvedRoleDisposition -Raw | ConvertFrom-Json)
    $uniqueDispositionCount = @($dispositions.id | Select-Object -Unique).Count
    if ($dispositions.Count -ne $roleAssignmentCandidates.Count -or $dispositions.Count -ne $uniqueDispositionCount) {
        throw "Role dispositions must classify every candidate exactly once; expected $($roleAssignmentCandidates.Count), found $($dispositions.Count), unique $uniqueDispositionCount."
    }
    foreach ($disposition in $dispositions) {
        if ($disposition.id -notin $roleAssignmentCandidates.id -or $disposition.disposition -notin @('retain','delete') -or [string]::IsNullOrWhiteSpace([string]$disposition.rationale)) {
            throw "Invalid role-assignment disposition: $($disposition.id)"
        }
    }
    $roleAssignments = @($roleAssignmentCandidates | ForEach-Object {
        $candidate = $_
        $disposition = @($dispositions | Where-Object id -eq $candidate.id)
        [ordered]@{ id=$candidate.id; scope=$candidate.scope; principalId=$candidate.principalId; principalName=$candidate.principalName; roleDefinitionName=$candidate.roleDefinitionName; disposition=$disposition[0].disposition; rationale=$disposition[0].rationale }
    })
    foreach ($retainedRole in @($roleAssignments | Where-Object disposition -eq 'retain')) {
        $destructiveScopes = @($deleteResources.id + $managedChildResources.id + $childGroupId | Select-Object -Unique)
        $destroyedWithScope = @($destructiveScopes | Where-Object {
            ([string]$retainedRole.scope).Equals($_, [StringComparison]::OrdinalIgnoreCase) -or
            ([string]$retainedRole.scope).StartsWith("$($_)/", [StringComparison]::OrdinalIgnoreCase)
        })
        if ($destroyedWithScope.Count -ne 0) {
            throw "A retained role assignment is scoped within a retirement target and must be migrated or reclassified: $($retainedRole.id)"
        }
    }
}

function Get-BatchName([string] $Type) {
    switch -Regex ($Type) {
        '^Microsoft\.Web/sites$|^Microsoft\.Web/staticSites$' { return 'leaf-compute' }
        '^Microsoft\.App/managedEnvironments$' { return 'container-environments' }
        '^Microsoft\.CognitiveServices/|^Microsoft\.Maps/' { return 'ai-and-maps' }
        '^Microsoft\.Web/serverFarms$|^Microsoft\.ManagedIdentity/' { return 'plans-and-identities' }
        '^Microsoft\.DBforPostgreSQL/|^Microsoft\.Storage/' { return 'data-stores' }
        '^Microsoft\.ContainerRegistry/' { return 'predecessor-registry' }
        '^Microsoft\.Insights/|^microsoft\.insights/|^Microsoft\.OperationalInsights/' { return 'telemetry' }
        '^Microsoft\.KeyVault/' { return 'obsolete-vaults' }
        default { return 'remaining-leaf-resources' }
    }
}

$batchOrder = @(
    'leaf-compute',
    'container-environments',
    'ai-and-maps',
    'plans-and-identities',
    'data-stores',
    'predecessor-registry',
    'telemetry',
    'obsolete-vaults',
    'remaining-leaf-resources'
)
$batches = foreach ($batchName in $batchOrder) {
    $ids = @($deleteResources |
        Where-Object { (Get-BatchName $_.type) -eq $batchName } |
        Sort-Object @{ Expression = { ($_.id -split '/').Count }; Descending = $true }, id |
        Select-Object -ExpandProperty id)
    if ($ids.Count -gt 0) {
        [ordered]@{ name = $batchName; resourceIds = $ids }
    }
}
$batchedIds = @($batches.resourceIds)
if ($batchedIds.Count -ne $deleteResources.Count -or $batchedIds.Count -ne @($batchedIds | Select-Object -Unique).Count) {
    throw 'The retirement batches do not cover every non-retained resource exactly once.'
}

$stopResourceIds = @($deleteResources |
    Where-Object { $_.type -ieq 'Microsoft.Web/sites' -or $_.type -ieq 'Microsoft.App/containerApps' } |
    Sort-Object id |
    Select-Object -ExpandProperty id)
if ($stopResourceIds.Count -eq 0) { throw 'The retirement manifest has no stoppable predecessor compute.' }

$retirementManifest = [ordered]@{
    schemaVersion = 2
    createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
    subscriptionId = $subscription
    allowedResourceGroups = $allowedGroups
    archiveManifestSha256 = (Get-FileHash -LiteralPath $archiveManifestPath -Algorithm SHA256).Hash
    retainedResourceIds = @($retainedResources.id | Sort-Object)
    managedChildResourceIds = @($managedChildResources.id | Sort-Object)
    managedChildResourceGroup = [ordered]@{
        id = $childGroupId
        managedBy = $managedBy
        parentResourceId = $managedParentId
    }
    roleDispositionSha256 = $roleDispositionSha256
    roleAssignments = $roleAssignments
    stopResourceIds = $stopResourceIds
    batches = @($batches)
}
$retirementManifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $retirementManifestPath -Encoding utf8NoBOM
Write-Output $retirementManifestPath
