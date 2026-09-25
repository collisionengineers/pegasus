[CmdletBinding()]
param(
    [string[]] $ChangedPath = @(),

    [switch] $ForceAll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Paths no CI job reads, so a change confined to them starts nothing. The
# standalone local tools under scripts/ belong to no CI lane, project and lock
# files included. The local-development toolchain is exercised by a local
# Start, never by a workflow job: package.json and package-lock.json pin the
# Azurite that Invoke-LocalDevelopment.ps1 runs, .config/dotnet-tools.json pins
# the dotnet-ef that Initialize-LocalDevelopment.ps1 restores and a release
# workstation runs, and Invoke-Doctor.ps1 only checks a workstation.
$noLanePattern = '^scripts/(jev-mail-eval|email-eval-desktop)/|^package(-lock)?\.json$|^\.config/dotnet-tools\.json$|^scripts/(Initialize-LocalDevelopment|Invoke-LocalDevelopment|Invoke-Doctor)\.ps1$'
$ChangedPath = @($ChangedPath | Where-Object { $_ -notmatch $noLanePattern })

# Inputs outside src and tests that the build or the tests actually read, so a
# change to one of them is not "prose": the Architecture tests assert on
# infra/modules/platform.bicep, Core and integration tests read reference/ data
# and the scripts/*.sql resets, and Pegasus.Infrastructure embeds the brand logo
# the report renderer draws, and .gitattributes fixes the checked-out bytes of
# the reference data and embedded resources those tests read.
$buildPattern = '^(src|tests)/|^Pegasus\.slnx$|\.csproj$|\.props$|\.targets$|packages\.lock\.json$|^global\.json$|^nuget\.config$|^\.gitattributes$|^infra/|^reference/|^docs/design/brand/logos/|^scripts/[^/]+\.sql$|^scripts/test-shard-durations\.json$|^scripts/(Invoke-TestShard|Update-TestShardDurations|Test-(MainBranchHistory|TestShard)|Get-CiChangeFlags|Get-CiHeavyLaneDecision)\.ps1$|^\.github/workflows/ci\.yml$|^\.github/actions/'
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
