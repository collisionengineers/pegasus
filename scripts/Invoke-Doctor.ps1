[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$requiredCommands = [ordered]@{
    'PowerShell 7' = 'pwsh'
    'Git' = 'git'
    '.NET SDK' = 'dotnet'
    'Azure CLI' = 'az'
    'Azure Developer CLI' = 'azd'
    'Azure Functions Core Tools' = 'func'
    'GitHub CLI' = 'gh'
    'Node.js' = 'node'
    'npm' = 'npm'
    'Python' = 'python'
    'Infisical CLI' = 'infisical'
    'Box CLI' = 'box'
    'SQL Server Express LocalDB' = 'sqllocaldb'
}

$missing = @()
$azdFallback = Join-Path $env:LOCALAPPDATA 'Programs/Azure Dev CLI/azd.exe'

foreach ($entry in $requiredCommands.GetEnumerator()) {
    $command = Get-Command $entry.Value -ErrorAction SilentlyContinue
    if (-not $command -and $entry.Value -eq 'azd' -and (Test-Path -LiteralPath $azdFallback)) {
        $command = Get-Item -LiteralPath $azdFallback
    }
    if ($command) {
        $commandPath = if ($command.Source) { $command.Source } else { $command.FullName }
        Write-Host ('[ok] {0}: {1}' -f $entry.Key, $commandPath)
    }
    else {
        Write-Host ('[missing] {0} ({1})' -f $entry.Key, $entry.Value) -ForegroundColor Red
        $missing += $entry.Value
    }
}

if (Get-Command az -ErrorAction SilentlyContinue) {
    $account = az account show --query '{subscription:name,id:id,tenant:tenantId}' -o json 2>$null
    if ($LASTEXITCODE -eq 0) {
        $summary = $account | ConvertFrom-Json
        Write-Host ('[ok] Azure login: {0} ({1})' -f $summary.subscription, $summary.id)
    }
    else {
        Write-Host '[missing] Azure CLI login. Run: az login' -ForegroundColor Red
        $missing += 'az-login'
    }

    az bicep version | Out-Host
}

if (Get-Command sqllocaldb -ErrorAction SilentlyContinue) {
    sqllocaldb versions | Out-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Host '[missing] SQL Server Express LocalDB runtime is unavailable.' -ForegroundColor Red
        $missing += 'sqllocaldb-runtime'
    }
}

if ($missing.Count -gt 0) {
    if ($missing -contains 'azd') {
        Write-Host 'Install Azure Developer CLI: winget install --id Microsoft.Azd --exact --accept-package-agreements --accept-source-agreements' -ForegroundColor Yellow
    }
    throw "Developer workstation is missing: $($missing -join ', ')"
}

Write-Host 'Developer workstation is ready.' -ForegroundColor Green
