[CmdletBinding()]
param(
    [ValidateSet('Offline', 'Cloud')]
    [string]$Profile = 'Offline'
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$checks = [System.Collections.Generic.List[object]]::new()

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')
$platform = Get-PegasusPlatform

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
        [string]$Repair,
        # An advisory check reports a real result but does not fail the profile.
        # Use it only where the requirement genuinely does not apply to this
        # platform; never report Passed for something that is not true.
        [switch]$Advisory
    )

    $checks.Add([pscustomobject][ordered]@{
        Name = $Name
        Passed = $Passed
        Detail = $Detail
        Repair = $Repair
        Advisory = [bool]$Advisory
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

if ($platform.IsWindows) {
    $platformReady = [System.Environment]::OSVersion.Version.Build -ge 22000
    $platformDetail = if ($platformReady) {
        "Windows build $([System.Environment]::OSVersion.Version.Build)."
    }
    else {
        'Windows 11 build 22000 or later is required.'
    }
}
else {
    # A reachable Docker daemon is a platform prerequisite on Linux: it hosts
    # the local database. Querying the server version proves both that the
    # daemon is running and that this account may talk to it.
    $dockerPath = Get-ApplicationPath -Name 'docker'
    $dockerServer = if ($null -eq $dockerPath) {
        [pscustomobject]@{ ExitCode = -1; Output = 'docker is not available.' }
    }
    else {
        Invoke-NativeCommand -Command $dockerPath -Arguments @('version', '--format', '{{.Server.Version}}')
    }
    $platformReady = $dockerServer.ExitCode -eq 0 -and
        -not [string]::IsNullOrWhiteSpace($dockerServer.Output)
    $platformDetail = if ($platformReady) {
        "$([System.Environment]::OSVersion.VersionString); Docker daemon $($dockerServer.Output.Trim())."
    }
    else {
        "A reachable Docker daemon is required: $($dockerServer.Output)"
    }
}

Add-Check `
    -Name 'Platform' `
    -Passed $platformReady `
    -Detail $platformDetail `
    -Repair $(if ($platform.IsWindows) {
        Get-PegasusRepairHint -Id 'platform'
    }
    else {
        Get-PegasusRepairHint -Id 'container-runtime'
    })

# The PowerShell host is not an input to any hashed build artifact, and there is
# no supported way to hold a workstation at one exact patch on both platforms.
# A floor plus a major-version ceiling is enforceable; an exact pin is not.
$minimumPowerShell = [version]'7.6.3'
$powerShellReady = $PSVersionTable.PSEdition -eq 'Core' -and
    $PSVersionTable.PSVersion -ge $minimumPowerShell -and
    $PSVersionTable.PSVersion -lt [version]'8.0'
Add-Check `
    -Name 'PowerShell' `
    -Passed $powerShellReady `
    -Detail "Found $($PSVersionTable.PSEdition) $($PSVersionTable.PSVersion); required PowerShell $minimumPowerShell or later, below 8.0." `
    -Repair (Get-PegasusRepairHint -Id 'powershell')

$gitPath = Get-ApplicationPath -Name 'git'
if ($null -eq $gitPath) {
    Add-Check `
        -Name 'Git checkout' `
        -Passed $false `
        -Detail 'git is not available.' `
        -Repair (Get-PegasusRepairHint -Id 'git')
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
        -Repair (Get-PegasusRepairHint -Id 'dotnet-sdk')
}
else {
    # Enumerate installed SDKs rather than asking for the resolved version.
    # 'dotnet --version' obeys global.json, so when the pinned SDK is absent it
    # reports a resolution error instead of a version, which hides the cause.
    $requiredSdk = '10.0.302'
    $installedSdks = Invoke-NativeCommand -Command $dotnetPath -Arguments @('--list-sdks')
    $sdkVersions = @()
    if ($installedSdks.ExitCode -eq 0) {
        $sdkVersions = @(
            $installedSdks.Output -split "`n" |
                ForEach-Object { ($_ -split ' ')[0].Trim() } |
                Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        )
    }
    $dotnetReady = $sdkVersions -contains $requiredSdk
    Add-Check `
        -Name '.NET SDK' `
        -Passed $dotnetReady `
        -Detail $(if ($dotnetReady) {
            "SDK $requiredSdk is installed."
        }
        else {
            "SDK $requiredSdk is required; installed: $(if ($sdkVersions.Count) { $sdkVersions -join ', ' } else { 'none' })."
        }) `
        -Repair (Get-PegasusRepairHint -Id 'dotnet-sdk')
}

$nodePath = Get-ApplicationPath -Name 'node'
if ($null -eq $nodePath) {
    Add-Check `
        -Name 'Node.js' `
        -Passed $false `
        -Detail 'node is not available.' `
        -Repair (Get-PegasusRepairHint -Id 'node')
}
else {
    $nodeVersion = Invoke-NativeCommand -Command $nodePath -Arguments @('--version')
    $nodeReady = $nodeVersion.ExitCode -eq 0 -and $nodeVersion.Output -match '^v24(?:\.|$)'
    Add-Check `
        -Name 'Node.js' `
        -Passed $nodeReady `
        -Detail "Found '$($nodeVersion.Output)'; required major version 24." `
        -Repair (Get-PegasusRepairHint -Id 'node')
}

$npmPath = Get-ApplicationPath -Name 'npm'
if ($null -eq $npmPath) {
    Add-Check `
        -Name 'npm' `
        -Passed $false `
        -Detail 'npm is not available.' `
        -Repair (Get-PegasusRepairHint -Id 'node')
}
else {
    $npmVersion = Invoke-NativeCommand -Command $npmPath -Arguments @('--version')
    $npmReady = $npmVersion.ExitCode -eq 0 -and $npmVersion.Output -match '^11(?:\.|$)'
    Add-Check `
        -Name 'npm' `
        -Passed $npmReady `
        -Detail "Found '$($npmVersion.Output)'; required major version 11." `
        -Repair (Get-PegasusRepairHint -Id 'npm')
}

# Most Linux distributions ship the interpreter as 'python3' and provide no
# unversioned 'python'.
$pythonPath = Get-ApplicationPath -Name 'python'
if ($null -eq $pythonPath -and -not $platform.IsWindows) {
    $pythonPath = Get-ApplicationPath -Name 'python3'
}
if ($null -eq $pythonPath) {
    Add-Check `
        -Name 'Python' `
        -Passed $false `
        -Detail 'python is not available.' `
        -Repair (Get-PegasusRepairHint -Id 'python')
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
        -Repair (Get-PegasusRepairHint -Id 'python')
}

$azuritePath = Join-Path $repositoryRoot (Join-Path 'node_modules/.bin' (Get-PegasusExecutableName -BaseName 'azurite' -Kind NodeShim))
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
        -Repair (Get-PegasusRepairHint -Id 'func')
}
else {
    $funcVersion = Invoke-NativeCommand -Command $funcPath -Arguments @('--version')
    $funcReady = $funcVersion.ExitCode -eq 0 -and
        $funcVersion.Output -match '^4\.12\.1(?:\D|$)'
    Add-Check `
        -Name 'Azure Functions Core Tools' `
        -Passed $funcReady `
        -Detail "Found '$($funcVersion.Output)'; required 4.12.1." `
        -Repair (Get-PegasusRepairHint -Id 'func')
}

if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
    $localDbPath = Get-ApplicationPath -Name 'sqllocaldb'
    if ($null -eq $localDbPath) {
        Add-Check `
            -Name 'Local database engine' `
            -Passed $false `
            -Detail 'sqllocaldb is not available.' `
            -Repair (Get-PegasusRepairHint -Id 'database-engine')
    }
    else {
        $localDbVersions = Invoke-NativeCommand -Command $localDbPath -Arguments @('versions')
        $localDbReady = $localDbVersions.ExitCode -eq 0 -and
            -not [string]::IsNullOrWhiteSpace($localDbVersions.Output)
        Add-Check `
            -Name 'Local database engine' `
            -Passed $localDbReady `
            -Detail $(if ($localDbReady) {
                "SQL Server Express LocalDB. Installed versions: $($localDbVersions.Output -replace '\s+', ' ')."
            }
            else {
                'No usable LocalDB version was reported.'
            }) `
            -Repair (Get-PegasusRepairHint -Id 'database-engine')
    }
}
else {
    # Assert the pinned image is already present. An initialized run must not
    # depend on a package feed, so this never pulls.
    $image = Get-PegasusDatabaseImageReference
    $dockerPath = Get-ApplicationPath -Name 'docker'
    $imageInspect = if ($null -eq $dockerPath) {
        [pscustomobject]@{ ExitCode = -1; Output = 'docker is not available.' }
    }
    else {
        Invoke-NativeCommand -Command $dockerPath -Arguments @('image', 'inspect', $image)
    }
    $imageReady = $imageInspect.ExitCode -eq 0
    Add-Check `
        -Name 'Local database engine' `
        -Passed $imageReady `
        -Detail $(if ($imageReady) {
            "SQL Server container image is present locally: $image."
        }
        else {
            "The pinned SQL Server image is not present locally: $image."
        }) `
        -Repair (Get-PegasusRepairHint -Id 'database-engine')
}

if ($platform.IsWindows) {
    # The module drives LocalDB and Azure SQL administration from the Windows
    # release terminal. Linux uses go-sqlcmd, checked in the Cloud profile.
    Test-ExactPowerShellModule `
        -Name 'SqlServer' `
        -Version ([version]'22.4.5.1') `
        -Repair (Get-PegasusRepairHint -Id 'module-sqlserver')
}

# Kestrel requires the certificate to exist; the loopback probes in the local
# development lifecycle do not validate it. Trust is therefore a separate
# question from existence, and only the browser evidence lane needs it.
$certificateCheck = if ($null -ne $dotnetPath) {
    Invoke-NativeCommand -Command $dotnetPath -Arguments @('dev-certs', 'https', '--check')
}
else {
    [pscustomobject]@{ ExitCode = -1; Output = 'dotnet is not available' }
}
Add-Check `
    -Name 'Development HTTPS certificate' `
    -Passed ($certificateCheck.ExitCode -eq 0) `
    -Detail $(if ($certificateCheck.ExitCode -eq 0) {
        'A valid Development HTTPS certificate is available.'
    }
    else {
        'A valid Development HTTPS certificate is not available.'
    }) `
    -Repair (Get-PegasusRepairHint -Id 'dev-certs')

$trustCheck = if ($null -ne $dotnetPath) {
    Invoke-NativeCommand -Command $dotnetPath -Arguments @('dev-certs', 'https', '--check', '--trust')
}
else {
    [pscustomobject]@{ ExitCode = -1; Output = 'dotnet is not available' }
}
$trustReady = $trustCheck.ExitCode -eq 0
if ($platform.IsWindows) {
    Add-Check `
        -Name 'Development HTTPS certificate trust' `
        -Passed $trustReady `
        -Detail $(if ($trustReady) {
            'The Development HTTPS certificate is trusted.'
        }
        else {
            'The Development HTTPS certificate is not trusted.'
        }) `
        -Repair (Get-PegasusRepairHint -Id 'dev-certs-trust')
}
else {
    # On Linux 'dotnet dev-certs https --trust' populates per-user NSS databases
    # and needs libnss3-tools. It does not affect HttpClient or curl, so the
    # local lifecycle lanes do not need it; the Playwright browser lane does.
    Add-Check `
        -Name 'Development HTTPS certificate trust' `
        -Passed $trustReady `
        -Detail $(if ($trustReady) {
            'The Development HTTPS certificate is trusted for browser clients.'
        }
        else {
            'The Development HTTPS certificate is not trusted for browser clients. Start, Status, Smoke and Stop do not require this; the Playwright browser evidence lane does.'
        }) `
        -Repair (Get-PegasusRepairHint -Id 'dev-certs-trust') `
        -Advisory
}

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
    # The Playwright driver writes its report straight to the console device,
    # so neither 2>&1 nor *>&1 captures it. Redirect standard output at the
    # process level instead.
    $playwrightOutputPath = [System.IO.Path]::GetTempFileName()
    try {
        $playwrightProcess = Start-Process `
            -FilePath 'pwsh' `
            -ArgumentList @('-NoProfile', '-File', $playwrightPath, 'install', '--dry-run', 'chromium') `
            -RedirectStandardOutput $playwrightOutputPath `
            -NoNewWindow `
            -PassThru `
            -Wait
        $playwrightDryRun = [pscustomobject]@{
            ExitCode = $playwrightProcess.ExitCode
            Output = [System.IO.File]::ReadAllText($playwrightOutputPath)
        }
    }
    catch {
        $playwrightDryRun = [pscustomobject]@{ ExitCode = -1; Output = $_.Exception.Message }
    }
    finally {
        Remove-Item -LiteralPath $playwrightOutputPath -Force -ErrorAction SilentlyContinue
    }

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
            Repair = (Get-PegasusRepairHint -Id 'az')
        },
        [pscustomobject]@{
            Name = 'Azure Developer CLI'
            Command = 'azd'
            Arguments = @('version')
            Pattern = '\b1\.28\.0\b'
            Repair = (Get-PegasusRepairHint -Id 'azd')
        },
        [pscustomobject]@{
            Name = 'Bicep CLI'
            Command = 'bicep'
            Arguments = @('--version')
            Pattern = '\b0\.45\.15\b'
            Repair = (Get-PegasusRepairHint -Id 'bicep')
        },
        [pscustomobject]@{
            Name = 'GitHub CLI'
            Command = 'gh'
            Arguments = @('--version')
            Pattern = '\b2\.88(?:\.0)?\b'
            Repair = (Get-PegasusRepairHint -Id 'gh')
        },
        [pscustomobject]@{
            Name = 'Infisical CLI'
            Command = 'infisical'
            Arguments = @('--version')
            Pattern = '\b0\.43\.104\b'
            Repair = (Get-PegasusRepairHint -Id 'infisical')
        },
        [pscustomobject]@{
            Name = 'Box CLI'
            Command = 'box'
            Arguments = @('--version')
            Pattern = '\b4\.9\.2\b'
            Repair = (Get-PegasusRepairHint -Id 'box')
        },
        [pscustomobject]@{
            Name = 'Microsoft Go sqlcmd'
            Command = 'sqlcmd'
            Arguments = @('--version')
            Pattern = '\bv?1\.10\.0\b'
            Repair = (Get-PegasusRepairHint -Id 'sqlcmd')
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
        -Repair (Get-PegasusRepairHint -Id 'module-exchange')
}

Write-Host "Pegasus Doctor profile: $Profile ($($platform.Kind))"
foreach ($check in $checks) {
    $label = if ($check.Passed) { 'PASS' } elseif ($check.Advisory) { 'WARN' } else { 'FAIL' }
    Write-Host "[$label] $($check.Name): $($check.Detail)"
    if (-not $check.Passed) {
        Write-Host "       Repair: $($check.Repair)"
    }
}

$failures = @($checks | Where-Object { -not $_.Passed -and -not $_.Advisory })
$advisories = @($checks | Where-Object { -not $_.Passed -and $_.Advisory })
if ($failures.Count -gt 0) {
    throw "Pegasus Doctor $Profile failed $($failures.Count) prerequisite check(s). No software was installed and no external service was contacted."
}

$advisoryNote = if ($advisories.Count -gt 0) {
    " $($advisories.Count) advisory check(s) did not pass; the lanes they gate are unavailable."
}
else {
    ''
}
Write-Host "Pegasus Doctor $Profile passed.$advisoryNote This result grants no external-operation approval."
