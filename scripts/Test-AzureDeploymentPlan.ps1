[CmdletBinding()]
param(
    [string]$ArtifactDirectory = (Join-Path $PSScriptRoot '../artifacts/release'),

    [ValidateSet('Local')]
    [string]$Mode = 'Local'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath($ArtifactDirectory)
$deploymentPlanPath = Join-Path $repositoryRoot '.azure/deployment-plan.md'
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
$expectedInputPaths = @($provenanceInputPaths + $deploymentInputPaths)
$expectedOutputNames = @(
    $webArtifactName,
    $workerArtifactName,
    $migrationArtifactName,
    $bootstrapArtifactName,
    $deploymentArtifactName,
    $manifestFileName,
    $manifestDigestFileName
)
$artifactContracts = @(
    [pscustomobject][ordered]@{
        family = 'web'
        fileName = $webArtifactName
        runtimeIdentifier = 'linux-x64'
        deploymentKind = 'framework-dependent'
    },
    [pscustomobject][ordered]@{
        family = 'worker'
        fileName = $workerArtifactName
        runtimeIdentifier = 'linux-x64'
        deploymentKind = 'framework-dependent'
    },
    [pscustomobject][ordered]@{
        family = 'migration'
        fileName = $migrationArtifactName
        runtimeIdentifier = 'win-x64'
        deploymentKind = 'self-contained-ef-bundle'
    },
    [pscustomobject][ordered]@{
        family = 'bootstrap'
        fileName = $bootstrapArtifactName
        runtimeIdentifier = 'win-x64'
        deploymentKind = 'self-contained-one-shot'
    },
    [pscustomobject][ordered]@{
        family = 'azure-deployment-inputs'
        fileName = $deploymentArtifactName
        runtimeIdentifier = 'none'
        deploymentKind = 'bicep-parameters'
    }
)

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-RequiredApplication {
    param([Parameter(Mandatory)][string]$Name)

    $command = Get-Command -Name $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "$Name is required for local deployment-plan validation."
    }

    return $command
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

function Get-StreamSha256 {
    param([Parameter(Mandatory)][System.IO.Stream]$Stream)

    return [Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($Stream)).ToLowerInvariant()
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

function Resolve-CleanRepositoryHead {
    $git = Get-RequiredApplication -Name 'git'
    $headOutput = @(
        & $git.Source -C $repositoryRoot rev-parse --verify 'HEAD^{commit}' 2>$null
    )
    if ($LASTEXITCODE -ne 0 -or $headOutput.Count -ne 1) {
        throw "The repository HEAD could not be resolved at '$repositoryRoot'."
    }

    $headRevision = ([string]$headOutput[0]).Trim().ToLowerInvariant()
    if ($headRevision -notmatch '^[0-9a-f]{40}$') {
        throw "The repository HEAD is not a 40-character Git source revision: '$headRevision'."
    }

    $workingTreeState = @(
        & $git.Source -C $repositoryRoot status --porcelain=v1 --untracked-files=all -- . 2>$null
    )
    if ($LASTEXITCODE -ne 0) {
        throw "The working-tree state could not be verified at '$repositoryRoot'."
    }
    if ($workingTreeState.Count -ne 0) {
        throw 'The deployment-plan checkout has tracked or untracked changes. Commit or remove them before validating revision-bound release artifacts.'
    }

    return $headRevision
}

function Assert-CleanRepositoryHead {
    param([Parameter(Mandatory)][string]$ExpectedRevision)

    $observedRevision = Resolve-CleanRepositoryHead
    if ($observedRevision -cne $ExpectedRevision) {
        throw "The checked-out source revision changed during deployment-plan validation from '$ExpectedRevision' to '$observedRevision'."
    }
}

function Get-GitBlobBytes {
    param(
        [Parameter(Mandatory)][object]$Git,
        [Parameter(Mandatory)][string]$ObjectId
    )

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Git.Source
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

    $git = Get-RequiredApplication -Name 'git'
    $treeOutput = @(
        & $git.Source -C $repositoryRoot -c core.quotepath=false ls-tree -r --full-tree $Revision 2>$null
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

            $blobBytes = Get-GitBlobBytes -Git $git -ObjectId $metadata[2]
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

function Assert-ReleaseInputTree {
    param(
        [Parameter(Mandatory)][object]$ManifestRecord,
        [Parameter(Mandatory)][string]$Revision
    )

    $expected = Get-ReleaseInputTreeRecord -Revision $Revision
    Assert-Condition -Condition (
        [string]$ManifestRecord.schema -ceq [string]$expected.schema -and
        [string]$ManifestRecord.algorithm -ceq [string]$expected.algorithm -and
        [string]$ManifestRecord.sha256 -ceq [string]$expected.sha256 -and
        [int]$ManifestRecord.includedPathCount -eq [int]$expected.includedPathCount) -Message 'Release manifest tracked input-tree digest does not match the exact checkout.'

    $excludedPaths = @(
        'docs/changes/2026-07-27-qdos-alpha-reference-corpora.md',
        'docs/reference/imp-docs/',
        'corpus/',
        'artifacts/'
    )
    Assert-ExactNames -Actual @(
        $ManifestRecord.excludedPathCounts.PSObject.Properties |
            ForEach-Object { $_.Name }) -Expected $excludedPaths -Description 'Release input-tree excluded path counters'
    foreach ($path in $excludedPaths) {
        $manifestCount = $ManifestRecord.excludedPathCounts.PSObject.Properties[$path].Value
        $expectedCount = $expected.excludedPathCounts.PSObject.Properties[$path].Value
        Assert-Condition -Condition ([int]$manifestCount -eq [int]$expectedCount) -Message "Release input-tree excluded path count differs for '$path'."
    }
}

function Assert-ManifestDigest {
    param([Parameter(Mandatory)][string]$ManifestPath)

    $digestPath = Join-Path $artifactRoot $manifestDigestFileName
    Assert-Condition -Condition ([System.IO.File]::Exists($digestPath)) -Message 'Release manifest digest is missing.'
    $digestText = [System.IO.File]::ReadAllText($digestPath)
    $match = [regex]::Match(
        $digestText,
        '^([0-9a-f]{64})  release-manifest\.json\n$',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
    Assert-Condition -Condition $match.Success -Message 'Release manifest digest must contain one canonical lowercase SHA-256 record.'
    $actualHash = Get-FileSha256 -Path $ManifestPath
    Assert-Condition -Condition ($actualHash -ceq $match.Groups[1].Value) -Message 'Release manifest digest does not match release-manifest.json.'
}

function Assert-ExactOutputDirectory {
    $actualNames = [System.Collections.Generic.List[string]]::new()
    foreach ($entry in [System.IO.Directory]::EnumerateFileSystemEntries(
        $artifactRoot,
        '*',
        [System.IO.SearchOption]::TopDirectoryOnly)) {
        Assert-Condition -Condition ([System.IO.File]::Exists($entry)) -Message "Release output contains a raw directory instead of an immutable artifact: $entry"
        $actualNames.Add([System.IO.Path]::GetFileName($entry))
    }
    Assert-ExactNames -Actual $actualNames.ToArray() -Expected $expectedOutputNames -Description 'Release output files'
}

function Get-ExpectedArtifact {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][object]$Contract
    )

    $matches = @(
        $Manifest.artifacts |
            Where-Object {
                [string]$_.family -ceq [string]$Contract.family -and
                [string]$_.fileName -ceq [string]$Contract.fileName
            }
    )
    Assert-Condition -Condition ($matches.Count -eq 1) -Message "Release manifest must contain exactly one '$($Contract.family)' artifact named '$($Contract.fileName)'."
    $record = $matches[0]
    Assert-Condition -Condition ([string]$record.runtimeIdentifier -ceq [string]$Contract.runtimeIdentifier) -Message "Release artifact '$($Contract.fileName)' has the wrong runtime identifier."
    Assert-Condition -Condition ([string]$record.deploymentKind -ceq [string]$Contract.deploymentKind) -Message "Release artifact '$($Contract.fileName)' has the wrong deployment kind."
    return $record
}

function Assert-ArtifactHash {
    param([Parameter(Mandatory)][object]$Record)

    $fileName = [string]$Record.fileName
    Assert-Condition -Condition ($fileName -cmatch '^[a-z0-9][a-z0-9.-]*$') -Message "Artifact file name is unsafe: '$fileName'."
    Assert-Condition -Condition ([string]$Record.sha256 -cmatch '^[0-9a-f]{64}$') -Message "Release artifact has an invalid SHA-256 digest: $fileName"
    Assert-Condition -Condition ([long]$Record.byteLength -gt 0) -Message "Release artifact has an invalid byte length: $fileName"

    $path = Join-Path $artifactRoot $fileName
    Assert-Condition -Condition ([System.IO.File]::Exists($path)) -Message "Release artifact is missing: $fileName"
    $file = [System.IO.FileInfo]::new($path)
    Assert-Condition -Condition ($file.Length -eq [long]$Record.byteLength) -Message "Release artifact length differs from the manifest: $fileName"
    $actualHash = Get-FileSha256 -Path $path
    Assert-Condition -Condition ($actualHash -ceq [string]$Record.sha256) -Message "Release artifact hash differs from the manifest: $fileName"
}

function Assert-VersionAndToolchainContract {
    param([Parameter(Mandatory)][object]$Manifest)

    Assert-Condition -Condition ([string]$Manifest.releaseVersion -ceq $releaseVersion) -Message "Release manifest version must be '$releaseVersion'."
    Assert-Condition -Condition ([string]$Manifest.configuration -ceq 'Release') -Message 'Release manifest configuration must be Release.'

    $buildProperties = [xml][System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'Directory.Build.props'))
    $versionNodes = @($buildProperties.SelectNodes('/Project/PropertyGroup/Version'))
    Assert-Condition -Condition ($versionNodes.Count -eq 1 -and [string]$versionNodes[0].InnerText -ceq $releaseVersion) -Message "Directory.Build.props does not match release version '$releaseVersion'."

    $package = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'package.json')) | ConvertFrom-Json -Depth 20
    $packageLock = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'package-lock.json')) |
        ConvertFrom-Json -AsHashtable -Depth 100
    $lockPackages = $packageLock['packages']
    $rootPackage = if ($null -ne $lockPackages) {
        $lockPackages['']
    }
    else {
        $null
    }
    Assert-Condition -Condition (
        [string]$package.version -ceq $releaseVersion -and
        [string]$packageLock['version'] -ceq $releaseVersion -and
        $null -ne $rootPackage -and
        [string]$rootPackage['version'] -ceq $releaseVersion) -Message 'Node package manifests do not match the release version.'

    $globalJson = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot 'global.json')) | ConvertFrom-Json -Depth 20
    $toolManifest = [System.IO.File]::ReadAllText(
        (Join-Path $repositoryRoot '.config/dotnet-tools.json')) | ConvertFrom-Json -Depth 20
    $efTool = $toolManifest.tools.PSObject.Properties['dotnet-ef']
    Assert-Condition -Condition (
        [string]$Manifest.toolchain.dotnetSdk -ceq [string]$globalJson.sdk.version -and
        $null -ne $efTool -and
        [string]$efTool.Value.version -ceq '10.0.10' -and
        [string]$Manifest.toolchain.dotnetEf -ceq [string]$efTool.Value.version -and
        [string]$Manifest.toolchain.restore -ceq 'locked-offline') -Message 'Release manifest toolchain provenance does not match the pinned offline toolchain.'
}

function Assert-ManifestInputs {
    param([Parameter(Mandatory)][object]$Manifest)

    $records = @($Manifest.inputs)
    Assert-Condition -Condition ($records.Count -eq $expectedInputPaths.Count) -Message 'Release manifest must contain every exact provenance and deployment input once.'
    Assert-ExactNames -Actual @($records | ForEach-Object { [string]$_.path }) -Expected $expectedInputPaths -Description 'Release manifest input paths'

    foreach ($record in $records) {
        $relativePath = [string]$record.path
        Assert-Condition -Condition (
            $relativePath -cmatch '^[A-Za-z0-9._/-]+$' -and
            -not $relativePath.StartsWith('/', [System.StringComparison]::Ordinal) -and
            $relativePath -notmatch '(^|/)\.\.(/|$)' -and
            $relativePath -notmatch '\\') -Message "Release manifest input path is unsafe: '$relativePath'."
        $expectedPurpose = if ($deploymentInputPaths -ccontains $relativePath) {
            'deployment'
        }
        else {
            'provenance'
        }
        Assert-Condition -Condition ([string]$record.purpose -ceq $expectedPurpose) -Message "Release manifest input '$relativePath' has the wrong purpose."
        Assert-Condition -Condition ([string]$record.sha256 -cmatch '^[0-9a-f]{64}$') -Message "Release manifest input '$relativePath' has an invalid digest."

        $path = Join-Path $repositoryRoot $relativePath
        Assert-Condition -Condition ([System.IO.File]::Exists($path)) -Message "Release manifest input is missing from the exact checkout: $relativePath"
        $canonicalBytes = Get-CanonicalTextBytes -Path $path
        Assert-Condition -Condition ($canonicalBytes.Length -eq [long]$record.byteLength) -Message "Release input length differs from the exact checkout: $relativePath"
        Assert-Condition -Condition ((Get-ByteArraySha256 -Bytes $canonicalBytes) -ceq [string]$record.sha256) -Message "Release input hash differs from the exact checkout: $relativePath"
    }
}

function Assert-SafeArchiveEntryName {
    param(
        [Parameter(Mandatory)][string]$ArchiveName,
        [Parameter(Mandatory)][string]$EntryName
    )

    Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($EntryName)) -Message "Release archive '$ArchiveName' contains an empty entry name."
    Assert-Condition -Condition ($EntryName -cnotmatch '\\') -Message "Release archive '$ArchiveName' contains a backslash path: $EntryName"
    Assert-Condition -Condition ($EntryName -cnotmatch '^[A-Za-z]:' -and -not $EntryName.StartsWith('/', [System.StringComparison]::Ordinal)) -Message "Release archive '$ArchiveName' contains an absolute path: $EntryName"
    Assert-Condition -Condition ($EntryName -cnotmatch '[\x00-\x1f]') -Message "Release archive '$ArchiveName' contains a control character in an entry name."
    $segments = $EntryName.Split('/')
    Assert-Condition -Condition (
        @($segments | Where-Object { $_ -ceq '' -or $_ -ceq '.' -or $_ -ceq '..' }).Count -eq 0) -Message "Release archive '$ArchiveName' contains an unsafe entry: $EntryName"
    Assert-Condition -Condition (-not $EntryName.EndsWith('/', [System.StringComparison]::Ordinal)) -Message "Release archive '$ArchiveName' contains raw directory metadata: $EntryName"
}

function Assert-ArchiveContract {
    param(
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string[]]$RequiredEntries,
        [switch]$ExactEntries
    )

    $archivePath = Join-Path $artifactRoot $FileName
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entries = @($archive.Entries)
        Assert-Condition -Condition ($entries.Count -gt 0) -Message "Release archive is empty: $FileName"
        $entryNames = [System.Collections.Generic.List[string]]::new()
        $caseInsensitiveNames = [System.Collections.Generic.HashSet[string]]::new(
            [System.StringComparer]::OrdinalIgnoreCase)
        foreach ($entry in $entries) {
            Assert-SafeArchiveEntryName -ArchiveName $FileName -EntryName $entry.FullName
            Assert-Condition -Condition ($caseInsensitiveNames.Add($entry.FullName)) -Message "Release archive '$FileName' contains a duplicate or case-colliding entry: $($entry.FullName)"
            Assert-Condition -Condition (
                $entry.LastWriteTime.Year -eq 1980 -and
                $entry.LastWriteTime.Month -eq 1 -and
                $entry.LastWriteTime.Day -eq 1 -and
                $entry.LastWriteTime.Hour -eq 0 -and
                $entry.LastWriteTime.Minute -eq 0 -and
                $entry.LastWriteTime.Second -eq 0) -Message "Release archive '$FileName' has non-replayable entry metadata: $($entry.FullName)"
            $leafName = [System.IO.Path]::GetFileName($entry.FullName)
            Assert-Condition -Condition (
                $leafName -notmatch '^(?i:appsettings\.Development\.json|local\.settings(?:\..+)?\.json|secrets?\.json|\.env(?:\..*)?)$' -and
                $leafName -notmatch '(?i:\.(?:pfx|p12|pem|key))$') -Message "Release archive '$FileName' contains a development or secret-bearing file: $($entry.FullName)"
            $entryNames.Add($entry.FullName)
        }

        foreach ($requiredEntry in $RequiredEntries) {
            Assert-Condition -Condition ($entryNames -ccontains $requiredEntry) -Message "Release archive '$FileName' is missing '$requiredEntry'."
        }
        if ($ExactEntries.IsPresent) {
            Assert-ExactNames -Actual $entryNames.ToArray() -Expected $RequiredEntries -Description "Release archive '$FileName' entries"
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Read-ArchiveEntryText {
    param(
        [Parameter(Mandatory)][string]$ArchiveFileName,
        [Parameter(Mandatory)][string]$EntryName,
        [int64]$MaximumLength = 1048576
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead((Join-Path $artifactRoot $ArchiveFileName))
    try {
        $matches = @($archive.Entries | Where-Object { $_.FullName -ceq $EntryName })
        Assert-Condition -Condition ($matches.Count -eq 1) -Message "Release archive '$ArchiveFileName' must contain exactly one '$EntryName' entry."
        Assert-Condition -Condition ($matches[0].Length -le $MaximumLength) -Message "Release archive '$ArchiveFileName' entry '$EntryName' is unexpectedly large."
        $reader = [System.IO.StreamReader]::new(
            $matches[0].Open(),
            [System.Text.UTF8Encoding]::new($false, $true),
            $true)
        try {
            return $reader.ReadToEnd()
        }
        finally {
            $reader.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-WorkerFunctionNames {
    $metadataText = Read-ArchiveEntryText -ArchiveFileName $workerArtifactName -EntryName 'functions.metadata'
    try {
        $metadata = $metadataText | ConvertFrom-Json -Depth 30
    }
    catch {
        throw "Release archive '$workerArtifactName' functions.metadata is not valid JSON."
    }

    $functions = @($metadata)
    $names = @($functions | ForEach-Object { [string]$_.name })
    Assert-Condition -Condition ($names.Count -eq @($names | Sort-Object -CaseSensitive -Unique).Count) -Message "Release archive '$workerArtifactName' contains missing or duplicate function names."
    foreach ($name in $names) {
        Assert-Condition -Condition ($name -cmatch '^[A-Za-z0-9_]+$') -Message "Release archive '$workerArtifactName' contains an unsafe function name: '$name'."
    }
    Assert-ExactNames -Actual $names -Expected $expectedWorkerFunctions -Description 'Release Worker function metadata'
    return $names
}

function Assert-BootstrapManifestContract {
    param(
        [Parameter(Mandatory)][object]$BootstrapManifest,
        [Parameter(Mandatory)][string]$ExpectedSourceRevision
    )

    Assert-ExactNames -Actual $BootstrapManifest.PSObject.Properties.Name -Expected @(
        'schemaVersion',
        'productVersion',
        'sourceRevision',
        'expectedMigrationId',
        'targetIdentity',
        'sqlServer',
        'sqlDatabase',
        'issuer',
        'administrators',
        'publicMcpClient') -Description 'Bootstrap manifest properties'
    Assert-Condition -Condition (
        $BootstrapManifest.schemaVersion -eq 1 -and
        [string]$BootstrapManifest.productVersion -ceq $releaseVersion -and
        [string]$BootstrapManifest.sourceRevision -ceq $ExpectedSourceRevision -and
        [string]$BootstrapManifest.expectedMigrationId -ceq $expectedMigrationId -and
        [string]$BootstrapManifest.sqlServer -cmatch '^[a-z0-9][a-z0-9.-]*\.database\.windows\.net$' -and
        -not [string]::IsNullOrWhiteSpace([string]$BootstrapManifest.sqlDatabase) -and
        [string]$BootstrapManifest.targetIdentity -cmatch '^sqlserver://[a-z0-9][a-z0-9.-]*\.database\.windows\.net/[A-Za-z0-9._~%+-]+$') -Message 'Bootstrap manifest has invalid version, source, migration, or SQL target metadata.'

    $expectedTargetIdentity = "sqlserver://$($BootstrapManifest.sqlServer)/$([System.Uri]::EscapeDataString([string]$BootstrapManifest.sqlDatabase))"
    Assert-Condition -Condition (
        [string]$BootstrapManifest.targetIdentity -ceq $expectedTargetIdentity) -Message 'Bootstrap manifest targetIdentity does not exactly match its SQL server and database.'

    try {
        $issuer = [System.Uri]::new(
            [string]$BootstrapManifest.issuer,
            [System.UriKind]::Absolute)
    }
    catch {
        throw 'Bootstrap manifest issuer must be an absolute HTTPS origin.'
    }
    Assert-Condition -Condition (
        $issuer.Scheme -ceq 'https' -and
        $issuer.AbsolutePath -ceq '/' -and
        [string]::IsNullOrEmpty($issuer.Query) -and
        [string]::IsNullOrEmpty($issuer.Fragment) -and
        [string]::IsNullOrEmpty($issuer.UserInfo)) -Message 'Bootstrap manifest issuer must be an exact HTTPS origin.'

    $administrators = @($BootstrapManifest.administrators)
    Assert-Condition -Condition ($administrators.Count -eq 2) -Message 'Bootstrap manifest must contain exactly two administrators.'
    $manifestIdentities = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::Ordinal)
    $userNames = [System.Collections.Generic.HashSet[string]]::new(
        [System.StringComparer]::OrdinalIgnoreCase)
    foreach ($administrator in $administrators) {
        Assert-Condition -Condition ($null -ne $administrator) -Message 'Bootstrap manifest contains a null administrator.'
        Assert-ExactNames -Actual $administrator.PSObject.Properties.Name -Expected @(
            'manifestIdentity',
            'userName') -Description 'Bootstrap administrator properties'
        $manifestIdentity = [string]$administrator.manifestIdentity
        $userName = [string]$administrator.userName
        Assert-Condition -Condition (
            -not [string]::IsNullOrWhiteSpace($manifestIdentity) -and
            $manifestIdentity -ceq $manifestIdentity.Trim() -and
            -not [string]::IsNullOrWhiteSpace($userName) -and
            $userName -ceq $userName.Trim() -and
            $manifestIdentities.Add($manifestIdentity) -and
            $userNames.Add($userName)) -Message 'Bootstrap administrators must have trimmed, distinct identities and user names.'
    }

    $publicClient = $BootstrapManifest.publicMcpClient
    Assert-ExactNames -Actual $publicClient.PSObject.Properties.Name -Expected @(
        'clientId',
        'displayName',
        'redirectUris',
        'resource',
        'scopes') -Description 'Bootstrap public MCP client properties'
    $clientId = [string]$publicClient.clientId
    $displayName = [string]$publicClient.displayName
    $redirectUris = @($publicClient.redirectUris)
    Assert-Condition -Condition (
        -not [string]::IsNullOrWhiteSpace($clientId) -and
        $clientId -ceq $clientId.Trim() -and
        -not [string]::IsNullOrWhiteSpace($displayName) -and
        $displayName -ceq $displayName.Trim() -and
        $redirectUris.Count -gt 0) -Message 'Bootstrap public MCP client metadata is incomplete.'
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
        throw 'Bootstrap public MCP client URIs must be absolute.'
    }
    $expectedResource = [System.Uri]::new($issuer, '/mcp')
    Assert-Condition -Condition ($resource.Equals($expectedResource)) -Message 'Bootstrap public MCP client resource must be the issuer /mcp URI.'
    Assert-ExactNames -Actual @(
        $publicClient.scopes | ForEach-Object { [string]$_ }) -Expected @(
        'pegasus.read',
        'pegasus.write') -Description 'Bootstrap public MCP client scopes'
}

function Assert-BootstrapManifest {
    param([Parameter(Mandatory)][object]$ReleaseManifest)

    Assert-Condition -Condition ($ReleaseManifest.PSObject.Properties.Name -contains 'bootstrapManifest') -Message 'Release manifest is missing the approved bootstrap-manifest digest.'
    $record = $ReleaseManifest.bootstrapManifest
    Assert-ExactNames -Actual $record.PSObject.Properties.Name -Expected @(
        'entryName',
        'byteLength',
        'sha256') -Description 'Release bootstrap-manifest digest properties'
    Assert-Condition -Condition (
        [string]$record.entryName -ceq 'bootstrap-manifest.json' -and
        [long]$record.byteLength -gt 0 -and
        [string]$record.sha256 -cmatch '^[0-9a-f]{64}$') -Message 'Release bootstrap-manifest digest is invalid.'

    $archive = [System.IO.Compression.ZipFile]::OpenRead(
        (Join-Path $artifactRoot $bootstrapArtifactName))
    try {
        $matches = @($archive.Entries | Where-Object {
            $_.FullName -ceq 'bootstrap-manifest.json'
        })
        Assert-Condition -Condition ($matches.Count -eq 1) -Message "Release archive '$bootstrapArtifactName' must contain exactly one bootstrap-manifest.json."
        Assert-Condition -Condition ($matches[0].Length -eq [long]$record.byteLength) -Message 'Bootstrap manifest length differs from the release manifest.'
        $entryStream = $matches[0].Open()
        try {
            $entryHash = Get-StreamSha256 -Stream $entryStream
        }
        finally {
            $entryStream.Dispose()
        }
        Assert-Condition -Condition ($entryHash -ceq [string]$record.sha256) -Message 'Bootstrap manifest hash differs from the release manifest.'
    }
    finally {
        $archive.Dispose()
    }

    $manifestText = Read-ArchiveEntryText -ArchiveFileName $bootstrapArtifactName -EntryName 'bootstrap-manifest.json'
    Assert-Condition -Condition ($manifestText -notmatch '(?i)"(?:password|secret|token|connectionString)"\s*:') -Message 'Bootstrap manifest contains a forbidden secret-bearing field.'
    try {
        $bootstrapManifest = $manifestText | ConvertFrom-Json -Depth 50
    }
    catch {
        throw "Release archive '$bootstrapArtifactName' bootstrap-manifest.json is not valid JSON."
    }
    Assert-Condition -Condition ($null -ne $bootstrapManifest) -Message "Release archive '$bootstrapArtifactName' bootstrap manifest is empty."
    Assert-BootstrapManifestContract -BootstrapManifest $bootstrapManifest -ExpectedSourceRevision ([string]$ReleaseManifest.sourceRevision)
}

function Get-ManifestInputRecord {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][string]$RelativePath
    )

    $matches = @($Manifest.inputs | Where-Object { [string]$_.path -ceq $RelativePath })
    Assert-Condition -Condition ($matches.Count -eq 1) -Message "Release manifest must contain exactly one input record for '$RelativePath'."
    return $matches[0]
}

function Expand-ValidatedDeploymentInputs {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][string]$DestinationRoot
    )

    $archive = [System.IO.Compression.ZipFile]::OpenRead(
        (Join-Path $artifactRoot $deploymentArtifactName))
    try {
        foreach ($entry in $archive.Entries) {
            $record = Get-ManifestInputRecord -Manifest $Manifest -RelativePath $entry.FullName
            Assert-Condition -Condition ([string]$record.purpose -ceq 'deployment') -Message "Packaged deployment input '$($entry.FullName)' is not marked as a deployment input."
            Assert-Condition -Condition ($entry.Length -eq [long]$record.byteLength) -Message "Packaged deployment input length differs from the manifest: $($entry.FullName)"
            $hashStream = $entry.Open()
            try {
                $entryHash = Get-StreamSha256 -Stream $hashStream
            }
            finally {
                $hashStream.Dispose()
            }
            Assert-Condition -Condition ($entryHash -ceq [string]$record.sha256) -Message "Packaged deployment input hash differs from the manifest: $($entry.FullName)"

            $destinationPath = [System.IO.Path]::GetFullPath((Join-Path $DestinationRoot $entry.FullName.Replace('/', [System.IO.Path]::DirectorySeparatorChar)))
            $destinationPrefix = [System.IO.Path]::GetFullPath($DestinationRoot).TrimEnd(
                [System.IO.Path]::DirectorySeparatorChar,
                [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            Assert-Condition -Condition ($destinationPath.StartsWith($destinationPrefix, [System.StringComparison]::OrdinalIgnoreCase)) -Message "Packaged deployment input escaped its validation directory: $($entry.FullName)"
            [System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($destinationPath)) | Out-Null
            $input = $entry.Open()
            try {
                $output = [System.IO.File]::Open(
                    $destinationPath,
                    [System.IO.FileMode]::CreateNew,
                    [System.IO.FileAccess]::Write,
                    [System.IO.FileShare]::None)
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
        Assert-Condition -Condition ($source -notmatch $forbiddenPattern) -Message "Packaged Azure deployment inputs contain forbidden release material matching '$forbiddenPattern'."
    }

    $parameters = [System.IO.File]::ReadAllText(
        (Join-Path $DeploymentRoot 'infra/main.parameters.json')) | ConvertFrom-Json -Depth 50
    Assert-Condition -Condition ([string]$parameters.parameters.deploymentMode.value -ceq 'offline-replay') -Message 'Packaged Azure parameters must remain fail-closed with deploymentMode=offline-replay.'
    $azureYaml = [System.IO.File]::ReadAllText((Join-Path $DeploymentRoot 'azure.yaml'))
    Assert-Condition -Condition ($azureYaml -match '(?m)^\s*template:\s*pegasus@0\.1\.0-alpha\.1\s*$') -Message "Packaged azure.yaml must identify release version '$releaseVersion'."
}

function Get-TemplateResources {
    param([Parameter(Mandatory)][object]$Template)

    foreach ($resource in @($Template.resources)) {
        Write-Output $resource
        if ($resource.type -eq 'Microsoft.Resources/deployments' -and
            $null -ne $resource.properties -and
            $null -ne $resource.properties.template) {
            Get-TemplateResources -Template $resource.properties.template
        }
    }
}

function Assert-CompiledTemplateContract {
    param(
        [Parameter(Mandatory)][object]$Template,
        [Parameter(Mandatory)][string[]]$WorkerFunctionNames,
        [Parameter(Mandatory)][string]$PackagedBicepPath
    )

    $modeParameter = $Template.parameters.deploymentMode
    Assert-Condition -Condition ($null -ne $modeParameter) -Message 'The packaged deployment template must declare deploymentMode.'
    Assert-Condition -Condition (@($modeParameter.allowedValues).Count -eq 1 -and [string]$modeParameter.allowedValues[0] -eq 'offline-replay') -Message 'The packaged deployment template must permit only offline-replay mode.'

    $resourceGroup = @($Template.resources | Where-Object { $_.type -eq 'Microsoft.Resources/resourceGroups' })
    Assert-Condition -Condition ($resourceGroup.Count -eq 1) -Message 'The packaged deployment template must contain one guarded resource-group declaration.'
    Assert-Condition -Condition ([string]$resourceGroup[0].condition -eq "[variables('activationAllowed')]") -Message 'The compiled resource-group declaration is not controlled by activationAllowed.'
    Assert-Condition -Condition ([string]$Template.variables.activationAllowed -eq "[equals(parameters('deploymentMode'), 'approved-live-deployment')]") -Message 'The compiled activation guard does not require the unpermitted approved-live-deployment mode.'

    $functionApps = @(
        Get-TemplateResources -Template $Template |
            Where-Object {
                $_.type -eq 'Microsoft.Web/sites' -and
                [string]$_.kind -match '(^|,)functionapp(,|$)'
            }
    )
    Assert-Condition -Condition ($functionApps.Count -eq 1) -Message 'The packaged deployment template must contain exactly one Function App.'
    $workerAppSettings = @($functionApps[0].properties.siteConfig.appSettings)
    $disabledSettings = @(
        $workerAppSettings |
            Where-Object { [string]$_.name -cmatch '^AzureWebJobs\.[A-Za-z0-9_]+\.Disabled$' }
    )
    $disabledFunctionNames = @(
        $disabledSettings |
            ForEach-Object {
                ([string]$_.name).Substring(
                    'AzureWebJobs.'.Length,
                    ([string]$_.name).Length - 'AzureWebJobs.'.Length - '.Disabled'.Length)
            }
    )
    Assert-ExactNames -Actual $disabledFunctionNames -Expected $WorkerFunctionNames -Description 'Function App disabled-trigger settings'
    foreach ($setting in $disabledSettings) {
        Assert-Condition -Condition ([string]$setting.value -ceq 'true') -Message "The Function App app setting '$($setting.name)' must be 'true' in the packaged deployment template."
    }

    $workerSettingsByName = [System.Collections.Generic.Dictionary[string, object]]::new(
        [System.StringComparer]::Ordinal)
    foreach ($setting in $workerAppSettings) {
        $settingName = [string]$setting.name
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($settingName)) -Message 'The Function App contains an app setting without an exact name.'
        Assert-Condition -Condition ($workerSettingsByName.TryAdd($settingName, $setting)) -Message "The Function App contains a duplicate app setting named '$settingName'."
    }

    $requiredWorkerAzureSettings = @(
        'AzureIdentity__WorkerClientId',
        'AzureWebJobsStorage__clientId',
        'AzureWebJobsStorage__credential',
        'IntakeStorage__ServiceUri',
        'IntakeQueue__ServiceUri',
        'ExternalWorkQueue__ServiceUri')
    foreach ($settingName in $requiredWorkerAzureSettings) {
        Assert-Condition -Condition ($workerSettingsByName.ContainsKey($settingName)) -Message "The Function App is missing required Worker Azure setting '$settingName'."
        $settingValue = [string]$workerSettingsByName[$settingName].value
        Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace($settingValue)) -Message "The Function App Worker Azure setting '$settingName' must have a non-empty value."
        Assert-Condition -Condition (
            $settingValue -notmatch '(?i:DefaultEndpointsProtocol|AccountKey|SharedAccessSignature|listKeys\s*\(|\.outputs\.)') -Message "The Function App Worker Azure setting '$settingName' must be direct non-secret identity or endpoint metadata, not a connection string, key, or deployment output."
    }

    $workerClientIdValue = [string]$workerSettingsByName['AzureIdentity__WorkerClientId'].value
    $hostStorageClientIdValue = [string]$workerSettingsByName['AzureWebJobsStorage__clientId'].value
    Assert-Condition -Condition ($workerClientIdValue -ceq $hostStorageClientIdValue) -Message 'AzureIdentity__WorkerClientId and AzureWebJobsStorage__clientId must select the same exact Worker user-assigned managed identity.'
    Assert-Condition -Condition (
        [string]$workerSettingsByName['AzureWebJobsStorage__credential'].value -ceq
        'managedidentity') -Message 'AzureWebJobsStorage__credential must require managed identity authentication.'
    Assert-Condition -Condition (
        [string]$workerSettingsByName['IntakeQueue__ServiceUri'].value -ceq
        [string]$workerSettingsByName['ExternalWorkQueue__ServiceUri'].value) -Message 'IntakeQueue__ServiceUri and ExternalWorkQueue__ServiceUri must identify the same pre-provisioned Queue service endpoint.'
    Assert-Condition -Condition (-not $workerSettingsByName.ContainsKey('AZURE_CLIENT_ID')) -Message 'The Worker Function App must not rely on the ambient AZURE_CLIENT_ID fallback.'
    foreach ($forbiddenSettingName in @(
        'AzureWebJobsStorage',
        'IntakeStorage__ConnectionString',
        'ConnectionStrings__IntakeStorage')) {
        Assert-Condition -Condition (-not $workerSettingsByName.ContainsKey($forbiddenSettingName)) -Message "The Worker Function App must not contain storage connection-string setting '$forbiddenSettingName'."
    }

    $bicepSource = [System.IO.File]::ReadAllText($PackagedBicepPath)
    Assert-Condition -Condition ($bicepSource -match "activationAllowed\s*=\s*deploymentMode\s*==\s*'approved-live-deployment'") -Message 'The packaged Bicep source does not retain the explicit approval-only activation guard.'
    Assert-Condition -Condition ($bicepSource -match 'resource\s+resourceGroup\b[^{=]*=\s*if\s*\(activationAllowed\)') -Message 'The packaged resource-group declaration is not controlled by the activation guard.'
}

if ($Mode -cne 'Local') {
    throw "Only Local validation is supported. Cloud activation is intentionally unavailable."
}
$checkoutSourceRevision = Resolve-CleanRepositoryHead

Assert-Condition -Condition ([System.IO.File]::Exists($deploymentPlanPath)) -Message 'The deployment plan is missing.'
Assert-Condition -Condition ([System.IO.Directory]::Exists($artifactRoot)) -Message "Artifact directory is missing: $artifactRoot"
$manifestPath = Join-Path $artifactRoot $manifestFileName
Assert-Condition -Condition ([System.IO.File]::Exists($manifestPath)) -Message 'Release manifest is missing.'
try {
    $manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 30
}
catch {
    throw 'Release manifest is not valid JSON.'
}
Assert-Condition -Condition ($manifest.schemaVersion -eq 1) -Message 'Release manifest schemaVersion must be 1.'
Assert-Condition -Condition ([string]$manifest.releaseMode -ceq 'offline-replay') -Message 'Release manifest is not an offline replay artifact.'
Assert-Condition -Condition ([string]$manifest.sourceRevision -cmatch '^[0-9a-f]{40}$') -Message 'Release manifest sourceRevision must be the exact lowercase 40-character checkout revision.'
Assert-Condition -Condition ([string]$manifest.sourceRevision -ceq $checkoutSourceRevision) -Message "Release manifest sourceRevision '$($manifest.sourceRevision)' does not match the clean checkout HEAD '$checkoutSourceRevision'."
Assert-Condition -Condition ($manifest.PSObject.Properties.Name -contains 'releaseInputTree') -Message 'Release manifest is missing the tracked release-input tree digest.'
Assert-ReleaseInputTree -ManifestRecord $manifest.releaseInputTree -Revision $checkoutSourceRevision
Assert-Condition -Condition ($manifest.PSObject.Properties.Name -contains 'webDiagnostic') -Message 'Release manifest is missing the verified Web build diagnostic.'
Assert-Condition -Condition ($manifest.webDiagnostic.schemaVersion -eq 1) -Message 'Release manifest Web build diagnostic schemaVersion must be 1.'
Assert-Condition -Condition ([string]$manifest.webDiagnostic.version -ceq $releaseVersion) -Message 'Release manifest Web build diagnostic version does not match the release version.'
Assert-Condition -Condition ([string]$manifest.webDiagnostic.sourceSha -ceq [string]$manifest.sourceRevision) -Message 'Release manifest Web source SHA does not match its exact sourceRevision.'

Assert-ManifestDigest -ManifestPath $manifestPath
Assert-ExactOutputDirectory
Assert-VersionAndToolchainContract -Manifest $manifest
Assert-ManifestInputs -Manifest $manifest
Assert-Condition -Condition (@($manifest.artifacts).Count -eq $artifactContracts.Count) -Message 'Release manifest must contain exactly the five named deployable artifact families.'
$artifactRecords = @(
    foreach ($contract in $artifactContracts) {
        Get-ExpectedArtifact -Manifest $manifest -Contract $contract
    }
)
foreach ($record in $artifactRecords) {
    Assert-ArtifactHash -Record $record
}
Assert-Condition -Condition (
    [string]$manifest.runtimes.web -ceq 'linux-x64-framework-dependent' -and
    [string]$manifest.runtimes.worker -ceq 'linux-x64-framework-dependent' -and
    [string]$manifest.runtimes.migration -ceq 'win-x64-self-contained' -and
    [string]$manifest.runtimes.bootstrap -ceq 'win-x64-self-contained' -and
    [string]$manifest.runtimes.azureDeploymentInputs -ceq 'bicep-parameters') -Message 'Release manifest runtime diagnostics do not match the fixed deployment contract.'

Assert-ArchiveContract -FileName $webArtifactName -RequiredEntries @(
    'Pegasus.Web.dll',
    'Pegasus.Web.deps.json',
    'Pegasus.Web.runtimeconfig.json',
    'appsettings.json')
Assert-ArchiveContract -FileName $workerArtifactName -RequiredEntries @(
    'host.json',
    'functions.metadata',
    'Pegasus.Worker.dll',
    'Pegasus.Worker.deps.json',
    'Pegasus.Worker.runtimeconfig.json')
Assert-ArchiveContract -FileName $migrationArtifactName -RequiredEntries @('Pegasus.Migrations.exe') -ExactEntries
Assert-ArchiveContract -FileName $bootstrapArtifactName -RequiredEntries @(
    'Pegasus.Bootstrap.exe',
    'bootstrap-manifest.json')
Assert-ArchiveContract -FileName $deploymentArtifactName -RequiredEntries $deploymentInputPaths -ExactEntries
$workerFunctionNames = @(Get-WorkerFunctionNames)
Assert-BootstrapManifest -ReleaseManifest $manifest

$validationRoot = Join-Path ([System.IO.Path]::GetTempPath()) "pegasus-deployment-plan-$([System.Guid]::NewGuid().ToString('N'))"
$templatePath = Join-Path $validationRoot 'compiled-template.json'
try {
    [System.IO.Directory]::CreateDirectory($validationRoot) | Out-Null
    Expand-ValidatedDeploymentInputs -Manifest $manifest -DestinationRoot $validationRoot
    Assert-DeploymentInputsAreFailClosed -DeploymentRoot $validationRoot
    $packagedBicepPath = Join-Path $validationRoot 'infra/main.bicep'

    $bicep = Get-Command -Name bicep -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    $az = Get-Command -Name az -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    Assert-Condition -Condition ($null -ne $bicep -or $null -ne $az) -Message 'Bicep CLI or Azure CLI with Bicep is required for offline plan compilation.'
    if ($null -ne $bicep) {
        & $bicep.Source build $packagedBicepPath --no-restore --outfile $templatePath
    }
    else {
        & $az.Source bicep build --file $packagedBicepPath --no-restore --outfile $templatePath
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Packaged Bicep compilation failed with exit code $LASTEXITCODE."
    }

    $template = [System.IO.File]::ReadAllText($templatePath) | ConvertFrom-Json -Depth 100
    Assert-CompiledTemplateContract -Template $template -WorkerFunctionNames $workerFunctionNames -PackagedBicepPath $packagedBicepPath
    Assert-CleanRepositoryHead -ExpectedRevision $checkoutSourceRevision
}
finally {
    if ([System.IO.Directory]::Exists($validationRoot)) {
        [System.IO.Directory]::Delete($validationRoot, $true)
    }
}

Write-Output "Exact immutable release artifacts and packaged fail-closed deployment inputs are valid for '$checkoutSourceRevision'. No source rebuild, Azure authentication, provisioning, or deployment was performed."
