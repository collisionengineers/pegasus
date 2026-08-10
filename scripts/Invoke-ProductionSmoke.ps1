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
    [ValidatePattern('^pegasus-prod-worker-[a-z0-9]{10}$')]
    [string] $WorkerAppName,

    [Parameter(Mandatory)]
    [ValidateSet('disabled', 'approved-live-worker')]
    [string] $ExpectedWorkerActivation,

    [Parameter(Mandatory, ParameterSetName = 'WorkerOnly')]
    [switch] $WorkerOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedWorkerSettings = @(
    'AzureWebJobs.PendingWorkDispatchFunction.Disabled',
    'AzureWebJobs.IntakeWorkFunction.Disabled',
    'AzureWebJobs.IntakePoisonFunction.Disabled',
    'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled',
    'AzureWebJobs.InboxPollFunction.Disabled',
    'AzureWebJobs.SentEvidencePollFunction.Disabled',
    'AzureWebJobs.DueWorkSweepFunction.Disabled',
    'AzureWebJobs.ExternalWorkFunction.Disabled',
    'AzureWebJobs.ExternalPoisonFunction.Disabled'
)

$settingsJson = (& az functionapp config appsettings list `
    --resource-group $ResourceGroupName `
    --name $WorkerAppName `
    --query "[?starts_with(name, 'AzureWebJobs.')].{name:name,value:value}" `
    --output json) -join "`n"
if ($LASTEXITCODE -ne 0) {
    throw "Unable to read Worker app settings from $ResourceGroupName/$WorkerAppName."
}

$allSettings = @($settingsJson | ConvertFrom-Json)
$workerSettings = @(
    $allSettings | Where-Object {
        $_.name -match '^AzureWebJobs\.[A-Za-z0-9]+\.Disabled$'
    }
)
$actualNames = @($workerSettings | ForEach-Object { [string]$_.name })
$censusDifference = @(
    Compare-Object -ReferenceObject $expectedWorkerSettings -DifferenceObject $actualNames
)
if ($workerSettings.Count -ne $expectedWorkerSettings.Count -or $censusDifference.Count -ne 0) {
    throw 'The live Worker disabled-setting census differs from the exact nine-function release contract.'
}

$expectedDisabledValue = if ($ExpectedWorkerActivation -eq 'approved-live-worker') {
    'false'
}
else {
    'true'
}
$unexpectedValues = @(
    $workerSettings | Where-Object { [string]$_.value -cne $expectedDisabledValue }
)
if ($unexpectedValues.Count -ne 0) {
    throw "The live Worker settings do not match the intended '$ExpectedWorkerActivation' activation value."
}

Write-Output "Production Worker activation smoke passed ($ExpectedWorkerActivation)."
if ($WorkerOnly) {
    return
}

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
    $anonymous = $client.GetAsync([uri]::new($BaseUri, 'Cases')).GetAwaiter().GetResult()
    if ($anonymous.StatusCode -notin @([Net.HttpStatusCode]::Redirect, [Net.HttpStatusCode]::Unauthorized, [Net.HttpStatusCode]::Forbidden)) {
        throw "The authenticated Cases surface was anonymously accessible ($([int]$anonymous.StatusCode))."
    }
    if ($anonymous.StatusCode -eq [Net.HttpStatusCode]::Redirect -and $anonymous.Headers.Location.Scheme -ne 'https') {
        throw "The sign-in redirect downgraded to $($anonymous.Headers.Location.Scheme) (forwarded headers are not applied)."
    }
    Write-Output 'Production smoke passed.'
}
finally {
    $client.Dispose()
}
