[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$CollisionSpikeRoot,
    [Parameter(Mandatory)] [string]$CorpusRoot,
    [string]$PackagePath = "reference/workproviders-and-repairers/principal-identification-corpus.v1.json",
    [switch]$Verify
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

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
$ResolvedCollisionSpikeRoot = [System.IO.Path]::GetFullPath($CollisionSpikeRoot)
$ResolvedCorpusRoot = [System.IO.Path]::GetFullPath($CorpusRoot)
$ResolvedPackagePath = Resolve-RepositoryPath -Path $PackagePath -RepositoryRoot $RepositoryRoot
$HelperPath = Join-Path $RepositoryRoot "scripts/reference_data/build_principal_identification_corpus.py"

if (-not (Test-Path -LiteralPath $ResolvedCollisionSpikeRoot -PathType Container)) {
    throw "CollisionSpike root not found: $ResolvedCollisionSpikeRoot"
}
if (-not (Test-Path -LiteralPath $ResolvedCorpusRoot -PathType Container)) {
    throw "Immutable Pegasus corpus root not found: $ResolvedCorpusRoot"
}
if (-not (Test-Path -LiteralPath $HelperPath -PathType Leaf)) {
    throw "Principal-identification authoring helper not found: $HelperPath"
}

$helperArguments = @(
    "--repository-root", $RepositoryRoot,
    "--collision-spike-root", $ResolvedCollisionSpikeRoot,
    "--corpus-root", $ResolvedCorpusRoot,
    "--package-path", $ResolvedPackagePath
)
if ($Verify) {
    $helperArguments += "--verify"
}

& python $HelperPath @helperArguments
exit $LASTEXITCODE
