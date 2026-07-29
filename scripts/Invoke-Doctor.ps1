[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Offline', 'Cloud')]
    [string]$Profile
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()

function Test-CommandVersion {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string]$Command,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [string]$ExpectedPattern,
        [Parameter(Mandatory)] [string]$Repair
    )

    $executable = Get-Command -Name $Command -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $executable) {
        $failures.Add("$Name is unavailable. Repair: $Repair")
        return
    }

    try {
        $output = (& $Command @Arguments 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $output -notmatch $ExpectedPattern) {
            $failures.Add("$Name does not match the selected profile. Observed: '$output'. Repair: $Repair")
        }
    }
    catch {
        $failures.Add("$Name could not be checked. Repair: $Repair")
    }
}

function Test-LocalDb {
    $executable = Get-Command -Name 'sqllocaldb' -CommandType Application -ErrorAction SilentlyContinue
    if ($null -eq $executable) {
        $failures.Add('SQL Server Express LocalDB is unavailable. Repair: Install SQL Server Express LocalDB for the current Windows user, then run sqllocaldb versions.')
        return
    }

    $versions = (& sqllocaldb versions 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($versions)) {
        $failures.Add('SQL Server Express LocalDB is unavailable. Repair: Install SQL Server Express LocalDB for the current Windows user, then run sqllocaldb versions.')
    }
}

function Test-DevelopmentCertificate {
    try {
        & dotnet dev-certs https --check | Out-Null
        if ($LASTEXITCODE -ne 0) {
            $failures.Add('The Development HTTPS certificate is not trusted. Repair: dotnet dev-certs https --trust')
        }
    }
    catch {
        $failures.Add('The Development HTTPS certificate could not be checked. Repair: dotnet dev-certs https --trust')
    }
}

if ($Profile -eq 'Offline') {
    Test-CommandVersion -Name 'PowerShell' -Command 'pwsh' -Arguments @('-NoLogo', '-NoProfile', '-Command', '$PSVersionTable.PSVersion.ToString()') -ExpectedPattern '^7\.6\.3$' -Repair 'Install PowerShell 7.6.3 for the current user.'
    Test-CommandVersion -Name '.NET SDK' -Command 'dotnet' -Arguments @('--version') -ExpectedPattern '^10\.0\.302$' -Repair 'Install .NET SDK 10.0.302 for the current user.'
    Test-CommandVersion -Name 'Python' -Command 'python' -Arguments @('--version') -ExpectedPattern '^Python 3\.(1[1-9]|[2-9][0-9])\.' -Repair 'Install Python 3.11 or later for the current user.'
    Test-CommandVersion -Name 'Node.js' -Command 'node' -Arguments @('--version') -ExpectedPattern '^v24\.' -Repair 'Install Node.js 24 for the current user.'
    Test-CommandVersion -Name 'npm' -Command 'npm' -Arguments @('--version') -ExpectedPattern '^11\.' -Repair 'Install npm 11 with Node.js 24 for the current user.'
    Test-CommandVersion -Name 'Azurite' -Command 'npx' -Arguments @('--no-install', 'azurite', '--version') -ExpectedPattern '^3\.36\.0' -Repair 'Run npm ci from the repository root.'
    Test-CommandVersion -Name 'Azure Functions Core Tools' -Command 'func' -Arguments @('--version') -ExpectedPattern '^4\.12\.1' -Repair 'Install Azure Functions Core Tools 4.12.1 for the current user.'
    Test-LocalDb
    Test-DevelopmentCertificate
}
else {
    Test-CommandVersion -Name 'Azure CLI' -Command 'az' -Arguments @('version', '--output', 'tsv') -ExpectedPattern 'azure-cli\s+2\.88\.' -Repair 'Install Azure CLI 2.88 for the current user.'
    Test-CommandVersion -Name 'Azure Developer CLI' -Command 'azd' -Arguments @('version') -ExpectedPattern 'azd version 1\.28\.0' -Repair 'Install Azure Developer CLI 1.28.0 for the current user.'
    Test-CommandVersion -Name 'Bicep CLI' -Command 'bicep' -Arguments @('--version') -ExpectedPattern '0\.45\.15' -Repair 'Install Bicep CLI 0.45.15 for the current user.'
    Test-CommandVersion -Name 'GitHub CLI' -Command 'gh' -Arguments @('--version') -ExpectedPattern '^gh version 2\.88\.' -Repair 'Install GitHub CLI 2.88 for the current user.'
    Test-CommandVersion -Name 'Infisical CLI' -Command 'infisical' -Arguments @('--version') -ExpectedPattern '0\.43\.104' -Repair 'Install Infisical CLI 0.43.104 for the current user.'
    Test-CommandVersion -Name 'Box CLI' -Command 'box' -Arguments @('--version') -ExpectedPattern '4\.9\.2' -Repair 'Install Box CLI 4.9.2 for the current user.'
    Test-CommandVersion -Name 'sqlcmd' -Command 'sqlcmd' -Arguments @('--version') -ExpectedPattern '1\.10\.0' -Repair 'Install Microsoft Go sqlcmd 1.10.0 for the current user.'

    if ($null -eq (Get-Module -ListAvailable -Name SqlServer | Where-Object { $_.Version -eq [version]'22.4.5.1' })) {
        $failures.Add('SqlServer PowerShell module 22.4.5.1 is unavailable. Repair: Install-Module SqlServer -Scope CurrentUser -RequiredVersion 22.4.5.1 -Force -AllowClobber -Repository PSGallery')
    }
    if ($null -eq (Get-Module -ListAvailable -Name ExchangeOnlineManagement | Where-Object { $_.Version -eq [version]'3.10.0' })) {
        $failures.Add('ExchangeOnlineManagement PowerShell module 3.10.0 is unavailable. Repair: Install-Module ExchangeOnlineManagement -Scope CurrentUser -RequiredVersion 3.10.0 -Force -AllowClobber -Repository PSGallery')
    }
}

if ($failures.Count -gt 0) {
    [Console]::Error.WriteLine("Doctor profile '$Profile' failed:")
    $failures | ForEach-Object { [Console]::Error.WriteLine(" - $_") }
    exit 1
}

Write-Output "Doctor profile '$Profile' is ready. No install, authentication, cloud, or vendor operation was performed."
