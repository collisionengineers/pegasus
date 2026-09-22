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

Assert-Flags -Case 'Bicep module' -ChangedPath 'infra/modules/platform.bicep' -Build $false -Infrastructure $true
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

# The standalone Jev harness the unit lane restores, builds and tests. Its
# project and lock files already matched; its source did not.
Assert-Flags -Case 'Jev harness source' -ChangedPath 'scripts/jev-mail-eval/Program.cs' -Build $true -Infrastructure $false
Assert-Flags -Case 'Jev harness tests' -ChangedPath 'scripts/jev-mail-eval/tests/ClassificationTests.cs' -Build $true -Infrastructure $false

# Refreshing the shard duration table changes the partition, so a real run has
# to validate it.
Assert-Flags -Case 'shard duration table' -ChangedPath 'scripts/test-shard-durations.json' -Build $true -Infrastructure $false

# Generated reference data is an input to the application, not to the generator.
Assert-Flags -Case 'generated reference data' -ChangedPath 'reference/workproviders-and-repairers/principal-identification-corpus.v1.json' -Build $false -Infrastructure $false

$forced = & $classifier -ChangedPath 'docs/index.md' -ForceAll
foreach ($lane in 'Build', 'Infrastructure', 'LocalDevelopment', 'ReferenceData') {
    if (-not $forced.$lane) {
        throw "ForceAll must enable every conditional lane when a reliable diff is unavailable; $lane stayed off."
    }
}

Write-Output 'CI change classification passed.'
