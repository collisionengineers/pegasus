[CmdletBinding()]
param(
    [ValidatePattern('^[0-9a-fA-F]{7,40}$')]
    [string]$SourceRevision,

    [ValidateSet('Release')]
    [string]$Configuration = 'Release',

    [ValidateSet('linux-x64')]
    [string]$ApplicationRuntime = 'linux-x64',

    [ValidateSet('win-x64')]
    [string]$MigrationRuntime = 'win-x64',

    [ValidateSet('win-x64')]
    [string]$BootstrapRuntime = 'win-x64',

    [Parameter(Mandatory)]
    [string]$BootstrapManifestPath,

    [switch]$VerifyReproducible,

    [string]$OutputDirectory = (Join-Path $PSScriptRoot '../artifacts/release')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$releaseVersion = '0.1.0-alpha.1'
$expectedMigrationId = '20260729199000_RuntimeRoleReconciliation'
$manifestFileName = 'release-manifest.json'
$manifestDigestFileName = 'release-manifest.sha256'
$webArtifactName = 'web-linux-x64.zip'
$workerArtifactName = 'worker-linux-x64.zip'
$migrationArtifactName = 'migration-bundle-win-x64.zip'
$bootstrapArtifactName = 'bootstrap-win-x64.zip'
$deploymentArtifactName = 'azure-deployment-inputs.zip'
$expectedWorkerFunctions = @(
    'InboxPollFunction',
    'SentEvidencePollFunction',
    'PendingWorkDispatchFunction',
    'IntakeWorkFunction',
    'ExternalWorkFunction',
    'IntakePoisonFunction',
    'ExternalPoisonFunction',
    'StagedArtifactReconciliationFunction',
    'DueWorkSweepFunction'
)
$deploymentInputPaths = @(
    'azure.yaml',
    'infra/main.bicep',
    'infra/main.parameters.json',
    'infra/modules/platform.bicep'
)
$provenanceInputPaths = @(
    '.config/dotnet-tools.json',
    'Directory.Build.props',
    'global.json',
    'package.json',
    'package-lock.json',
    'Pegasus.slnx',
    'src/Pegasus.Core/Pegasus.Core.csproj',
    'src/Pegasus.Core/packages.lock.json',
    'src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj',
    'src/Pegasus.Infrastructure/packages.lock.json',
    'src/Pegasus.Web/Pegasus.Web.csproj',
    'src/Pegasus.Web/packages.lock.json',
    'src/Pegasus.Worker/Pegasus.Worker.csproj',
    'src/Pegasus.Worker/packages.lock.json',
    'src/Pegasus.Bootstrap/Pegasus.Bootstrap.csproj',
    'src/Pegasus.Bootstrap/packages.lock.json'
)
$releaseInputPaths = @($provenanceInputPaths + $deploymentInputPaths)
$expectedOutputNames = @(
    $webArtifactName,
    $workerArtifactName,
    $migrationArtifactName,
    $bootstrapArtifactName,
    $deploymentArtifactName,
    $manifestFileName,
    $manifestDigestFileName
)

function Get-RequiredApplication {
    param([Parameter(Mandatory)][string]$Name)

    $command = Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "$Name is required to build revision-bound release artifacts."
    }

    return $command
}

function Assert-RequiredFile {
    param([Parameter(Mandatory)][string]$RelativePath)

    $path = Join-Path $repositoryRoot $RelativePath
    if (-not [System.IO.File]::Exists($path)) {
        throw "Required release input is missing: $RelativePath"
    }
}

function Get-FileSha256 {
    param([Parameter(Mandatory)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        return [Convert]::ToHexString(
            [System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
    }
}

function Get-CanonicalTextBytes {
    param([Parameter(Mandatory)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    $offset = if ($bytes.Length -ge 3 -and
        $bytes[0] -eq 0xEF -and
        $bytes[1] -eq 0xBB -and
        $bytes[2] -eq 0xBF) {
        3
    }
    else {
        0
    }
    try {
        $text = [System.Text.UTF8Encoding]::new($false, $true).GetString(
            $bytes,
            $offset,
            $bytes.Length - $offset)
    }
    catch {
        throw "Release text input is not valid UTF-8: $Path"
    }

    $canonicalText = $text.Replace("`r`n", "`n").Replace("`r", "`n")
    return ,([System.Text.UTF8Encoding]::new($false).GetBytes($canonicalText))
}

function Get-ByteArraySha256 {
    param([Parameter(Mandatory)][byte[]]$Bytes)

    return [Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($Bytes)).ToLowerInvariant()
}

function Assert-ExactNames {
    param(
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Actual,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]]$Expected,
        [Parameter(Mandatory)][string]$Description
    )

    $actualNames = [string[]]@($Actual)
    $expectedNames = [string[]]@($Expected)
    [Array]::Sort($actualNames, [System.StringComparer]::Ordinal)
    [Array]::Sort($expectedNames, [System.StringComparer]::Ordinal)

    $matches = $actualNames.Length -eq $expectedNames.Length
    if ($matches) {
        for ($index = 0; $index -lt $expectedNames.Length; $index++) {
            if ($actualNames[$index] -cne $expectedNames[$index]) {
                $matches = $false
                break
            }
        }
    }

    if (-not $matches) {
        throw "$Description must be exact. Expected: $($expectedNames -join ', '). Actual: $($actualNames -join ', ')."
    }
}

function Resolve-RepositorySourceRevision {
    param([string]$RequestedRevision)

    $headOutput = @(
        & $gitCommand.Source -C $repositoryRoot rev-parse --verify 'HEAD^{commit}' 2>$null
    )
    if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) {
        throw "The repository HEAD could not be resolved at '$repositoryRoot'."
    }

    $headRevision = ([string]$headOutput[0]).Trim().ToLowerInvariant()
    if ($headRevision -notmatch '^[0-9a-f]{40}$') {
        throw "The repository HEAD is not a 40-character Git source revision: '$headRevision'."
    }

    if (-not [string]::IsNullOrWhiteSpace($RequestedRevision)) {
        $requested = $RequestedRevision.ToLowerInvariant()
        $requestedOutput = @(
            & $gitCommand.Source -C $repositoryRoot rev-parse --verify "$requested^{commit}" 2>$null
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
    }

    Assert-CleanRepositoryRevision -ExpectedRevision $headRevision
    return $headRevision
}

function Assert-CleanRepositoryRevision {
    param([Parameter(Mandatory)][string]$ExpectedRevision)

    $headOutput = @(
        & $gitCommand.Source -C $repositoryRoot rev-parse --verify 'HEAD^{commit}' 2>$null
    )
    if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) {
        throw "The repository HEAD could not be reverified at '$repositoryRoot'."
    }

    $observedRevision = ([string]$headOutput[0]).Trim().ToLowerInvariant()
    if ($observedRevision -cne $ExpectedRevision) {
        throw "The checked-out source revision changed during release packaging from '$ExpectedRevision' to '$observedRevision'."
    }

    $workingTreeState = @(
        & $gitCommand.Source -C $repositoryRoot status --porcelain=v1 --untracked-files=all -- . 2>$null
    )
    if ($LASTEXITCODE -ne 0) {
        throw "The working-tree state could not be verified at '$repositoryRoot'."
    }
    if ($workingTreeState.Count -ne 0) {
        throw 'The executed checkout has tracked or untracked changes. Commit or remove them before producing revision-bound release artifacts.'
    }
}

function Get-SourceCommitUnixTime {
    param([Parameter(Mandatory)][string]$Revision)

    $output = @(
        & $gitCommand.Source -C $repositoryRoot show -s '--format=%ct' $Revision 2>$null
    )
    if ($LASTEXITCODE -ne 0 -or $output.Count -ne 1) {
        throw "The commit timestamp could not be resolved for source revision '$Revision'."
    }

    $timestamp = ([string]$output[0]).Trim()
    if ($timestamp -notmatch '^[1-9][0-9]*$') {
        throw "The commit timestamp for source revision '$Revision' is invalid."
    }

    return $timestamp
}

function Get-GitBlobBytes {
    param([Parameter(Mandatory)][string]$ObjectId)

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $gitCommand.Source
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @('-C', $repositoryRoot, 'cat-file', 'blob', $ObjectId)) {
        [void]$startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $memory = [System.IO.MemoryStream]::new()
    try {
        if (-not $process.Start()) {
            throw "Git could not read release-input blob '$ObjectId'."
        }
        $errorRead = $process.StandardError.ReadToEndAsync()
        $process.StandardOutput.BaseStream.CopyTo($memory)
        $process.WaitForExit()
        $errorText = $errorRead.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) {
            throw "Git could not read release-input blob '$ObjectId': $errorText"
        }
        return ,$memory.ToArray()
    }
    finally {
        $memory.Dispose()
        $process.Dispose()
    }
}

function Get-ReleaseInputTreeRecord {
    param([Parameter(Mandatory)][string]$Revision)

    $treeOutput = @(
        & $gitCommand.Source -C $repositoryRoot -c core.quotepath=false ls-tree -r --full-tree $Revision 2>$null
    )
    if ($LASTEXITCODE -ne 0) {
        throw "The tracked release-input tree could not be enumerated for '$Revision'."
    }

    $excludedCounts = [ordered]@{
        'docs/changes/2026-07-27-qdos-alpha-reference-corpora.md' = 0
        'docs/reference/imp-docs/' = 0
        'corpus/' = 0
        'artifacts/' = 0
    }
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash(
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $includedPathCount = 0
    try {
        $hash.AppendData($utf8.GetBytes("Pegasus.ReleaseInputTree/v1`n"))
        foreach ($lineValue in $treeOutput) {
            $line = [string]$lineValue
            $tabIndex = $line.IndexOf("`t", [System.StringComparison]::Ordinal)
            if ($tabIndex -le 0 -or $tabIndex -eq $line.Length - 1) {
                throw "Git returned an invalid release-input tree record: '$line'."
            }
            $metadata = $line.Substring(0, $tabIndex).Split(
                ' ',
                [System.StringSplitOptions]::RemoveEmptyEntries)
            $path = $line.Substring($tabIndex + 1)
            if ($metadata.Count -ne 3 -or
                $metadata[0] -notmatch '^[0-9]{6}$' -or
                $metadata[1] -cne 'blob' -or
                $metadata[2] -notmatch '^[0-9a-f]{40,64}$' -or
                $path -match '[\x00-\x1f]' -or
                $path.StartsWith('/', [System.StringComparison]::Ordinal) -or
                $path -match '(^|/)\.\.(/|$)' -or
                $path -match '\\') {
                throw "Git returned an unsafe release-input tree record: '$line'."
            }

            $excludedKey = $null
            if ($path -ceq 'docs/changes/2026-07-27-qdos-alpha-reference-corpora.md') {
                $excludedKey = 'docs/changes/2026-07-27-qdos-alpha-reference-corpora.md'
            }
            elseif ($path.StartsWith(
                'docs/reference/imp-docs/',
                [System.StringComparison]::Ordinal)) {
                $excludedKey = 'docs/reference/imp-docs/'
            }
            elseif ($path.StartsWith('corpus/', [System.StringComparison]::Ordinal)) {
                $excludedKey = 'corpus/'
            }
            elseif ($path.StartsWith('artifacts/', [System.StringComparison]::Ordinal)) {
                $excludedKey = 'artifacts/'
            }
            if ($null -ne $excludedKey) {
                $excludedCounts[$excludedKey] = [int]$excludedCounts[$excludedKey] + 1
                continue
            }

            $blobBytes = Get-GitBlobBytes -ObjectId $metadata[2]
            $recordHeader = "$($metadata[0])`0$path`0$($blobBytes.Length)`0"
            $hash.AppendData($utf8.GetBytes($recordHeader))
            $hash.AppendData($blobBytes)
            $hash.AppendData([byte[]]@(0))
            $includedPathCount++
        }

        return [pscustomobject][ordered]@{
            schema = 'tracked-path-mode-file-bytes-v1'
            algorithm = 'sha256'
            sha256 = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
            includedPathCount = $includedPathCount
            excludedPathCounts = [pscustomobject]$excludedCounts
        }
    }
    finally {
        $hash.Dispose()
    }
}

function Invoke-DotNet {
    param(
        [Parameter(Mandatory)][string]$Operation,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    & $dotnetCommand.Source @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Operation failed with exit code $LASTEXITCODE. No release directory was created."
    }
}

function Get-PinnedReleaseMetadata {
    $buildProperties = [xml][System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'Directory.Build.props'))
    $versionNodes = @($buildProperties.SelectNodes('/Project/PropertyGroup/Version'))
    if ($versionNodes.Count -ne 1 -or [string]$versionNodes[0].InnerText -cne $releaseVersion) {
        throw "Directory.Build.props must declare the exact release version '$releaseVersion'."
    }

    $package = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'package.json')) | ConvertFrom-Json -Depth 20
    if ([string]$package.version -cne $releaseVersion) {
        throw "package.json must declare the exact release version '$releaseVersion'."
    }

    $packageLock = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'package-lock.json')) | ConvertFrom-Json -Depth 100
    $rootPackageProperty = @(
        $packageLock.packages.PSObject.Properties |
            Where-Object { $_.Name -ceq '' }
    )
    if ([string]$packageLock.version -cne $releaseVersion -or
        $rootPackageProperty.Count -ne 1 -or
        [string]$rootPackageProperty[0].Value.version -cne $releaseVersion) {
        throw "package-lock.json must declare the exact release version '$releaseVersion' at both package roots."
    }

    foreach ($projectPath in @(
        'src/Pegasus.Core/Pegasus.Core.csproj',
        'src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj',
        'src/Pegasus.Web/Pegasus.Web.csproj',
        'src/Pegasus.Worker/Pegasus.Worker.csproj',
        'src/Pegasus.Bootstrap/Pegasus.Bootstrap.csproj')) {
        $project = [xml][System.IO.File]::ReadAllText((Join-Path $repositoryRoot $projectPath))
        foreach ($projectVersion in @($project.SelectNodes('/Project/PropertyGroup/Version'))) {
            if ([string]$projectVersion.InnerText -cne $releaseVersion) {
                throw "$projectPath overrides the release version with '$($projectVersion.InnerText)'."
            }
        }
    }

    $globalJson = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'global.json')) | ConvertFrom-Json -Depth 20
    $pinnedSdkVersion = [string]$globalJson.sdk.version
    if ($pinnedSdkVersion -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
        throw 'global.json does not contain an exact SDK version.'
    }

    $toolManifest = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot '.config/dotnet-tools.json')) | ConvertFrom-Json -Depth 20
    $efTool = $toolManifest.tools.PSObject.Properties['dotnet-ef']
    if ($null -eq $efTool -or [string]$efTool.Value.version -cne '10.0.10') {
        throw '.config/dotnet-tools.json must pin dotnet-ef 10.0.10.'
    }

    $sdkOutput = @(& $dotnetCommand.Source --version)
    if ($LASTEXITCODE -ne 0 -or $sdkOutput.Count -ne 1) {
        throw 'The active .NET SDK version could not be read.'
    }
    $activeSdkVersion = ([string]$sdkOutput[0]).Trim()
    if ($activeSdkVersion -cne $pinnedSdkVersion) {
        throw "The active .NET SDK '$activeSdkVersion' does not match global.json '$pinnedSdkVersion'."
    }

    return [pscustomobject][ordered]@{
        dotnetSdk = $pinnedSdkVersion
        dotnetEf = [string]$efTool.Value.version
    }
}

function Get-InputRecord {
    param(
        [Parameter(Mandatory)][string]$RelativePath,
        [Parameter(Mandatory)][ValidateSet('provenance', 'deployment')][string]$Purpose
    )

    $path = Join-Path $repositoryRoot $RelativePath
    $canonicalBytes = Get-CanonicalTextBytes -Path $path
    return [pscustomobject][ordered]@{
        path = $RelativePath.Replace(
            [System.IO.Path]::DirectorySeparatorChar,
            [char]'/')
        purpose = $Purpose
        byteLength = $canonicalBytes.Length
        sha256 = Get-ByteArraySha256 -Bytes $canonicalBytes
    }
}

function New-DeterministicZip {
    param(
        [Parameter(Mandatory)][string]$SourceDirectory,
        [Parameter(Mandatory)][string]$DestinationPath
    )

    if ([System.IO.File]::Exists($DestinationPath) -or
        [System.IO.Directory]::Exists($DestinationPath)) {
        throw "Deterministic archive destination already exists: $DestinationPath"
    }

    $sourceRoot = [System.IO.Path]::GetFullPath($SourceDirectory).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $relativeFiles = [System.Collections.Generic.List[string]]::new()
    foreach ($file in [System.IO.Directory]::EnumerateFiles(
        $sourceRoot,
        '*',
        [System.IO.SearchOption]::AllDirectories)) {
        $relativePath = [System.IO.Path]::GetRelativePath($sourceRoot, $file).Replace(
            [System.IO.Path]::DirectorySeparatorChar,
            [char]'/')
        if ($relativePath.StartsWith('/', [System.StringComparison]::Ordinal) -or
            $relativePath -match '(^|/)\.\.(/|$)' -or
            $relativePath -match '^[A-Za-z]:') {
            throw "Release archive input has an unsafe relative path: $relativePath"
        }
        $relativeFiles.Add($relativePath)
    }

    if ($relativeFiles.Count -eq 0) {
        throw "Cannot create a release archive from an empty directory: $SourceDirectory"
    }

    $files = $relativeFiles.ToArray()
    [Array]::Sort($files, [System.StringComparer]::Ordinal)
    $archive = [System.IO.Compression.ZipFile]::Open(
        $DestinationPath,
        [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($relativePath in $files) {
            $sourcePath = Join-Path $sourceRoot $relativePath.Replace(
                '/',
                [System.IO.Path]::DirectorySeparatorChar)
            $entry = $archive.CreateEntry(
                $relativePath,
                [System.IO.Compression.CompressionLevel]::Optimal)
            $entry.LastWriteTime = [System.DateTimeOffset]::new(
                1980,
                1,
                1,
                0,
                0,
                0,
                [System.TimeSpan]::Zero)
            $entry.ExternalAttributes = 0

            $input = [System.IO.File]::OpenRead($sourcePath)
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
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][string]$Family,
        [Parameter(Mandatory)][string]$RuntimeIdentifier,
        [Parameter(Mandatory)][string]$DeploymentKind
    )

    $file = [System.IO.FileInfo]::new($Path)
    return [pscustomobject][ordered]@{
        family = $Family
        fileName = $file.Name
        runtimeIdentifier = $RuntimeIdentifier
        deploymentKind = $DeploymentKind
        byteLength = $file.Length
        sha256 = Get-FileSha256 -Path $file.FullName
    }
}

function Assert-NoForbiddenPublishFiles {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    foreach ($path in [System.IO.Directory]::EnumerateFileSystemEntries(
        $PublishDirectory,
        '*',
        [System.IO.SearchOption]::AllDirectories)) {
        $attributes = [System.IO.File]::GetAttributes($path)
        if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Release publish output contains a link or reparse point: $path"
        }
        if ([System.IO.Directory]::Exists($path)) {
            continue
        }

        $name = [System.IO.Path]::GetFileName($path)
        if ($name -match '^(?i:appsettings\.Development\.json|local\.settings(?:\..+)?\.json|secrets?\.json|\.env(?:\..*)?)$' -or
            $name -match '(?i:\.(?:pfx|p12|pem|key))$') {
            throw "Release publish output contains a development or secret-bearing file: $name"
        }
    }
}

function Convert-PublishTextFilesToCanonicalUtf8 {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    $textExtensions = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($extension in @(
        '.config',
        '.css',
        '.htm',
        '.html',
        '.js',
        '.json',
        '.map',
        '.mjs',
        '.svg',
        '.txt',
        '.xml',
        '.yaml',
        '.yml')) {
        [void]$textExtensions.Add($extension)
    }

    foreach ($path in [System.IO.Directory]::EnumerateFiles(
        $PublishDirectory,
        '*',
        [System.IO.SearchOption]::AllDirectories)) {
        if (-not $textExtensions.Contains([System.IO.Path]::GetExtension($path))) {
            continue
        }
        [System.IO.File]::WriteAllBytes(
            $path,
            (Get-CanonicalTextBytes -Path $path))
    }
}

function Assert-WebProductionConfiguration {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    $configurationPath = Join-Path $PublishDirectory 'appsettings.json'
    if (-not [System.IO.File]::Exists($configurationPath)) {
        throw 'Published Web output is missing appsettings.json.'
    }

    $configuration = [System.IO.File]::ReadAllText($configurationPath) |
        ConvertFrom-Json -Depth 30
    if ([string]$configuration.Runtime.Profile -cne 'Production') {
        throw 'Published Web appsettings.json must select only the Production runtime profile.'
    }
    if ($configuration.Features.LocalDocumentCustody -isnot [bool] -or
        [bool]$configuration.Features.LocalDocumentCustody) {
        throw 'Published Web appsettings.json must keep local document custody disabled.'
    }
}

function Get-PublishedWebDiagnostic {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    $assemblyPath = Join-Path $PublishDirectory 'Pegasus.Web.dll'
    if (-not [System.IO.File]::Exists($assemblyPath)) {
        throw "Published Web assembly is missing: $assemblyPath"
    }

    $diagnosticOutput = @(& $dotnetCommand.Source $assemblyPath '--diagnostics-version')
    if ($LASTEXITCODE -ne 0) {
        throw "Published Web build diagnostic failed with exit code $LASTEXITCODE."
    }

    try {
        $diagnostic = ($diagnosticOutput -join [System.Environment]::NewLine) |
            ConvertFrom-Json -Depth 10
    }
    catch {
        throw 'Published Web build diagnostic did not return valid JSON.'
    }

    if ($diagnostic.PSObject.Properties.Name -notcontains 'schemaVersion' -or
        $diagnostic.PSObject.Properties.Name -notcontains 'version' -or
        $diagnostic.PSObject.Properties.Name -notcontains 'sourceSha' -or
        $diagnostic.schemaVersion -ne 1 -or
        [string]$diagnostic.version -cne $releaseVersion -or
        [string]$diagnostic.sourceSha -cnotmatch '^[0-9a-f]{40}$') {
        throw 'Published Web build diagnostic has invalid release version or source metadata.'
    }

    return $diagnostic
}

function Assert-WorkerMetadata {
    param([Parameter(Mandatory)][string]$PublishDirectory)

    $metadataPath = Join-Path $PublishDirectory 'functions.metadata'
    if (-not [System.IO.File]::Exists($metadataPath)) {
        throw 'Published Worker output is missing functions.metadata.'
    }

    try {
        $metadata = [System.IO.File]::ReadAllText($metadataPath) | ConvertFrom-Json -Depth 30
    }
    catch {
        throw 'Published Worker functions.metadata is not valid JSON.'
    }

    $functions = @($metadata)
    $names = @($functions | ForEach-Object { [string]$_.name })
    if ($names.Count -ne @($names | Sort-Object -CaseSensitive -Unique).Count) {
        throw 'Published Worker functions.metadata contains missing or duplicate function names.'
    }
    Assert-ExactNames -Actual $names -Expected $expectedWorkerFunctions -Description 'Published Worker function metadata'
}

function Assert-ApprovedBootstrapManifestContract {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][string]$ExpectedSourceRevision
    )

    Assert-ExactNames -Actual $Manifest.PSObject.Properties.Name -Expected @(
        'schemaVersion',
        'productVersion',
        'sourceRevision',
        'expectedMigrationId',
        'targetIdentity',
        'sqlServer',
        'sqlDatabase',
        'issuer',
        'administrators',
        'publicMcpClient') -Description 'Approved bootstrap manifest properties'
    if ($Manifest.schemaVersion -ne 1 -or
        [string]$Manifest.productVersion -cne $releaseVersion -or
        [string]$Manifest.sourceRevision -cne $ExpectedSourceRevision -or
        [string]$Manifest.expectedMigrationId -cne $expectedMigrationId -or
        [string]$Manifest.sqlServer -cnotmatch '^[a-z0-9][a-z0-9.-]*\.database\.windows\.net$' -or
        [string]::IsNullOrWhiteSpace([string]$Manifest.sqlDatabase) -or
        [string]$Manifest.targetIdentity -cnotmatch '^sqlserver://[a-z0-9][a-z0-9.-]*\.database\.windows\.net/[A-Za-z0-9._~%+-]+$') {
        throw 'Approved bootstrap manifest has invalid version, source, migration, or SQL target metadata.'
    }

    $expectedTargetIdentity = "sqlserver://$($Manifest.sqlServer)/$([System.Uri]::EscapeDataString([string]$Manifest.sqlDatabase))"
    if ([string]$Manifest.targetIdentity -cne $expectedTargetIdentity) {
        throw 'Approved bootstrap manifest targetIdentity does not exactly match its SQL server and database.'
    }

    try {
        $issuer = [System.Uri]::new([string]$Manifest.issuer, [System.UriKind]::Absolute)
    }
    catch {
        throw 'Approved bootstrap manifest issuer must be an absolute HTTPS origin.'
    }
    if ($issuer.Scheme -cne 'https' -or
        $issuer.AbsolutePath -cne '/' -or
        -not [string]::IsNullOrEmpty($issuer.Query) -or
        -not [string]::IsNullOrEmpty($issuer.Fragment) -or
        -not [string]::IsNullOrEmpty($issuer.UserInfo)) {
        throw 'Approved bootstrap manifest issuer must be an exact HTTPS origin.'
    }

    $administrators = @($Manifest.administrators)
    if ($administrators.Count -ne 2) {
        throw 'Approved bootstrap manifest must contain exactly two administrators.'
    }
    $manifestIdentities = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $userNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($administrator in $administrators) {
        if ($null -eq $administrator) {
            throw 'Approved bootstrap manifest contains a null administrator.'
        }
        Assert-ExactNames -Actual $administrator.PSObject.Properties.Name -Expected @(
            'manifestIdentity',
            'userName') -Description 'Approved bootstrap administrator properties'
        $manifestIdentity = [string]$administrator.manifestIdentity
        $userName = [string]$administrator.userName
        if ([string]::IsNullOrWhiteSpace($manifestIdentity) -or
            $manifestIdentity -cne $manifestIdentity.Trim() -or
            [string]::IsNullOrWhiteSpace($userName) -or
            $userName -cne $userName.Trim() -or
            -not $manifestIdentities.Add($manifestIdentity) -or
            -not $userNames.Add($userName)) {
            throw 'Approved bootstrap administrators must have trimmed, distinct identities and user names.'
        }
    }

    $publicClient = $Manifest.publicMcpClient
    Assert-ExactNames -Actual $publicClient.PSObject.Properties.Name -Expected @(
        'clientId',
        'displayName',
        'redirectUris',
        'resource',
        'scopes') -Description 'Approved bootstrap public MCP client properties'
    $clientId = [string]$publicClient.clientId
    $displayName = [string]$publicClient.displayName
    $redirectUris = @($publicClient.redirectUris)
    if ([string]::IsNullOrWhiteSpace($clientId) -or
        $clientId -cne $clientId.Trim() -or
        [string]::IsNullOrWhiteSpace($displayName) -or
        $displayName -cne $displayName.Trim() -or
        $redirectUris.Count -eq 0) {
        throw 'Approved bootstrap public MCP client metadata is incomplete.'
    }
    try {
        $resource = [System.Uri]::new(
            [string]$publicClient.resource,
            [System.UriKind]::Absolute)
        foreach ($redirectUri in $redirectUris) {
            [void][System.Uri]::new(
                [string]$redirectUri,
                [System.UriKind]::Absolute)
        }
    }
    catch {
        throw 'Approved bootstrap public MCP client URIs must be absolute.'
    }
    $expectedResource = [System.Uri]::new($issuer, '/mcp')
    if (-not $resource.Equals($expectedResource)) {
        throw 'Approved bootstrap public MCP client resource must be the issuer /mcp URI.'
    }
    Assert-ExactNames -Actual @(
        $publicClient.scopes | ForEach-Object { [string]$_ }) -Expected @(
        'pegasus.read',
        'pegasus.write') -Description 'Approved bootstrap public MCP client scopes'
}

function Assert-BootstrapPublishContract {
    param(
        [Parameter(Mandatory)][string]$PublishDirectory,
        [Parameter(Mandatory)][string]$ExpectedSourceRevision
    )

    foreach ($requiredFile in @('Pegasus.Bootstrap.exe', 'bootstrap-manifest.json')) {
        if (-not [System.IO.File]::Exists((Join-Path $PublishDirectory $requiredFile))) {
            throw "Published bootstrap output is missing '$requiredFile'."
        }
    }

    try {
        $bootstrapManifest = [System.IO.File]::ReadAllText(
            (Join-Path $PublishDirectory 'bootstrap-manifest.json')) |
            ConvertFrom-Json -Depth 50
    }
    catch {
        throw 'Published bootstrap-manifest.json is not valid JSON.'
    }
    if ($null -eq $bootstrapManifest) {
        throw 'Published bootstrap-manifest.json must contain a JSON object.'
    }
    Assert-ApprovedBootstrapManifestContract -Manifest $bootstrapManifest -ExpectedSourceRevision $ExpectedSourceRevision
}

function Copy-DeploymentInputs {
    param([Parameter(Mandatory)][string]$DestinationRoot)

    foreach ($relativePath in $deploymentInputPaths) {
        $source = Join-Path $repositoryRoot $relativePath
        $destination = Join-Path $DestinationRoot $relativePath.Replace(
            '/',
            [System.IO.Path]::DirectorySeparatorChar)
        [System.IO.Directory]::CreateDirectory(
            [System.IO.Path]::GetDirectoryName($destination)) | Out-Null
        [System.IO.File]::WriteAllBytes(
            $destination,
            (Get-CanonicalTextBytes -Path $source))
    }
}

function Assert-DeploymentInputsAreFailClosed {
    param([Parameter(Mandatory)][string]$DeploymentRoot)

    $combinedSource = [System.Text.StringBuilder]::new()
    foreach ($relativePath in $deploymentInputPaths) {
        [void]$combinedSource.AppendLine([System.IO.File]::ReadAllText(
            (Join-Path $DeploymentRoot $relativePath.Replace(
                '/',
                [System.IO.Path]::DirectorySeparatorChar))))
    }
    $source = $combinedSource.ToString()
    foreach ($forbiddenPattern in @(
        '(?i)Grant-DatabaseAccess\.ps1',
        '(?i)postprovision',
        '(?i)documentIntelligence',
        '(?i)infisical',
        '(?i)SCM_DO_BUILD_DURING_DEPLOYMENT',
        '(?i)remoteBuild\s*[:=]\s*true',
        '(?i)DefaultEndpointsProtocol\s*=',
        '(?i)AccountKey\s*=',
        '(?i)listKeys\s*\(')) {
        if ($source -match $forbiddenPattern) {
            throw "Packaged Azure deployment inputs contain forbidden release material matching '$forbiddenPattern'."
        }
    }

    $parameterPath = Join-Path $DeploymentRoot 'infra/main.parameters.json'
    $parameters = [System.IO.File]::ReadAllText($parameterPath) | ConvertFrom-Json -Depth 50
    if ([string]$parameters.parameters.deploymentMode.value -cne 'offline-replay') {
        throw 'Packaged Azure parameters must remain fail-closed with deploymentMode=offline-replay.'
    }

    $azureYaml = [System.IO.File]::ReadAllText((Join-Path $DeploymentRoot 'azure.yaml'))
    if ($azureYaml -notmatch '(?m)^\s*template:\s*pegasus@0\.1\.0-alpha\.1\s*$') {
        throw "Packaged azure.yaml must identify release version '$releaseVersion'."
    }
}

function Write-ReleaseManifest {
    param(
        [Parameter(Mandatory)][string]$ArtifactDirectory,
        [Parameter(Mandatory)][string]$ResolvedSourceRevision,
        [Parameter(Mandatory)][object]$WebDiagnostic,
        [Parameter(Mandatory)][object]$Toolchain,
        [Parameter(Mandatory)][object[]]$Inputs,
        [Parameter(Mandatory)][object]$ReleaseInputTree,
        [Parameter(Mandatory)][object]$BootstrapManifest
    )

    $artifactRecords = @(
        (Get-ArtifactRecord -Path (Join-Path $ArtifactDirectory $webArtifactName) -Family 'web' -RuntimeIdentifier $ApplicationRuntime -DeploymentKind 'framework-dependent'),
        (Get-ArtifactRecord -Path (Join-Path $ArtifactDirectory $workerArtifactName) -Family 'worker' -RuntimeIdentifier $ApplicationRuntime -DeploymentKind 'framework-dependent'),
        (Get-ArtifactRecord -Path (Join-Path $ArtifactDirectory $migrationArtifactName) -Family 'migration' -RuntimeIdentifier $MigrationRuntime -DeploymentKind 'self-contained-ef-bundle'),
        (Get-ArtifactRecord -Path (Join-Path $ArtifactDirectory $bootstrapArtifactName) -Family 'bootstrap' -RuntimeIdentifier $BootstrapRuntime -DeploymentKind 'self-contained-one-shot'),
        (Get-ArtifactRecord -Path (Join-Path $ArtifactDirectory $deploymentArtifactName) -Family 'azure-deployment-inputs' -RuntimeIdentifier 'none' -DeploymentKind 'bicep-parameters')
    )

    $manifest = [pscustomobject][ordered]@{
        schemaVersion = 1
        releaseMode = 'offline-replay'
        releaseVersion = $releaseVersion
        sourceRevision = $ResolvedSourceRevision
        releaseInputTree = $ReleaseInputTree
        configuration = $Configuration
        webDiagnostic = [pscustomobject][ordered]@{
            schemaVersion = [int]$WebDiagnostic.schemaVersion
            version = [string]$WebDiagnostic.version
            sourceSha = [string]$WebDiagnostic.sourceSha
        }
        bootstrapManifest = $BootstrapManifest
        toolchain = [pscustomobject][ordered]@{
            dotnetSdk = [string]$Toolchain.dotnetSdk
            dotnetEf = [string]$Toolchain.dotnetEf
            restore = 'locked-offline'
        }
        runtimes = [pscustomobject][ordered]@{
            web = "$ApplicationRuntime-framework-dependent"
            worker = "$ApplicationRuntime-framework-dependent"
            migration = "$MigrationRuntime-self-contained"
            bootstrap = "$BootstrapRuntime-self-contained"
            azureDeploymentInputs = 'bicep-parameters'
        }
        inputs = $Inputs
        artifacts = $artifactRecords
    }

    $manifestPath = Join-Path $ArtifactDirectory $manifestFileName
    $json = ($manifest | ConvertTo-Json -Depth 20).Replace("`r`n", "`n") + "`n"
    [System.IO.File]::WriteAllText(
        $manifestPath,
        $json,
        [System.Text.UTF8Encoding]::new($false))
    $manifestHash = Get-FileSha256 -Path $manifestPath
    [System.IO.File]::WriteAllText(
        (Join-Path $ArtifactDirectory $manifestDigestFileName),
        "$manifestHash  $manifestFileName`n",
        [System.Text.UTF8Encoding]::new($false))
}

function Assert-ExactArtifactDirectory {
    param([Parameter(Mandatory)][string]$ArtifactDirectory)

    $actualNames = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in [System.IO.Directory]::EnumerateFileSystemEntries(
        $ArtifactDirectory,
        '*',
        [System.IO.SearchOption]::TopDirectoryOnly)) {
        if (-not [System.IO.File]::Exists($entry)) {
            throw "Release output contains a raw directory instead of an immutable artifact: $entry"
        }
        $actualNames.Add([System.IO.Path]::GetFileName($entry))
    }
    Assert-ExactNames -Actual $actualNames.ToArray() -Expected $expectedOutputNames -Description 'Release output files'
}

function New-ReleaseCandidate {
    param(
        [Parameter(Mandatory)][string]$CandidateRoot,
        [Parameter(Mandatory)][string]$ResolvedSourceRevision,
        [Parameter(Mandatory)][object]$Toolchain,
        [Parameter(Mandatory)][object[]]$Inputs,
        [Parameter(Mandatory)][object]$ReleaseInputTree,
        [Parameter(Mandatory)][string]$BootstrapManifestSource,
        [Parameter(Mandatory)][object]$BootstrapManifestInput
    )

    $webPublishDirectory = Join-Path $CandidateRoot 'publish/web'
    $workerPublishDirectory = Join-Path $CandidateRoot 'publish/worker'
    $migrationDirectory = Join-Path $CandidateRoot 'publish/migration'
    $bootstrapPublishDirectory = Join-Path $CandidateRoot 'publish/bootstrap'
    $deploymentDirectory = Join-Path $CandidateRoot 'deployment-inputs'
    $artifactDirectory = Join-Path $CandidateRoot 'artifacts'
    foreach ($directory in @(
        $webPublishDirectory,
        $workerPublishDirectory,
        $migrationDirectory,
        $bootstrapPublishDirectory,
        $deploymentDirectory,
        $artifactDirectory)) {
        [System.IO.Directory]::CreateDirectory($directory) | Out-Null
    }

    $commonBuildProperties = @(
        '/p:ContinuousIntegrationBuild=true',
        '/p:Deterministic=true',
        '/p:DeterministicSourcePaths=true',
        '/p:DebugSymbols=false',
        '/p:DebugType=None',
        '/p:IncludeSourceRevisionInInformationalVersion=true',
        "/p:SourceRevisionId=$ResolvedSourceRevision",
        "/p:PathMap=$repositoryRoot=/_/src"
    )

    Invoke-DotNet -Operation 'Web linux-x64 framework-dependent publish' -Arguments @(
        'publish',
        'src/Pegasus.Web/Pegasus.Web.csproj',
        '--configuration', $Configuration,
        '-r', $ApplicationRuntime,
        '--self-contained', 'false',
        '--no-restore',
        '--nologo',
        '--output', $webPublishDirectory,
        '/p:UseAppHost=false'
    ) + $commonBuildProperties
    Assert-CleanRepositoryRevision -ExpectedRevision $ResolvedSourceRevision

    $developmentSettingsPath = Join-Path $webPublishDirectory 'appsettings.Development.json'
    if ([System.IO.File]::Exists($developmentSettingsPath)) {
        [System.IO.File]::Delete($developmentSettingsPath)
    }
    Convert-PublishTextFilesToCanonicalUtf8 -PublishDirectory $webPublishDirectory
    Assert-NoForbiddenPublishFiles -PublishDirectory $webPublishDirectory
    Assert-WebProductionConfiguration -PublishDirectory $webPublishDirectory
    $webDiagnostic = Get-PublishedWebDiagnostic -PublishDirectory $webPublishDirectory
    if ([string]$webDiagnostic.sourceSha -cne $ResolvedSourceRevision) {
        throw "Published Web source SHA '$($webDiagnostic.sourceSha)' does not match the executed checkout '$ResolvedSourceRevision'."
    }

    Invoke-DotNet -Operation 'Worker linux-x64 framework-dependent publish' -Arguments @(
        'publish',
        'src/Pegasus.Worker/Pegasus.Worker.csproj',
        '--configuration', $Configuration,
        '-r', $ApplicationRuntime,
        '--self-contained', 'false',
        '--no-restore',
        '--nologo',
        '--output', $workerPublishDirectory,
        '/p:UseAppHost=false'
    ) + $commonBuildProperties
    Assert-CleanRepositoryRevision -ExpectedRevision $ResolvedSourceRevision
    Convert-PublishTextFilesToCanonicalUtf8 -PublishDirectory $workerPublishDirectory
    Assert-NoForbiddenPublishFiles -PublishDirectory $workerPublishDirectory
    Assert-WorkerMetadata -PublishDirectory $workerPublishDirectory

    Invoke-DotNet -Operation 'Bootstrap win-x64 self-contained publish' -Arguments @(
        'publish',
        'src/Pegasus.Bootstrap/Pegasus.Bootstrap.csproj',
        '--configuration', $Configuration,
        '-r', $BootstrapRuntime,
        '--self-contained', 'true',
        '--no-restore',
        '--nologo',
        '--output', $bootstrapPublishDirectory
    ) + $commonBuildProperties
    Assert-CleanRepositoryRevision -ExpectedRevision $ResolvedSourceRevision
    $publishedBootstrapManifestPath = Join-Path $bootstrapPublishDirectory 'bootstrap-manifest.json'
    if ([System.IO.File]::Exists($publishedBootstrapManifestPath)) {
        throw 'Pegasus.Bootstrap must not publish a generic bootstrap-manifest.json. Supply only the separately approved -BootstrapManifestPath input.'
    }
    $currentBootstrapManifestBytes = Get-CanonicalTextBytes -Path $BootstrapManifestSource
    if ($currentBootstrapManifestBytes.Length -ne [long]$BootstrapManifestInput.byteLength -or
        (Get-ByteArraySha256 -Bytes $currentBootstrapManifestBytes) -cne [string]$BootstrapManifestInput.sha256) {
        throw 'The approved bootstrap manifest changed during release packaging.'
    }
    [System.IO.File]::WriteAllBytes(
        $publishedBootstrapManifestPath,
        $currentBootstrapManifestBytes)
    Convert-PublishTextFilesToCanonicalUtf8 -PublishDirectory $bootstrapPublishDirectory
    Assert-NoForbiddenPublishFiles -PublishDirectory $bootstrapPublishDirectory
    Assert-BootstrapPublishContract -PublishDirectory $bootstrapPublishDirectory -ExpectedSourceRevision $ResolvedSourceRevision

    Invoke-DotNet -Operation 'Migration startup build' -Arguments @(
        'build',
        'src/Pegasus.Web/Pegasus.Web.csproj',
        '--configuration', $Configuration,
        '--no-restore',
        '--nologo'
    ) + $commonBuildProperties
    Assert-CleanRepositoryRevision -ExpectedRevision $ResolvedSourceRevision

    $migrationExecutable = Join-Path $migrationDirectory 'Pegasus.Migrations.exe'
    Invoke-DotNet -Operation 'Self-contained win-x64 EF migrations bundle generation' -Arguments @(
        'ef',
        'migrations',
        'bundle',
        '--self-contained',
        '-r', $MigrationRuntime,
        '--configuration', $Configuration,
        '--project', 'src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj',
        '--startup-project', 'src/Pegasus.Web/Pegasus.Web.csproj',
        '--output', $migrationExecutable,
        '--no-build'
    )
    Assert-CleanRepositoryRevision -ExpectedRevision $ResolvedSourceRevision
    if (-not [System.IO.File]::Exists($migrationExecutable)) {
        throw 'The EF migrations bundle did not produce Pegasus.Migrations.exe.'
    }
    Assert-ExactNames -Actual @(
        [System.IO.Directory]::EnumerateFiles($migrationDirectory) |
            ForEach-Object { [System.IO.Path]::GetFileName($_) }) -Expected @('Pegasus.Migrations.exe') -Description 'Migration bundle files'

    Copy-DeploymentInputs -DestinationRoot $deploymentDirectory
    Assert-DeploymentInputsAreFailClosed -DeploymentRoot $deploymentDirectory
    Assert-CleanRepositoryRevision -ExpectedRevision $ResolvedSourceRevision

    New-DeterministicZip -SourceDirectory $webPublishDirectory -DestinationPath (Join-Path $artifactDirectory $webArtifactName)
    New-DeterministicZip -SourceDirectory $workerPublishDirectory -DestinationPath (Join-Path $artifactDirectory $workerArtifactName)
    New-DeterministicZip -SourceDirectory $migrationDirectory -DestinationPath (Join-Path $artifactDirectory $migrationArtifactName)
    New-DeterministicZip -SourceDirectory $bootstrapPublishDirectory -DestinationPath (Join-Path $artifactDirectory $bootstrapArtifactName)
    New-DeterministicZip -SourceDirectory $deploymentDirectory -DestinationPath (Join-Path $artifactDirectory $deploymentArtifactName)
    $bootstrapManifestFile = [System.IO.FileInfo]::new(
        (Join-Path $bootstrapPublishDirectory 'bootstrap-manifest.json'))
    $bootstrapManifestRecord = [pscustomobject][ordered]@{
        entryName = 'bootstrap-manifest.json'
        byteLength = $bootstrapManifestFile.Length
        sha256 = Get-FileSha256 -Path $bootstrapManifestFile.FullName
    }
    Write-ReleaseManifest -ArtifactDirectory $artifactDirectory -ResolvedSourceRevision $ResolvedSourceRevision -WebDiagnostic $webDiagnostic -Toolchain $Toolchain -Inputs $Inputs -ReleaseInputTree $ReleaseInputTree -BootstrapManifest $bootstrapManifestRecord
    Assert-ExactArtifactDirectory -ArtifactDirectory $artifactDirectory
    Assert-CleanRepositoryRevision -ExpectedRevision $ResolvedSourceRevision

    return $artifactDirectory
}

function Assert-FilesEqual {
    param(
        [Parameter(Mandatory)][string]$ExpectedPath,
        [Parameter(Mandatory)][string]$ActualPath
    )

    $expected = [System.IO.FileInfo]::new($ExpectedPath)
    $actual = [System.IO.FileInfo]::new($ActualPath)
    if ($expected.Length -ne $actual.Length) {
        throw "Reproducible release verification failed for '$($expected.Name)': byte lengths differ."
    }

    $expectedStream = [System.IO.File]::OpenRead($expected.FullName)
    $actualStream = [System.IO.File]::OpenRead($actual.FullName)
    try {
        $expectedBuffer = [byte[]]::new(131072)
        $actualBuffer = [byte[]]::new(131072)
        while ($true) {
            $expectedCount = $expectedStream.Read($expectedBuffer, 0, $expectedBuffer.Length)
            $actualCount = $actualStream.Read($actualBuffer, 0, $actualBuffer.Length)
            if ($expectedCount -ne $actualCount) {
                throw "Reproducible release verification failed for '$($expected.Name)': byte streams differ."
            }
            if ($expectedCount -eq 0) {
                break
            }
            for ($index = 0; $index -lt $expectedCount; $index++) {
                if ($expectedBuffer[$index] -ne $actualBuffer[$index]) {
                    throw "Reproducible release verification failed for '$($expected.Name)': bytes differ."
                }
            }
        }
    }
    finally {
        $actualStream.Dispose()
        $expectedStream.Dispose()
    }
}

function Assert-ReproducibleCandidates {
    param(
        [Parameter(Mandatory)][string]$ExpectedDirectory,
        [Parameter(Mandatory)][string]$ReplayDirectory
    )

    Assert-ExactArtifactDirectory -ArtifactDirectory $ExpectedDirectory
    Assert-ExactArtifactDirectory -ArtifactDirectory $ReplayDirectory
    foreach ($name in $expectedOutputNames) {
        Assert-FilesEqual -ExpectedPath (Join-Path $ExpectedDirectory $name) -ActualPath (Join-Path $ReplayDirectory $name)
    }
}

if (-not $VerifyReproducible.IsPresent) {
    throw 'Release artifact creation requires -VerifyReproducible so a second independent package pass can prove byte equality.'
}
$bootstrapManifestSourcePath = [System.IO.Path]::GetFullPath($BootstrapManifestPath)
if (-not [System.IO.File]::Exists($bootstrapManifestSourcePath)) {
    throw "Approved bootstrap manifest is missing: $bootstrapManifestSourcePath"
}
if (([System.IO.File]::GetAttributes($bootstrapManifestSourcePath) -band
    [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
    throw 'Approved bootstrap manifest must be a regular file, not a link or reparse point.'
}
$outputPrefix = $outputRoot.TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ($bootstrapManifestSourcePath.Equals($outputRoot, [System.StringComparison]::OrdinalIgnoreCase) -or
    $bootstrapManifestSourcePath.StartsWith(
        $outputPrefix,
        [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Approved bootstrap manifest must be outside the release output directory.'
}
$bootstrapManifestBytes = Get-CanonicalTextBytes -Path $bootstrapManifestSourcePath
$bootstrapManifestText = [System.Text.UTF8Encoding]::new($false, $true).GetString(
    $bootstrapManifestBytes)
try {
    $approvedBootstrapManifest = $bootstrapManifestText | ConvertFrom-Json -Depth 50
}
catch {
    throw 'Approved bootstrap manifest is not valid UTF-8 JSON.'
}
if ($null -eq $approvedBootstrapManifest -or
    $bootstrapManifestText -match '(?i)"(?:password|secret|token|connectionString)"\s*:') {
    throw 'Approved bootstrap manifest is empty or contains a forbidden secret-bearing field.'
}
$bootstrapManifestInput = [pscustomobject][ordered]@{
    entryName = 'bootstrap-manifest.json'
    byteLength = $bootstrapManifestBytes.Length
    sha256 = Get-ByteArraySha256 -Bytes $bootstrapManifestBytes
}
if ([System.IO.Directory]::Exists($outputRoot) -or [System.IO.File]::Exists($outputRoot)) {
    throw "Release output path already exists: $outputRoot. Use a new path so a replay cannot mix or overwrite artifacts."
}

$parentDirectory = [System.IO.Path]::GetDirectoryName($outputRoot)
if ([string]::IsNullOrWhiteSpace($parentDirectory)) {
    throw "OutputDirectory must include a parent directory: $OutputDirectory"
}

$gitCommand = Get-RequiredApplication -Name 'git'
$dotnetCommand = Get-RequiredApplication -Name 'dotnet'
foreach ($inputPath in $releaseInputPaths) {
    Assert-RequiredFile -RelativePath $inputPath
}
$resolvedSourceRevision = Resolve-RepositorySourceRevision -RequestedRevision $SourceRevision
Assert-ApprovedBootstrapManifestContract -Manifest $approvedBootstrapManifest -ExpectedSourceRevision $resolvedSourceRevision
$sourceCommitUnixTime = Get-SourceCommitUnixTime -Revision $resolvedSourceRevision
$toolchain = Get-PinnedReleaseMetadata
$releaseInputs = @(
    foreach ($relativePath in $releaseInputPaths) {
        $purpose = if ($deploymentInputPaths -ccontains $relativePath) {
            'deployment'
        }
        else {
            'provenance'
        }
        Get-InputRecord -RelativePath $relativePath -Purpose $purpose
    }
)
$releaseInputTree = Get-ReleaseInputTreeRecord -Revision $resolvedSourceRevision

[System.IO.Directory]::CreateDirectory($parentDirectory) | Out-Null
$stagingRoot = Join-Path $parentDirectory "$([System.IO.Path]::GetFileName($outputRoot)).staging-$([System.Guid]::NewGuid().ToString('N'))"
$offlineNuGetConfig = Join-Path $stagingRoot 'offline-nuget.config'
$previousRestoreConfigFile = [Environment]::GetEnvironmentVariable('RestoreConfigFile', 'Process')
$previousSourceDateEpoch = [Environment]::GetEnvironmentVariable('SOURCE_DATE_EPOCH', 'Process')
$previousSourceRevisionId = [Environment]::GetEnvironmentVariable('SourceRevisionId', 'Process')
$previousContinuousIntegrationBuild = [Environment]::GetEnvironmentVariable('ContinuousIntegrationBuild', 'Process')
$previousIncludeRevision = [Environment]::GetEnvironmentVariable('IncludeSourceRevisionInInformationalVersion', 'Process')

try {
    [System.IO.Directory]::CreateDirectory($stagingRoot) | Out-Null
    [System.IO.File]::WriteAllText(
        $offlineNuGetConfig,
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>`n<configuration>`n  <packageSources>`n    <clear />`n  </packageSources>`n</configuration>`n",
        [System.Text.UTF8Encoding]::new($false))
    [Environment]::SetEnvironmentVariable('RestoreConfigFile', $offlineNuGetConfig, 'Process')
    [Environment]::SetEnvironmentVariable('SOURCE_DATE_EPOCH', $sourceCommitUnixTime, 'Process')
    [Environment]::SetEnvironmentVariable('SourceRevisionId', $resolvedSourceRevision, 'Process')
    [Environment]::SetEnvironmentVariable('ContinuousIntegrationBuild', 'true', 'Process')
    [Environment]::SetEnvironmentVariable('IncludeSourceRevisionInInformationalVersion', 'true', 'Process')

    Push-Location $repositoryRoot
    try {
        Invoke-DotNet -Operation 'Locked offline solution restore' -Arguments @(
            'restore', 'Pegasus.slnx', '--locked-mode', '--configfile', $offlineNuGetConfig, '--nologo')
        Assert-CleanRepositoryRevision -ExpectedRevision $resolvedSourceRevision
        Invoke-DotNet -Operation 'Locked offline tool restore' -Arguments @(
            'tool', 'restore', '--configfile', $offlineNuGetConfig)
        Assert-CleanRepositoryRevision -ExpectedRevision $resolvedSourceRevision

        foreach ($runtimeRestore in @(
            [pscustomobject]@{ project = 'src/Pegasus.Web/Pegasus.Web.csproj'; runtime = $ApplicationRuntime },
            [pscustomobject]@{ project = 'src/Pegasus.Worker/Pegasus.Worker.csproj'; runtime = $ApplicationRuntime },
            [pscustomobject]@{ project = 'src/Pegasus.Bootstrap/Pegasus.Bootstrap.csproj'; runtime = $BootstrapRuntime })) {
            Invoke-DotNet -Operation "Locked offline $($runtimeRestore.runtime) restore for $($runtimeRestore.project)" -Arguments @(
                'restore',
                $runtimeRestore.project,
                '--locked-mode',
                '--configfile', $offlineNuGetConfig,
                '-r', $runtimeRestore.runtime,
                '--nologo')
            Assert-CleanRepositoryRevision -ExpectedRevision $resolvedSourceRevision
        }

        $efVersionOutput = @(& $dotnetCommand.Source ef --version)
        if ($LASTEXITCODE -ne 0 -or
            ($efVersionOutput -join ' ') -notmatch '(?<![0-9.])10\.0\.10(?![0-9.])') {
            throw 'The restored dotnet-ef tool does not report the pinned version 10.0.10.'
        }

        $firstCandidate = New-ReleaseCandidate -CandidateRoot (Join-Path $stagingRoot 'candidate-a') -ResolvedSourceRevision $resolvedSourceRevision -Toolchain $toolchain -Inputs $releaseInputs -ReleaseInputTree $releaseInputTree -BootstrapManifestSource $bootstrapManifestSourcePath -BootstrapManifestInput $bootstrapManifestInput
        $replayCandidate = New-ReleaseCandidate -CandidateRoot (Join-Path $stagingRoot 'candidate-b') -ResolvedSourceRevision $resolvedSourceRevision -Toolchain $toolchain -Inputs $releaseInputs -ReleaseInputTree $releaseInputTree -BootstrapManifestSource $bootstrapManifestSourcePath -BootstrapManifestInput $bootstrapManifestInput
        Assert-ReproducibleCandidates -ExpectedDirectory $firstCandidate -ReplayDirectory $replayCandidate
        Assert-CleanRepositoryRevision -ExpectedRevision $resolvedSourceRevision
    }
    finally {
        Pop-Location
    }

    if ([System.IO.Directory]::Exists($outputRoot) -or [System.IO.File]::Exists($outputRoot)) {
        throw "Release output path appeared during packaging: $outputRoot. Refusing to mix or overwrite artifacts."
    }
    [System.IO.Directory]::Move($firstCandidate, $outputRoot)
}
finally {
    [Environment]::SetEnvironmentVariable('RestoreConfigFile', $previousRestoreConfigFile, 'Process')
    [Environment]::SetEnvironmentVariable('SOURCE_DATE_EPOCH', $previousSourceDateEpoch, 'Process')
    [Environment]::SetEnvironmentVariable('SourceRevisionId', $previousSourceRevisionId, 'Process')
    [Environment]::SetEnvironmentVariable('ContinuousIntegrationBuild', $previousContinuousIntegrationBuild, 'Process')
    [Environment]::SetEnvironmentVariable('IncludeSourceRevisionInInformationalVersion', $previousIncludeRevision, 'Process')
    if ([System.IO.Directory]::Exists($stagingRoot)) {
        [System.IO.Directory]::Delete($stagingRoot, $true)
    }
}

Write-Output "Deterministic release artifacts for '$resolvedSourceRevision' were written to '$outputRoot'. No Azure authentication, provisioning, or deployment was performed."
