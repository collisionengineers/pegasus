[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('Stop', 'DeleteBatch')][string] $Stage,
    [Parameter(Mandatory)][string] $ManifestPath,
    [string] $Batch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$subscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$allowedGroups = @('rg-collisionspike-dev', 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117')
$retainedVaults = @('cespkboxkvv76a47', 'cespkenrichkvgi62sd')
$resolvedManifest = Resolve-Path -LiteralPath $ManifestPath
$manifestHash = (Get-FileHash -LiteralPath $resolvedManifest -Algorithm SHA256).Hash
$approvedHash = Read-Host "Type the separately approved retirement manifest SHA-256 ($manifestHash)"
if ($approvedHash -ne $manifestHash) { throw 'The approved retirement manifest hash did not match.' }
$manifest = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json

function Assert-ExactResourceId([string] $Id) {
    if ($Id -notmatch '^/subscriptions/([^/]+)/resourceGroups/([^/]+)/providers/.+$') { throw "Invalid resource ID: $Id" }
    if ($Matches[1] -ne $subscription -or $Matches[2] -notin $allowedGroups) { throw "Resource ID escaped the approved subscription/groups: $Id" }
    if ($retainedVaults | Where-Object { $Id -match "/vaults/$_$" }) { throw "Retained vault is prohibited from retirement: $Id" }
}

$account = az account show --output json | ConvertFrom-Json
if ($account.id -ne $subscription) { throw 'Azure CLI is not targeting the approved subscription.' }
if ($Stage -eq 'Stop') {
    $ids = @($manifest.stopResourceIds)
    if ($ids.Count -eq 0) { throw 'The retirement manifest contains no stopResourceIds.' }
    foreach ($id in $ids) {
        Assert-ExactResourceId $id
        $current = az resource show --ids $id --output json | ConvertFrom-Json
        if ($current.id -ne $id) { throw "Azure identity drifted before stop: $id" }
        Write-Output "STOP $id"
        az resource invoke-action --action stop --ids $id --only-show-errors
        if ($LASTEXITCODE -ne 0) { throw "Stop failed: $id" }
    }
    return
}
if ([string]::IsNullOrWhiteSpace($Batch)) { throw '-Batch is required for DeleteBatch.' }
$selected = @($manifest.batches | Where-Object name -eq $Batch)
if ($selected.Count -ne 1) { throw "The retirement manifest does not contain exactly one batch named $Batch." }
$ids = @($selected[0].resourceIds)
if ($ids.Count -eq 0 -or $ids.Count -ne @($ids | Select-Object -Unique).Count) { throw 'The selected retirement batch must contain unique exact IDs.' }
foreach ($id in $ids) {
    Assert-ExactResourceId $id
    $current = az resource show --ids $id --output json | ConvertFrom-Json
    if ($current.id -ne $id) { throw "Azure identity drifted before deletion: $id" }
    Write-Output "DELETE $id"
    az resource delete --ids $id --verbose
    if ($LASTEXITCODE -ne 0) { throw "Deletion failed: $id" }
}
