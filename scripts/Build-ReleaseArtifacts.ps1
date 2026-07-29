[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$SourceRevision,

    [ValidateSet('OfflineReplay')]
    [string]$Mode = 'OfflineReplay',

    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/release')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$requiredLocks = @(
    '.config/dotnet-tools.json',
    'src/Pegasus.Core/packages.lock.json',
    'src/Pegasus.Infrastructure/packages.lock.json',
    'src/Pegasus.Web/packages.lock.json',
    'src/Pegasus.Worker/packages.lock.json'
)

function Resolve-RepositorySourceRevision {
    param([Parameter(Mandatory)][string]$RequestedRevision)

    $git = Get-Command -Name git -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $git) {
        throw 'Git is required to bind release artifacts to the executed checkout.'
    }

    $headOutput = @(& $git.Source -C $repositoryRoot rev-parse --verify 'HEAD^{commit}' 2>$null)
    if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) {
        throw "The repository HEAD could not be resolved at '$repositoryRoot'."
    }

    $headRevision = ([string]$headOutput[0]).Trim().ToLowerInvariant()
    if ($headRevision -notmatch '^[0-9a-f]{40}$') {
        throw "The repository HEAD is not a 40-character Git source revision: '$headRevision'."
    }

    $requested = $RequestedRevision.ToLowerInvariant()
    $requestedOutput = @(
        & $git.Source -C $repositoryRoot rev-parse --verify "$requested^{commit}" 2>$null
    )
    if ($LASTEXITCODE -ne 0 -or $requestedOutput.Count -ne 1) {
        throw "SourceRevision '$RequestedRevision' does not unambiguously identify a commit in the executed checkout."
    }

    $resolvedRequested = ([string]$requestedOutput[0]).Trim().ToLowerInvariant()
    if ($resolvedRequested -notmatch '^[0-9a-f]{40}$') {
        throw "SourceRevision '$RequestedRevision' did not resolve to a 40-character Git commit."
    }
    if ($resolvedRequested -cne $headRevision) {
        throw "SourceRevision '$RequestedRevision' resolves to '$resolvedRequested', but the executed checkout HEAD is '$headRevision'."
    }

    $workingTreeState = @(
        & $git.Source -C $repositoryRoot status --porcelain=v1 --untracked-files=all -- . 2>$null
    )
    if ($LASTEXITCODE -ne 0) {
        throw "The working-tree state could not be verified at '$repositoryRoot'."
    }
    if ($workingTreeState.Count -ne 0) {
        throw "The executed checkout has tracked or untracked changes. Commit or remove them before producing revision-bound release artifacts."
    }

    return $headRevision
}

function Assert-RequiredFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Join-Path $repositoryRoot $RelativePath
    if (-not [System.IO.File]::Exists($path)) {
        throw "Required locked input is missing: $RelativePath"
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)][string]$Operation,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE. The release directory was not created."
    }
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory)][string]$SourceDirectory,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    $sourceRoot = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $files = [System.Collections.Generic.List[string]]::new()
    foreach ($file in [System.IO.Directory]::EnumerateFiles($sourceRoot, '*', [System.IO.SearchOption]::AllDirectories)) {
        $files.Add($file)
    }
    $files.Sort([System.StringComparer]::Ordinal)

    if (@($files).Count -eq 0) {
        throw "Cannot create a release archive from an empty directory: $SourceDirectory"
    }

    $archive = [System.IO.Compression.ZipFile]::Open($DestinationPath, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($file in $files) {
            $relativePath = $file.Substring($sourceRoot.Length).TrimStart('\', '/').Replace('\', '/')
            $entry = $archive.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [System.DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [System.TimeSpan]::Zero)

            $input = [System.IO.File]::OpenRead($file)
            try {
                $output = $entry.Open()
                try {
                    $input.CopyTo($output)
                }
                finally {
                    $output.Dispose()
                }
            }
            finally {
                $input.Dispose()
            }
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-ArtifactRecord {
    param([Parameter(Mandatory)][string]$Path)

    $file = [System.IO.FileInfo]::new($Path)
    [ordered]@{
        fileName = $file.Name
        byteLength = $file.Length
        sha256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}

function Get-PublishedWebDiagnostic {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    $assemblyPath = Join-Path $PublishDirectory 'Pegasus.Web.dll'
    if (-not [System.IO.File]::Exists($assemblyPath)) {
        throw "Published Web assembly is missing: $assemblyPath"
    }

    $diagnosticOutput = @(& dotnet $assemblyPath '--diagnostics-version')
    if ($LASTEXITCODE -ne 0) {
        throw "Published Web build diagnostic failed with exit code $LASTEXITCODE."
    }

    try {
        $diagnostic = ($diagnosticOutput -join [System.Environment]::NewLine) |
            ConvertFrom-Json
    }
    catch {
        throw 'Published Web build diagnostic did not return valid JSON.'
    }

    $requiredProperties = @('schemaVersion', 'version', 'sourceSha')
    foreach ($property in $requiredProperties) {
        if ($diagnostic.PSObject.Properties.Name -notcontains $property) {
            throw "Published Web build diagnostic is missing '$property'."
        }
    }
    if ($diagnostic.schemaVersion -ne 1 -or
        [string]::IsNullOrWhiteSpace([string]$diagnostic.version) -or
        [string]$diagnostic.sourceSha -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Published Web build diagnostic has invalid version or source metadata.'
    }

    return $diagnostic
}


if ($Mode -ne 'OfflineReplay') {
    throw "Only the OfflineReplay release mode is supported. Cloud activation is intentionally unavailable."
}

$resolvedSourceRevision = Resolve-RepositorySourceRevision -RequestedRevision $SourceRevision

if ($null -eq (Get-Command -Name dotnet -CommandType Application -ErrorAction SilentlyContinue)) {
    throw 'dotnet is required to build locked offline release artifacts.'
}

foreach ($lock in $requiredLocks) {
    Assert-RequiredFile -RelativePath $lock
}

if ([System.IO.Directory]::Exists($outputRoot) -or [System.IO.File]::Exists($outputRoot)) {
    throw "Release output path already exists: $outputRoot. Use a new empty path so a replay cannot mix artifacts."
}

$parentDirectory = Split-Path -Parent $outputRoot
if ([string]::IsNullOrWhiteSpace($parentDirectory)) {
    throw "OutputDirectory must include a parent directory: $OutputDirectory"
}

[System.IO.Directory]::CreateDirectory($parentDirectory) | Out-Null
$stagingRoot = "$outputRoot.staging-$([System.Guid]::NewGuid().ToString('N'))"

try {
    $webPublishDirectory = Join-Path $stagingRoot 'publish/web'
    $workerPublishDirectory = Join-Path $stagingRoot 'publish/worker'
    $migrationDirectory = Join-Path $stagingRoot 'publish/migration'
    $artifactDirectory = Join-Path $stagingRoot 'artifacts'
    [System.IO.Directory]::CreateDirectory($webPublishDirectory) | Out-Null
    [System.IO.Directory]::CreateDirectory($workerPublishDirectory) | Out-Null
    [System.IO.Directory]::CreateDirectory($migrationDirectory) | Out-Null
    [System.IO.Directory]::CreateDirectory($artifactDirectory) | Out-Null
    $offlineNuGetConfig = Join-Path $stagingRoot 'offline-nuget.config'
    @'
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
  </packageSources>
</configuration>
'@ | Set-Content -LiteralPath $offlineNuGetConfig -Encoding utf8NoBOM

    Push-Location $repositoryRoot
    try {
        Invoke-DotNet -Operation 'Locked package restore' -Arguments @('restore', 'Pegasus.slnx', '--locked-mode', '--configfile', $offlineNuGetConfig, '--nologo')
        Invoke-DotNet -Operation 'Locked tool restore' -Arguments @('tool', 'restore', '--configfile', $offlineNuGetConfig)
        $sourceRevisionProperty = "/p:SourceRevisionId=$resolvedSourceRevision"
        $includeSourceRevisionProperty = '/p:IncludeSourceRevisionInInformationalVersion=true'
        Invoke-DotNet -Operation 'Web publish' -Arguments @('publish', 'src/Pegasus.Web/Pegasus.Web.csproj', '--configuration', 'Release', '--no-restore', '--nologo', '--output', $webPublishDirectory, '/p:ContinuousIntegrationBuild=true', '/p:UseAppHost=false', $includeSourceRevisionProperty, $sourceRevisionProperty)
        $webDiagnostic = Get-PublishedWebDiagnostic -PublishDirectory $webPublishDirectory
        if ([string]$webDiagnostic.sourceSha -cne $resolvedSourceRevision) {
            throw "Published Web source SHA '$($webDiagnostic.sourceSha)' does not match the executed checkout '$resolvedSourceRevision'."
        }
        Invoke-DotNet -Operation 'Worker publish' -Arguments @('publish', 'src/Pegasus.Worker/Pegasus.Worker.csproj', '--configuration', 'Release', '--no-restore', '--nologo', '--output', $workerPublishDirectory, '/p:ContinuousIntegrationBuild=true', '/p:UseAppHost=false', $includeSourceRevisionProperty, $sourceRevisionProperty)
        Invoke-DotNet -Operation 'Idempotent migration bundle generation' -Arguments @('ef', 'migrations', 'script', '--idempotent', '--no-build', '--configuration', 'Release', '--project', 'src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj', '--startup-project', 'src/Pegasus.Web/Pegasus.Web.csproj', '--output', (Join-Path $migrationDirectory 'migration.sql'))
    }
    finally {
        Pop-Location
    }


    New-DeterministicZip -SourceDirectory $webPublishDirectory -DestinationPath (Join-Path $artifactDirectory 'web.zip')
    New-DeterministicZip -SourceDirectory $workerPublishDirectory -DestinationPath (Join-Path $artifactDirectory 'worker.zip')
    New-DeterministicZip -SourceDirectory $migrationDirectory -DestinationPath (Join-Path $artifactDirectory 'migration.zip')

    $manifest = [ordered]@{
        schemaVersion = 1
        releaseMode = 'offline-replay'
        sourceRevision = $resolvedSourceRevision
        webDiagnostic = [ordered]@{
            schemaVersion = [int]$webDiagnostic.schemaVersion
            version = [string]$webDiagnostic.version
            sourceSha = [string]$webDiagnostic.sourceSha
        }
        runtimes = [ordered]@{
            web = 'portable-net10.0'
            worker = 'portable-dotnet-isolated-net10.0'
            migration = 'idempotent-sql'
        }
        artifacts = @(
            (Get-ArtifactRecord -Path (Join-Path $artifactDirectory 'web.zip')),
            (Get-ArtifactRecord -Path (Join-Path $artifactDirectory 'worker.zip')),
            (Get-ArtifactRecord -Path (Join-Path $artifactDirectory 'migration.zip'))
        )
    }

    $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $artifactDirectory 'release-manifest.json') -Encoding utf8NoBOM
    Move-Item -LiteralPath $artifactDirectory -Destination $outputRoot
}
finally {
    if ([System.IO.Directory]::Exists($stagingRoot)) {
        Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    }
}

Write-Output "Offline replay release artifacts written to '$outputRoot'. No Azure authentication, provisioning, or deployment was performed."
