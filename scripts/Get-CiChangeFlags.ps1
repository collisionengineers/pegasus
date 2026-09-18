[CmdletBinding()]
param(
    [string[]] $ChangedPath = @(),

    [switch] $ForceAll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$buildPattern = '^(src|tests)/|^Pegasus\.slnx$|\.csproj$|\.props$|\.targets$|packages\.lock\.json$|^global\.json$|^nuget\.config$|^scripts/(Invoke-TestShard|Test-(MainBranchHistory|TestShard)|Get-CiChangeFlags)\.ps1$|^\.github/workflows/ci\.yml$|^\.github/actions/'
# PegasusPlatform.ps1 carries the release manifest validator and the Worker
# census the release scripts share, so it routes to the infrastructure lane.
$infrastructurePattern = '^infra/|^azure\.yaml$|^src/Pegasus\.Infrastructure/Persistence/Migrations/|^scripts/(Get-CiChangeFlags|Test-CiChangeFlags|Test-AzureDeploymentPlan|Test-ReleaseValidation|Test-MigrationGrants|Invoke-ProductionSmoke|Build-ReleaseArtifacts|Invoke-ProductionAdministratorBootstrap|Invoke-AzureDatabaseBootstrap|PegasusPlatform)\.ps1$|^\.github/workflows/ci\.yml$'

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
}
