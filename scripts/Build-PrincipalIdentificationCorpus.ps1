[CmdletBinding(DefaultParameterSetName = "Package")]
param(
    [Parameter(Mandatory, ParameterSetName = "Package")] [string]$CollisionSpikeRoot,
    [Parameter(Mandatory, ParameterSetName = "Package")]
    [Parameter(Mandatory, ParameterSetName = "Principal")] [string]$CorpusRoot,
    [Parameter(ParameterSetName = "Package")]
    [string]$PackagePath = "reference/workproviders-and-repairers/principal-identification-corpus.v1.json",
    [Parameter(ParameterSetName = "Package")] [switch]$Verify,
    [Parameter(Mandatory, ParameterSetName = "Principal")] [string]$PrincipalCode,
    [Parameter(ParameterSetName = "Principal")] [string]$ManifestPath
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
$ResolvedCorpusRoot = [System.IO.Path]::GetFullPath($CorpusRoot)
$HelperPath = Join-Path $RepositoryRoot "scripts/reference_data/build_principal_identification_corpus.py"

if (-not (Test-Path -LiteralPath $ResolvedCorpusRoot -PathType Container)) {
    throw "Immutable Pegasus corpus root not found: $ResolvedCorpusRoot"
}
if (-not (Test-Path -LiteralPath $HelperPath -PathType Leaf)) {
    throw "Principal-identification authoring helper not found: $HelperPath"
}

$helperArguments = @(
    "--repository-root", $RepositoryRoot,
    "--corpus-root", $ResolvedCorpusRoot
)
if ($PSCmdlet.ParameterSetName -eq "Principal") {
    if (-not $ManifestPath) {
        $ManifestPath = "artifacts/evaluation/principal-identification-$PrincipalCode-buckets.json"
    }
    $helperArguments += @(
        "--principal", $PrincipalCode,
        "--manifest-path", (Resolve-RepositoryPath -Path $ManifestPath -RepositoryRoot $RepositoryRoot)
    )
}
else {
    $ResolvedCollisionSpikeRoot = [System.IO.Path]::GetFullPath($CollisionSpikeRoot)
    if (-not (Test-Path -LiteralPath $ResolvedCollisionSpikeRoot -PathType Container)) {
        throw "CollisionSpike root not found: $ResolvedCollisionSpikeRoot"
    }
    $helperArguments += @(
        "--collision-spike-root", $ResolvedCollisionSpikeRoot,
        "--package-path", (Resolve-RepositoryPath -Path $PackagePath -RepositoryRoot $RepositoryRoot)
    )
    if ($Verify) {
        $helperArguments += "--verify"
    }
}

& python $HelperPath @helperArguments
exit $LASTEXITCODE
