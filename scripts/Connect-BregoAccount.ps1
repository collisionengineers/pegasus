<#
    .SYNOPSIS
    Links the Brego valuation account to a Pegasus host by Brego's e-mailed
    sign-in link, and stores the sign-in the host renews itself from (ADR-0084).

    .DESCRIPTION
    Brego signs the valuation account in by an e-mailed link, never by a
    password. A person has to open the account's mailbox, so this script does
    the one step a host cannot do. It has two steps, and nothing is sent or
    written without -Approve:

    RequestLink  asks Brego to e-mail a sign-in link to -Email. The link
                 expires 15 minutes later.
    Redeem       redeems the pasted -Link for the account's Cognito tokens,
                 has Pegasus's device approved (Brego may e-mail a six-digit
                 code: the script asks for it, or takes -Code), mints the
                 one-year Brego session and writes
                 authentication-ring/brego-session.json for each -Target: the
                 e-mail, the session cookie and the refresh token.

    Brego keeps one web session for an account. Hosts that use the same
    account share one sign-in: name both targets in one Redeem. Linking them
    in separate runs makes each end the other's session.

    The host reads that file at its next Get valuation; no restart is needed.
    Run it again whenever Brego ends the sign-in: a host then answers Brego
    valuations as unavailable and logs stage `cognito_refresh` or `sign_in`.

    -Code without a prompt: a code Brego e-mailed in an earlier run is used
    with a NEW link, because the tokens of the earlier run are not kept. The
    code is submitted before the approval state is read, so no second code is
    sent.

    Production needs the signed-in Azure CLI account to hold Storage Blob Data
    Contributor on the `authentication-ring` container of the custody account.
    TestInstance writes to the run's Azurite account named by its run manifest.

    No token, cookie, link or code is printed, logged or left on disk.

    .EXAMPLE
    pwsh ./scripts/Connect-BregoAccount.ps1 -Step RequestLink -Email valuations@example.test -Approve
    pwsh ./scripts/Connect-BregoAccount.ps1 -Step Redeem -Email valuations@example.test -Target Production -Link '<pasted link>' -Approve
    pwsh ./scripts/Connect-BregoAccount.ps1 -Step Redeem -Email valuations@example.test -Target Production, TestInstance -RunManifestPath <run>/run-manifest.json -Link '<pasted link>' -Approve
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('RequestLink', 'Redeem')]
    [string]$Step,
    [Parameter(Mandatory)]
    [ValidatePattern('^[^@\s]+@[^@\s]+$')]
    [string]$Email,
    [string[]]$Target,
    [string]$Link,
    [ValidatePattern('^[0-9]{6}$')]
    [string]$Code,
    [string]$StorageAccountName = 'pegcustody252ow37gij',
    [string]$RunManifestPath,
    [switch]$Approve
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

$Email = $Email.Trim().ToLowerInvariant()
$bregoOrigin = 'https://www.brego.ai'
$containerName = 'authentication-ring'
$blobName = 'brego-session.json'

# The device id and the user agent are the host's own (BregoClient): Brego
# approves a device, and the session this script mints is the host's session.
$clientSource = Get-Content -LiteralPath (Join-Path $repositoryRoot 'src/Pegasus.Infrastructure/Brego/BregoClient.cs') -Raw
if ($clientSource -notmatch 'const string DefaultDeviceId = "([0-9a-f-]{36})"') {
    throw 'BregoClient.DefaultDeviceId was not found in the source.'
}
$deviceId = $Matches[1]
if ($clientSource -notmatch 'const string UserAgent = "([^"]+)"') {
    throw 'BregoClient.UserAgent was not found in the source.'
}
$userAgent = $Matches[1]

# `pwsh -File` hands "Production,TestInstance" over as one word.
$Target = @($Target | Where-Object { $_ } | ForEach-Object { $_.Split(',') } | ForEach-Object { $_.Trim() } | Where-Object { $_ })
foreach ($name in $Target) {
    if ($name -cnotin 'Production', 'TestInstance') { throw "-Target is Production, TestInstance or both; '$name' is neither." }
}

if ($Step -eq 'Redeem') {
    if ($Target.Count -eq 0) { throw '-Target is required for Redeem.' }
    if ([string]::IsNullOrWhiteSpace($Link)) { throw '-Link is required for Redeem.' }
    if ($Target -contains 'TestInstance' -and [string]::IsNullOrWhiteSpace($RunManifestPath)) {
        throw '-RunManifestPath (the run''s run-manifest.json) is required for -Target TestInstance.'
    }
}

if (-not $Approve) {
    if ($Step -eq 'RequestLink') {
        Write-Output "Would ask Brego to e-mail a sign-in link to $Email. Nothing was sent. Re-run with -Approve."
    }
    else {
        Write-Output "Would redeem the link for $Email, mint a Brego session for device $deviceId and write $containerName/$blobName for $($Target -join ' and '). Nothing was sent or written. Re-run with -Approve."
    }
    exit 2
}

function Invoke-BregoJson {
    param(
        [Parameter(Mandatory)][string]$Uri,
        [Parameter(Mandatory)][hashtable]$Body,
        [hashtable]$Headers = @{},
        [string]$ContentType = 'application/json'
    )

    $all = @{ Accept = 'application/json'; 'User-Agent' = $userAgent }
    foreach ($entry in $Headers.GetEnumerator()) { $all[$entry.Key] = $entry.Value }
    $response = Invoke-WebRequest -Method Post -Uri $Uri -Headers $all -ContentType $ContentType `
        -Body ($Body | ConvertTo-Json -Compress -Depth 6) -SkipHttpErrorCheck -TimeoutSec 30
    # Cognito answers application/x-amz-json-1.1, which arrives as bytes, not text.
    $text = if ($response.Content -is [byte[]]) { [System.Text.Encoding]::UTF8.GetString($response.Content) } else { [string]$response.Content }
    $document = $null
    try { $document = $text | ConvertFrom-Json -Depth 10 } catch { $document = $null }
    return [pscustomobject]@{ Status = [int]$response.StatusCode; Document = $document; Headers = $response.Headers }
}

function Get-OptionalValue {
    # A property that may be absent, under strict mode.
    param($Object, [string]$Name)
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties[$Name]
    if ($null -eq $property) { return $null }
    return $property.Value
}

function Invoke-Cognito {
    param([Parameter(Mandatory)][string]$Operation, [Parameter(Mandatory)][hashtable]$Body)

    $answer = Invoke-BregoJson -Uri $script:cognitoUri -Body $Body -ContentType 'application/x-amz-json-1.1' `
        -Headers @{ 'X-Amz-Target' = "AWSCognitoIdentityProviderService.$Operation" }
    if ($answer.Status -ne 200) {
        # Cognito's error type and message name no secret.
        throw "Cognito $Operation answered $($answer.Status): $(Get-OptionalValue $answer.Document '__type') $(Get-OptionalValue $answer.Document 'message')"
    }
    return $answer.Document
}

function Invoke-Bootstrap {
    param([Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][hashtable]$Body, [Parameter(Mandatory)][string]$AccessToken)

    return Invoke-BregoJson -Uri "$bregoOrigin/login/api/brego/$Path" -Body $Body `
        -Headers @{ 'X-Brego-Bootstrap-Access-Token' = $AccessToken }
}

function Read-JwtClaims {
    param([Parameter(Mandatory)][string]$Token)

    $payload = $Token.Split('.')[1].Replace('-', '+').Replace('_', '/')
    $payload = $payload.PadRight($payload.Length + ((4 - $payload.Length % 4) % 4), '=')
    return [System.Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload)) | ConvertFrom-Json -Depth 6
}

# Which Cognito pool and app client the account belongs to.
$authorizers = Invoke-BregoJson -Uri "$bregoOrigin/login/api/getAuthorizer" -Body @{ email = $Email; application = $null }
$first = @($authorizers.Document)[0]
$pool = [string](Get-OptionalValue $first 'authorizer')
$clientId = [string](Get-OptionalValue $first 'authorizerId')
if ($authorizers.Status -ne 200 -or $pool -notmatch '^([a-z0-9-]+)_[A-Za-z0-9]+$' -or [string]::IsNullOrWhiteSpace($clientId)) {
    throw "Brego did not name a sign-in pool for $Email (status $($authorizers.Status))."
}
$script:cognitoUri = "https://cognito-idp.$($Matches[1]).amazonaws.com/"

# Both steps open their own custom-auth session, as Brego's login page does.
$started = Invoke-Cognito -Operation 'InitiateAuth' -Body @{
    AuthFlow = 'CUSTOM_AUTH'; ClientId = $clientId; AuthParameters = @{ USERNAME = $Email }
}
if ((Get-OptionalValue $started 'ChallengeName') -ne 'CUSTOM_CHALLENGE') {
    throw "Brego did not offer a sign-in link for $Email (challenge '$(Get-OptionalValue $started 'ChallengeName')')."
}
$username = [string](Get-OptionalValue (Get-OptionalValue $started 'ChallengeParameters') 'USERNAME')
if ([string]::IsNullOrWhiteSpace($username)) { $username = $Email }

if ($Step -eq 'RequestLink') {
    $sent = Invoke-Cognito -Operation 'RespondToAuthChallenge' -Body @{
        ChallengeName = 'CUSTOM_CHALLENGE'; ClientId = $clientId; Session = $started.Session
        ChallengeResponses = @{ USERNAME = $username; ANSWER = 'request' }
        ClientMetadata = @{ action = 'request'; origin = $bregoOrigin }
    }
    $state = [string](Get-OptionalValue (Get-OptionalValue $sent 'ChallengeParameters') 'step')
    if ($state -ne 'MAGIC_LINK_SENT') { throw "Brego did not confirm the link was sent (step '$state')." }
    Write-Output "Brego e-mailed a sign-in link to $Email at $([DateTimeOffset]::UtcNow.ToString('u')). It expires 15 minutes later."
    exit 0
}

# The link carries the token in its fragment: #magic-link=<token>&email=<address>.
Add-Type -AssemblyName System.Web
$fragment = if ($Link.Contains('#')) { $Link.Substring($Link.IndexOf('#') + 1) } else { $Link }
$fields = [System.Web.HttpUtility]::ParseQueryString($fragment)
$token = $fields['magic-link']
if ([string]::IsNullOrWhiteSpace($token)) { throw 'The link has no magic-link token in its fragment.' }
$linkEmail = $fields['email']
if (-not [string]::IsNullOrWhiteSpace($linkEmail) -and $linkEmail.Trim().ToLowerInvariant() -ne $Email) {
    throw "The link is for another account than $Email."
}

# A link redeems once, so each store is reached before the link is used. A role
# assignment made a minute ago may not have reached the storage account yet.
if ($Target -contains 'Production') {
    $azProbe = Get-Command az -CommandType Application -ErrorAction Stop | Select-Object -First 1
    & $azProbe.Source storage blob exists --auth-mode login --account-name $StorageAccountName --container-name $containerName `
        --name $blobName --only-show-errors --output none
    if ($LASTEXITCODE -ne 0) {
        throw "$StorageAccountName/$containerName cannot be reached with the signed-in account (az exit $LASTEXITCODE). The link was not used. The account needs Storage Blob Data Contributor on that container; a new assignment can take a few minutes."
    }
}
if ($Target -contains 'TestInstance' -and -not (Test-Path -LiteralPath $RunManifestPath -PathType Leaf)) {
    throw "The run manifest $RunManifestPath was not found. The link was not used."
}

$redeemed = Invoke-Cognito -Operation 'RespondToAuthChallenge' -Body @{
    ChallengeName = 'CUSTOM_CHALLENGE'; ClientId = $clientId; Session = $started.Session
    ChallengeResponses = @{ USERNAME = $username; ANSWER = $token }
    ClientMetadata = @{ action = 'redeem'; deviceId = $deviceId }
}
$tokens = Get-OptionalValue $redeemed 'AuthenticationResult'
$accessToken = [string](Get-OptionalValue $tokens 'AccessToken')
$idToken = [string](Get-OptionalValue $tokens 'IdToken')
$refreshToken = [string](Get-OptionalValue $tokens 'RefreshToken')
if ([string]::IsNullOrWhiteSpace($accessToken) -or [string]::IsNullOrWhiteSpace($idToken) -or [string]::IsNullOrWhiteSpace($refreshToken)) {
    throw 'Brego rejected the link: it is used, expired or not the latest one sent.'
}
Write-Output 'Link redeemed.'

function Submit-DeviceCode {
    param([Parameter(Mandatory)][string]$Value)

    $verified = Invoke-Bootstrap -Path 'users/me/device-approval/verify' -AccessToken $accessToken -Body @{ deviceId = $deviceId; code = $Value }
    if ($verified.Status -lt 200 -or $verified.Status -ge 300) {
        throw "Brego refused the device code (status $($verified.Status))."
    }
}

function Read-DeviceApproval {
    # An unapproved device makes Brego e-mail a code, so this is read once
    # before a code and once after it, never in a loop.
    $challenge = Invoke-Bootstrap -Path 'users/me/device-approval/challenge' -AccessToken $accessToken -Body @{ deviceId = $deviceId }
    if ($challenge.Status -ne 200) { throw "Brego's device approval answered $($challenge.Status)." }
    return [string](Get-OptionalValue $challenge.Document 'status')
}

if (-not [string]::IsNullOrWhiteSpace($Code)) { Submit-DeviceCode -Value $Code }
$approval = Read-DeviceApproval
if ($approval -ne 'approved') {
    if (-not [string]::IsNullOrWhiteSpace($Code) -or -not [Environment]::UserInteractive -or [Console]::IsInputRedirected) {
        Write-Output "Brego e-mailed a six-digit code to $Email. Run RequestLink again and Redeem the new link with -Code <code>. Nothing was written."
        exit 3
    }
    $typed = (Read-Host "Brego e-mailed a six-digit code to $Email. Code").Trim()
    if ($typed -notmatch '^[0-9]{6}$') { throw 'A device code is six digits.' }
    Submit-DeviceCode -Value $typed
    $approval = Read-DeviceApproval
    if ($approval -ne 'approved') { throw "Brego did not approve the device (status '$approval')." }
}
Write-Output 'Device approved.'

$claims = Read-JwtClaims -Token $idToken
$slug = [string](Get-OptionalValue $claims 'slug')
if ([string]::IsNullOrWhiteSpace($slug)) { throw 'The sign-in does not name the Brego tenant.' }
$session = Invoke-Bootstrap -Path 'v1/sessions' -AccessToken $accessToken -Body @{
    accessToken = $accessToken; idToken = $idToken; refreshToken = $refreshToken; client = $slug; deviceId = $deviceId
}
$cookie = $null
if ($session.Headers.ContainsKey('Set-Cookie')) {
    foreach ($header in @($session.Headers['Set-Cookie'])) {
        if ($header -match '(?:^|[,;\s])brego-session=([^;,\s]+)') { $cookie = $Matches[1]; break }
    }
}
if ($session.Status -lt 200 -or $session.Status -ge 300 -or [string]::IsNullOrWhiteSpace($cookie)) {
    throw "Brego did not issue a session (status $($session.Status))."
}
Write-Output "Brego session minted; it ends $([string](Get-OptionalValue $session.Document 'expiresAt'))."

$payload = [System.Text.Encoding]::UTF8.GetBytes(([ordered]@{
            email = $Email; bregoSession = $cookie; refreshToken = $refreshToken
        } | ConvertTo-Json -Compress))

function Write-ProductionSignIn {
    $az = Get-Command az -CommandType Application -ErrorAction Stop | Select-Object -First 1
    $file = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), [System.IO.Path]::GetRandomFileName())
    try {
        # Created empty with owner-only access before the secret is written.
        [System.IO.File]::Create($file).Dispose()
        if (-not $IsWindows) { [System.IO.File]::SetUnixFileMode($file, [System.IO.UnixFileMode]'UserRead, UserWrite') }
        [System.IO.File]::WriteAllBytes($file, $payload)
        & $az.Source storage blob upload --auth-mode login --account-name $StorageAccountName --container-name $containerName `
            --name $blobName --file $file --overwrite --content-type 'application/json' --only-show-errors --output none
        if ($LASTEXITCODE -ne 0) {
            throw "The sign-in could not be written to $StorageAccountName/$containerName/$blobName (az exit $LASTEXITCODE). The signed-in account needs Storage Blob Data Contributor on that container."
        }
        $length = (& $az.Source storage blob show --auth-mode login --account-name $StorageAccountName --container-name $containerName `
                --name $blobName --query 'properties.contentLength' --output tsv --only-show-errors | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $length -ne [string]$payload.Length) {
            throw "The stored sign-in did not read back as written ($length of $($payload.Length) bytes)."
        }
    }
    finally {
        if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file -Force }
    }
    Write-Output "Stored for Production in $StorageAccountName/$containerName/$blobName ($($payload.Length) bytes)."
}

if ($Target -contains 'Production') { Write-ProductionSignIn }
if ($Target -notcontains 'TestInstance') { exit 0 }

# The test instance's Azurite account: its key is the tracked development
# constant of the run lifecycle, and the az CLI's API version is refused there.
$lifecycle = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Invoke-LocalDevelopment.ps1') -Raw
if ($lifecycle -notmatch "\`$azuriteAccountName = '([^']+)'") { throw 'The Azurite account name was not found.' }
$accountName = $Matches[1]
if ($lifecycle -notmatch "\`$azuriteAccountKey = '([^']+)'") { throw 'The Azurite account key was not found.' }
$accountKey = [Convert]::FromBase64String($Matches[1])
$manifest = Get-Content -LiteralPath $RunManifestPath -Raw | ConvertFrom-Json -Depth 20
$blobEndpoint = ([string]$manifest.endpoints.azuriteBlob).TrimEnd('/')
if ($blobEndpoint -notmatch '^http://127\.0\.0\.1:[0-9]+/') { throw 'The run manifest does not name a loopback Azurite blob endpoint.' }

function Invoke-Azurite {
    param([Parameter(Mandatory)][string]$Method, [Parameter(Mandatory)][string]$Path, [hashtable]$Query = @{}, [byte[]]$Body, [hashtable]$Extra = @{})

    $headers = [ordered]@{ 'x-ms-date' = [DateTimeOffset]::UtcNow.ToString('R'); 'x-ms-version' = '2021-08-06' }
    foreach ($entry in $Extra.GetEnumerator()) { $headers[$entry.Key] = $entry.Value }
    $hasBody = $null -ne $Body -and $Body.Length -gt 0
    $contentLength = if ($hasBody) { [string]$Body.Length } else { '' }
    $contentType = if ($hasBody) { 'application/json' } else { '' }
    $canonicalHeaders = (($headers.GetEnumerator() | Where-Object { $_.Key -like 'x-ms-*' } | Sort-Object Key |
            ForEach-Object { "$($_.Key):$($_.Value)`n" }) -join '')
    $canonicalResource = "/$accountName/$accountName$Path" + (($Query.GetEnumerator() | Sort-Object Key |
            ForEach-Object { "`n$($_.Key):$($_.Value)" }) -join '')
    $toSign = (@($Method, '', '', $contentLength, '', $contentType, '', '', '', '', '', '') -join "`n") + "`n" + $canonicalHeaders + $canonicalResource
    $hmac = [System.Security.Cryptography.HMACSHA256]::new($accountKey)
    try { $signature = [Convert]::ToBase64String($hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($toSign))) }
    finally { $hmac.Dispose() }

    $uri = $blobEndpoint + $Path
    if ($Query.Count -gt 0) { $uri += '?' + (($Query.GetEnumerator() | ForEach-Object { "$($_.Key)=$($_.Value)" }) -join '&') }
    $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::new($Method), $uri)
    foreach ($entry in $headers.GetEnumerator()) { [void]$request.Headers.TryAddWithoutValidation($entry.Key, [string]$entry.Value) }
    [void]$request.Headers.TryAddWithoutValidation('Authorization', "SharedKey $accountName`:$signature")
    if ($hasBody) {
        $request.Content = [System.Net.Http.ByteArrayContent]::new($Body)
        $request.Content.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::new('application/json')
    }
    elseif ($Method -eq 'PUT') {
        $request.Content = [System.Net.Http.ByteArrayContent]::new([byte[]]::new(0))
    }
    $client = [System.Net.Http.HttpClient]::new()
    try {
        $response = $client.Send($request)
        $bytes = $response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
        return [pscustomobject]@{ Status = [int]$response.StatusCode; Bytes = $bytes }
    }
    finally {
        $request.Dispose()
        $client.Dispose()
    }
}

$created = Invoke-Azurite -Method 'PUT' -Path "/$containerName" -Query @{ restype = 'container' }
if ($created.Status -notin 201, 409) { throw "The Azurite container could not be created (status $($created.Status))." }
$put = Invoke-Azurite -Method 'PUT' -Path "/$containerName/$blobName" -Body $payload -Extra @{ 'x-ms-blob-type' = 'BlockBlob' }
if ($put.Status -ne 201) { throw "The sign-in could not be written to the run's Azurite account (status $($put.Status))." }
$read = Invoke-Azurite -Method 'GET' -Path "/$containerName/$blobName"
if ($read.Status -ne 200 -or -not [System.Linq.Enumerable]::SequenceEqual([byte[]]$read.Bytes, [byte[]]$payload)) {
    throw "The stored sign-in did not read back as written (status $($read.Status))."
}
Write-Output "Stored for the test instance in $containerName/$blobName ($($payload.Length) bytes)."
