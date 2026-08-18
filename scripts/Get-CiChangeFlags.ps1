[CmdletBinding()]
param(
    [string[]] $ChangedPath = @(),

    [switch] $ForceAll
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$buildPattern = '^(src|tests)/|^Pegasus\.slnx$|\.csproj$|\.props$|\.targets$|packages\.lock\.json$|^global\.json$|^nuget\.config$|^scripts/(Invoke-TestShard|Test-(MainBranchHistory|TestShard)|Get-CiChangeFlags)\.ps1$|^\.github/workflows/ci\.yml$|^\.github/actions/'
$infrastructurePattern = '^infra/|^azure\.yaml$|^scripts/(Get-CiChangeFlags|Test-CiChangeFlags|Test-AzureDeploymentPlan|Invoke-ProductionSmoke|Invoke-ProductionAdministratorBootstrap|Invoke-AzureDatabaseBootstrap)\.ps1$|^\.github/workflows/ci\.yml$'

function Test-AnyPath {
    param(
        [Parameter(Mandatory)][string[]] $Path,
        [Parameter(Mandatory)][string] $Pattern
    )

    return [bool]($Path | Where-Object { $_ -match $Pattern } | Select-Object -First 1)
}

[pscustomobject]@{
    Build = $ForceAll -or (Test-AnyPath -Path $ChangedPath -Pattern $buildPattern)
    Infrastructure = $ForceAll -or (Test-AnyPath -Path $ChangedPath -Pattern $infrastructurePattern)
}
