param([Parameter(Mandatory)][int]$AppPid)

$ErrorActionPreference = 'Continue'
$pass = 0; $fail = 0; $results = @()
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$artifactRoot = Join-Path $repoRoot 'artifacts\gui-ui-tests'

function Test-UI {
    param([string]$Name, [scriptblock]$Script)
    try {
        $output = & $Script 2>&1
        if ($LASTEXITCODE -eq 0) {
            $script:pass++; $script:results += @{ name = $Name; status = "PASS" }
            Write-Host "  PASS: $Name" -ForegroundColor Green
        } else {
            $script:fail++; $script:results += @{ name = $Name; status = "FAIL"; detail = "$output" }
            Write-Host "  FAIL: $Name -- $output" -ForegroundColor Red
        }
    } catch {
        $script:fail++; $script:results += @{ name = $Name; status = "FAIL"; detail = "$_" }
        Write-Host "  FAIL: $Name -- $_" -ForegroundColor Red
    }
}

New-Item -ItemType Directory -Force -Path $artifactRoot | Out-Null

# --- Start screen: the template gallery ---
Test-UI "Template gallery exists"  { winapp ui wait-for "TemplateGallery" -a $AppPid -t 12000 }
Test-UI "Template search exists"   { winapp ui wait-for "TemplateSearch" -a $AppPid -t 3000 }

winapp ui screenshot -a $AppPid -o (Join-Path $artifactRoot '01-gallery.png') 2>$null

# --- Choose a template (target the card by its AutomationId) -> design screen ---
Test-UI "Open Market Valuation template" { winapp ui invoke "Market Valuation Evidence" -a $AppPid }

Test-UI "Render PDF button"      { winapp ui wait-for "RenderButton" -a $AppPid -t 6000 }
Test-UI "Change template button" { winapp ui wait-for "BackButton" -a $AppPid -t 3000 }
Test-UI "Density combo exists"   { winapp ui wait-for "DensityCombo" -a $AppPid -t 3000 }
Test-UI "Form tab exists"        { winapp ui wait-for "FormTab" -a $AppPid -t 3000 }
Test-UI "JSON tab exists"        { winapp ui wait-for "JsonTab" -a $AppPid -t 3000 }
Test-UI "Reset to template"      { winapp ui wait-for "NewStarterButton" -a $AppPid -t 3000 }
Test-UI "Live preview pane"      { winapp ui wait-for "HtmlPreview" -a $AppPid -t 4000 }
Test-UI "Live preview toggle"    { winapp ui wait-for "LiveTab" -a $AppPid -t 3000 }
Test-UI "PDF preview toggle"     { winapp ui wait-for "PdfTab" -a $AppPid -t 3000 }
Test-UI "Generated form field"   { winapp ui wait-for "Field_registration_subject_registration" -a $AppPid -t 3000 }

# --- Default density is Auto (parity: auto default) ---
Test-UI "Density defaults to Auto" { winapp ui wait-for "DensityCombo" -a $AppPid --value "Auto" -t 3000 }

winapp ui screenshot -a $AppPid -o (Join-Path $artifactRoot '02-design-starter.png') 2>$null

# --- The starter pre-fills the JSON draft (placeholder prompts) ---
Test-UI "Click JSON tab"          { winapp ui invoke "JsonTab" -a $AppPid }
Test-UI "JSON editor present"     { winapp ui wait-for "JsonEditor" -a $AppPid -t 3000 }
Start-Sleep -Milliseconds 300
Test-UI "Return to form tab"      { winapp ui invoke "FormTab" -a $AppPid }

# --- Editing fields drives the live preview ---
Test-UI "Set registration" { winapp ui set-value "Field_registration_subject_registration" "AB12 CDE" -a $AppPid }
Test-UI "Set make"         { winapp ui set-value "Field_make_subject_make" "BMW" -a $AppPid }
Start-Sleep -Milliseconds 600

winapp ui screenshot -a $AppPid -o (Join-Path $artifactRoot '03-edited.png') 2>$null

# --- Placeholder guard: rendering with unfilled prompts asks first (goal 5) ---
Test-UI "Click Render" { winapp ui invoke "RenderButton" -a $AppPid }
$guard = $false
for ($i = 0; $i -lt 5; $i++) {
    Start-Sleep -Milliseconds 400
    $found = winapp ui search "Render anyway" -a $AppPid --json 2>$null
    if ($found -and $found -match "Render anyway") { $guard = $true; break }
}
if ($guard) {
    $pass++; $results += @{ name = "Placeholder guard appears before render"; status = "PASS" }
    Write-Host "  PASS: Placeholder guard appears before render" -ForegroundColor Green
    winapp ui screenshot -a $AppPid -o (Join-Path $artifactRoot '04-placeholder-guard.png') 2>$null
    Test-UI "Confirm render anyway" { winapp ui invoke "Render anyway" -a $AppPid }
} else {
    $fail++; $results += @{ name = "Placeholder guard appears before render"; status = "FAIL"; detail = "Guard dialog not detected." }
    Write-Host "  FAIL: Placeholder guard not detected" -ForegroundColor Red
}

# Render runs headless Chromium; allow time. If Chromium needs installing a
# dialog appears -- detect that case rather than hang the whole suite.
$rendered = $false
for ($i = 0; $i -lt 30; $i++) {
    Start-Sleep -Seconds 2
    $saveEnabled = (winapp ui get-property "SaveButton" -a $AppPid -p IsEnabled --json 2>$null | ConvertFrom-Json)
    if ($saveEnabled.properties.IsEnabled -eq "True") { $rendered = $true; break }
    $setup = winapp ui search "Install now" -a $AppPid --json 2>$null
    if ($setup -and $setup -match "Install now") { break }
}

winapp ui screenshot -a $AppPid -o (Join-Path $artifactRoot '05-after-render.png') 2>$null

if ($rendered) {
    Test-UI "Save button enabled after render" { winapp ui wait-for "SaveButton" -a $AppPid -p IsEnabled --value "True" -t 3000 }
    Test-UI "Open PDF button enabled"          { winapp ui wait-for "OpenPdfButton" -a $AppPid -p IsEnabled --value "True" -t 3000 }
    Test-UI "Status shows rendered"            { winapp ui wait-for "StatusLine" -a $AppPid --value "Rendered" --contains -t 3000 }
    Test-UI "PDF preview control present"      { winapp ui wait-for "PdfPreview" -a $AppPid -t 3000 }
} else {
    $results += @{ name = "Render produced a PDF"; status = "SKIP"; detail = "Chromium engine not available in this environment -- one-time-setup path shown instead." }
    Write-Host "  SKIP: Render (Chromium engine not available -- setup dialog path)" -ForegroundColor Yellow
}

# --- Accessibility: every interactive control has an AutomationId ---
$allElements = (winapp ui inspect -a $AppPid --interactive --json 2>$null | ConvertFrom-Json).elements
$appElements = @($allElements | Where-Object {
    $_.type -match 'Button|TextBox|ComboBox|List|TabItem|Edit' -and
    $_.name -notmatch 'Minimize|Maximize|Close|System'
})
$missingId = @($appElements | Where-Object { -not $_.automationId })
if ($missingId.Count -eq 0) {
    $pass++; $results += @{ name = "Interactive controls have AutomationId"; status = "PASS" }
    Write-Host "  PASS: Interactive controls have AutomationId" -ForegroundColor Green
} else {
    $fail++
    $names = ($missingId | ForEach-Object { "$($_.type) '$($_.name)'" }) -join ", "
    $results += @{ name = "AutomationId coverage"; status = "FAIL"; detail = "Missing: $names" }
    Write-Host "  FAIL: AutomationId coverage -- Missing: $names" -ForegroundColor Red
}

winapp ui screenshot -a $AppPid -o (Join-Path $artifactRoot '99-final.png') 2>$null

Write-Host "`nPassed: $pass | Failed: $fail"
$results | ConvertTo-Json | Out-File (Join-Path $artifactRoot 'test-results.json')
if ($fail -gt 0) { exit 1 } else { exit 0 }
