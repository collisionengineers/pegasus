[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$launcherPath = Join-Path $PSScriptRoot 'Invoke-LocalDevelopment.ps1'
$cataloguePath = Join-Path $repositoryRoot 'docs/design/test-ui/index.html'
$launcher = Get-Content -LiteralPath $launcherPath -Raw
$tokens = $null
$parseErrors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile(
    $launcherPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) {
    throw "Invoke-LocalDevelopment.ps1 has $($parseErrors.Count) parse error(s)."
}

$uiModeParameter = $ast.ParamBlock.Parameters |
    Where-Object { $_.Name.VariablePath.UserPath -eq 'UiMode' }
if ($null -eq $uiModeParameter -or $uiModeParameter.DefaultValue.Value -ne 'Live') {
    throw 'UiMode must exist and default to Live.'
}
$validateSet = $uiModeParameter.Attributes |
    Where-Object { $_.TypeName.FullName -eq 'ValidateSet' }
$allowedModes = @($validateSet.PositionalArguments | ForEach-Object Value)
if ($allowedModes.Count -ne 2 -or 'Live' -notin $allowedModes -or 'Test' -notin $allowedModes) {
    throw 'UiMode must validate exactly Live and Test.'
}
if (-not (Test-Path -LiteralPath $cataloguePath -PathType Leaf)) {
    throw "Test UI catalogue not found: $cataloguePath"
}
$testBranch = $launcher.IndexOf("if (`$UiMode -eq 'Test')", [StringComparison]::Ordinal)
$liveMutex = $launcher.IndexOf("`$mutex = Enter-LifecycleMutex", [StringComparison]::Ordinal)
if ($testBranch -lt 0 -or $liveMutex -lt 0 -or $testBranch -gt $liveMutex) {
    throw 'Test UI must branch before the Live lifecycle mutex.'
}
if ($launcher -notmatch '\$platform\.Kind -eq ''Windows''' -or $launcher -notmatch '-Name ''xdg-open''') {
    throw 'Test UI must select the supported Windows and Linux openers.'
}

function Invoke-TestMode {
    param([Parameter(Mandatory)][hashtable]$Arguments)

    $global:PegasusTestUiOpenedFile = $null
    function global:Start-Process {
        param([string]$FilePath, [object[]]$ArgumentList)
        $global:PegasusTestUiOpenedFile = $FilePath
    }
    try {
        $result = & $launcherPath @Arguments
        [pscustomobject]@{ Result = $result; OpenedFile = $global:PegasusTestUiOpenedFile }
    }
    finally {
        Remove-Item Function:\global:Start-Process -ErrorAction SilentlyContinue
        Remove-Variable PegasusTestUiOpenedFile -Scope Global -ErrorAction SilentlyContinue
    }
}

$stateRoot = Join-Path $repositoryRoot 'artifacts/local-development'
$before = if (Test-Path -LiteralPath $stateRoot) {
    Get-ChildItem -LiteralPath $stateRoot -Force -Recurse |
        Select-Object FullName, Length, LastWriteTimeUtc
}
$testRun = Invoke-TestMode -Arguments @{ Action = 'Start'; UiMode = 'Test' }
$after = if (Test-Path -LiteralPath $stateRoot) {
    Get-ChildItem -LiteralPath $stateRoot -Force -Recurse |
        Select-Object FullName, Length, LastWriteTimeUtc
}
if (($before | ConvertTo-Json -Depth 3 -Compress) -cne ($after | ConvertTo-Json -Depth 3 -Compress)) {
    throw 'Test UI changed local-development runtime state.'
}
if ($testRun.Result.UiMode -ne 'Test' -or
    $testRun.Result.CataloguePath -ne [System.IO.Path]::GetFullPath($cataloguePath) -or
    $testRun.Result.CatalogueUri -notmatch '^file:' -or
    [string]::IsNullOrWhiteSpace([string]$testRun.OpenedFile)) {
    throw 'Test UI did not open and return its resolved catalogue contract.'
}

foreach ($invalid in @(
        @{ Action = 'Status'; UiMode = 'Test' },
        @{ Action = 'Start'; UiMode = 'Test'; RunId = ('a' * 32) },
        @{ Action = 'Start'; UiMode = 'Test'; FailureMode = 'AfterWeb' },
        @{ Action = 'Start'; UiMode = 'Test'; StoragePressureMegabytes = 32 },
        @{ Action = 'Start'; UiMode = 'Test'; StartupTimeoutSeconds = 120 })) {
    try {
        Invoke-TestMode -Arguments $invalid | Out-Null
        throw "Invalid Test UI arguments were accepted: $($invalid.Keys -join ', ')"
    }
    catch {
        if ($_.Exception.Message -like 'Invalid Test UI arguments were accepted:*') { throw }
    }
}

$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("pegasus-ui-mode-{0}" -f [Guid]::NewGuid().ToString('N'))
try {
    $temporaryScripts = Join-Path $temporaryRoot 'scripts'
    [System.IO.Directory]::CreateDirectory($temporaryScripts) | Out-Null
    Copy-Item -LiteralPath $launcherPath -Destination $temporaryScripts
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PegasusPlatform.ps1') -Destination $temporaryScripts
    try {
        & (Join-Path $temporaryScripts 'Invoke-LocalDevelopment.ps1') -Action Start -UiMode Test
        throw 'Test UI accepted a missing catalogue.'
    }
    catch {
        if ($_.Exception.Message -eq 'Test UI accepted a missing catalogue.') { throw }
        if ($_.Exception.Message -notlike 'Test UI catalogue not found:*') { throw }
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryRoot) {
        Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
    }
}

Write-Output 'Live/Test UI launcher checks passed.'
