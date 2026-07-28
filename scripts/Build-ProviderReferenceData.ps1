[CmdletBinding()]
param(
    [string]$SourcePath = "docs/reference/workproviders-and-repairers/initial.xlsx",
    [string]$Version = "provider-domains-v1",
    [string]$PackagePath = "src/Pegasus.Infrastructure/Persistence/ReferenceData/provider-domains.v1.json",
    [string]$PreviousPackagePath,
    [switch]$Verify
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Stop-Authoring {
    param(
        [Parameter(Mandatory)] [string]$Category,
        [Parameter(Mandatory)] [string]$Message,
        [Parameter(Mandatory)] [int]$ExitCode
    )

    [Console]::Error.WriteLine(("ERROR[{0}] {1}" -f $Category, $Message))
    exit $ExitCode
}

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$RepositoryRoot
    )

    if ([System.IO.Path]::IsPathFullyQualified($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot $Path))
}

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$ResolvedSourcePath = Resolve-RepositoryPath -Path $SourcePath -RepositoryRoot $RepositoryRoot
$ResolvedPackagePath = Resolve-RepositoryPath -Path $PackagePath -RepositoryRoot $RepositoryRoot
$ResolvedPreviousPackagePath = if ([string]::IsNullOrWhiteSpace($PreviousPackagePath)) {
    $null
}
else {
    Resolve-RepositoryPath -Path $PreviousPackagePath -RepositoryRoot $RepositoryRoot
}
$StagingRoot = Join-Path $RepositoryRoot "artifacts/reference-data-staging"
$HelperPath = Join-Path $RepositoryRoot "scripts/reference_data/build_provider_reference_data.py"

# Lock proof is deliberately first. It runs before Python discovery, source
# hashing/parsing, staging, and every output write.
$LockPath = Join-Path ([System.IO.Path]::GetDirectoryName($ResolvedSourcePath)) ('~$' + [System.IO.Path]::GetFileName($ResolvedSourcePath))
if (Test-Path -LiteralPath $LockPath) {
    Stop-Authoring -Category "source-locked" -Message "Close the selected source workbook before authoring." -ExitCode 21
}

try {
    $sourceStream = [System.IO.FileStream]::new(
        $ResolvedSourcePath,
        [System.IO.FileMode]::Open,
        [System.IO.FileAccess]::Read,
        [System.IO.FileShare]::None
    )
    $sourceStream.Dispose()
}
catch {
    Stop-Authoring -Category "source-locked" -Message "The selected source workbook is unavailable for exclusive read." -ExitCode 21
}

if (-not (Test-Path -LiteralPath $HelperPath -PathType Leaf)) {
    Stop-Authoring -Category "missing-input" -Message "The provider-domain authoring helper is missing." -ExitCode 20
}

try {
    $versionOutput = (& python -c 'import sys; print(f"{sys.version_info.major}.{sys.version_info.minor}")' 2>&1 | Out-String).Trim()
}
catch {
    Stop-Authoring -Category "python-version" -Message "Python 3.11 or later is required." -ExitCode 26
}
if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch '^(?<major>\d+)\.(?<minor>\d+)$') {
    Stop-Authoring -Category "python-version" -Message "Python 3.11 or later could not be validated." -ExitCode 26
}
if ([int]$Matches.major -lt 3 -or ([int]$Matches.major -eq 3 -and [int]$Matches.minor -lt 11)) {
    Stop-Authoring -Category "python-version" -Message "Python 3.11 or later is required." -ExitCode 26
}

$helperArguments = @(
    "--repository-root", $RepositoryRoot,
    "--source-path", $ResolvedSourcePath,
    "--version", $Version,
    "--package-path", $ResolvedPackagePath,
    "--staging-root", $StagingRoot
)
if ($null -ne $ResolvedPreviousPackagePath) {
    $helperArguments += @("--previous-package-path", $ResolvedPreviousPackagePath)
}
if ($Verify) {
    $helperArguments += "--verify"
}

& python $HelperPath @helperArguments
exit $LASTEXITCODE
