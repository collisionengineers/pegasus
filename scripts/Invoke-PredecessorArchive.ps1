[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ArchiveRoot,
    [switch] $IncludeOciImages,
    [switch] $ExcludeData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $ExcludeData) { throw '-ExcludeData is mandatory; predecessor blobs, databases, queues, Durable state, and telemetry must not be downloaded.' }
$subscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$groups = @('rg-collisionspike-dev', 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117')
$vaults = @('cespkboxkvv76a47', 'cespkenrichkvgi62sd', 'cespk-pg-kv-dev')
$registry = 'cespkocracraeee76'
$resolvedRoot = [IO.Path]::GetFullPath($ArchiveRoot)
if ($resolvedRoot.StartsWith([IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The predecessor archive must remain outside the repository.'
}
New-Item -ItemType Directory -Path $resolvedRoot -Force | Out-Null
$account = az account show --output json | ConvertFrom-Json
if ($account.id -ne $subscription) { throw 'Azure CLI is not targeting the approved subscription.' }
foreach ($group in $groups) {
    az resource list --resource-group $group --output json | Set-Content (Join-Path $resolvedRoot "$group-resources.json") -Encoding utf8NoBOM
    az lock list --resource-group $group --output json | Set-Content (Join-Path $resolvedRoot "$group-locks.json") -Encoding utf8NoBOM
    az role assignment list --resource-group $group --all --output json | Set-Content (Join-Path $resolvedRoot "$group-roles.json") -Encoding utf8NoBOM
    az deployment group list --resource-group $group --query '[].{id:id,name:name,timestamp:properties.timestamp,state:properties.provisioningState,templateHash:properties.templateHash}' --output json | Set-Content (Join-Path $resolvedRoot "$group-deployments.json") -Encoding utf8NoBOM
    az monitor activity-log list --resource-group $group --offset 30d --output json | Set-Content (Join-Path $resolvedRoot "$group-activity-30d.json") -Encoding utf8NoBOM
}
foreach ($vault in $vaults) {
    az keyvault show --name $vault --output json | Set-Content (Join-Path $resolvedRoot "$vault-vault.json") -Encoding utf8NoBOM
    az keyvault secret list --vault-name $vault --query '[].{name:name,enabled:attributes.enabled,updated:attributes.updated}' --output json | Set-Content (Join-Path $resolvedRoot "$vault-secret-names.json") -Encoding utf8NoBOM
}
az acr repository list --name $registry --output json | Set-Content (Join-Path $resolvedRoot "$registry-repositories.json") -Encoding utf8NoBOM
foreach ($repository in @('ce-ocr', 'valuationbot-mcp')) {
    az acr manifest list-metadata --registry $registry --name $repository --output json | Set-Content (Join-Path $resolvedRoot "$registry-$repository-manifests.json") -Encoding utf8NoBOM
    if ($IncludeOciImages) {
        if (-not (Get-Command oras -ErrorAction SilentlyContinue)) { throw 'ORAS 1.3.0 is required to archive OCI images.' }
        $manifests = Get-Content (Join-Path $resolvedRoot "$registry-$repository-manifests.json") -Raw | ConvertFrom-Json
        foreach ($manifest in $manifests) {
            $digest = $manifest.digest
            if ([string]::IsNullOrWhiteSpace($digest)) { throw "A $repository manifest omitted its digest." }
            $target = Join-Path $resolvedRoot "oci/$repository/$($digest.Replace(':','-'))"
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            oras pull "$registry.azurecr.io/$repository@$digest" -o $target
            if ($LASTEXITCODE -ne 0) { throw "ORAS failed for $repository@$digest" }
        }
    }
}
$usageStart = [DateTime]::UtcNow.Date.AddDays(-30).ToString('yyyy-MM-dd')
$usageEnd = [DateTime]::UtcNow.Date.AddDays(1).ToString('yyyy-MM-dd')
$usage = az consumption usage list --start-date $usageStart --end-date $usageEnd --output json | ConvertFrom-Json
$groupUsage = @($usage | Where-Object {
    $resourceId = $_.instanceId
    $resourceId -and ($groups | Where-Object { $resourceId -match "/resourceGroups/$_/" })
})
$groupUsage | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $resolvedRoot 'predecessor-usage-30d.json') -Encoding utf8NoBOM
Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{ path = [IO.Path]::GetRelativePath($resolvedRoot, $_.FullName).Replace('\','/'); sizeBytes = $_.Length; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
} | ConvertTo-Json -Depth 4 | Set-Content (Join-Path $resolvedRoot 'archive-manifest.json') -Encoding utf8NoBOM
