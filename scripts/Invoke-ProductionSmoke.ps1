[CmdletBinding(DefaultParameterSetName = 'WebAndWorker')]
param(
    [Parameter(Mandatory, ParameterSetName = 'WebAndWorker')]
    [ValidatePattern('^https://')]
    [uri] $BaseUri,

    [Parameter(Mandatory, ParameterSetName = 'WebAndWorker')]
    [ValidatePattern('^[0-9a-f]{40}$')]
    [string] $ExpectedSourceRevision,

    [Parameter(Mandatory, ParameterSetName = 'WebAndWorker')]
    [string] $ExpectedVersion,

    [Parameter(Mandatory)]
    [ValidatePattern('^rg-pegasus-prod$')]
    [string] $ResourceGroupName,

    [Parameter(Mandatory)]
    [ValidateSet('e6076573-23a5-46a8-acef-7e22d264e5db')]
    [string] $SubscriptionId,

    [Parameter(Mandatory)]
    [ValidateSet('disabled', 'approved-live-worker')]
    [string] $ExpectedWorkerActivation,

    [Parameter(Mandatory, ParameterSetName = 'WorkerOnly')]
    [switch] $WorkerOnly,

    [Parameter(ParameterSetName = 'WorkerOnly')]
    [switch] $ActivationOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedWorkerSettings = @(
    'AzureWebJobs.PendingWorkRecoveryFunction.Disabled',
    'AzureWebJobs.UnifiedWorkFunction.Disabled',
    'AzureWebJobs.UnifiedWorkPoisonFunction.Disabled',
    'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled',
    'AzureWebJobs.InboxRecoveryFunction.Disabled',
    'AzureWebJobs.SentEvidencePollFunction.Disabled',
    'AzureWebJobs.DueWorkSweepFunction.Disabled'
)
$workerAppName = 'pegasus-prod-worker-252ow37gij'

$settingsJson = (& az functionapp config appsettings list `
    --subscription $SubscriptionId `
    --resource-group $ResourceGroupName `
    --name $workerAppName `
    --query "[?starts_with(name, 'AzureWebJobs.') && ends_with(name, '.Disabled')].{name:name,value:value}" `
    --output json) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Worker app settings from $ResourceGroupName/$workerAppName."
}

$workerSettings = @($settingsJson | ConvertFrom-Json)
if ($workerSettings.Count -eq 0) {
    throw 'The live Worker has no AzureWebJobs settings to validate.'
}

$expectedNames = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal
)
foreach ($expectedName in $expectedWorkerSettings) {
    [void]$expectedNames.Add($expectedName)
}
$actualNames = [Collections.Generic.HashSet[string]]::new(
    [StringComparer]::Ordinal
)
$censusIsExact = $workerSettings.Count -eq $expectedWorkerSettings.Count
foreach ($setting in $workerSettings) {
    $nameProperty = $setting.PSObject.Properties['name']
    if ($null -eq $nameProperty) {
        $censusIsExact = $false
        continue
    }

    $name = [string]$nameProperty.Value
    if (-not $expectedNames.Contains($name) -or -not $actualNames.Add($name)) {
        $censusIsExact = $false
    }
}
foreach ($expectedName in $expectedWorkerSettings) {
    if (-not $actualNames.Contains($expectedName)) {
        $censusIsExact = $false
    }
}
if (-not $ActivationOnly -and -not $censusIsExact) {
    throw 'The live Worker disabled-setting census differs from the exact seven-function release contract.'
}

$expectedDisabledValue = if ($ExpectedWorkerActivation -eq 'approved-live-worker') {
    'false'
}
else {
    'true'
}
$valuesAreExact = $true
foreach ($setting in $workerSettings) {
    $valueProperty = $setting.PSObject.Properties['value']
    if ($null -eq $valueProperty -or
        -not [StringComparer]::Ordinal.Equals(
            [string]$valueProperty.Value,
            $expectedDisabledValue
        )) {
        $valuesAreExact = $false
    }
}
if (-not $valuesAreExact) {
    throw "The live Worker settings do not match the intended '$ExpectedWorkerActivation' activation value."
}

Write-Output "Production Worker activation smoke passed ($ExpectedWorkerActivation)."
if (-not $ActivationOnly) {
    $recoverySchedule = (& az functionapp config appsettings list `
        --subscription $SubscriptionId `
        --resource-group $ResourceGroupName `
        --name $workerAppName `
        --query "[?name == 'PendingWorkRecoverySchedule'].value | [0]" `
        --output tsv) -join "`n"
    if ($LASTEXITCODE -ne 0 -or
        -not [StringComparer]::Ordinal.Equals($recoverySchedule.Trim(), '0 * * * * *')) {
        throw 'The live PendingWorkRecoverySchedule is not configured to run once per minute.'
    }
}

if ($WorkerOnly) {
    return
}

# Inbox intake liveness (MAIL-019). Releases 33 and 34 passed every gate above
# while no Graph subscription existed and no inbound poll ran, because a
# setting readback proves configuration, not that the recovery timer did its
# job. This reads the live rows (read-only, same access-token pattern as
# Invoke-AzureDatabaseBootstrap.ps1) and fails when an intake mailbox is
# approved but never activated (the release-33 defect), when no unexpired
# Active subscription exists, or when the newest completed poll is older than
# three recovery intervals (15 min, RetainedMail.StaleAfter) — one interval of
# grace is expected right after a deploy.
if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'The SqlServer PowerShell module is required for the inbox intake liveness smoke.'
}
$accessToken = (& az account get-access-token --resource https://database.windows.net/ --query accessToken --output tsv).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($accessToken)) {
    throw 'Unable to obtain an Azure SQL access token from the approved Azure CLI identity.'
}
$intake = Invoke-Sqlcmd `
    -ServerInstance 'tcp:pegasus-prod-sql-252ow37gij.database.windows.net,1433' `
    -Database 'pegasus' `
    -AccessToken $accessToken `
    -AbortOnError `
    -ErrorAction Stop `
    -Query @'
SET NOCOUNT ON;
SELECT
    (SELECT COUNT(*) FROM ApprovedMailboxes
     WHERE State = N'Approved' AND AllowInboundIntake = 1 AND ActivatedAtUtc IS NULL) AS UnactivatedIntakeMailboxes,
    (SELECT COUNT(*) FROM ApprovedMailboxSubscriptions
     WHERE LifecycleState = N'Active' AND ExpiresAtUtc > SYSDATETIMEOFFSET()) AS ActiveSubscriptions,
    (SELECT MIN(ExpiresAtUtc) FROM ApprovedMailboxSubscriptions
     WHERE LifecycleState = N'Active' AND ExpiresAtUtc > SYSDATETIMEOFFSET()) AS EarliestSubscriptionExpiryUtc,
    (SELECT MAX(LastCompletedAtUtc) FROM ApprovedInboxPollStates) AS LastPollCompletedAtUtc,
    SYSDATETIMEOFFSET() AS DatabaseNowUtc;
'@
if ([int]$intake.UnactivatedIntakeMailboxes -ne 0) {
    throw "$($intake.UnactivatedIntakeMailboxes) approved intake mailbox(es) have no ActivatedAtUtc: inbound intake never started."
}
if ([int]$intake.ActiveSubscriptions -eq 0) {
    throw 'No unexpired Active row exists in ApprovedMailboxSubscriptions: the Graph webhook has no subscription.'
}
if ($intake.LastPollCompletedAtUtc -is [DBNull]) {
    throw 'No inbound poll has ever completed (ApprovedInboxPollStates.LastCompletedAtUtc is NULL).'
}
$pollAge = [DateTimeOffset]$intake.DatabaseNowUtc - [DateTimeOffset]$intake.LastPollCompletedAtUtc
if ($pollAge -gt [TimeSpan]::FromMinutes(15)) {
    throw "The newest inbound poll completed $([int]$pollAge.TotalMinutes) minutes ago; the recovery timer is not running."
}
Write-Output "Inbox intake liveness smoke passed (last poll $(([DateTimeOffset]$intake.LastPollCompletedAtUtc).ToString('u')), subscription expires $(([DateTimeOffset]$intake.EarliestSubscriptionExpiryUtc).ToString('u')))."

# Redirects must surface raw: with auto-redirect on, the anonymous-denial
# check would follow the sign-in redirect and mistake the login page's 200
# for anonymous access (it only "passed" before release 3 because the
# broken http:// redirect could not be followed from https).
$handler = [Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(30)
try {
    foreach ($path in @('health/live', 'health/ready')) {
        $response = $client.GetAsync([uri]::new($BaseUri, $path)).GetAwaiter().GetResult()
        if (-not $response.IsSuccessStatusCode) { throw "$path returned $([int]$response.StatusCode)." }
    }
    $version = $client.GetStringAsync([uri]::new($BaseUri, 'diagnostics/version')).GetAwaiter().GetResult() | ConvertFrom-Json
    if ($version.sourceSha -ne $ExpectedSourceRevision -or $version.version -ne $ExpectedVersion) {
        throw 'The deployed version endpoint does not match the immutable release manifest.'
    }
    $validationToken = 'pegasus-release-validation'
    $graphValidation = $client.PostAsync(
        [uri]::new($BaseUri, "hooks/microsoft-graph/mail?validationToken=$validationToken"),
        [Net.Http.StringContent]::new('')).GetAwaiter().GetResult()
    $graphValidationBody = $graphValidation.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if (-not $graphValidation.IsSuccessStatusCode -or $graphValidationBody -cne $validationToken) {
        throw 'The Microsoft Graph webhook validation handshake is not active on the deployed Web revision.'
    }
    $anonymous = $client.GetAsync([uri]::new($BaseUri, 'Cases')).GetAwaiter().GetResult()
    if ($anonymous.StatusCode -notin @([Net.HttpStatusCode]::Redirect, [Net.HttpStatusCode]::Unauthorized, [Net.HttpStatusCode]::Forbidden)) {
        throw "The authenticated Cases surface was anonymously accessible ($([int]$anonymous.StatusCode))."
    }
    if ($anonymous.StatusCode -eq [Net.HttpStatusCode]::Redirect -and $anonymous.Headers.Location.Scheme -ne 'https') {
        throw "The sign-in redirect downgraded to $($anonymous.Headers.Location.Scheme) (forwarded headers are not applied)."
    }
    # The security headers are added only outside Development, so no test in the
    # suite ever sees them and a wrong value reaches production green. That is
    # exactly how frame-ancestors 'none' shipped and silently blocked the
    # same-origin PDF preview (DOCS-011). Asserted here because here is the only
    # place the real header exists.
    $csp = ($anonymous.Headers.GetValues('Content-Security-Policy') | Select-Object -First 1)
    if (-not $csp) { throw 'The deployed response carries no Content-Security-Policy header.' }
    foreach ($directive in @("default-src 'self'", "object-src 'none'", "base-uri 'self'", "frame-ancestors 'self'")) {
        if ($csp -notlike "*$directive*") {
            throw "The deployed Content-Security-Policy is missing ""$directive"": $csp"
        }
    }

    Write-Output 'Production smoke passed.'
}
finally {
    $client.Dispose()
}
