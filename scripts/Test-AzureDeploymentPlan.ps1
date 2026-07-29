[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ArtifactDirectory,

    [ValidateSet('OfflineReplay')]
    [string]$Mode = 'OfflineReplay'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [System.IO.Path]::GetFullPath($ArtifactDirectory)
$bicepPath = Join-Path $repositoryRoot 'infra/main.bicep'
$deploymentPlanPath = Join-Path $repositoryRoot '.azure/deployment-plan.md'

function Assert-Condition {
    param(
        [Parameter(Mandatory)][bool]$Condition,
        [Parameter(Mandatory)][string]$Message
    )

    if (-not $Condition) {
        throw $Message
    }
}

function Get-ExpectedArtifact {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][string]$Name
    )

    $matches = @($Manifest.artifacts | Where-Object { $_.fileName -eq $Name })
    Assert-Condition -Condition ($matches.Count -eq 1) -Message "Release manifest must contain exactly one '$Name' artifact."
    return $matches[0]
}

function Assert-ArtifactHash {
    param(
        [Parameter(Mandatory)][object]$Record
    )

    $fileName = [string]$Record.fileName
    Assert-Condition -Condition ($fileName -match '^[a-z0-9][a-z0-9.-]*$') -Message "Artifact file name is unsafe: '$fileName'."

    $path = Join-Path $artifactRoot $fileName
    Assert-Condition -Condition ([System.IO.File]::Exists($path)) -Message "Release artifact is missing: $fileName"
    $file = [System.IO.FileInfo]::new($path)
    Assert-Condition -Condition ($file.Length -eq [long]$Record.byteLength) -Message "Release artifact length differs from the manifest: $fileName"
    $actualHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
    Assert-Condition -Condition ($actualHash -eq [string]$Record.sha256) -Message "Release artifact hash differs from the manifest: $fileName"
}

function Assert-ArchiveContents {
    param(
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string[]]$RequiredEntries
    )

    $archivePath = Join-Path $artifactRoot $FileName
    $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName })
        Assert-Condition -Condition ($entries.Count -gt 0) -Message "Release archive is empty: $FileName"
        foreach ($entry in $entries) {
            Assert-Condition -Condition ($entry -notmatch '(^|/)\.\.(/|$)' -and -not $entry.StartsWith('/')) -Message "Release archive has an unsafe entry: $FileName/$entry"
        }
        foreach ($requiredEntry in $RequiredEntries) {
            Assert-Condition -Condition ($entries -contains $requiredEntry) -Message "Release archive '$FileName' is missing '$requiredEntry'."
        }
        foreach ($entry in $archive.Entries) {
            Assert-Condition -Condition ($entry.LastWriteTime.DateTime -eq [System.DateTime]::new(1980, 1, 1, 0, 0, 0, [System.DateTimeKind]::Unspecified)) -Message "Release archive '$FileName' has non-replayable entry metadata: $($entry.FullName)"
        }
    }
    finally {
        $archive.Dispose()
    }
}

if ($Mode -ne 'OfflineReplay') {
    throw "Only OfflineReplay validation is supported. Cloud activation is intentionally unavailable."
}

Assert-Condition -Condition ([System.IO.File]::Exists($bicepPath)) -Message 'The Bicep entrypoint is missing.'
Assert-Condition -Condition ([System.IO.File]::Exists($deploymentPlanPath)) -Message 'The deployment plan is missing.'
Assert-Condition -Condition ([System.IO.Directory]::Exists($artifactRoot)) -Message "Artifact directory is missing: $artifactRoot"

$manifestPath = Join-Path $artifactRoot 'release-manifest.json'
Assert-Condition -Condition ([System.IO.File]::Exists($manifestPath)) -Message 'Release manifest is missing.'
$manifest = [System.IO.File]::ReadAllText($manifestPath) | ConvertFrom-Json -Depth 10
Assert-Condition -Condition ($manifest.schemaVersion -eq 1) -Message 'Release manifest schemaVersion must be 1.'
Assert-Condition -Condition ($manifest.releaseMode -eq 'offline-replay') -Message 'Release manifest is not an offline replay artifact.'
Assert-Condition -Condition ([string]$manifest.sourceRevision -cmatch '^[0-9a-f]{40}$') -Message 'Release manifest sourceRevision must be the exact lowercase 40-character checkout revision.'
Assert-Condition -Condition ($manifest.PSObject.Properties.Name -contains 'webDiagnostic') -Message 'Release manifest is missing the verified Web build diagnostic.'
Assert-Condition -Condition ($manifest.webDiagnostic.schemaVersion -eq 1) -Message 'Release manifest Web build diagnostic schemaVersion must be 1.'
Assert-Condition -Condition (-not [string]::IsNullOrWhiteSpace([string]$manifest.webDiagnostic.version)) -Message 'Release manifest Web build diagnostic version is missing.'
Assert-Condition -Condition ([string]$manifest.webDiagnostic.sourceSha -ceq [string]$manifest.sourceRevision) -Message 'Release manifest Web source SHA does not match its exact sourceRevision.'
Assert-Condition -Condition (@($manifest.artifacts).Count -eq 3) -Message 'Release manifest must contain Web, Worker, and migration artifacts only.'

$webArtifact = Get-ExpectedArtifact -Manifest $manifest -Name 'web.zip'
$workerArtifact = Get-ExpectedArtifact -Manifest $manifest -Name 'worker.zip'
$migrationArtifact = Get-ExpectedArtifact -Manifest $manifest -Name 'migration.zip'
foreach ($artifact in @($webArtifact, $workerArtifact, $migrationArtifact)) {
    Assert-ArtifactHash -Record $artifact
}

Assert-ArchiveContents -FileName 'web.zip' -RequiredEntries @('Pegasus.Web.dll', 'Pegasus.Web.deps.json', 'Pegasus.Web.runtimeconfig.json')
Assert-ArchiveContents -FileName 'worker.zip' -RequiredEntries @('host.json', 'Pegasus.Worker.dll', 'Pegasus.Worker.deps.json', 'Pegasus.Worker.runtimeconfig.json')
Assert-ArchiveContents -FileName 'migration.zip' -RequiredEntries @('migration.sql')

$bicep = Get-Command -Name bicep -CommandType Application -ErrorAction SilentlyContinue
$az = Get-Command -Name az -CommandType Application -ErrorAction SilentlyContinue
Assert-Condition -Condition ($null -ne $bicep -or $null -ne $az) -Message 'Bicep CLI or Azure CLI with Bicep is required for offline plan compilation.'
$templatePath = Join-Path ([System.IO.Path]::GetTempPath()) "pegasus-deployment-plan-$([System.Guid]::NewGuid().ToString('N')).json"
try {
    if ($null -ne $bicep) {
        & $bicep.Source build $bicepPath --no-restore --outfile $templatePath
    }
    else {
        & az bicep build --file $bicepPath --no-restore --outfile $templatePath
    }
    if ($LASTEXITCODE -ne 0) {
        throw "Bicep compilation failed with exit code $LASTEXITCODE."
    }

    $template = [System.IO.File]::ReadAllText($templatePath) | ConvertFrom-Json -Depth 100
    $modeParameter = $template.parameters.deploymentMode
    Assert-Condition -Condition ($null -ne $modeParameter) -Message 'The deployment template must declare deploymentMode.'
    Assert-Condition -Condition (@($modeParameter.allowedValues).Count -eq 1 -and [string]$modeParameter.allowedValues[0] -eq 'offline-replay') -Message 'The deployment template must permit only offline-replay mode.'

    $resourceGroup = @($template.resources | Where-Object { $_.type -eq 'Microsoft.Resources/resourceGroups' })
    Assert-Condition -Condition ($resourceGroup.Count -eq 1) -Message 'The deployment template must contain one guarded resource-group declaration.'
    Assert-Condition -Condition ([string]$resourceGroup[0].condition -eq "[variables('activationAllowed')]") -Message 'The compiled resource-group declaration is not controlled by activationAllowed.'
    Assert-Condition -Condition ([string]$template.variables.activationAllowed -eq "[equals(parameters('deploymentMode'), 'approved-live-deployment')]") -Message 'The compiled activation guard does not require the unpermitted approved-live-deployment mode.'
    $bicepSource = [System.IO.File]::ReadAllText($bicepPath)
    Assert-Condition -Condition ($bicepSource -match "activationAllowed\s*=\s*deploymentMode\s*==\s*'approved-live-deployment'") -Message 'The Bicep source does not retain the explicit approval-only activation guard.'
    Assert-Condition -Condition ($bicepSource -match 'resource\s+resourceGroup\b[^{=]*=\s*if\s*\(activationAllowed\)') -Message 'The resource-group declaration is not controlled by the activation guard.'
}
finally {
    if ([System.IO.File]::Exists($templatePath)) {
        Remove-Item -LiteralPath $templatePath -Force
    }
}

Write-Output "Offline replay release artifacts and fail-closed deployment template are valid. No Azure authentication, provisioning, or deployment was performed."
