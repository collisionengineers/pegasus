[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$catalogueRoot = Join-Path $repoRoot 'docs/design/test-ui'
$indexPath = Join-Path $catalogueRoot 'index.html'
$manifestPath = Join-Path $catalogueRoot 'catalogue.json'
$pagesRoot = Join-Path $catalogueRoot 'pages'
$errors = [System.Collections.Generic.List[string]]::new()

if (-not (Test-Path -LiteralPath $indexPath -PathType Leaf)) {
    throw "Test UI index not found: $indexPath"
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Test UI catalogue manifest not found: $manifestPath"
}
$inventory = @(Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json)
$allowedClassifications = @('visual', 'redirect', 'download', 'protocol')
$sourceRoot = Join-Path $repoRoot 'src/Pegasus.Web/Pages'
$routedSources = @(
    Get-ChildItem -LiteralPath $sourceRoot -Filter '*.cshtml' -File -Recurse |
        Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match '(?m)^@page(?:\s|$)' } |
        ForEach-Object {
            $_.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        } |
        Sort-Object
)

$duplicateSources = @($inventory | Group-Object source | Where-Object Count -ne 1)
foreach ($duplicate in $duplicateSources) {
    $errors.Add("Inventory source appears $($duplicate.Count) times: $($duplicate.Name)")
}

$inventorySources = @($inventory.source | Sort-Object)
foreach ($missing in @($routedSources | Where-Object { $_ -notin $inventorySources })) {
    $errors.Add("Routed Razor source is not classified: $missing")
}
foreach ($orphan in @($inventorySources | Where-Object { $_ -notin $routedSources })) {
    $errors.Add("Inventory source is not a current routed Razor page: $orphan")
}

foreach ($entry in $inventory) {
    if ($entry.classification -notin $allowedClassifications) {
        $errors.Add("Unknown classification '$($entry.classification)' for $($entry.source)")
    }

    $states = @($entry.states)
    if ($entry.classification -eq 'visual' -and $states.Count -eq 0) {
        $errors.Add("Visual route has no prototype: $($entry.source)")
    }
    if ($entry.classification -ne 'visual' -and [string]::IsNullOrWhiteSpace($entry.reason)) {
        $errors.Add("Non-visual route has no reason: $($entry.source)")
    }

    foreach ($state in $states) {
        if ($null -eq $state) {
            continue
        }
        if ([string]::IsNullOrWhiteSpace($state.branch)) {
            $errors.Add("Visual state has no documented Razor branch claim: $($entry.source)|$($state.state)")
        }
        if ($state.file -notmatch '^pages/[a-z0-9-]+--[a-z0-9-]+\.html$') {
            $errors.Add("Prototype does not follow the flat naming convention: $($state.file)")
            continue
        }
        $prototypePath = Join-Path $catalogueRoot $state.file
        if (-not (Test-Path -LiteralPath $prototypePath -PathType Leaf)) {
            $errors.Add("Prototype does not exist: $($state.file)")
        }
    }
}

$linkedPrototypeFiles = @(
    $inventory |
        ForEach-Object { @($_.states) } |
        ForEach-Object { $_.file } |
        Where-Object { $_ } |
        Sort-Object -Unique
)
$prototypeReferences = @(
    $inventory |
        ForEach-Object { @($_.states) } |
        Where-Object { $null -ne $_ }
)
foreach ($duplicate in @($prototypeReferences | Group-Object file | Where-Object Count -ne 1)) {
    $errors.Add("Prototype is referenced $($duplicate.Count) times: $($duplicate.Name)")
}
$sourceStates = @(
    foreach ($entry in $inventory) {
        foreach ($state in @($entry.states)) {
            if ($null -ne $state) {
                "$($entry.source)|$($state.state)"
            }
        }
    }
)
foreach ($duplicate in @($sourceStates | Group-Object | Where-Object Count -ne 1)) {
    $errors.Add("Source state appears $($duplicate.Count) times: $($duplicate.Name)")
}
$actualPrototypeFiles = @(
    Get-ChildItem -LiteralPath $pagesRoot -Filter '*.html' -File |
        ForEach-Object { "pages/$($_.Name)" } |
        Sort-Object
)
foreach ($orphan in @($actualPrototypeFiles | Where-Object { $_ -notin $linkedPrototypeFiles })) {
    $errors.Add("Prototype is not linked by the inventory: $orphan")
}

$htmlFiles = @((Get-Item -LiteralPath $indexPath)) +
    @(Get-ChildItem -LiteralPath $pagesRoot -Filter '*.html' -File)
foreach ($htmlFile in $htmlFiles) {
    $html = Get-Content -LiteralPath $htmlFile.FullName -Raw
    $relativeFile = $htmlFile.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
    foreach ($image in [regex]::Matches(
            $html,
            '<img\b[^>]*>',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)) {
        if ($image.Value -notmatch '(?i)\bsrc\s*=\s*["'']\s*[^\s"''][^"'']*["'']' -and
            $image.Value -notmatch '(?i)\bdata-evidence-image\b[^>]*\bhidden\b') {
            $errors.Add("Image has no non-empty source in $($relativeFile): $($image.Value)")
        }
    }
    $references = [regex]::Matches(
        $html,
        '(?:href|src)="(?<target>[^"]+)"',
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    foreach ($reference in $references) {
        $target = $reference.Groups['target'].Value
        if ($target -match '^(?:#|https?:|mailto:|data:|javascript:)') {
            continue
        }
        $targetWithoutFragment = ($target -split '[?#]', 2)[0]
        $resolved = [System.IO.Path]::GetFullPath(
            (Join-Path $htmlFile.DirectoryName $targetWithoutFragment))
        if (-not (Test-Path -LiteralPath $resolved)) {
            $errors.Add("Broken local reference in $($relativeFile): $target")
        }
    }
}

$publishInputs = @(
    Get-ChildItem -LiteralPath $repoRoot -Filter '*.csproj' -File -Recurse
    Get-ChildItem -LiteralPath $repoRoot -Filter '*.slnx' -File
    Get-Item -LiteralPath (Join-Path $repoRoot 'scripts/Build-ReleaseArtifacts.ps1')
)
foreach ($inputFile in $publishInputs) {
    if ((Get-Content -LiteralPath $inputFile.FullName -Raw) -match 'docs[/\\]design[/\\]test-ui') {
        $relativeInput = $inputFile.FullName.Substring($repoRoot.Length + 1).Replace('\', '/')
        $errors.Add("Application or release input references Test UI: $relativeInput")
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    throw "Test UI catalogue validation failed with $($errors.Count) error(s)."
}

Write-Host "Test UI catalogue valid: $($routedSources.Count) routed sources, $($linkedPrototypeFiles.Count) prototypes, 0 broken local references."
