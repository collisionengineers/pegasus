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
    'docs/product/areas/intake-and-casework.md',
    'docs/product/areas/identity-and-access.md',
    'docs/product/areas/documents-and-integrations.md',
    'docs/product/areas/interfaces-and-automation.md',
    'docs/product/areas/platform-and-operator-experience.md',
    'docs/product/v1-gap.md',
    'docs/product/boundaries.md',
    'docs/product/open-decisions.md',
    'docs/history/plans/README.md',
    'design/product/requirements.md',
    'design/product/ui-spec.md',
    'design/product/traceability-matrix.md',
    'docs/runbooks/testing/README.md',
    'docs/index.md',
    'docs/product/index.md',
    'docs/product/capabilities.md',
    'docs/roadmap.md',
    'docs/architecture.md',
    'docs/operations.md',
    'docs/agent-mistakes.md',
    'design/README.md',
    '.github/pull_request_template.md'
)

$missing = @($required | Where-Object { -not (Test-Path -LiteralPath (Join-Path $root $_)) })
if ($missing.Count -gt 0) {
    throw "Required repository paths are missing: $($missing -join ', ')"
}

if (Test-Path -LiteralPath (Join-Path $root 'docs/plans')) {
    throw 'The superseded docs/plans tree must not remain after Azure Workflow conversion.'
}

$trackedCorpus = @(& git -C $root ls-files -- corpus)
if ($trackedCorpus.Count -gt 0) {
    throw 'The local corpus must not contain tracked files.'
}

# CI deliberately has no local corpus directory. Probe a nonexistent descendant
# against the ignore rules so the guard proves the boundary in both environments.
& git -C $root check-ignore --quiet --no-index -- 'corpus/.collisionspike-ignore-probe'
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
