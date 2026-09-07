[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$classifier = Join-Path $PSScriptRoot 'Get-CiChangeFlags.ps1'

function Assert-Flags {
    param(
        [Parameter(Mandatory)][string] $Case,
        [Parameter(Mandatory)][AllowEmptyCollection()][string[]] $ChangedPath,
        [Parameter(Mandatory)][bool] $Build,
        [Parameter(Mandatory)][bool] $Infrastructure
    )

    $actual = & $classifier -ChangedPath $ChangedPath
    if ($actual.Build -ne $Build -or $actual.Infrastructure -ne $Infrastructure) {
        throw "$Case expected Build=$Build Infrastructure=$Infrastructure; got Build=$($actual.Build) Infrastructure=$($actual.Infrastructure)."
    }
}

Assert-Flags -Case 'Bicep module' -ChangedPath 'infra/modules/platform.bicep' -Build $false -Infrastructure $true
Assert-Flags -Case 'azd configuration' -ChangedPath 'azure.yaml' -Build $false -Infrastructure $true
Assert-Flags -Case 'local validator dependency' -ChangedPath 'scripts/Invoke-ProductionSmoke.ps1' -Build $false -Infrastructure $true
Assert-Flags -Case 'migration validator dependency' -ChangedPath 'scripts/Test-MigrationGrants.ps1' -Build $false -Infrastructure $true
Assert-Flags -Case 'deployment validator release-artifact dependency' -ChangedPath 'scripts/Build-ReleaseArtifacts.ps1' -Build $false -Infrastructure $true
Assert-Flags -Case 'migration source' -ChangedPath 'src/Pegasus.Infrastructure/Persistence/Migrations/20260906_Example.cs' -Build $true -Infrastructure $true
Assert-Flags -Case 'classification code' -ChangedPath 'scripts/Get-CiChangeFlags.ps1' -Build $true -Infrastructure $true
Assert-Flags -Case 'shard assignment tests' -ChangedPath 'scripts/Test-TestShard.ps1' -Build $true -Infrastructure $false
Assert-Flags -Case 'workflow definition' -ChangedPath '.github/workflows/ci.yml' -Build $true -Infrastructure $true
Assert-Flags -Case 'UI-only source' -ChangedPath 'src/Pegasus.Web/Pages/Index.cshtml' -Build $true -Infrastructure $false
Assert-Flags -Case 'documentation only' -ChangedPath 'docs/index.md' -Build $false -Infrastructure $false
Assert-Flags -Case 'Test UI snapshot' -ChangedPath 'docs/design/test-ui/pages/inbox--default.html' -Build $true -Infrastructure $false
Assert-Flags -Case 'Test UI snapshot script' -ChangedPath 'scripts/Update-TestUiSnapshots.ps1' -Build $true -Infrastructure $false
Assert-Flags -Case 'Test UI catalogue script' -ChangedPath 'scripts/Test-UiCatalogue.ps1' -Build $true -Infrastructure $false
Assert-Flags -Case 'design authority only' -ChangedPath 'docs/design/README.md' -Build $false -Infrastructure $false
Assert-Flags -Case 'empty diff' -ChangedPath @() -Build $false -Infrastructure $false

$forced = & $classifier -ChangedPath 'docs/index.md' -ForceAll
if (-not $forced.Build -or -not $forced.Infrastructure) {
    throw 'ForceAll must enable every conditional lane when a reliable diff is unavailable.'
}

Write-Output 'CI change classification passed.'
