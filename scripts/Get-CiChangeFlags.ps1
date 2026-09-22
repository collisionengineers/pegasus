[CmdletBinding()]
param(
    [string[]] $ChangedPath = @(),

    [switch] $ForceAll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# jev-mail-eval is outside Pegasus.slnx but the unit lane restores, builds and
# tests it, so its source has to route somewhere; its .csproj and lock files
# already matched the extension rules, its .cs files did not.
$buildPattern = '^(src|tests)/|^Pegasus\.slnx$|\.csproj$|\.props$|\.targets$|packages\.lock\.json$|^global\.json$|^nuget\.config$|^scripts/jev-mail-eval/|^scripts/test-shard-durations\.json$|^scripts/(Invoke-TestShard|Test-(MainBranchHistory|TestShard)|Get-CiChangeFlags)\.ps1$|^\.github/workflows/ci\.yml$|^\.github/actions/'
# PegasusPlatform.ps1 carries the release manifest validator and the Worker
# census the release scripts share, so it routes to the infrastructure lane.
$infrastructurePattern = '^infra/|^azure\.yaml$|^src/Pegasus\.Infrastructure/Persistence/Migrations/|^scripts/(Get-CiChangeFlags|Test-CiChangeFlags|Test-AzureDeploymentPlan|Test-ReleaseValidation|Test-MigrationGrants|Invoke-ProductionSmoke|Build-ReleaseArtifacts|Invoke-ProductionAdministratorBootstrap|Invoke-AzureDatabaseBootstrap|PegasusPlatform)\.ps1$|^\.github/workflows/ci\.yml$'
# Test-PegasusPlatform.ps1 dot-sources PegasusPlatform.ps1 and nothing else, so
# the LocalDB lifecycle classifier contract turns on for that pair alone. The
# shared platform script deliberately routes to this lane and to infrastructure.
$localDevelopmentPattern = '^scripts/(PegasusPlatform|Test-PegasusPlatform)\.ps1$'
# The provider-reference generator and its tests share one scripts-owned
# component; the two PowerShell wrappers are its only other callers.
$referenceDataPattern = '^scripts/reference_data/|^scripts/Build-(ProviderReferenceData|PrincipalIdentificationCorpus)\.ps1$'

function Test-AnyPath {
    param(
        [string[]] $Path = @(),
        [Parameter(Mandatory)][string] $Pattern
    )

    return $Path.Count -gt 0 -and [bool]($Path |
        Where-Object { $_ -match $Pattern } |
        Select-Object -First 1)
}

[pscustomobject]@{
    Build = $ForceAll -or (Test-AnyPath -Path $ChangedPath -Pattern $buildPattern)
    Infrastructure = $ForceAll -or (Test-AnyPath -Path $ChangedPath -Pattern $infrastructurePattern)
    LocalDevelopment = $ForceAll -or (Test-AnyPath -Path $ChangedPath -Pattern $localDevelopmentPattern)
    ReferenceData = $ForceAll -or (Test-AnyPath -Path $ChangedPath -Pattern $referenceDataPattern)
}
