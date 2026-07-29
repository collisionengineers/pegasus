[CmdletBinding()]
param(
    [ValidateSet('Offline', 'Cloud')]
    [string]$Profile = 'Offline'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$checks = [System.Collections.Generic.List[object]]::new()

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [string[]]$Arguments = @()
    )

    try {
        $output = (& $Command @Arguments 2>&1 | Out-String).Trim()
        return [pscustomobject]@{
            ExitCode = $LASTEXITCODE
            Output = $output
        }
    }
    catch {
        return [pscustomobject]@{
            ExitCode = -1
            Output = $_.Exception.Message
        }
    }
}

function Add-Check {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [bool]$Passed,
        [Parameter(Mandatory)]
        [string]$Detail,
        [Parameter(Mandatory)]
        [string]$Repair
    )

    $checks.Add([pscustomobject][ordered]@{
        Name = $Name
        Passed = $Passed
        Detail = $Detail
        Repair = $Repair
    })
}

function Get-ApplicationPath {
    param([Parameter(Mandatory)][string]$Name)

    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        return $null
    }

    return [string]$command.Source
}

function Test-ExactPowerShellModule {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [version]$Version,
        [Parameter(Mandatory)]
        [string]$Repair
    )

    $installed = Get-Module -ListAvailable -Name $Name |
        Where-Object { $_.Version -eq $Version } |
        Select-Object -First 1
    Add-Check `
        -Name "$Name PowerShell module" `
        -Passed ($null -ne $installed) `
        -Detail $(if ($null -eq $installed) {
            "Required version $Version is not installed."
        }
        else {
            "Version $($installed.Version) is available at $($installed.ModuleBase)."
        }) `
        -Repair $Repair
}

$isWindows11 = $IsWindows -and [System.Environment]::OSVersion.Version.Build -ge 22000
Add-Check `
    -Name 'Windows' `
    -Passed $isWindows11 `
    -Detail $(if ($isWindows11) {
        "Windows build $([System.Environment]::OSVersion.Version.Build)."
    }
    else {
        'Windows 11 build 22000 or later is required.'
    }) `
    -Repair 'Use the approved workstation-administration route to update this workstation to Windows 11.'

$requiredPowerShell = [version]'7.6.3'
$powerShellReady = $PSVersionTable.PSEdition -eq 'Core' -and
    $PSVersionTable.PSVersion -eq $requiredPowerShell
Add-Check `
    -Name 'PowerShell' `
    -Passed $powerShellReady `
    -Detail "Found $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion); required PowerShell $requiredPowerShell." `
    -Repair 'winget install --exact --id Microsoft.PowerShell --version 7.6.3 --scope user'

$gitPath = Get-ApplicationPath -Name 'git'
if ($null -eq $gitPath) {
    Add-Check `
        -Name 'Git checkout' `
        -Passed $false `
        -Detail 'git is not available.' `
        -Repair 'winget install --exact --id Git.Git --scope user'
}
else {
    $gitVersion = Invoke-NativeCommand -Command $gitPath -Arguments @('--version')
    $gitRoot = Invoke-NativeCommand -Command $gitPath -Arguments @(
        '-C',
        $repositoryRoot,
        'rev-parse',
        '--show-toplevel'
    )
    $resolvedGitRoot = $null
    if ($gitRoot.ExitCode -eq 0 -and -not [string]::IsNullOrWhiteSpace($gitRoot.Output)) {
        try {
            $resolvedGitRoot = [System.IO.Path]::GetFullPath($gitRoot.Output.Trim())
        }
        catch {
            $resolvedGitRoot = $null
        }
    }
    $gitReady = $gitVersion.ExitCode -eq 0 -and
        $null -ne $resolvedGitRoot -and
        $resolvedGitRoot.Equals($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)
    Add-Check `
        -Name 'Git checkout' `
        -Passed $gitReady `
        -Detail $(if ($gitReady) {
            "$($gitVersion.Output); checkout root $resolvedGitRoot."
        }
        else {
            "The checkout root could not be proved as $repositoryRoot."
        }) `
        -Repair "git -C `"$repositoryRoot`" rev-parse --show-toplevel"
}

$dotnetPath = Get-ApplicationPath -Name 'dotnet'
if ($null -eq $dotnetPath) {
    Add-Check `
        -Name '.NET SDK' `
        -Passed $false `
        -Detail 'dotnet is not available.' `
        -Repair 'winget install --exact --id Microsoft.DotNet.SDK.10 --version 10.0.302 --scope user'
}
else {
    $dotnetVersion = Invoke-NativeCommand -Command $dotnetPath -Arguments @('--version')
    $dotnetReady = $dotnetVersion.ExitCode -eq 0 -and $dotnetVersion.Output -eq '10.0.302'
    Add-Check `
        -Name '.NET SDK' `
        -Passed $dotnetReady `
        -Detail "Found '$($dotnetVersion.Output)'; required 10.0.302." `
        -Repair 'winget install --exact --id Microsoft.DotNet.SDK.10 --version 10.0.302 --scope user'
}

$nodePath = Get-ApplicationPath -Name 'node'
if ($null -eq $nodePath) {
    Add-Check `
        -Name 'Node.js' `
        -Passed $false `
        -Detail 'node is not available.' `
        -Repair 'winget install --exact --id OpenJS.NodeJS --version 24.0.0 --scope user'
}
else {
    $nodeVersion = Invoke-NativeCommand -Command $nodePath -Arguments @('--version')
    $nodeReady = $nodeVersion.ExitCode -eq 0 -and $nodeVersion.Output -match '^v24(?:\.|$)'
    Add-Check `
        -Name 'Node.js' `
        -Passed $nodeReady `
        -Detail "Found '$($nodeVersion.Output)'; required major version 24." `
        -Repair 'winget install --exact --id OpenJS.NodeJS --version 24.0.0 --scope user'
}

$npmPath = Get-ApplicationPath -Name 'npm'
if ($null -eq $npmPath) {
    Add-Check `
        -Name 'npm' `
        -Passed $false `
        -Detail 'npm is not available.' `
        -Repair 'winget install --exact --id OpenJS.NodeJS --version 24.0.0 --scope user'
}
else {
    $npmVersion = Invoke-NativeCommand -Command $npmPath -Arguments @('--version')
    $npmReady = $npmVersion.ExitCode -eq 0 -and $npmVersion.Output -match '^11(?:\.|$)'
    Add-Check `
        -Name 'npm' `
        -Passed $npmReady `
        -Detail "Found '$($npmVersion.Output)'; required major version 11." `
        -Repair 'npm install --global npm@11'
}

$pythonPath = Get-ApplicationPath -Name 'python'
if ($null -eq $pythonPath) {
    Add-Check `
        -Name 'Python' `
        -Passed $false `
        -Detail 'python is not available.' `
        -Repair 'winget install --exact --id Python.Python.3.14 --scope user'
}
else {
    $pythonVersion = Invoke-NativeCommand -Command $pythonPath -Arguments @('--version')
    $pythonReady = $false
    if ($pythonVersion.ExitCode -eq 0 -and
        $pythonVersion.Output -match 'Python\s+(?<version>\d+\.\d+(?:\.\d+)?)') {
        $pythonReady = [version]$Matches.version -ge [version]'3.11'
    }
    Add-Check `
        -Name 'Python' `
        -Passed $pythonReady `
        -Detail "Found '$($pythonVersion.Output)'; required 3.11 or later." `
        -Repair 'winget install --exact --id Python.Python.3.14 --scope user'
}

$azuritePath = Join-Path $repositoryRoot 'node_modules/.bin/azurite.cmd'
$packagePath = Join-Path $repositoryRoot 'package.json'
$azuritePinned = $false
if ([System.IO.File]::Exists($packagePath)) {
    try {
        $package = [System.IO.File]::ReadAllText($packagePath) | ConvertFrom-Json
        $azuritePinned = [string]$package.devDependencies.azurite -eq '3.36.0'
    }
    catch {
        $azuritePinned = $false
    }
}
$azuriteVersion = if ([System.IO.File]::Exists($azuritePath)) {
    Invoke-NativeCommand -Command $azuritePath -Arguments @('--version')
}
else {
    [pscustomobject]@{ ExitCode = -1; Output = 'not installed' }
}
$azuriteReady = $azuritePinned -and
    $azuriteVersion.ExitCode -eq 0 -and
    $azuriteVersion.Output -eq '3.36.0'
Add-Check `
    -Name 'Azurite' `
    -Passed $azuriteReady `
    -Detail "Repository pin present: $azuritePinned; installed version '$($azuriteVersion.Output)'." `
    -Repair "npm ci --prefix `"$repositoryRoot`""

$funcPath = Get-ApplicationPath -Name 'func'
if ($null -eq $funcPath) {
    Add-Check `
        -Name 'Azure Functions Core Tools' `
        -Passed $false `
        -Detail 'func is not available.' `
        -Repair 'winget install --exact --id Microsoft.Azure.FunctionsCoreTools --version 4.12.1 --scope user'
}
else {
    $funcVersion = Invoke-NativeCommand -Command $funcPath -Arguments @('--version')
    $funcReady = $funcVersion.ExitCode -eq 0 -and
        $funcVersion.Output -match '^4\.12\.1(?:\D|$)'
    Add-Check `
        -Name 'Azure Functions Core Tools' `
        -Passed $funcReady `
        -Detail "Found '$($funcVersion.Output)'; required 4.12.1." `
        -Repair 'winget install --exact --id Microsoft.Azure.FunctionsCoreTools --version 4.12.1 --scope user'
}

$localDbPath = Get-ApplicationPath -Name 'sqllocaldb'
if ($null -eq $localDbPath) {
    Add-Check `
        -Name 'SQL Server Express LocalDB' `
        -Passed $false `
        -Detail 'sqllocaldb is not available.' `
        -Repair 'winget install --exact --id Microsoft.SQLServer.2022.Express --override "/ACTION=Install /QUIET /IACCEPTSQLSERVERLICENSETERMS /FEATURES=LocalDB"'
}
else {
    $localDbVersions = Invoke-NativeCommand -Command $localDbPath -Arguments @('versions')
    $localDbReady = $localDbVersions.ExitCode -eq 0 -and
        -not [string]::IsNullOrWhiteSpace($localDbVersions.Output)
    Add-Check `
        -Name 'SQL Server Express LocalDB' `
        -Passed $localDbReady `
        -Detail $(if ($localDbReady) {
            "Installed LocalDB versions: $($localDbVersions.Output -replace '\s+', ' ')."
        }
        else {
            'No usable LocalDB version was reported.'
        }) `
        -Repair 'winget install --exact --id Microsoft.SQLServer.2022.Express --override "/ACTION=Install /QUIET /IACCEPTSQLSERVERLICENSETERMS /FEATURES=LocalDB"'
}

Test-ExactPowerShellModule `
    -Name 'SqlServer' `
    -Version ([version]'22.4.5.1') `
    -Repair 'Install-Module SqlServer -Scope CurrentUser -RequiredVersion 22.4.5.1 -Force -AllowClobber -Repository PSGallery'

$certificateCheck = if ($null -ne $dotnetPath) {
    Invoke-NativeCommand -Command $dotnetPath -Arguments @(
        'dev-certs',
        'https',
        '--check',
        '--trust'
    )
}
else {
    [pscustomobject]@{ ExitCode = -1; Output = 'dotnet is not available' }
}
Add-Check `
    -Name 'Development HTTPS certificate' `
    -Passed ($certificateCheck.ExitCode -eq 0) `
    -Detail $(if ($certificateCheck.ExitCode -eq 0) {
        'A valid trusted Development HTTPS certificate is available.'
    }
    else {
        'A valid trusted Development HTTPS certificate is not available.'
    }) `
    -Repair 'dotnet dev-certs https --trust'

$playwrightCandidates = @(
    (Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/bin/Debug/net10.0/playwright.ps1'),
    (Join-Path $repositoryRoot 'tests/Pegasus.IntegrationTests/bin/Release/net10.0/playwright.ps1')
)
$playwrightPath = $playwrightCandidates |
    Where-Object { [System.IO.File]::Exists($_) } |
    Select-Object -First 1
$playwrightReady = $false
$playwrightDetail = 'The generated Microsoft.Playwright install command is missing.'
if ($null -ne $playwrightPath) {
    $playwrightDryRun = Invoke-NativeCommand -Command $playwrightPath -Arguments @(
        'install',
        '--dry-run',
        'chromium'
    )
    $installLocations = @()
    if ($playwrightDryRun.ExitCode -eq 0) {
        $installLocations = @(
            [regex]::Matches(
                $playwrightDryRun.Output,
                '(?im)^\s*Install location:\s*(?<path>.+?)\s*$') |
                ForEach-Object { $_.Groups['path'].Value.Trim() }
        )
    }
    $playwrightReady = $installLocations.Count -gt 0 -and
        @($installLocations | Where-Object {
            -not [System.IO.Directory]::Exists($_)
        }).Count -eq 0
    $playwrightDetail = if ($playwrightReady) {
        "Pinned Chromium payload is installed at $($installLocations -join ', ')."
    }
    elseif ($playwrightDryRun.ExitCode -ne 0) {
        "Playwright inspection failed: $($playwrightDryRun.Output)"
    }
    else {
        'One or more package-pinned Chromium payload directories are missing.'
    }
}
$playwrightRepair = @(
    "dotnet restore `"$repositoryRoot/Pegasus.slnx`" --locked-mode",
    "dotnet build `"$repositoryRoot/tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj`" --no-restore",
    "pwsh `"$repositoryRoot/tests/Pegasus.IntegrationTests/bin/Debug/net10.0/playwright.ps1`" install chromium"
) -join '; '
Add-Check `
    -Name 'Microsoft.Playwright Chromium' `
    -Passed $playwrightReady `
    -Detail $playwrightDetail `
    -Repair $playwrightRepair

if ($Profile -eq 'Cloud') {
    $cloudApplications = @(
        [pscustomobject]@{
            Name = 'Azure CLI'
            Command = 'az'
            Arguments = @('version', '--output', 'json')
            Pattern = '"azure-cli"\s*:\s*"2\.88(?:\.0)?"'
            Repair = 'winget install --exact --id Microsoft.AzureCLI --version 2.88.0 --scope user'
        },
        [pscustomobject]@{
            Name = 'Azure Developer CLI'
            Command = 'azd'
            Arguments = @('version')
            Pattern = '\b1\.28\.0\b'
            Repair = 'winget install --exact --id Microsoft.Azd --version 1.28.0 --scope user'
        },
        [pscustomobject]@{
            Name = 'Bicep CLI'
            Command = 'bicep'
            Arguments = @('--version')
            Pattern = '\b0\.45\.15\b'
            Repair = 'winget install --exact --id Microsoft.Bicep --version 0.45.15 --scope user'
        },
        [pscustomobject]@{
            Name = 'GitHub CLI'
            Command = 'gh'
            Arguments = @('--version')
            Pattern = '\b2\.88(?:\.0)?\b'
            Repair = 'winget install --exact --id GitHub.cli --version 2.88.0 --scope user'
        },
        [pscustomobject]@{
            Name = 'Infisical CLI'
            Command = 'infisical'
            Arguments = @('--version')
            Pattern = '\b0\.43\.104\b'
            Repair = 'winget install --exact --id Infisical.cli --version 0.43.104 --scope user'
        },
        [pscustomobject]@{
            Name = 'Box CLI'
            Command = 'box'
            Arguments = @('--version')
            Pattern = '\b4\.9\.2\b'
            Repair = 'npm install --global @box/cli@4.9.2'
        },
        [pscustomobject]@{
            Name = 'Microsoft Go sqlcmd'
            Command = 'sqlcmd'
            Arguments = @('--version')
            Pattern = '\b1\.10\.0\b'
            Repair = 'winget install --exact --id Microsoft.Sqlcmd --version 1.10.0 --scope user'
        }
    )

    foreach ($tool in $cloudApplications) {
        $toolPath = Get-ApplicationPath -Name $tool.Command
        if ($null -eq $toolPath) {
            Add-Check `
                -Name $tool.Name `
                -Passed $false `
                -Detail "$($tool.Command) is not available." `
                -Repair $tool.Repair
            continue
        }

        $toolVersion = Invoke-NativeCommand -Command $toolPath -Arguments $tool.Arguments
        $toolReady = $toolVersion.ExitCode -eq 0 -and
            $toolVersion.Output -match $tool.Pattern
        Add-Check `
            -Name $tool.Name `
            -Passed $toolReady `
            -Detail "Version output: '$($toolVersion.Output)'." `
            -Repair $tool.Repair
    }

    Test-ExactPowerShellModule `
        -Name 'ExchangeOnlineManagement' `
        -Version ([version]'3.10.0') `
        -Repair 'Install-Module ExchangeOnlineManagement -Scope CurrentUser -RequiredVersion 3.10.0 -Force -AllowClobber -Repository PSGallery'
}

Write-Host "Pegasus Doctor profile: $Profile"
foreach ($check in $checks) {
    $label = if ($check.Passed) { 'PASS' } else { 'FAIL' }
    Write-Host "[$label] $($check.Name): $($check.Detail)"
    if (-not $check.Passed) {
        Write-Host "       Repair: $($check.Repair)"
    }
}

$failures = @($checks | Where-Object { -not $_.Passed })
if ($failures.Count -gt 0) {
    throw "Pegasus Doctor $Profile failed $($failures.Count) prerequisite check(s). No software was installed and no external service was contacted."
}

Write-Host "Pegasus Doctor $Profile passed. This result grants no external-operation approval."
