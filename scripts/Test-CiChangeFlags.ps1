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
        [Parameter(Mandatory)][bool] $Infrastructure,
        # A case that does not name a lane asserts that lane stays off, so a
        # pattern that widens accidentally fails here rather than buying runners.
        [bool] $LocalDevelopment = $false,
        [bool] $ReferenceData = $false
    )

    $expected = [ordered]@{
        Build = $Build
        Infrastructure = $Infrastructure
        LocalDevelopment = $LocalDevelopment
        ReferenceData = $ReferenceData
    }

    $actual = & $classifier -ChangedPath $ChangedPath
    foreach ($lane in $expected.Keys) {
        if ($actual.$lane -ne $expected[$lane]) {
            $wanted = ($expected.Keys | ForEach-Object { "$_=$($expected[$_])" }) -join ' '
            $got = ($expected.Keys | ForEach-Object { "$_=$($actual.$_)" }) -join ' '
            throw "$Case expected $wanted; got $got."
        }
    }
}

# The Architecture tests assert on platform.bicep, so a template change has to
# run them as well as the infrastructure lane.
Assert-Flags -Case 'Bicep module' -ChangedPath 'infra/modules/platform.bicep' -Build $true -Infrastructure $true
Assert-Flags -Case 'azd configuration' -ChangedPath 'azure.yaml' -Build $false -Infrastructure $true
Assert-Flags -Case 'local validator dependency' -ChangedPath 'scripts/Invoke-ProductionSmoke.ps1' -Build $false -Infrastructure $true
Assert-Flags -Case 'migration validator dependency' -ChangedPath 'scripts/Test-MigrationGrants.ps1' -Build $false -Infrastructure $true
Assert-Flags -Case 'release validation behaviour tests' -ChangedPath 'scripts/Test-ReleaseValidation.ps1' -Build $false -Infrastructure $true
Assert-Flags -Case 'shared platform script (manifest validator, Worker census)' -ChangedPath 'scripts/PegasusPlatform.ps1' -Build $false -Infrastructure $true -LocalDevelopment $true
Assert-Flags -Case 'deployment validator release-artifact dependency' -ChangedPath 'scripts/Build-ReleaseArtifacts.ps1' -Build $false -Infrastructure $true
Assert-Flags -Case 'migration source' -ChangedPath 'src/Pegasus.Infrastructure/Persistence/Migrations/20260906_Example.cs' -Build $true -Infrastructure $true
Assert-Flags -Case 'classification code' -ChangedPath 'scripts/Get-CiChangeFlags.ps1' -Build $true -Infrastructure $true
Assert-Flags -Case 'shard assignment tests' -ChangedPath 'scripts/Test-TestShard.ps1' -Build $true -Infrastructure $false
Assert-Flags -Case 'workflow definition' -ChangedPath '.github/workflows/ci.yml' -Build $true -Infrastructure $true
Assert-Flags -Case 'UI-only source' -ChangedPath 'src/Pegasus.Web/Pages/Index.cshtml' -Build $true -Infrastructure $false
Assert-Flags -Case 'documentation only' -ChangedPath 'docs/index.md' -Build $false -Infrastructure $false
Assert-Flags -Case 'design authority only' -ChangedPath 'docs/design/README.md' -Build $false -Infrastructure $false
Assert-Flags -Case 'empty diff' -ChangedPath @() -Build $false -Infrastructure $false

# The LocalDB lifecycle classifier contract: its own tests turn the lane on
# without implying a release-script change.
Assert-Flags -Case 'LocalDB lifecycle classifier tests' -ChangedPath 'scripts/Test-PegasusPlatform.ps1' -Build $false -Infrastructure $false -LocalDevelopment $true

# The provider-reference component and its only callers.
Assert-Flags -Case 'reference-data generator' -ChangedPath 'scripts/reference_data/build_provider_reference_data.py' -Build $false -Infrastructure $false -ReferenceData $true
Assert-Flags -Case 'reference-data generator tests' -ChangedPath 'scripts/reference_data/tests/test_build_provider_reference_data.py' -Build $false -Infrastructure $false -ReferenceData $true
Assert-Flags -Case 'provider-reference wrapper' -ChangedPath 'scripts/Build-ProviderReferenceData.ps1' -Build $false -Infrastructure $false -ReferenceData $true
Assert-Flags -Case 'principal-corpus wrapper' -ChangedPath 'scripts/Build-PrincipalIdentificationCorpus.ps1' -Build $false -Infrastructure $false -ReferenceData $true

# Refreshing the shard duration table changes the partition, so a real run has
# to validate it.
Assert-Flags -Case 'shard duration table' -ChangedPath 'scripts/test-shard-durations.json' -Build $true -Infrastructure $false

# Reference data is read by Core and integration tests; an earlier revision of
# this file asserted the opposite, which encoded the gap as the rule.
Assert-Flags -Case 'reference data' -ChangedPath 'reference/workproviders-and-repairers/principal-identification-corpus.v1.json' -Build $true -Infrastructure $false
Assert-Flags -Case 'EVA bundle reference' -ChangedPath 'reference/eva_information/AX_SP58WVO.json' -Build $true -Infrastructure $false
Assert-Flags -Case 'embedded report logo' -ChangedPath 'docs/design/brand/logos/logo_no_margin.png' -Build $true -Infrastructure $false
Assert-Flags -Case 'SQL read by integration tests' -ChangedPath 'scripts/Reset-TestEstate.sql' -Build $true -Infrastructure $false
Assert-Flags -Case 'shard duration refresh script' -ChangedPath 'scripts/Update-TestShardDurations.ps1' -Build $true -Infrastructure $false
Assert-Flags -Case 'brand guidance prose stays prose' -ChangedPath 'docs/design/README.md' -Build $false -Infrastructure $false

$forced = & $classifier -ChangedPath 'docs/index.md' -ForceAll
foreach ($lane in 'Build', 'Infrastructure', 'LocalDevelopment', 'ReferenceData') {
    if (-not $forced.$lane) {
        throw "ForceAll must enable every conditional lane when a reliable diff is unavailable; $lane stayed off."
    }
}

$decide = Join-Path $PSScriptRoot 'Get-CiHeavyLaneDecision.ps1'
function Assert-Heavy {
    param([string] $Case, [bool] $Expected, [hashtable] $Arguments)
    $actual = & $decide @Arguments
    if ($actual.Heavy -ne $Expected) {
        throw "$Case expected heavy=$Expected; got $($actual.Heavy) ($($actual.Reason))."
    }
}

$pr = @{ Build = $true; EventName = 'pull_request' }
Assert-Heavy 'push to main' $true @{ Build = $true; EventName = 'push' }
Assert-Heavy 'no build-relevant path' $false @{ Build = $false; EventName = 'pull_request'; BaseRef = 'dev'; StackedAbove = 0 }
Assert-Heavy 'merges into dev, nothing stacked' $true ($pr + @{ BaseRef = 'dev'; StackedAbove = 0 })
Assert-Heavy 'bottom of a stack still runs' $true ($pr + @{ BaseRef = 'dev'; StackedAbove = 1 })
Assert-Heavy 'dev to main promotion' $true ($pr + @{ BaseRef = 'main'; StackedAbove = 0 })
# The case the first version got backwards: the tip holds the whole stack.
Assert-Heavy 'tip of a stack' $true ($pr + @{ BaseRef = 'task/v28-wording'; StackedAbove = 0 })
Assert-Heavy 'middle of a stack defers' $false ($pr + @{ BaseRef = 'task/v28-vehicle'; StackedAbove = 1 })
Assert-Heavy 'ci:full overrides deferral' $true ($pr + @{ BaseRef = 'task/v28-vehicle'; StackedAbove = 1; Label = @('docs', ' ci:full') })
Assert-Heavy 'near-miss label does not' $false ($pr + @{ BaseRef = 'task/v28-vehicle'; StackedAbove = 1; Label = @('ci-full') })
Assert-Heavy 'unknown stacking counts as tip' $true ($pr + @{ BaseRef = 'task/v28-vehicle'; StackedAbove = $null })

Assert-Flags -Case 'build-output pruning' -ChangedPath 'scripts/Clear-BuildOutput.ps1' -Build $false -Infrastructure $false -LocalDevelopment $true

Write-Output 'CI change classification passed.'
