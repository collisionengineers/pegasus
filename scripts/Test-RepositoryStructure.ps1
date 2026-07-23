[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$required = @(
    'AGENTS.md',
    'CollisionSpike.slnx',
    'Directory.Build.props',
    'global.json',
    'azure.yaml',
    '.azure/deployment-plan.md',
    'src/CollisionSpike.Core/CollisionSpike.Core.csproj',
    'src/CollisionSpike.Infrastructure/CollisionSpike.Infrastructure.csproj',
    'src/CollisionSpike.Web/CollisionSpike.Web.csproj',
    'src/CollisionSpike.Worker/CollisionSpike.Worker.csproj',
    'tests/CollisionSpike.Core.Tests/CollisionSpike.Core.Tests.csproj',
    'tests/CollisionSpike.IntegrationTests/CollisionSpike.IntegrationTests.csproj',
    'tests/CollisionSpike.ArchitectureTests/CollisionSpike.ArchitectureTests.csproj',
    'infra/main.bicep',
    'infra/main.parameters.json',
    'docs/azure/current-inventory.md',
    'docs/evaluation/corpus.md',
    'docs/ui-ux/README.md'
)

$missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $root $_)) })
if ($missing.Count -gt 0) {
    throw "Required repository paths are missing: $($missing -join ', ')"
}

$trackedCorpus = @(& git -C $root ls-files -- corpus)
if ($trackedCorpus.Count -gt 0) {
    throw 'The local corpus must not contain tracked files.'
}

& git -C $root check-ignore --quiet corpus
if ($LASTEXITCODE -ne 0) {
    throw 'The local corpus must be ignored by Git.'
}

$projectNames = @(
    Get-ChildItem -LiteralPath (Join-Path $root 'src') -Recurse -Filter '*.csproj' |
        Where-Object { $_.FullName -notmatch '[\\/]obj[\\/]' } |
        Select-Object -ExpandProperty BaseName
)
$expectedProjects = @('CollisionSpike.Core', 'CollisionSpike.Infrastructure', 'CollisionSpike.Web', 'CollisionSpike.Worker')
if (Compare-Object $expectedProjects $projectNames) {
    throw "Unexpected production project set: $($projectNames -join ', ')"
}

Write-Host 'Repository structure is valid.' -ForegroundColor Green
