param(
    [switch]$Approve,
    [string]$ReferenceMap,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$artifactRoot = Join-Path $repoRoot "artifacts\visual-regression"
$candidateRoot = Join-Path $artifactRoot "candidates"
$approvedRoot = Join-Path $artifactRoot "approved"
$renderRoot = Join-Path $artifactRoot "renders"
$cliProject = Join-Path $repoRoot "src\CollisionRenderer.Cli"

function Require-Command {
    param([string]$Name)
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if (-not $cmd) {
        throw "Missing required command '$Name'. Install Poppler and ensure '$Name' is on PATH."
    }
}

function Reset-Folder {
    param([string]$Path)
    if (Test-Path $Path) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

function Get-TemplateIds {
    $output = dotnet run --no-restore --project $cliProject -- list
    if ($LASTEXITCODE -ne 0) {
        throw "Could not list templates."
    }

    $ids = @()
    foreach ($line in $output) {
        if ($line -match "^\s{2}([a-z0-9-]+)\s+") {
            $ids += $Matches[1]
        }
    }

    if ($ids.Count -eq 0) {
        throw "No templates were discovered from the CLI list command."
    }

    return $ids
}

function Render-Starter {
    param([string]$TemplateId)

    $templateRenderDir = Join-Path $renderRoot $TemplateId
    New-Item -ItemType Directory -Force -Path $templateRenderDir | Out-Null
    $payload = Join-Path $templateRenderDir "payload.json"
    $pdf = Join-Path $templateRenderDir "$TemplateId.pdf"

    dotnet run --no-restore --project $cliProject -- forms starter --template $TemplateId --out $payload | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not write starter payload for '$TemplateId'."
    }

    dotnet run --no-restore --project $cliProject -- render --template $TemplateId --data $payload --out $pdf | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not render starter payload for '$TemplateId'."
    }

    return $pdf
}

function Render-MapItem {
    param([pscustomobject]$Item)

    $templateRenderDir = Join-Path $renderRoot $Item.templateId
    New-Item -ItemType Directory -Force -Path $templateRenderDir | Out-Null
    $pdf = Join-Path $templateRenderDir "$($Item.templateId).pdf"
    $args = @(
        "run", "--no-restore", "--project", $cliProject, "--",
        "render", "--template", $Item.templateId, "--data", $Item.dataPath, "--out", $pdf
    )
    if ($Item.density) {
        $args += @("--density", $Item.density)
    }

    dotnet @args | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not render '$($Item.templateId)' from '$($Item.dataPath)'."
    }

    return $pdf
}

function Rasterize-Pdf {
    param(
        [string]$Pdf,
        [string]$OutputDir,
        [string]$Prefix
    )

    New-Item -ItemType Directory -Force -Path $OutputDir | Out-Null
    $outPrefix = Join-Path $OutputDir $Prefix
    pdftoppm -png $Pdf $outPrefix | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "pdftoppm failed for '$Pdf'."
    }
}

function Compare-Folders {
    param(
        [string]$Name,
        [string]$ActualDir,
        [string]$ExpectedDir
    )

    if (-not (Test-Path $ExpectedDir)) {
        if ($Approve) {
            New-Item -ItemType Directory -Force -Path $ExpectedDir | Out-Null
            Copy-Item -Path (Join-Path $ActualDir "*.png") -Destination $ExpectedDir -Force
            Write-Host "APPROVED $Name"
            return $true
        }

        Write-Host "MISSING APPROVAL $Name - run with -Approve after reviewing candidates." -ForegroundColor Red
        return $false
    }

    $actual = @(Get-ChildItem -Path $ActualDir -Filter "*.png" | Sort-Object Name)
    $expected = @(Get-ChildItem -Path $ExpectedDir -Filter "*.png" | Sort-Object Name)
    if ($actual.Count -ne $expected.Count) {
        Write-Host "PAGE COUNT MISMATCH $Name actual=$($actual.Count) expected=$($expected.Count)" -ForegroundColor Red
        return $false
    }

    $ok = $true
    for ($i = 0; $i -lt $actual.Count; $i++) {
        $a = Get-FileHash -Algorithm SHA256 -LiteralPath $actual[$i].FullName
        $e = Get-FileHash -Algorithm SHA256 -LiteralPath $expected[$i].FullName
        if ($a.Hash -ne $e.Hash) {
            Write-Host "PIXEL HASH MISMATCH $Name page=$($i + 1)" -ForegroundColor Red
            $ok = $false
        }
    }

    if ($ok) {
        Write-Host "PASS $Name"
    }

    return $ok
}

Require-Command "pdftoppm"
Reset-Folder $candidateRoot
New-Item -ItemType Directory -Force -Path $approvedRoot | Out-Null
New-Item -ItemType Directory -Force -Path $renderRoot | Out-Null

$failed = 0

if ($ReferenceMap) {
    $map = Get-Content $ReferenceMap -Raw | ConvertFrom-Json
    foreach ($item in $map.items) {
        $name = $item.templateId
        $actualPdf = Render-MapItem $item
        $actualDir = Join-Path $candidateRoot "$name\actual"
        $expectedDir = Join-Path $candidateRoot "$name\reference"
        Rasterize-Pdf -Pdf $actualPdf -OutputDir $actualDir -Prefix "actual"
        Rasterize-Pdf -Pdf $item.referencePdf -OutputDir $expectedDir -Prefix "reference"
        if (-not (Compare-Folders -Name $name -ActualDir $actualDir -ExpectedDir $expectedDir)) {
            $failed++
        }
    }
} else {
    foreach ($id in Get-TemplateIds) {
        $pdf = Render-Starter $id
        $actualDir = Join-Path $candidateRoot $id
        $expectedDir = Join-Path $approvedRoot $id
        Rasterize-Pdf -Pdf $pdf -OutputDir $actualDir -Prefix $id
        if (-not (Compare-Folders -Name $id -ActualDir $actualDir -ExpectedDir $expectedDir)) {
            $failed++
        }
    }
}

if ($failed -gt 0) {
    throw "$failed visual regression comparison(s) failed. Candidate images are under $candidateRoot."
}

Write-Host "Visual regression checks passed."
