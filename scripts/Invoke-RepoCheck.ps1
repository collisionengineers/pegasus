[CmdletBinding()]
param(
    [switch]$SkipBicep
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Push-Location $root

try {
    & (Join-Path $PSScriptRoot 'Test-RepositoryStructure.ps1')

    dotnet restore CollisionSpike.slnx
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    dotnet build CollisionSpike.slnx --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    dotnet test CollisionSpike.slnx --configuration Release --no-build
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

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

    Write-Host 'Repository checks passed.' -ForegroundColor Green
}
finally {
    Pop-Location
}
