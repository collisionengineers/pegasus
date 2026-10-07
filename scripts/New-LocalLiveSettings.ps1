<#
    .SYNOPSIS
    Writes the ignored local live-integration settings file for a hosted local
    Pegasus run: real DVLA/DVSA, real Box custody under the test root and real
    Glass's, with the secrets read from the production Key Vault.

    .DESCRIPTION
    Nothing is read until -Approve is given; without it the script prints the
    exact secret names it would read and exits 2. With -Approve it requires an
    Azure CLI session, reads each named secret with `az keyvault secret show`
    into memory only, creates the file empty with owner-only permissions, and
    then writes the secrets beside the non-secret literals. Secret values are
    never printed, logged or passed on a command line.

    The non-secret literals (Box API origins, DVLA/DVSA origins, scope and
    token endpoint, Glass's provider origins and repair profile) are owned by
    infra/modules/platform.bicep; this file restates the deployed values and
    must follow that module when they change. Glass__CallbackBaseUri is not
    written: the run lifecycle sets it from the run's own Web origin.

    The Box root is the operator-approved local-test folder 425169015650, never
    the production root. The holding folder must be a child of it; the
    optional Box preflight proves both are reachable by the app's service
    account before the first Case folder is written.

    .EXAMPLE
    pwsh ./scripts/New-LocalLiveSettings.ps1 -BoxHoldingFolderId 123456789012
    pwsh ./scripts/New-LocalLiveSettings.ps1 -BoxHoldingFolderId 123456789012 -AdministratorPassword (Read-Host -AsSecureString) -Approve
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9]{6,20}$')]
    [string]$BoxHoldingFolderId,
    [securestring]$AdministratorPassword,
    [string]$VaultName = 'pegasusprodkv252ow37g',
    [string]$Path,
    [switch]$IncludeProblemReports,
    [switch]$SkipBoxPreflight,
    [switch]$Approve
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

$localDevelopmentRoot = Join-Path $repositoryRoot 'artifacts/local-development'
if ([string]::IsNullOrWhiteSpace($Path)) {
    $Path = Get-PegasusLocalSettingsPath -LocalDevelopmentRoot $localDevelopmentRoot
}
$Path = [System.IO.Path]::GetFullPath($Path)

# The operator-approved local-test custody root (not the production root).
$boxRootFolderId = '425169015650'
if ($BoxHoldingFolderId -eq $boxRootFolderId) {
    throw 'The Box holding folder must be a folder below the local-test root, not the root itself.'
}

# Secret names as declared for the deployed estate (infra/modules/platform.bicep
# resolves them through Key Vault references; the migration recipe names them).
$secretNames = [ordered]@{
    Box__ConfigJson = 'box-config-json'
    Box__ClientSecret = 'box-client-secret'
    Dvla__ApiKey = 'dvla-api-key'
    Dvsa__ClientId = 'dvsa-client-id'
    Dvsa__ClientSecret = 'dvsa-client-secret'
    Dvsa__ApiKey = 'dvsa-api-key'
}
if ($IncludeProblemReports) {
    $secretNames['GitHub__ProblemReports__Token'] = 'github-problem-report-token'
}

# Non-secret literals: infra/modules/platform.bicep app settings for the Web
# (Glass's) and Worker (Box, DVLA, DVSA) hosts.
$literals = [ordered]@{
    Features__LiveVehicleLookup = 'true'
    Features__LiveBoxCustody = 'true'
    Features__LiveGlass = 'true'
    Box__BaseUri = 'https://api.box.com/2.0/'
    Box__UploadUri = 'https://upload.box.com/api/2.0/'
    Box__RootFolderId = $boxRootFolderId
    Box__HoldingFolderId = $BoxHoldingFolderId
    Dvla__BaseUri = 'https://driver-vehicle-licensing.api.gov.uk/vehicle-enquiry/v1/'
    Dvsa__BaseUri = 'https://history.mot.api.gov.uk/v1/trade/vehicles/registration/'
    Dvsa__TokenUri = 'https://login.microsoftonline.com/a455b827-244f-4c97-b5b4-ce5d13b4d00c/oauth2/v2.0/token'
    Dvsa__Scope = 'https://tapi.dvsa.gov.uk/.default'
    Glass__MarketValueAssessorBaseUri = 'https://www.marketvalueassessor.jdpower.com/'
    Glass__EstimatorBaseUri = 'https://repairestimate.autovistagroup.com/'
    Glass__RepairProfileId = '4063'
}
if ($IncludeProblemReports) {
    $literals['GitHub__ProblemReports__Repository'] = 'collisionengineers/pegasus'
    $literals['GitHub__ProblemReports__Labels'] = 'problem-report'
}
if ($null -ne $AdministratorPassword -and $AdministratorPassword.Length -gt 0) {
    $literals['Features__PasswordSignIn'] = 'true'
}

Write-Host "Settings file: $Path"
Write-Host "Key Vault:     $VaultName"
Write-Host 'Secrets read (names only):'
foreach ($entry in $secretNames.GetEnumerator()) {
    Write-Host "  $($entry.Value) -> $($entry.Key)"
}
Write-Host "Literals written: $($literals.Keys -join ', ')"
if ($null -ne $AdministratorPassword -and $AdministratorPassword.Length -gt 0) {
    Write-Host 'Also written: DevelopmentOffline__AdministratorPassword (from -AdministratorPassword)'
}
if (-not $Approve) {
    Write-Host 'Nothing was read or written. Re-run with -Approve to read these secrets and write the file.'
    exit 2
}

$az = Get-Command az -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
if ($null -eq $az) {
    throw 'The Azure CLI (az) is required to read the Key Vault secrets.'
}
$account = & $az.Source account show --output json 2>$null | Out-String
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($account)) {
    throw 'az account show failed: sign in with az login before reading secrets.'
}
$accountInfo = $account | ConvertFrom-Json
Write-Host "Azure account: $($accountInfo.user.name) ($($accountInfo.name))"

function Read-VaultSecret {
    param([Parameter(Mandatory)][string]$Name)

    # Captured into a variable; never echoed. --query value returns the raw
    # value with no surrounding quotes.
    $value = (& $az.Source keyvault secret show --vault-name $VaultName --name $Name --query value --output tsv 2>$null | Out-String).TrimEnd("`r", "`n")
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrEmpty($value)) {
        throw "Key Vault secret '$Name' could not be read from '$VaultName' (exit $LASTEXITCODE)."
    }
    return $value
}

$secrets = [ordered]@{}
foreach ($entry in $secretNames.GetEnumerator()) {
    $secrets[$entry.Key] = Read-VaultSecret -Name $entry.Value
}
# One line: the settings file is line-oriented and the host parses the JSON.
$secrets['Box__ConfigJson'] = ($secrets['Box__ConfigJson'] | ConvertFrom-Json -Depth 10 | ConvertTo-Json -Compress -Depth 10)
$boxConfiguration = $secrets['Box__ConfigJson'] | ConvertFrom-Json -Depth 10

$preflightSummary = 'skipped (-SkipBoxPreflight)'
if (-not $SkipBoxPreflight) {
    # Read-only: a JWT grant for the app's service account, then two folder
    # reads. Mirrors the Box SDK's enterprise JWT assertion the Worker and Web
    # mint through BoxJwtAuthorizationHeaderProvider.
    $appSettings = $boxConfiguration.boxAppSettings
    $rsa = [System.Security.Cryptography.RSA]::Create()
    try {
        $rsa.ImportFromEncryptedPem([string]$appSettings.appAuth.privateKey, [string]$appSettings.appAuth.passphrase)
        function ConvertTo-Base64Url {
            param([Parameter(Mandatory)][byte[]]$Bytes)
            return [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
        }
        $header = ConvertTo-Base64Url -Bytes ([System.Text.Encoding]::UTF8.GetBytes((
            [ordered]@{ alg = 'RS512'; typ = 'JWT'; kid = [string]$appSettings.appAuth.publicKeyID } | ConvertTo-Json -Compress)))
        $claims = ConvertTo-Base64Url -Bytes ([System.Text.Encoding]::UTF8.GetBytes((
            [ordered]@{
                iss = [string]$appSettings.clientID
                sub = [string]$boxConfiguration.enterpriseID
                box_sub_type = 'enterprise'
                aud = 'https://api.box.com/oauth2/token'
                jti = [Guid]::NewGuid().ToString('N') + [Guid]::NewGuid().ToString('N')
                exp = [DateTimeOffset]::UtcNow.AddSeconds(45).ToUnixTimeSeconds()
            } | ConvertTo-Json -Compress)))
        $signature = ConvertTo-Base64Url -Bytes ($rsa.SignData(
            [System.Text.Encoding]::UTF8.GetBytes("$header.$claims"),
            [System.Security.Cryptography.HashAlgorithmName]::SHA512,
            [System.Security.Cryptography.RSASignaturePadding]::Pkcs1))
        $assertion = "$header.$claims.$signature"
    }
    finally {
        $rsa.Dispose()
    }

    $token = Invoke-RestMethod -Method Post -Uri 'https://api.box.com/oauth2/token' -ContentType 'application/x-www-form-urlencoded' -Body @{
        grant_type = 'urn:ietf:params:oauth:grant-type:jwt-bearer'
        assertion = $assertion
        client_id = [string]$appSettings.clientID
        client_secret = $secrets['Box__ClientSecret']
    }
    $authorization = @{ Authorization = "Bearer $($token.access_token)" }
    $me = Invoke-RestMethod -Method Get -Uri 'https://api.box.com/2.0/users/me?fields=login,name' -Headers $authorization
    $root = Invoke-RestMethod -Method Get -Uri "https://api.box.com/2.0/folders/$boxRootFolderId`?fields=name,path_collection" -Headers $authorization
    $holding = Invoke-RestMethod -Method Get -Uri "https://api.box.com/2.0/folders/$BoxHoldingFolderId`?fields=name,path_collection" -Headers $authorization
    $holdingAncestors = @($holding.path_collection.entries | ForEach-Object { [string]$_.id })
    if ($holdingAncestors -notcontains $boxRootFolderId) {
        throw "Box folder $BoxHoldingFolderId ('$($holding.name)') is not below the local-test root $boxRootFolderId ('$($root.name)')."
    }
    $preflightSummary = "service account $($me.login) reads root '$($root.name)' ($boxRootFolderId) and holding folder '$($holding.name)' ($BoxHoldingFolderId)."
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add('# Pegasus local live-integration settings. Owner-only; never commit.')
$lines.Add("# Written $([DateTimeOffset]::UtcNow.ToString('O')) by New-LocalLiveSettings.ps1 from Key Vault $VaultName.")
$lines.Add('# Literal values follow infra/modules/platform.bicep; Glass__CallbackBaseUri is set by the run lifecycle.')
foreach ($entry in $literals.GetEnumerator()) { $lines.Add("$($entry.Key)=$($entry.Value)") }
foreach ($entry in $secrets.GetEnumerator()) { $lines.Add("$($entry.Key)=$($entry.Value)") }
if ($null -ne $AdministratorPassword -and $AdministratorPassword.Length -gt 0) {
    $plain = [System.Net.NetworkCredential]::new('', $AdministratorPassword).Password
    if ($plain.Contains("`n") -or $plain.Contains("`r")) {
        throw 'The administrator password cannot contain a line break.'
    }
    $lines.Add("DevelopmentOffline__AdministratorPassword=$plain")
}

[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($Path)) | Out-Null
# Create empty and restrict first, so no reader can race the content write.
$stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
try {
    if ($IsLinux) {
        [System.IO.File]::SetUnixFileMode($Path, [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite)
    }
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes(($lines -join "`n") + "`n")
    $stream.Write($bytes, 0, $bytes.Length)
}
finally {
    $stream.Dispose()
}

# Prove the lifecycle will accept what was written.
$written = Read-PegasusLocalSettingsFile -Path $Path
Write-Host "Wrote $($written.Count) setting(s) to $Path$(if ($IsLinux) { ' (mode 600)' })."
Write-Host "Live integrations: $(Format-PegasusLiveIntegrationFlags -Flags (Get-PegasusLiveIntegrationFlags -Settings $written))"
Write-Host "Box preflight: $preflightSummary"
Write-Host 'Next: pwsh ./scripts/Invoke-LocalDevelopment.ps1 -Action Start -WebPort 7139'
