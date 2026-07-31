[CmdletBinding()]
param(
    [Parameter(Mandatory)][string] $Environment,
    [Parameter(Mandatory)][ValidateSet('alex')][string] $UserName,
    [Parameter(Mandatory)][string] $ManifestPath,
    [Parameter(Mandatory)][string] $ManifestSha256
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$expectedSubscriptionId = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$expectedTenantId = '858cf5b3-aa0a-47a6-9b40-4851fd0afa94'
$manifestFile = Resolve-Path -LiteralPath $ManifestPath
if ([IO.Path]::GetFileName($manifestFile) -ne 'release-manifest.json') {
    throw 'ManifestPath must identify the reviewed release-manifest.json.'
}
if ($ManifestSha256 -notmatch '^[0-9a-fA-F]{64}$') {
    throw 'ManifestSha256 must be the operator-approved 64-character SHA-256.'
}
$actualManifestSha256 = (Get-FileHash -LiteralPath $manifestFile -Algorithm SHA256).Hash
if (-not $actualManifestSha256.Equals($ManifestSha256, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'release-manifest.json does not match the operator-approved SHA-256.'
}
& (Join-Path $PSScriptRoot 'Test-AzureDeploymentPlan.ps1') -Mode Artifact -ManifestPath $manifestFile
$manifest = Get-Content -Raw -LiteralPath $manifestFile | ConvertFrom-Json -Depth 10
$webEntries = @($manifest.artifacts | Where-Object name -eq 'web.zip')
$package = Join-Path ([IO.Path]::GetDirectoryName($manifestFile)) 'web.zip'
$packageFile = Get-Item -LiteralPath $package
$packageHash = (Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash
if (
    $packageFile.Length -ne $webEntries[0].sizeBytes -or
    $packageHash -ne $webEntries[0].sha256
) {
    throw 'web.zip size or SHA-256 differs from the reviewed release manifest.'
}
$values = @{}
foreach ($line in (& azd env get-values -e $Environment --no-prompt)) {
    if ($line -match '^([A-Z0-9_]+)=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim('"') }
}
if (
    $values['AZURE_SUBSCRIPTION_ID'] -ne $expectedSubscriptionId -or
    $values['AZURE_TENANT_ID'] -ne $expectedTenantId -or
    $values['AZURE_RESOURCE_GROUP'] -ne 'rg-pegasus-prod'
) {
    throw 'Administrator bootstrap refuses an azd environment outside the exact approved subscription, tenant, and resource group.'
}
$account = & az account show --query '{subscription:id,tenant:tenantId}' --output json | ConvertFrom-Json
if (
    $LASTEXITCODE -ne 0 -or
    $account.subscription -ne $expectedSubscriptionId -or
    $account.tenant -ne $expectedTenantId
) {
    throw 'Administrator bootstrap refuses the current Azure CLI account context.'
}
$bootstrapRoot = Join-Path (Split-Path -Parent $PSScriptRoot) "artifacts/bootstrap/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Path $bootstrapRoot -Force | Out-Null
try {
    Expand-Archive -LiteralPath $package -DestinationPath $bootstrapRoot
    $assembly = Join-Path $bootstrapRoot 'Pegasus.Web.dll'
    if (-not (Test-Path -LiteralPath $assembly)) { throw 'web.zip does not contain Pegasus.Web.dll.' }
    $previous = @{}
    $settings = @{
        ASPNETCORE_ENVIRONMENT = 'Production'
        Runtime__Profile = 'Production'
        ConnectionStrings__Pegasus = "Server=tcp:$($values['AZURE_SQL_SERVER_FQDN']),1433;Database=$($values['AZURE_SQL_DATABASE_NAME']);Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
        AzureIdentity__WebClientId = $values['WEB_IDENTITY_CLIENT_ID']
        TransportStorage__AccountName = $values['TRANSPORT_STORAGE_ACCOUNT_NAME']
        CustodyStorage__AccountName = $values['CUSTODY_STORAGE_ACCOUNT_NAME']
        CustodyStorage__ServiceUri = "https://$($values['CUSTODY_STORAGE_ACCOUNT_NAME']).blob.core.windows.net/"
    }
    foreach ($entry in $settings.GetEnumerator()) {
        $previous[$entry.Key] = [Environment]::GetEnvironmentVariable($entry.Key, 'Process')
        [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
    }
    Write-Output "Interactive bootstrap for $UserName against $($values['AZURE_SQL_SERVER_FQDN'])/$($values['AZURE_SQL_DATABASE_NAME'])."
    & dotnet $assembly --bootstrap-production-administrator
    if ($LASTEXITCODE -ne 0) { throw 'Production Administrator bootstrap failed.' }
}
finally {
    if ($previous) {
        foreach ($entry in $previous.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process') }
    }
    Remove-Item -LiteralPath $bootstrapRoot -Recurse -Force
}
