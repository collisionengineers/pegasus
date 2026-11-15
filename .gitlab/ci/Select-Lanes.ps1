#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Decides which lanes a pipeline's changed paths need and writes them as a
    dotenv report for the `lanes` trigger job (.gitlab-ci.yml).

.DESCRIPTION
    A merge request diffs its head against the base GitLab names for it. A list
    that cannot be trusted (the base could not be fetched, the diff failed)
    runs every lane. A push to dev runs every lane: the merged tree is the
    release candidate and the exact-head evidence the preflight reads (R683). A
    push to main runs none: it receives a SHA dev already ran (R682, R683) and
    the history guard is that push's whole check.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Lanes {
    param([bool]$Code, [bool]$Infrastructure, [bool]$Classifier, [string]$Migrations)

    @(
        "LANE_CODE=$($Code.ToString().ToLowerInvariant())"
        "LANE_INFRASTRUCTURE=$($Infrastructure.ToString().ToLowerInvariant())"
        "LANE_CLASSIFIER=$($Classifier.ToString().ToLowerInvariant())"
        "LANE_MIGRATIONS=$Migrations"
    ) | Set-Content -LiteralPath $OutputPath
}

if ($env:CI_PIPELINE_SOURCE -eq 'push' -and $env:CI_COMMIT_BRANCH -eq 'main') {
    Write-Lanes -Code $false -Infrastructure $false -Classifier $false -Migrations ''
    Write-Host 'Lanes: none (a push to main is guarded, not re-run; its lanes ran on dev).'
    exit 0
}

$all = $true
$files = @()
if ($env:CI_PIPELINE_SOURCE -eq 'merge_request_event') {
    # A merged results pipeline checks out the merge of the head into the
    # target, so its changes are read against the target's tip; any other
    # merge request pipeline checks out the head and reads against the merge
    # base. Both lists name a renamed file under its old and its new path.
    $base = if ($env:CI_MERGE_REQUEST_TARGET_BRANCH_SHA) { $env:CI_MERGE_REQUEST_TARGET_BRANCH_SHA } else { $env:CI_MERGE_REQUEST_DIFF_BASE_SHA }
    if ($base) {
        git fetch --quiet --no-tags --depth=1 origin $base
        if ($LASTEXITCODE -eq 0) {
            $files = @(git diff --name-only --no-renames $base HEAD)
            if ($LASTEXITCODE -eq 0) { $all = $false }
        }
    }
}

# Code is every path except the ones no .NET build or test reads: Markdown,
# docs/ (bar the brand logo the report renderer embeds), the skills, the Python
# helper tests, the reference-data generator, the classifier agent, and the
# scripts under scripts/ other than the seven the Architecture tests read or
# the shards run.
$prose = '\.md$|^docs/(?!design/brand/logos/)|^\.agents/|^scripts/skill_scripts/|^scripts/reference_data/|^src/pegasus-classifier-agent/|^scripts/(?!(Invoke-TestShard|Update-TestShardDurations|Test-MainBranchHistory|Test-MigrationClass|PegasusPlatform|Test-PegasusPlatform|Invoke-LocalDevelopment)\.ps1$)[^/]+\.ps1$'
$infrastructure = '^infra/|^azure\.yaml$|^src/Pegasus\.Infrastructure/Persistence/Migrations/|^scripts/(Test-AzureDeploymentPlan|Test-ReleaseValidation|Invoke-ProductionSmoke|Build-ReleaseArtifacts|Invoke-ProductionAdministratorBootstrap|Invoke-AzureDatabaseBootstrap|Invoke-Release|PegasusPlatform|Test-MigrationClass)\.ps1$|^\.gitlab-ci\.yml$|^\.gitlab/ci/'
$classifier = '^src/pegasus-classifier-agent/|^\.gitlab-ci\.yml$|^\.gitlab/ci/'

$code = $all -or [bool]($files | Where-Object { $_ -notmatch $prose } | Select-Object -First 1)
$infra = $all -or [bool]($files | Where-Object { $_ -match $infrastructure } | Select-Object -First 1)
$node = $all -or [bool]($files | Where-Object { $_ -match $classifier } | Select-Object -First 1)

# The changed migration sources the migration class guard reads, comma
# separated: a merge request's alone, and none when its list cannot be trusted.
$migrations = if ($all) { '' } else { @($files | Where-Object { $_ -match '^src/Pegasus\.Infrastructure/Persistence/Migrations/\d{14}_[A-Za-z0-9]+\.cs$' }) -join ',' }

Write-Lanes -Code $code -Infrastructure $infra -Classifier $node -Migrations $migrations
Write-Host "Lanes: code=$code infrastructure=$infra classifier=$node ($($files.Count) changed paths$(if ($all) { '; the list could not be trusted, so every lane runs' }))."
