[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^https://')][uri] $BaseUri,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $ExpectedSourceRevision,
    [Parameter(Mandatory)][string] $ExpectedVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
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
