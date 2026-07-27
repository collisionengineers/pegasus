[CmdletBinding()]
param(
    [string]$PythonExecutable = "python",
    [string]$WorkbookRoot = "docs/reference/workproviders-and-repairers",
    [string]$ToolRoot = "artifacts/reference-data-tools",
    [string]$StagingRoot = "artifacts/reference-data-staging",
    [string]$PackagePath = "src/CollisionSpike.Infrastructure/ReferenceData/provider-reference-data.v1.json",
    [string]$ManifestPath = "src/CollisionSpike.Infrastructure/ReferenceData/provider-reference-data.v1.manifest.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Stop-Authoring {
    param(
        [Parameter(Mandatory)] [string]$Category,
        [Parameter(Mandatory)] [string]$Message,
        [Parameter(Mandatory)] [int]$ExitCode
    )

    [Console]::Error.WriteLine("ERROR[{0}] {1}" -f $Category, $Message)
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

function Test-PathIsWithin {
    param(
        [Parameter(Mandatory)] [string]$Candidate,
        [Parameter(Mandatory)] [string]$Parent
    )

    $candidateFull = [System.IO.Path]::GetFullPath($Candidate).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $parentFull = [System.IO.Path]::GetFullPath($Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    return $candidateFull.Equals($parentFull, [System.StringComparison]::OrdinalIgnoreCase) -or $candidateFull.StartsWith(
        $parentFull + [System.IO.Path]::DirectorySeparatorChar,
        [System.StringComparison]::OrdinalIgnoreCase
    )
}

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$WorkbookRoot = Resolve-RepositoryPath -Path $WorkbookRoot -RepositoryRoot $RepositoryRoot
$ToolRoot = Resolve-RepositoryPath -Path $ToolRoot -RepositoryRoot $RepositoryRoot
$StagingRoot = Resolve-RepositoryPath -Path $StagingRoot -RepositoryRoot $RepositoryRoot
$PackagePath = Resolve-RepositoryPath -Path $PackagePath -RepositoryRoot $RepositoryRoot
$ManifestPath = Resolve-RepositoryPath -Path $ManifestPath -RepositoryRoot $RepositoryRoot
$HelperPath = Join-Path $RepositoryRoot "scripts/reference_data/build_provider_reference_data.py"
$RequirementsPath = Join-Path $RepositoryRoot "scripts/reference-data-requirements.lock"

if (-not (Test-Path -LiteralPath $WorkbookRoot -PathType Container)) {
    Stop-Authoring -Category "missing-input" -Message "Workbook root does not exist: docs/reference/workproviders-and-repairers." -ExitCode 20
}

# This is deliberately the first operation that enumerates the supplied input.
# It runs before Python discovery, dependency installation, workbook hashing,
# workbook parsing, staging, and every output write.
try {
    $OfficeLocks = @(
        Get-ChildItem -LiteralPath $WorkbookRoot -Recurse -Force -File |
            Where-Object { $_.Name -match '^~\$.*\.xls.*$' } |
            Sort-Object -Property FullName
    )
}
catch {
    Stop-Authoring -Category "unreadable-workbook" -Message "Could not inspect the workbook root for Office locks: $($_.Exception.Message)" -ExitCode 23
}

if ($OfficeLocks.Count -gt 0) {
    $lockNames = @(
        foreach ($lock in $OfficeLocks) {
            [System.IO.Path]::GetRelativePath($WorkbookRoot, $lock.FullName).Replace('\', '/')
        }
    ) -join ", "
    Stop-Authoring -Category "office-lock" -Message "Close every Office workbook and remove its lock file before authoring. Detected: $lockNames" -ExitCode 21
}

if ($PackagePath -ieq $ManifestPath) {
    Stop-Authoring -Category "output-collision" -Message "The package and manifest paths must be distinct." -ExitCode 25
}

foreach ($outputPath in @($ToolRoot, $StagingRoot, $PackagePath, $ManifestPath)) {
    if (Test-PathIsWithin -Candidate $outputPath -Parent $WorkbookRoot) {
        Stop-Authoring -Category "output-collision" -Message "Authoring artifacts must not be written beneath the immutable workbook root." -ExitCode 25
    }
}

foreach ($outputPath in @($PackagePath, $ManifestPath)) {
    if ((Test-Path -LiteralPath $outputPath) -and (Test-Path -LiteralPath $outputPath -PathType Container)) {
        Stop-Authoring -Category "output-collision" -Message "An output path is an existing directory: $outputPath" -ExitCode 25
    }
}

if (-not (Test-Path -LiteralPath $HelperPath -PathType Leaf)) {
    Stop-Authoring -Category "missing-input" -Message "The reference-data helper is missing." -ExitCode 20
}
if (-not (Test-Path -LiteralPath $RequirementsPath -PathType Leaf)) {
    Stop-Authoring -Category "missing-input" -Message "The hash-locked dependency requirements file is missing." -ExitCode 20
}

try {
    $versionOutput = (& $PythonExecutable -c 'import sys; print(f"{sys.version_info.major}.{sys.version_info.minor}")' 2>&1 | Out-String).Trim()
}
catch {
    Stop-Authoring -Category "python-version" -Message "Python 3.14 or later is required: $($_.Exception.Message)" -ExitCode 26
}
if ($LASTEXITCODE -ne 0 -or $versionOutput -notmatch '^(?<major>\d+)\.(?<minor>\d+)$') {
    Stop-Authoring -Category "python-version" -Message "Python 3.14 or later is required and could not be validated." -ExitCode 26
}
if ([int]$Matches.major -lt 3 -or ([int]$Matches.major -eq 3 -and [int]$Matches.minor -lt 14)) {
    Stop-Authoring -Category "python-version" -Message "Python 3.14 or later is required; detected $versionOutput." -ExitCode 26
}

try {
    $requirementsHash = (Get-FileHash -LiteralPath $RequirementsPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $toolEnvironment = Join-Path $ToolRoot "python-calamine-$requirementsHash"
    $sitePackages = Join-Path $toolEnvironment "site-packages"
    $wheelCache = Join-Path $ToolRoot "packages"

    if (-not (Test-Path -LiteralPath $wheelCache -PathType Container)) {
        Stop-Authoring -Category "missing-input" -Message "The local wheel cache is missing: artifacts/reference-data-tools/packages." -ExitCode 20
    }

    New-Item -ItemType Directory -Path $sitePackages -Force | Out-Null
    & $PythonExecutable -m pip install `
        --disable-pip-version-check `
        --no-input `
        --no-cache-dir `
        --no-index `
        --find-links $wheelCache `
        --require-hashes `
        --only-binary=:all: `
        --upgrade `
        --target $sitePackages `
        -r $RequirementsPath
    if ($LASTEXITCODE -ne 0) {
        Stop-Authoring -Category "dependency" -Message "The hash-locked local python-calamine dependency could not be installed." -ExitCode 26
    }
}
catch {
    if ($_.Exception -is [System.Management.Automation.ExitException]) {
        throw
    }
    Stop-Authoring -Category "dependency" -Message "The hash-locked local python-calamine dependency could not be installed: $($_.Exception.Message)" -ExitCode 26
}

$previousPythonPath = $env:PYTHONPATH
try {
    $env:PYTHONPATH = if ([string]::IsNullOrWhiteSpace($previousPythonPath)) {
        $sitePackages
    }
    else {
        "$sitePackages$([System.IO.Path]::PathSeparator)$previousPythonPath"
    }

    & $PythonExecutable $HelperPath `
        --repository-root $RepositoryRoot `
        --workbook-root $WorkbookRoot `
        --staging-root $StagingRoot `
        --package-path $PackagePath `
        --manifest-path $ManifestPath
    $helperExitCode = $LASTEXITCODE
}
finally {
    if ($null -eq $previousPythonPath) {
        Remove-Item Env:PYTHONPATH -ErrorAction SilentlyContinue
    }
    else {
        $env:PYTHONPATH = $previousPythonPath
    }
}

exit $helperExitCode
