[CmdletBinding()]
param(
    [switch]$SkipBicep,
    [switch]$RequireCorpusEvidence
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$testRunId = '{0}-{1}' -f (Get-Date -AsUTC -Format 'yyyyMMddTHHmmssZ'), ([Guid]::NewGuid().ToString('N').Substring(0, 8))
$testResultsDirectory = Join-Path $root "artifacts/test-results/$testRunId"

function Assert-TrxResult {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "$Name did not produce the required VSTest TRX result: $Path"
    }

    [xml]$trx = Get-Content -LiteralPath $Path -Raw
    $counters = $trx.SelectSingleNode("/*[local-name()='TestRun']/*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counters) {
        throw "$Name TRX does not contain VSTest result counters."
    }

    $total = [int]$counters.GetAttribute('total')
    $executed = [int]$counters.GetAttribute('executed')
    $failed = [int]$counters.GetAttribute('failed')
    if ($total -eq 0) {
        throw "$Name discovered zero tests."
    }

    if ($failed -gt 0) {
        throw "$Name reported $failed failed test(s) out of $total."
    }

    if ($executed -ne $total) {
        throw "$Name executed $executed of $total discovered tests; skipped or otherwise unexecuted tests are not accepted."
    }

    Write-Host "${Name}: $executed/$total executed, 0 failed, 0 skipped." -ForegroundColor Green
}

function Invoke-TestGate {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [string]$Project,

        [Parameter(Mandatory)]
        [string]$TrxFileName,

        [string]$Filter
    )

    $arguments = @(
        'test',
        $Project,
        '--configuration', 'Release',
        '--no-build',
        '--logger', "trx;LogFileName=$TrxFileName",
        '--results-directory', $testResultsDirectory
    )
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @('--filter', $Filter)
    }

    & dotnet @arguments
    $testExitCode = $LASTEXITCODE
    $trxPath = Join-Path $testResultsDirectory $TrxFileName
    Assert-TrxResult -Name $Name -Path $trxPath
    if ($testExitCode -ne 0) {
        throw "$Name test process failed with exit code $testExitCode."
    }
}

if ($RequireCorpusEvidence) {
    $qdosCorpusRelativePath = 'corpus/emailevals/qdos-email-corpus'
    $qdosCorpus = Join-Path $root $qdosCorpusRelativePath
    if (-not (Test-Path -LiteralPath $qdosCorpus -PathType Container)) {
        throw "Corpus evidence was required, but the ignored local $qdosCorpusRelativePath directory is absent."
    }

    $hasEligibleCorpusFile = [System.IO.Directory]::EnumerateFiles(
        $qdosCorpus,
        '*',
        [System.IO.SearchOption]::AllDirectories
    ).Where({
        $extension = [System.IO.Path]::GetExtension($_)
        $extension.Equals('.eml', [System.StringComparison]::OrdinalIgnoreCase) -or
            $extension.Equals('.pdf', [System.StringComparison]::OrdinalIgnoreCase)
    }, 'First').Count -gt 0
    if (-not $hasEligibleCorpusFile) {
        throw "Corpus evidence was required, but $qdosCorpusRelativePath contains no eligible EML or PDF sample."
    }

    & (Join-Path $PSScriptRoot 'Get-CorpusInventory.ps1') -CorpusRelativePath $qdosCorpusRelativePath
}
else {
    Write-Host 'Corpus evidence: NOT RUN. The default gate excludes Category=Corpus; use -RequireCorpusEvidence to require genuine local evidence.' -ForegroundColor Yellow
}

$localDbCommand = Get-Command sqllocaldb -ErrorAction SilentlyContinue
if (-not $localDbCommand) {
    throw 'SQL Server Express LocalDB is required for the mandatory SQL Server integration tests. Install the LocalDB runtime before running the repository check.'
}

& $localDbCommand.Source versions | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'SQL Server Express LocalDB is installed but unavailable. Repair the LocalDB runtime before running the repository check.'
}

Push-Location $root

try {
    & (Join-Path $PSScriptRoot 'Test-RepositoryStructure.ps1')

    dotnet restore CollisionSpike.slnx
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet build CollisionSpike.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    New-Item -ItemType Directory -Force -Path $testResultsDirectory | Out-Null
    Invoke-TestGate `
        -Name 'Core tests' `
        -Project 'tests/CollisionSpike.Core.Tests/CollisionSpike.Core.Tests.csproj' `
        -TrxFileName 'core.trx'
    Invoke-TestGate `
        -Name 'Integration tests (excluding corpus)' `
        -Project 'tests/CollisionSpike.IntegrationTests/CollisionSpike.IntegrationTests.csproj' `
        -TrxFileName 'integration.trx' `
        -Filter 'Category!=Corpus'
    Invoke-TestGate `
        -Name 'Architecture tests' `
        -Project 'tests/CollisionSpike.ArchitectureTests/CollisionSpike.ArchitectureTests.csproj' `
        -TrxFileName 'architecture.trx'

    if ($RequireCorpusEvidence) {
        Invoke-TestGate `
            -Name 'Corpus evidence tests' `
            -Project 'tests/CollisionSpike.IntegrationTests/CollisionSpike.IntegrationTests.csproj' `
            -TrxFileName 'corpus.trx' `
            -Filter 'Category=Corpus'
    }

    if (-not $SkipBicep) {
        if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
            throw 'Azure CLI is required to compile Bicep. Use -SkipBicep only in a deliberately limited environment.'
        }

        $bicepOutput = Join-Path $root 'artifacts/bicep/main.json'
        New-Item -ItemType Directory -Force -Path (Split-Path $bicepOutput) | Out-Null
        az bicep build --file infra/main.bicep --outfile $bicepOutput
        if ($LASTEXITCODE -ne 0) { throw 'Bicep compilation failed.' }
    }

    if (-not (Get-Command python -ErrorAction SilentlyContinue)) {
        throw 'Python is required for project skill validation.'
    }

    python scripts/validate_project_skills.py .codex/skills
    if ($LASTEXITCODE -ne 0) { throw 'Portable project skill validation failed.' }

    $validator = 'C:\Users\PC\.codex\skills\.system\skill-creator\scripts\quick_validate.py'
    if (Test-Path -LiteralPath $validator) {
        Get-ChildItem -LiteralPath '.codex/skills' -Directory |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'agents/openai.yaml') } |
            ForEach-Object {
                python $validator $_.FullName
                if ($LASTEXITCODE -ne 0) { throw "Skill validation failed: $($_.Name)" }
            }
    }

    if (-not $RequireCorpusEvidence) {
        Write-Host 'Corpus evidence was not run.' -ForegroundColor Yellow
    }

    Write-Host "Repository checks passed. Test results: $testResultsDirectory" -ForegroundColor Green
}
finally {
    Pop-Location
}
