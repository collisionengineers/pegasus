[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^https://')][uri] $BaseUri,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $ExpectedSourceRevision,
    [Parameter(Mandatory)][string] $ExpectedVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$client = [Net.Http.HttpClient]::new()
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
    Write-Output 'Production smoke passed.'
}
finally {
    $client.Dispose()
}
