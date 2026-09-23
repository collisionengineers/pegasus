[CmdletBinding()]
param(
    [string[]] $ChangedPath = @(),

    [switch] $ForceAll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Inputs outside src and tests that the build or the tests actually read, so a
# change to one of them is not "prose": the Architecture tests assert on
# infra/modules/platform.bicep, Core and integration tests read reference/ data
# and the scripts/*.sql resets, and Pegasus.Infrastructure embeds the brand logo
# the report renderer draws. scripts/jev-mail-eval is a standalone harness the
# unit lane is being extended to test; routing it here covers that from the
# commit that adds it.
$buildPattern = '^(src|tests)/|^Pegasus\.slnx$|\.csproj$|\.props$|\.targets$|packages\.lock\.json$|^global\.json$|^nuget\.config$|^infra/|^reference/|^docs/design/brand/logos/|^scripts/[^/]+\.sql$|^scripts/jev-mail-eval/|^scripts/test-shard-durations\.json$|^scripts/(Invoke-TestShard|Update-TestShardDurations|Test-(MainBranchHistory|TestShard)|Get-CiChangeFlags|Get-CiHeavyLaneDecision)\.ps1$|^\.github/workflows/ci\.yml$|^\.github/actions/'
# PegasusPlatform.ps1 carries the release manifest validator and the Worker
# census the release scripts share, so it routes to the infrastructure lane.
$infrastructurePattern = '^infra/|^azure\.yaml$|^src/Pegasus\.Infrastructure/Persistence/Migrations/|^scripts/(Get-CiChangeFlags|Get-CiHeavyLaneDecision|Test-CiChangeFlags|Test-AzureDeploymentPlan|Test-ReleaseValidation|Test-MigrationGrants|Invoke-ProductionSmoke|Build-ReleaseArtifacts|Invoke-ProductionAdministratorBootstrap|Invoke-AzureDatabaseBootstrap|PegasusPlatform)\.ps1$|^\.github/workflows/ci\.yml$'
# Test-PegasusPlatform.ps1 dot-sources PegasusPlatform.ps1 and nothing else, so
# the LocalDB lifecycle classifier contract turns on for that pair alone. The
# shared platform script deliberately routes to this lane and to infrastructure.
$localDevelopmentPattern = '^scripts/(PegasusPlatform|Test-PegasusPlatform|Clear-BuildOutput|Test-ClearBuildOutput)\.ps1$'
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
