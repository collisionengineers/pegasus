[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Inspect', 'Stop', 'DeleteBatch', 'DeleteRoleAssignment', 'DeleteManagedChildGroup')][string] $Stage,
    [Parameter(Mandatory)][string] $ManifestPath,
    [string] $Batch,
    [string] $RoleAssignmentId
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$subscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$allowedGroups = @('rg-collisionspike-dev', 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117')
$retainedVaults = @('cespkboxkvv76a47', 'cespkenrichkvgi62sd')
$resolvedManifest = Resolve-Path -LiteralPath $ManifestPath
$manifestHash = (Get-FileHash -LiteralPath $resolvedManifest -Algorithm SHA256).Hash
$manifest = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 2 -or $manifest.subscriptionId -ne $subscription) {
    throw 'The retirement manifest schema or subscription is invalid.'
}
$archiveManifestPath = Join-Path (Split-Path -Parent $resolvedManifest) 'archive-manifest.json'
if (-not (Test-Path -LiteralPath $archiveManifestPath -PathType Leaf)) {
    throw 'The retirement manifest is not adjacent to its archive-manifest.json.'
}
$archiveManifestHash = (Get-FileHash -LiteralPath $archiveManifestPath -Algorithm SHA256).Hash
if ($manifest.archiveManifestSha256 -ne $archiveManifestHash) {
    throw 'The retirement manifest is not bound to the adjacent verified archive manifest.'
}
$archiveRoot = Split-Path -Parent $resolvedManifest
$archiveManifest = @(Get-Content -LiteralPath $archiveManifestPath -Raw | ConvertFrom-Json)
$manifestedPaths = @{}
foreach ($entry in $archiveManifest) {
    $path = [IO.Path]::GetFullPath((Join-Path $archiveRoot ([string]$entry.path)))
    if (-not $path.StartsWith($archiveRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Archive manifest entry escaped the archive root: $($entry.path)"
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Bound archive entry is missing: $($entry.path)"
    }
    $file = Get-Item -LiteralPath $path
    $hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    if ($file.Length -ne $entry.sizeBytes -or $hash -ne $entry.sha256) {
        throw "Bound archive entry identity mismatch: $($entry.path)"
    }
    $manifestedPaths[[IO.Path]::GetRelativePath($archiveRoot, $path).Replace('\','/')] = $true
}
$allowedUnmanifested = @('archive-manifest.json', 'retirement-manifest.json')
$unmanifested = @(Get-ChildItem -LiteralPath $archiveRoot -File -Recurse | Where-Object {
    $relative = [IO.Path]::GetRelativePath($archiveRoot, $_.FullName).Replace('\','/')
    $relative -notin $allowedUnmanifested -and -not $manifestedPaths.ContainsKey($relative)
})
if ($unmanifested.Count -ne 0) {
    throw "The bound archive contains unmanifested files: $($unmanifested.FullName -join ', ')"
}
$allBatchIds = @($manifest.batches.resourceIds)
if ($allBatchIds.Count -eq 0 -or $allBatchIds.Count -ne @($allBatchIds | Select-Object -Unique).Count) {
    throw 'The retirement manifest batches must contain unique exact IDs.'
}
$expectedRetainedIds = @($retainedVaults | ForEach-Object {
    "/subscriptions/$subscription/resourceGroups/rg-collisionspike-dev/providers/Microsoft.KeyVault/vaults/$_"
} | Sort-Object)
$actualRetainedIds = @($manifest.retainedResourceIds | Sort-Object -Unique)
foreach ($expectedVaultId in $expectedRetainedIds) {
    if ($expectedVaultId -notin $actualRetainedIds) {
        throw "The retirement manifest omitted retained vault: $expectedVaultId"
    }
}
foreach ($retainedId in $actualRetainedIds) {
    $approvedVault = @($expectedRetainedIds | Where-Object {
        $retainedId.Equals($_, [StringComparison]::OrdinalIgnoreCase) -or
        $retainedId.StartsWith("$($_)/", [StringComparison]::OrdinalIgnoreCase)
    })
    if ($approvedVault.Count -ne 1) {
        throw "The retirement manifest contains an unapproved retained resource: $retainedId"
    }
}
$managedChildIds = @($manifest.managedChildResourceIds)
if ($managedChildIds.Count -ne @($managedChildIds | Select-Object -Unique).Count) {
    throw 'The retirement manifest managed child IDs must be unique.'
}
foreach ($managedChildId in $managedChildIds) {
    if ($managedChildId -notmatch "^/subscriptions/$subscription/resourceGroups/cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117/providers/.+") {
        throw "Managed child resource escaped the platform-owned child group: $managedChildId"
    }
    if ($managedChildId -in $allBatchIds) {
        throw "A platform-owned child resource must not be directly deletion-batched: $managedChildId"
    }
}
$expectedChildGroupId = "/subscriptions/$subscription/resourceGroups/cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117"
$managedChildGroup = $manifest.managedChildResourceGroup
if (
    -not ([string]$managedChildGroup.id).Equals($expectedChildGroupId, [StringComparison]::OrdinalIgnoreCase) -or
    [string]::IsNullOrWhiteSpace([string]$managedChildGroup.managedBy) -or
    $managedChildGroup.managedBy -notin $allBatchIds
) {
    throw 'The manifest does not bind the exact managed child group to a deletion-batched parent.'
}
$roleAssignments = @($manifest.roleAssignments)
if ($roleAssignments.Count -ne @($roleAssignments.id | Select-Object -Unique).Count) {
    throw 'The retirement role-assignment candidate IDs must be unique.'
}
foreach ($candidate in $roleAssignments) {
    if (
        [string]::IsNullOrWhiteSpace([string]$candidate.id) -or
        $candidate.id -notmatch '^/subscriptions/([^/]+)/resourceGroups/([^/]+)/providers/(?:.+/providers/)?Microsoft\.Authorization/roleAssignments/[^/]+$' -or
        $Matches[1] -ne $subscription -or
        $Matches[2] -notin $allowedGroups
    ) {
        throw "Invalid locally scoped role-assignment candidate: $($candidate.id)"
    }
    if ($candidate.disposition -notin @('pending','retain','delete')) {
        throw "Invalid role-assignment disposition: $($candidate.id)"
    }
    if ($candidate.disposition -eq 'retain') {
        $destructiveScopes = @($allBatchIds + $managedChildIds + $expectedChildGroupId | Select-Object -Unique)
        if (@($destructiveScopes | Where-Object {
            ([string]$candidate.scope).Equals($_, [StringComparison]::OrdinalIgnoreCase) -or
            ([string]$candidate.scope).StartsWith("$($_)/", [StringComparison]::OrdinalIgnoreCase)
        }).Count -ne 0) {
            throw "A retained role assignment is scoped within a retirement target: $($candidate.id)"
        }
    }
}
$stopIds = @($manifest.stopResourceIds)
if ($stopIds.Count -ne @($stopIds | Select-Object -Unique).Count) {
    throw 'The retirement stop target IDs must be unique.'
}
if (@($stopIds | Where-Object { $_ -notin $allBatchIds -and $_ -notin $managedChildIds }).Count -ne 0) {
    throw 'Every stop target must be a deletion-batched resource or a platform-owned managed child.'
}

if ($Stage -eq 'Inspect') {
    [ordered]@{
        manifestSha256 = $manifestHash
        archiveManifestSha256 = $archiveManifestHash
        retainedResourceIds = $actualRetainedIds
        managedChildResourceIds = $managedChildIds
        managedChildResourceGroup = $managedChildGroup
        roleDispositionSha256 = $manifest.roleDispositionSha256
        roleAssignments = $roleAssignments
        stopResourceIds = $stopIds
        batches = @($manifest.batches)
    } | ConvertTo-Json -Depth 8
    return
}

$approvedHash = Read-Host "Type the separately approved retirement manifest SHA-256 ($manifestHash)"
if (-not $approvedHash.Equals($manifestHash, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The approved retirement manifest hash did not match.'
}

function Assert-ExactResourceId([string] $Id) {
    if ($Id -notmatch '^/subscriptions/([^/]+)/resourceGroups/([^/]+)/providers/.+$') { throw "Invalid resource ID: $Id" }
    if ($Matches[1] -ne $subscription -or $Matches[2] -notin $allowedGroups) { throw "Resource ID escaped the approved subscription/groups: $Id" }
    if ($expectedRetainedIds | Where-Object {
        $Id.Equals($_, [StringComparison]::OrdinalIgnoreCase) -or
        $Id.StartsWith("$($_)/", [StringComparison]::OrdinalIgnoreCase)
    }) { throw "Retained vault or descendant is prohibited from retirement: $Id" }
}

function Get-ManagedChildGroup {
    $exists = (az group exists --name 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117' --subscription $subscription --output tsv).Trim()
    if ($LASTEXITCODE -ne 0 -or $exists -notin @('true','false')) { throw 'Unable to determine whether the managed child resource group exists.' }
    if ($exists -eq 'false') { return $null }
    $group = az group show --name 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117' --subscription $subscription --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $group.id.Equals([string]$managedChildGroup.id, [StringComparison]::OrdinalIgnoreCase) -or -not ([string]$group.managedBy).Equals([string]$managedChildGroup.managedBy, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Managed child resource-group identity or ownership drifted.'
    }
    return $group
}

function Get-CurrentPredecessorResources {
    $current = @(az resource list --resource-group 'rg-collisionspike-dev' --output json | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to refresh the main predecessor resource-group inventory.' }
    $childGroup = Get-ManagedChildGroup
    if ($null -ne $childGroup) {
        $current += @(az resource list --resource-group 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117' --output json | ConvertFrom-Json)
        if ($LASTEXITCODE -ne 0) { throw 'Unable to refresh the managed child resource-group inventory.' }
    }
    return @($current)
}

function Assert-InventoryBoundary {
    $current = @(Get-CurrentPredecessorResources)
    $approvedIds = @($actualRetainedIds + $managedChildIds + $allBatchIds | Select-Object -Unique)
    $unexpectedIds = @($current.id | Where-Object { $_ -notin $approvedIds })
    if ($unexpectedIds.Count -ne 0) { throw "Unexpected live predecessor resources are absent from the approved manifest: $($unexpectedIds -join ', ')" }
    foreach ($expectedVaultId in $expectedRetainedIds) {
        if ($expectedVaultId -notin $current.id) { throw "Approved retained vault is unexpectedly absent: $expectedVaultId" }
    }
    $parentExists = $managedChildGroup.managedBy -in $current.id
    $liveManagedChildren = @($current.id | Where-Object { $_ -in $managedChildIds })
    $childGroup = Get-ManagedChildGroup
    if (-not $parentExists -and ($null -ne $childGroup -or $liveManagedChildren.Count -ne 0)) {
        throw 'Managed child cleanup has not completed after parent deletion; later retirement batches are blocked.'
    }
    return $current
}

function Get-OperationalState([object] $Resource) {
    $stateProperty = $Resource.properties.PSObject.Properties['state']
    $runningStatusProperty = $Resource.properties.PSObject.Properties['runningStatus']
    $state = if ($null -ne $stateProperty) {
        $stateProperty.Value
    } elseif ($null -ne $runningStatusProperty) {
        $runningStatusProperty.Value
    } else {
        $null
    }
    return [string]$state
}

function Assert-ResourceStopped([string] $Id) {
    $current = az resource show --ids $Id --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $current.id.Equals($Id, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Azure identity drifted while verifying stopped state: $Id"
    }
    $state = Get-OperationalState $current
    if ($state -ne 'Stopped') { throw "Resource has not verified as Stopped ($state): $Id" }
}

$account = az account show --output json | ConvertFrom-Json
if ($account.id -ne $subscription) { throw 'Azure CLI is not targeting the approved subscription.' }
if ($Stage -eq 'Stop') {
    $approvedResources = @(Assert-InventoryBoundary)
    $ids = @($manifest.stopResourceIds)
    if ($ids.Count -eq 0) { throw 'The retirement manifest contains no stopResourceIds.' }
    foreach ($id in $ids) {
        Assert-ExactResourceId $id
        if ($id -notin $approvedResources.id) { throw "Approved stop target is unexpectedly absent: $id" }
        $current = az resource show --ids $id --output json | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0 -or -not $current.id.Equals($id, [StringComparison]::OrdinalIgnoreCase)) { throw "Azure identity drifted before stop: $id" }
        Write-Output "STOP $id"
        az resource invoke-action --action stop --ids $id --only-show-errors
        if ($LASTEXITCODE -ne 0) { throw "Stop failed: $id" }
        $verified = $false
        for ($attempt = 1; $attempt -le 20; $attempt++) {
            $current = az resource show --ids $id --output json | ConvertFrom-Json
            if ($LASTEXITCODE -ne 0 -or -not $current.id.Equals($id, [StringComparison]::OrdinalIgnoreCase)) {
                throw "Azure identity drifted after stop: $id"
            }
            if ((Get-OperationalState $current) -eq 'Stopped') {
                $verified = $true
                break
            }
            Start-Sleep -Seconds 15
        }
        if (-not $verified) { throw "Resource did not verify as Stopped after five minutes: $id" }
    }
    return
}
if ($Stage -eq 'DeleteManagedChildGroup') {
    if ($manifest.roleDispositionSha256 -notmatch '^[0-9A-Fa-f]{64}$' -or @($roleAssignments | Where-Object disposition -eq 'pending').Count -ne 0) {
        throw 'Managed child-group deletion requires complete hash-bound role dispositions.'
    }
    $currentResources = @(Get-CurrentPredecessorResources)
    $approvedIds = @($actualRetainedIds + $managedChildIds + $allBatchIds | Select-Object -Unique)
    $unexpectedIds = @($currentResources.id | Where-Object { $_ -notin $approvedIds })
    if ($unexpectedIds.Count -ne 0) { throw "Unexpected live predecessor resources are absent from the approved manifest: $($unexpectedIds -join ', ')" }
    foreach ($expectedVaultId in $expectedRetainedIds) {
        if ($expectedVaultId -notin $currentResources.id) { throw "Approved retained vault is unexpectedly absent: $expectedVaultId" }
    }
    if ($managedChildGroup.managedBy -in $currentResources.id) { throw 'The managed child parent still exists.' }
    $childGroup = Get-ManagedChildGroup
    if ($null -eq $childGroup) {
        Write-Output "ALREADY ABSENT $($managedChildGroup.id)"
        return
    }
    $childResources = @(az resource list --resource-group 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117' --output json | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0 -or $childResources.Count -ne 0) { throw 'The managed child resource group is not empty.' }
    Write-Output "DELETE GROUP $($managedChildGroup.id)"
    az group delete --name 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117' --subscription $subscription --yes
    if ($LASTEXITCODE -ne 0) { throw 'Managed child resource-group deletion failed.' }
    if ($null -ne (Get-ManagedChildGroup)) { throw 'Managed child resource group still exists after deletion.' }
    return
}
if ($Stage -eq 'DeleteRoleAssignment') {
    if ($manifest.roleDispositionSha256 -notmatch '^[0-9A-Fa-f]{64}$' -or @($roleAssignments | Where-Object disposition -eq 'pending').Count -ne 0) { throw 'Role-assignment disposition is not complete and hash-bound.' }
    if ([string]::IsNullOrWhiteSpace($RoleAssignmentId)) { throw '-RoleAssignmentId is required for DeleteRoleAssignment.' }
    $selectedRole = @($roleAssignments | Where-Object { $_.id -eq $RoleAssignmentId -and $_.disposition -eq 'delete' })
    if ($selectedRole.Count -ne 1) { throw 'The role assignment is not uniquely approved for deletion by the bound manifest.' }
    $remainingResources = @(Assert-InventoryBoundary | Where-Object { $_.id -in $allBatchIds })
    if ($remainingResources.Count -ne 0) { throw 'Resource deletion batches must complete before role-assignment deletion.' }
    $currentRoles = @(az role assignment list --all --include-inherited --output json | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) { throw 'Unable to refresh role assignments before deletion.' }
    if ($RoleAssignmentId -notin $currentRoles.id) { Write-Output "ALREADY ABSENT $RoleAssignmentId"; return }
    Write-Output "DELETE ROLE $RoleAssignmentId"
    az role assignment delete --ids $RoleAssignmentId
    if ($LASTEXITCODE -ne 0) { throw "Role-assignment deletion failed: $RoleAssignmentId" }
    $remainingRoles = @(az role assignment list --all --include-inherited --output json | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0 -or $RoleAssignmentId -in $remainingRoles.id) { throw "Role assignment still exists after deletion: $RoleAssignmentId" }
    return
}
if ([string]::IsNullOrWhiteSpace($Batch)) { throw '-Batch is required for DeleteBatch.' }
$pendingRoles = @($roleAssignments | Where-Object disposition -eq 'pending')
if ($manifest.roleDispositionSha256 -notmatch '^[0-9A-Fa-f]{64}$' -or $pendingRoles.Count -ne 0) { throw 'Role-assignment disposition is not complete and hash-bound; destructive batches are prohibited.' }
$selected = @($manifest.batches | Where-Object name -eq $Batch)
if ($selected.Count -ne 1) { throw "The retirement manifest does not contain exactly one batch named $Batch." }
$ids = @($selected[0].resourceIds)
if ($ids.Count -eq 0 -or $ids.Count -ne @($ids | Select-Object -Unique).Count) { throw 'The selected retirement batch must contain unique exact IDs.' }
$currentResources = @(Assert-InventoryBoundary)
$currentIds = @($currentResources.id)
foreach ($stopId in $stopIds) {
    if ($stopId -in $currentIds) { Assert-ResourceStopped $stopId }
}
$firstRemainingBatch = @($manifest.batches | Where-Object {
    @($_.resourceIds | Where-Object { $_ -in $currentIds }).Count -gt 0
} | Select-Object -First 1)
if ($firstRemainingBatch.Count -eq 0) {
    throw 'No deletion-batched predecessor resources remain.'
}
if ($firstRemainingBatch[0].name -ne $Batch) {
    throw "Retirement batches must run in manifest order. Next batch: $($firstRemainingBatch[0].name)"
}
foreach ($id in $ids) {
    Assert-ExactResourceId $id
    if ($id -notin $currentIds) {
        Write-Output "ALREADY ABSENT $id"
        continue
    }
    $current = az resource show --ids $id --output json | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or -not $current.id.Equals($id, [StringComparison]::OrdinalIgnoreCase)) { throw "Azure identity drifted before deletion: $id" }
    Write-Output "DELETE $id"
    az resource delete --ids $id --verbose
    if ($LASTEXITCODE -ne 0) { throw "Deletion failed: $id" }
    if ($id.Equals([string]$managedChildGroup.managedBy, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Output "MANAGED CHILD GROUP GATE $($managedChildGroup.id)"
        return
    }
    $remainingIds = @((Assert-InventoryBoundary).id)
    if ($id -in $remainingIds) { throw "Resource still exists after deletion: $id" }
}
