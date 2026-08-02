[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $ArchiveRoot,
    [switch] $IncludeOciImages,
    [switch] $ResumeIncompleteOciArchive,
    [switch] $ExcludeData
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $true
if (-not $ExcludeData) { throw '-ExcludeData is mandatory; predecessor blobs, databases, queues, Durable state, and telemetry must not be downloaded.' }
$subscription = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$groups = @('rg-collisionspike-dev', 'cespkocr-env-dev_FunctionApps_247f14f1-8d57-491f-a325-a97e99634117')
$vaults = @('cespkboxkvv76a47', 'cespkenrichkvgi62sd', 'cespk-pg-kv-dev', 'cespkevakvufa3ci', 'cespklockva7tzj2')
$registry = 'cespkocracraeee76'
$resolvedRoot = [IO.Path]::GetFullPath($ArchiveRoot)
if ($resolvedRoot.StartsWith([IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)), [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The predecessor archive must remain outside the repository.'
}
$archiveManifestPath = Join-Path $resolvedRoot 'archive-manifest.json'
if (Test-Path -LiteralPath $resolvedRoot) {
    $existingEntries = @(Get-ChildItem -LiteralPath $resolvedRoot -Force)
    if ($existingEntries.Count -ne 0) {
        if (-not $ResumeIncompleteOciArchive -or -not $IncludeOciImages -or
            (Test-Path -LiteralPath $archiveManifestPath)) {
            throw 'A predecessor archive refresh requires a new empty timestamped ArchiveRoot; only an OCI archive without archive-manifest.json may use explicit resume mode.'
        }
    }
}
New-Item -ItemType Directory -Path $resolvedRoot -Force | Out-Null
function Invoke-AzJson([string[]] $Arguments, [string] $OutputPath) {
    $json = (& az @Arguments --output json) -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "Azure CLI evidence read failed: az $($Arguments -join ' ')" }
    if ([string]::IsNullOrWhiteSpace($json)) { throw "Azure CLI evidence read returned no JSON: az $($Arguments -join ' ')" }
    try { $parsed = $json | ConvertFrom-Json } catch { throw "Azure CLI evidence read returned invalid JSON: az $($Arguments -join ' ')" }
    if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
        $json | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM
    }
    return $parsed
}

$account = Invoke-AzJson -Arguments @('account', 'show') -OutputPath $null
if ($account.id -ne $subscription) { throw 'Azure CLI is not targeting the approved subscription.' }
foreach ($group in $groups) {
    Invoke-AzJson -Arguments @('group', 'show', '--name', $group) -OutputPath (Join-Path $resolvedRoot "$group-group.json") | Out-Null
    Invoke-AzJson -Arguments @('resource', 'list', '--resource-group', $group) -OutputPath (Join-Path $resolvedRoot "$group-resources.json") | Out-Null
    Invoke-AzJson -Arguments @('lock', 'list', '--resource-group', $group) -OutputPath (Join-Path $resolvedRoot "$group-locks.json") | Out-Null
    Invoke-AzJson -Arguments @('deployment', 'group', 'list', '--resource-group', $group, '--query', '[].{id:id,name:name,timestamp:properties.timestamp,state:properties.provisioningState,templateHash:properties.templateHash}') -OutputPath (Join-Path $resolvedRoot "$group-deployments.json") | Out-Null
    Invoke-AzJson -Arguments @('monitor', 'activity-log', 'list', '--resource-group', $group, '--offset', '30d') -OutputPath (Join-Path $resolvedRoot "$group-activity-30d.json") | Out-Null
}
Invoke-AzJson -Arguments @('role', 'assignment', 'list', '--all', '--include-inherited') -OutputPath (Join-Path $resolvedRoot 'subscription-role-assignments.json') | Out-Null
foreach ($vault in $vaults) {
    Invoke-AzJson -Arguments @('keyvault', 'show', '--name', $vault) -OutputPath (Join-Path $resolvedRoot "$vault-vault.json") | Out-Null
    Invoke-AzJson -Arguments @('keyvault', 'secret', 'list', '--vault-name', $vault, '--query', '[].{name:name,enabled:attributes.enabled,updated:attributes.updated}') -OutputPath (Join-Path $resolvedRoot "$vault-secret-names.json") | Out-Null
}
Invoke-AzJson -Arguments @('acr', 'repository', 'list', '--name', $registry) -OutputPath (Join-Path $resolvedRoot "$registry-repositories.json") | Out-Null
foreach ($repository in @('ce-ocr', 'valuationbot-mcp')) {
    Invoke-AzJson -Arguments @('acr', 'manifest', 'list-metadata', '--registry', $registry, '--name', $repository) -OutputPath (Join-Path $resolvedRoot "$registry-$repository-manifests.json") | Out-Null
    if ($IncludeOciImages) {
        if (-not (Get-Command oras -ErrorAction SilentlyContinue)) { throw 'ORAS 1.3.0 is required to archive OCI images.' }
        $manifests = Get-Content (Join-Path $resolvedRoot "$registry-$repository-manifests.json") -Raw | ConvertFrom-Json
        foreach ($digest in @($manifests.digest | Sort-Object -Unique)) {
            if ([string]::IsNullOrWhiteSpace($digest)) { throw "A $repository manifest omitted its digest." }
            $target = Join-Path $resolvedRoot "oci/$repository/$($digest.Replace(':','-'))"
            if (-not ([IO.Path]::GetFullPath($target)).StartsWith($resolvedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                throw "OCI archive target escaped the approved archive root: $target"
            }
            if (Test-Path -LiteralPath $target) {
                $existingDigest = ((& oras resolve --oci-layout "${target}:archive" 2>$null) -join '').Trim()
                if ($LASTEXITCODE -eq 0 -and $existingDigest -eq $digest) {
                    continue
                }
                Remove-Item -LiteralPath $target -Recurse -Force
            }
            New-Item -ItemType Directory -Path $target -Force | Out-Null
            oras copy --to-oci-layout "$registry.azurecr.io/$repository@$digest" "${target}:archive"
            if ($LASTEXITCODE -ne 0) { throw "ORAS OCI-layout copy failed for $repository@$digest" }
            $archivedDigest = (oras resolve --oci-layout "${target}:archive").Trim()
            if ($LASTEXITCODE -ne 0 -or $archivedDigest -ne $digest) {
                throw "Archived OCI digest verification failed for $repository@$digest; found $archivedDigest"
            }
        }
    }
}
$usageStart = [DateTime]::UtcNow.Date.AddDays(-30).ToString('yyyy-MM-dd')
$usageEnd = [DateTime]::UtcNow.Date.AddDays(1).ToString('yyyy-MM-dd')
$usage = Invoke-AzJson -Arguments @('consumption', 'usage', 'list', '--start-date', $usageStart, '--end-date', $usageEnd) -OutputPath $null
$groupUsage = @($usage | Where-Object {
    $instanceIdProperty = $_.PSObject.Properties['instanceId']
    $instanceNameProperty = $_.PSObject.Properties['instanceName']
    $resourceId = if ($null -ne $instanceIdProperty) {
        $instanceIdProperty.Value
    } elseif ($null -ne $instanceNameProperty) {
        $instanceNameProperty.Value
    } else {
        $null
    }
    $resourceId -and ($groups | Where-Object { $resourceId -match "/resourceGroups/$_/" })
})
$groupUsage | ConvertTo-Json -Depth 20 | Set-Content (Join-Path $resolvedRoot 'predecessor-usage-30d.json') -Encoding utf8NoBOM
Get-ChildItem -LiteralPath $resolvedRoot -File -Recurse | Sort-Object FullName | ForEach-Object {
    [pscustomobject]@{ path = [IO.Path]::GetRelativePath($resolvedRoot, $_.FullName).Replace('\','/'); sizeBytes = $_.Length; sha256 = (Get-FileHash $_.FullName -Algorithm SHA256).Hash }
} | ConvertTo-Json -Depth 4 | Set-Content $archiveManifestPath -Encoding utf8NoBOM
