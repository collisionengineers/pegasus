[CmdletBinding()]
param(
    [switch]$SkipRestore
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$solutionPath = Join-Path $repositoryRoot 'CollisionDocNet.slnx'

function Invoke-NativeStep {
    param(
        [Parameter(Mandatory)]
        [string]$Name,

        [Parameter(Mandatory)]
        [scriptblock]$Action
    )

    Write-Output "[$Name]"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

Push-Location -LiteralPath $repositoryRoot
try {
    if (-not $SkipRestore) {
        Invoke-NativeStep 'restore (locked)' {
            dotnet restore $solutionPath --locked-mode
        }
    }

    Invoke-NativeStep 'format' {
        dotnet format $solutionPath --verify-no-changes --no-restore
    }
    Invoke-NativeStep 'build (Release)' {
        dotnet build $solutionPath --configuration Release --no-restore
    }
    Invoke-NativeStep 'test (Release, MTP)' {
        dotnet test --solution $solutionPath --configuration Release --no-build
    }

    Write-Output '[JSON documents]'
    $jsonPaths = @(
        (Join-Path $repositoryRoot 'global.json'),
        (Join-Path $repositoryRoot 'docs/licensing/provenance-manifest.schema.json'),
        (Join-Path $repositoryRoot 'docs/licensing/doc-source-provenance.json'),
        (Join-Path $repositoryRoot 'docs/architecture/doc-format-classification.v1.json'),
        (Join-Path $repositoryRoot 'docs/architecture/doc-text-story-contract.v1.json'),
        (Join-Path $repositoryRoot 'docs/architecture/doc-sprm-catalogue.v1.json'),
        (Join-Path $repositoryRoot 'docs/schemas/extraction-result.v1.schema.json'),
        (Join-Path $repositoryRoot 'docs/schemas/evidence-bundle-manifest.v1.schema.json'),
        (Join-Path $repositoryRoot 'tests/fixtures/manifests/fixture-manifest.schema.json')
    )
    foreach ($jsonPath in $jsonPaths) {
        Get-Content -Raw -LiteralPath $jsonPath | ConvertFrom-Json | Out-Null
    }

    & (Join-Path $repositoryRoot 'scripts/Test-DocFibAtlas.ps1')
    & (Join-Path $repositoryRoot 'scripts/Test-DocFormatClassification.ps1')
    & (Join-Path $repositoryRoot 'scripts/Test-DocTextStoryContract.ps1')
    & (Join-Path $repositoryRoot 'scripts/Test-DocSprmCatalogue.ps1')

    Write-Output '[local Markdown links]'
    $markdownFiles = @(
        Get-Item -LiteralPath (Join-Path $repositoryRoot 'README.md')
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'docs') -Recurse -Filter '*.md' -File
    )
    $missingLinks = foreach ($markdownFile in $markdownFiles) {
        $contents = Get-Content -Raw -LiteralPath $markdownFile.FullName
        foreach ($match in [regex]::Matches($contents, '\[[^\]]+\]\(([^)]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^(https?://|#)') {
                continue
            }

            $pathOnly = ($target -split '#', 2)[0]
            $resolvedTarget = Join-Path $markdownFile.DirectoryName $pathOnly
            if (-not (Test-Path -LiteralPath $resolvedTarget)) {
                "$($markdownFile.FullName): $target"
            }
        }
    }
    if ($missingLinks.Count -gt 0) {
        throw "Broken local Markdown links:`n$($missingLinks -join "`n")"
    }

    Write-Output "Repository checks passed for $solutionPath."
}
finally {
    Pop-Location
}
