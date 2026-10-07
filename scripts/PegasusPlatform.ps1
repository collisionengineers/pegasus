# Shared platform abstraction for Pegasus repository scripts.
#
# Dot-source this file; it defines functions and performs no other work:
#   . (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')
#
# Pegasus supports one platform per workstation: Windows with PowerShell 7, or
# Linux with PowerShell 7. Every function here resolves the current platform and
# refuses an unsupported one rather than degrading silently.
#
# This file is deliberately a .ps1 rather than a .psm1 so callers dot-source it
# without a module path or import step.

# Deliberately no Set-StrictMode here. This file is dot-sourced, so any strict
# mode it set would apply to the whole calling script and change the behaviour
# of code it does not own.

$script:PegasusDatabaseImage =
    'mcr.microsoft.com/mssql/server@sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89'
$script:PegasusDatabaseMemoryLimitMb = 2048

# ---------------------------------------------------------------------------
# Platform
# ---------------------------------------------------------------------------

function Get-PegasusPlatform {
    <#
        .SYNOPSIS
        Resolves the supported platform, or throws on an unsupported one.
    #>
    if ($IsWindows) {
        return [pscustomobject]@{
            Kind = 'Windows'
            IsWindows = $true
            IsLinux = $false
        }
    }

    if ($IsLinux) {
        return [pscustomobject]@{
            Kind = 'Linux'
            IsWindows = $false
            IsLinux = $true
        }
    }

    $described = if ($IsMacOS) { 'macOS' } else { 'this operating system' }
    throw "Pegasus supports Windows and Linux with PowerShell 7. It does not support $described."
}

function Get-PegasusMigrationBundle {
    # The migration runs on the release workstation; deployed hosts stay Linux.
    $platform = Get-PegasusPlatform
    if ([Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne
        [Runtime.InteropServices.Architecture]::X64) {
        throw 'Release artifacts require a Windows x64 or Linux x64 workstation.'
    }

    return [pscustomobject]@{
        RuntimeIdentifier = if ($platform.IsWindows) { 'win-x64' } else { 'linux-x64' }
        Name = if ($platform.IsWindows) { 'efbundle.exe' } else { 'efbundle' }
        IsLinux = $platform.IsLinux
    }
}

function Test-PegasusArtifactManifest {
    <#
        .SYNOPSIS
        Validates a release manifest and the artifacts beside it, or throws.

        .DESCRIPTION
        Checks the schema-3 manifest identity, that exactly the Web ZIP, Worker
        ZIP and this workstation's migration bundle are present with matching
        size and SHA-256, that each ZIP has its required root entries, and that
        a Linux migration bundle is executable. Never shells out (ADR-0049: no
        image tooling is part of the release).
    #>
    param([Parameter(Mandatory)][string] $Path)

    function Assert-ZipRoot {
        param(
            [Parameter(Mandatory)][string] $ArchivePath,
            [Parameter(Mandatory)][string] $RequiredRoot
        )

        $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
        try {
            $hasRequiredRoot = @(
                $archive.Entries | Where-Object {
                    $_.FullName.StartsWith($RequiredRoot, [StringComparison]::Ordinal)
                }
            ).Count -gt 0
            if (-not $hasRequiredRoot) {
                throw "$(Split-Path -Leaf $ArchivePath) must contain $RequiredRoot at its root."
            }
        }
        finally {
            $archive.Dispose()
        }
    }

    function Assert-ZipRootFile {
        param(
            [Parameter(Mandatory)][string] $ArchivePath,
            [Parameter(Mandatory)][string] $RequiredFile
        )

        # App Service run-from-package mounts the zip root as the site root, so
        # the entry must be exactly the file name: no directory prefix.
        $archive = [IO.Compression.ZipFile]::OpenRead($ArchivePath)
        try {
            $hasRequiredFile = @(
                $archive.Entries | Where-Object {
                    [StringComparer]::Ordinal.Equals($_.FullName, $RequiredFile)
                }
            ).Count -eq 1
            if (-not $hasRequiredFile) {
                throw "$(Split-Path -Leaf $ArchivePath) must contain $RequiredFile at its root."
            }
        }
        finally {
            $archive.Dispose()
        }
    }

    $resolvedManifest = Resolve-Path -LiteralPath $Path
    $manifest = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 3) {
        throw 'The release manifest schemaVersion must be 3.'
    }
    if ($manifest.sourceRevision -notmatch '^[0-9a-f]{40}$') {
        throw 'The release manifest sourceRevision must be an exact Git SHA.'
    }
    if ($manifest.sourceStatus -ne 'clean') {
        throw 'The release manifest must record a clean source status.'
    }
    if (-not $manifest.artifacts -or $manifest.artifacts.Count -ne 3) {
        throw 'The release manifest must contain exactly the Web ZIP, Worker ZIP, and migration bundle.'
    }

    $manifestDirectory = Split-Path -Parent $resolvedManifest
    $migrationBundle = Get-PegasusMigrationBundle
    if ($manifest.migrationRuntimeIdentifier -cne $migrationBundle.RuntimeIdentifier -or
        $manifest.migrationBundleName -cne $migrationBundle.Name) {
        throw "The release manifest must carry $($migrationBundle.RuntimeIdentifier)/$($migrationBundle.Name) for this workstation."
    }
    $migrationBundleName = $migrationBundle.Name
    $requiredNames = @('web.zip', 'worker.zip', $migrationBundleName)
    foreach ($name in $requiredNames) {
        $entry = @($manifest.artifacts | Where-Object name -eq $name)
        if ($entry.Count -ne 1) {
            throw "The release manifest must contain exactly one $name entry."
        }
        $artifactPath = Join-Path $manifestDirectory $name
        if (-not (Test-Path -LiteralPath $artifactPath -PathType Leaf)) {
            throw "Release artifact is missing: $artifactPath"
        }
        $file = Get-Item -LiteralPath $artifactPath
        $hash = (Get-FileHash -LiteralPath $artifactPath -Algorithm SHA256).Hash
        if ($file.Length -ne $entry[0].sizeBytes -or $hash -ne $entry[0].sha256) {
            throw "Release artifact identity mismatch: $name"
        }
    }

    if ($migrationBundle.IsLinux) {
        $migrationBundlePath = Join-Path $manifestDirectory $migrationBundleName
        $migrationBundleMode = [IO.File]::GetUnixFileMode($migrationBundlePath)
        if (($migrationBundleMode -band [IO.UnixFileMode]::UserExecute) -eq 0) {
            throw 'The Linux x64 migration bundle must be executable by its owner.'
        }
    }

    Assert-ZipRoot -ArchivePath (Join-Path $manifestDirectory 'worker.zip') -RequiredRoot '.azurefunctions/'
    # ADR-0049: web.zip is a framework-dependent ReadyToRun publish that App Service runs
    # from package on the platform DOTNETCORE|10.0 stack; the entry assembly and
    # its runtimeconfig must sit at the zip root.
    Assert-ZipRootFile -ArchivePath (Join-Path $manifestDirectory 'web.zip') -RequiredFile 'Pegasus.Web.dll'
    Assert-ZipRootFile -ArchivePath (Join-Path $manifestDirectory 'web.zip') -RequiredFile 'Pegasus.Web.runtimeconfig.json'

    $webPackage = $manifest.PSObject.Properties['webPackage']
    if (
        $null -eq $webPackage -or
        $webPackage.Value.name -cne 'web.zip' -or
        $webPackage.Value.runtimeIdentifier -cne 'linux-x64' -or
        $webPackage.Value.selfContained -ne $false -or
        -not ($webPackage.Value.PSObject.Properties['readyToRun'] -and $webPackage.Value.readyToRun -eq $true) -or
        $webPackage.Value.hostStack -cne 'DOTNETCORE|10.0'
    ) {
        throw 'The release manifest Web package identity is incomplete or invalid.'
    }
}

function Get-PegasusWorkerDisabledSettingNames {
    <#
        .SYNOPSIS
        Returns the complete production Worker Disabled-setting census.

        .DESCRIPTION
        Bicep remains the deployment owner of these settings. Release validation
        and smoke use this one producer so their exact-name census cannot drift.
    #>
    return @(
        'AzureWebJobs.PendingWorkRecoveryFunction.Disabled',
        'AzureWebJobs.UnifiedWorkFunction.Disabled',
        'AzureWebJobs.UnifiedWorkPoisonFunction.Disabled',
        'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled',
        'AzureWebJobs.SentEvidencePollFunction.Disabled'
    )
}

function Get-PegasusPathComparison {
    <#
        .SYNOPSIS
        Returns the StringComparison that matches filesystem case semantics.

        .DESCRIPTION
        Ownership proofs compare recorded paths against observed paths. On a
        case-sensitive filesystem an ordinal-ignore-case comparison would let a
        different file satisfy the proof, so the comparison must follow the
        platform rather than being fixed.
    #>
    if ((Get-PegasusPlatform).IsWindows) {
        return [System.StringComparison]::OrdinalIgnoreCase
    }

    return [System.StringComparison]::Ordinal
}

function Get-PegasusExecutableName {
    <#
        .SYNOPSIS
        Applies the platform executable or shim suffix to a base name.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$BaseName,
        [ValidateSet('Executable', 'NodeShim')]
        [string]$Kind = 'Executable'
    )

    if (-not (Get-PegasusPlatform).IsWindows) {
        return $BaseName
    }

    if ($Kind -eq 'NodeShim') {
        return "$BaseName.cmd"
    }

    return "$BaseName.exe"
}

# ---------------------------------------------------------------------------
# Native interop (Linux only)
# ---------------------------------------------------------------------------

function Initialize-PegasusPosixInterop {
    if ('Pegasus.Posix' -as [type]) {
        return
    }

    Add-Type -Namespace 'Pegasus' -Name 'Posix' -MemberDefinition @'
[DllImport("libc", SetLastError = true)]
public static extern int kill(int pid, int sig);

[DllImport("libc", SetLastError = true)]
public static extern int setpgid(int pid, int pgid);
'@
}

function Get-PegasusProcessGroupPreamble {
    <#
        .SYNOPSIS
        Returns launcher preamble that places a launched process in its own
        process group, or an empty string on Windows.

        .DESCRIPTION
        On Linux a process whose parent exits is reparented to init or to a
        subreaper, which removes it from any parent-chain closure. Placing the
        launcher in its own process group makes every descendant inherit that
        group, so the group identifies exactly the processes this repository
        started. POSIX only permits a process to join a group via itself or its
        own children within the same session, so an unrelated process cannot
        enter the group.
    #>
    if ((Get-PegasusPlatform).IsWindows) {
        return ''
    }

    return @'
Add-Type -Namespace 'PegasusLauncher' -Name 'Posix' -MemberDefinition @"
[DllImport("libc", SetLastError = true)] public static extern int setsid();
[DllImport("libc", SetLastError = true)] public static extern int setpgid(int pid, int pgid);
"@
# Start a new session. This detaches from the controlling terminal, so the
# hangup raised when the starting command exits cannot reach these processes,
# and it makes this process its own process group leader with a group
# identifier equal to its process identifier. Ownership and termination both
# rely on that group. Signal dispositions are not a usable alternative here
# because the runtime resets them when it starts a child.
if ([PegasusLauncher.Posix]::setsid() -lt 0) {
    # Already a process group leader, so a session cannot be created. The
    # process group is what ownership needs, and it already holds.
    if ([PegasusLauncher.Posix]::setpgid(0, 0) -ne 0) {
        throw 'Failed to create a process group for the owned Pegasus process.'
    }
}
'@
}

# ---------------------------------------------------------------------------
# Processes
# ---------------------------------------------------------------------------

function Get-PegasusProcessSnapshot {
    <#
        .SYNOPSIS
        Returns ProcessId, ParentProcessId and ProcessGroupId for live processes.

        .DESCRIPTION
        ProcessGroupId is always 0 on Windows, which has no process groups in
        this sense; callers must not use it there.
    #>
    if ((Get-PegasusPlatform).IsWindows) {
        return @(
            Get-CimInstance -ClassName Win32_Process -ErrorAction Stop |
                ForEach-Object {
                    [pscustomobject]@{
                        ProcessId = [int]$_.ProcessId
                        ParentProcessId = [int]$_.ParentProcessId
                        ProcessGroupId = 0
                    }
                }
        )
    }

    $snapshot = [System.Collections.Generic.List[object]]::new()
    foreach ($directory in [System.IO.Directory]::EnumerateDirectories('/proc')) {
        $name = [System.IO.Path]::GetFileName($directory)
        $processId = 0
        if (-not [int]::TryParse($name, [ref]$processId)) {
            continue
        }

        try {
            $stat = [System.IO.File]::ReadAllText("/proc/$processId/stat")
        }
        catch {
            # The process exited between enumeration and read.
            continue
        }

        # Field 2 is the executable name in parentheses and may itself contain
        # spaces or a closing parenthesis, so parse from the LAST ')' rather
        # than splitting the whole line on spaces.
        $commEnd = $stat.LastIndexOf(')')
        if ($commEnd -lt 0 -or ($commEnd + 2) -ge $stat.Length) {
            continue
        }

        $fields = $stat.Substring($commEnd + 2) -split ' '
        if ($fields.Count -lt 3) {
            continue
        }

        $parentProcessId = 0
        $processGroupId = 0
        if (-not [int]::TryParse($fields[1], [ref]$parentProcessId)) { continue }
        if (-not [int]::TryParse($fields[2], [ref]$processGroupId)) { continue }

        $snapshot.Add([pscustomobject]@{
            ProcessId = $processId
            ParentProcessId = $parentProcessId
            ProcessGroupId = $processGroupId
        })
    }

    return @($snapshot)
}

function Get-PegasusProcessCommandLine {
    <#
        .SYNOPSIS
        Returns the full command line of a live process, or $null.
    #>
    param([Parameter(Mandatory)][int]$ProcessId)

    if ((Get-PegasusPlatform).IsWindows) {
        $nativeProcess = Get-CimInstance `
            -ClassName Win32_Process `
            -Filter "ProcessId = $ProcessId" `
            -ErrorAction Stop
        if ($null -eq $nativeProcess) {
            return $null
        }

        return [string]$nativeProcess.CommandLine
    }

    $bytes = [System.IO.File]::ReadAllBytes("/proc/$ProcessId/cmdline")
    if ($bytes.Length -eq 0) {
        return $null
    }

    $arguments = [System.Text.Encoding]::UTF8.GetString($bytes) -split "`0" |
        Where-Object { -not [string]::IsNullOrEmpty($_) }
    return ($arguments -join ' ')
}

function Test-PegasusProcessStartTimeMatch {
    <#
        .SYNOPSIS
        Compares a recorded process start time with an observed one.

        .DESCRIPTION
        The comparison exists to defeat process-identifier reuse: a recycled
        identifier belongs to a process that started at a different time.

        On Windows the value is exact and is compared exactly. On Linux
        Process.StartTime is derived from the kernel boot time, which .NET
        re-estimates on each read, so two readers can observe the same process
        with times differing by microseconds. Requiring exact equality there
        would reject genuinely owned processes. A one-second tolerance keeps the
        reuse check meaningful while tolerating that estimate.
    #>
    param(
        [Parameter(Mandatory)][AllowEmptyString()][string]$Recorded,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Actual
    )

    if ($Recorded -eq $Actual) {
        return $true
    }

    if ((Get-PegasusPlatform).IsWindows) {
        return $false
    }

    $recordedTime = [datetime]::MinValue
    $actualTime = [datetime]::MinValue
    $styles = [System.Globalization.DateTimeStyles]::RoundtripKind
    if (-not [datetime]::TryParse(
            $Recorded, [cultureinfo]::InvariantCulture, $styles, [ref]$recordedTime) -or
        -not [datetime]::TryParse(
            $Actual, [cultureinfo]::InvariantCulture, $styles, [ref]$actualTime)) {
        return $false
    }

    return [Math]::Abs(
        ($recordedTime.ToUniversalTime() - $actualTime.ToUniversalTime()).TotalSeconds) -le 1
}

function Test-PegasusProcessTreeAlive {
    <#
        .SYNOPSIS
        Returns $true while the root process or any member of its process group
        is still running.
    #>
    param([Parameter(Mandatory)][int]$RootProcessId)

    if ($null -ne (Get-Process -Id $RootProcessId -ErrorAction SilentlyContinue)) {
        return $true
    }

    if ((Get-PegasusPlatform).IsWindows) {
        return $false
    }

    return @(
        Get-PegasusProcessSnapshot | Where-Object { $_.ProcessGroupId -eq $RootProcessId }
    ).Count -gt 0
}

function Stop-PegasusProcessTree {
    <#
        .SYNOPSIS
        Stops a proved-owned process and every process it started.

        .DESCRIPTION
        On Windows this walks the transitive ParentProcessId closure and stops
        leaves before their parents. On Linux it signals the process group
        created by the launcher preamble, which also reaches descendants that
        were reparented away from the closure when an intermediate process
        exited.

        The caller is responsible for proving ownership of RootProcessId before
        calling this function.
    #>
    param(
        [Parameter(Mandatory)]
        [int]$RootProcessId,
        [int]$TimeoutSeconds = 10
    )

    $platform = Get-PegasusPlatform
    $snapshot = Get-PegasusProcessSnapshot
    $warnings = [System.Collections.Generic.List[string]]::new()

    $closure = [System.Collections.Generic.HashSet[int]]::new()
    $closure.Add($RootProcessId) | Out-Null
    $added = $true
    while ($added) {
        $added = $false
        foreach ($entry in $snapshot) {
            if (-not $closure.Contains($entry.ProcessId) -and
                $closure.Contains($entry.ParentProcessId)) {
                $closure.Add($entry.ProcessId) | Out-Null
                $added = $true
            }
        }
    }

    if ($platform.IsWindows) {
        $remaining = [System.Collections.Generic.HashSet[int]]::new()
        foreach ($childProcessId in @($closure | Where-Object { $_ -ne $RootProcessId })) {
            $remaining.Add($childProcessId) | Out-Null
        }

        while ($remaining.Count -gt 0) {
            $leaves = @(
                $remaining | Where-Object {
                    $candidateParentId = $_
                    @($snapshot | Where-Object {
                        $remaining.Contains($_.ProcessId) -and
                        $_.ParentProcessId -eq $candidateParentId
                    }).Count -eq 0
                }
            )
            if ($leaves.Count -eq 0) {
                $leaves = @($remaining)
            }
            foreach ($childProcessId in $leaves) {
                Stop-Process -Id $childProcessId -Force -ErrorAction SilentlyContinue
                $remaining.Remove($childProcessId) | Out-Null
            }
        }

        Stop-Process -Id $RootProcessId -Force -ErrorAction SilentlyContinue
    }
    else {
        Initialize-PegasusPosixInterop
        $groupMembers = @($snapshot | Where-Object { $_.ProcessGroupId -eq $RootProcessId })

        # The launcher preamble calls setpgid(0, 0), so the group identifier is
        # the root process identifier by construction. A live group therefore
        # proves ownership even after the root itself has exited, which is the
        # case that matters: an intermediate process can exit and leave its
        # children reparented but still inside the group.
        $useProcessGroup = $groupMembers.Count -gt 0

        if ($useProcessGroup) {
            # Membership of the group is the ownership proof: the kernel only
            # admits this process and its descendants. Members outside the
            # parent-chain closure are expected whenever an intermediate process
            # exited and its children were reparented, so record them rather
            # than refusing.
            foreach ($member in $groupMembers) {
                if (-not $closure.Contains($member.ProcessId)) {
                    $warnings.Add(
                        "PID $($member.ProcessId) was reparented away from the owned tree and was reaped by process group $RootProcessId.") |
                        Out-Null
                }
            }

            [Pegasus.Posix]::kill(-$RootProcessId, 15) | Out-Null
        }
        else {
            # No process carries the group, so the launcher preamble did not
            # take effect. Fall back to the parent chain, which is weaker
            # because it cannot see reparented descendants, and say so.
            $warnings.Add(
                "No process group was found for PID $RootProcessId; stopped using the parent-chain closure only.") |
                Out-Null
            foreach ($memberProcessId in @($closure | Where-Object { $_ -ne $RootProcessId })) {
                [Pegasus.Posix]::kill($memberProcessId, 15) | Out-Null
            }
            [Pegasus.Posix]::kill($RootProcessId, 15) | Out-Null
        }

        # Wait for both the root and every remaining group member to exit, then
        # escalate. Waiting only on the root would report success while a
        # reparented descendant still held the run's ports.
        $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
        while ([DateTimeOffset]::UtcNow -lt $deadline -and
            (Test-PegasusProcessTreeAlive -RootProcessId $RootProcessId)) {
            Start-Sleep -Milliseconds 100
        }

        if (Test-PegasusProcessTreeAlive -RootProcessId $RootProcessId) {
            if ($useProcessGroup) {
                [Pegasus.Posix]::kill(-$RootProcessId, 9) | Out-Null
            }
            else {
                [Pegasus.Posix]::kill($RootProcessId, 9) | Out-Null
            }
        }
    }

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline -and
        (Test-PegasusProcessTreeAlive -RootProcessId $RootProcessId)) {
        Start-Sleep -Milliseconds 100
    }

    $residual = @()
    if (-not $platform.IsWindows) {
        $residual = @(
            Get-PegasusProcessSnapshot |
                Where-Object { $_.ProcessGroupId -eq $RootProcessId } |
                ForEach-Object { $_.ProcessId }
        )
    }

    return [pscustomobject]@{
        Stopped = ($null -eq (Get-Process -Id $RootProcessId -ErrorAction SilentlyContinue)) -and
            $residual.Count -eq 0
        ResidualProcessIds = $residual
        Warnings = @($warnings)
    }
}

# ---------------------------------------------------------------------------
# Local database engine
# ---------------------------------------------------------------------------

function Get-PegasusDatabaseEngineKind {
    if ((Get-PegasusPlatform).IsWindows) {
        return 'LocalDb'
    }

    return 'DockerSqlServer'
}

function Get-PegasusDatabaseImageReference {
    return $script:PegasusDatabaseImage
}

function Get-PegasusDatabaseCommandName {
    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        return 'sqllocaldb'
    }

    return 'docker'
}

function Get-PegasusDatabaseContainerName {
    param([Parameter(Mandatory)][string]$RunId)

    return "pegasus-localdev-$RunId"
}

function New-PegasusDatabasePassword {
    <#
        .SYNOPSIS
        Generates a SQL Server password meeting complexity requirements.

        .DESCRIPTION
        Excludes quotes, semicolons, backslashes, dollar signs and backticks so
        the value is safe in both connection strings and environment files.
    #>
    $upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ'
    $lower = 'abcdefghijkmnopqrstuvwxyz'
    $digit = '23456789'
    $symbol = '!#%&*+-.:=?@^_~'
    $alphabet = ($upper + $lower + $digit + $symbol).ToCharArray()

    while ($true) {
        $buffer = [byte[]]::new(40)
        [System.Security.Cryptography.RandomNumberGenerator]::Fill($buffer)
        $candidate = -join ($buffer | ForEach-Object { $alphabet[$_ % $alphabet.Length] })

        if ($candidate.IndexOfAny($upper.ToCharArray()) -ge 0 -and
            $candidate.IndexOfAny($lower.ToCharArray()) -ge 0 -and
            $candidate.IndexOfAny($digit.ToCharArray()) -ge 0 -and
            $candidate.IndexOfAny($symbol.ToCharArray()) -ge 0) {
            return $candidate
        }
    }
}

function Write-PegasusDatabaseSecretFile {
    <#
        .SYNOPSIS
        Writes the container environment file readable only by its owner.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Password
    )

    $content = @(
        'ACCEPT_EULA=Y',
        'MSSQL_PID=Developer',
        "MSSQL_SA_PASSWORD=$Password"
    ) -join "`n"

    [System.IO.File]::WriteAllText($Path, $content + "`n")
    if (-not (Get-PegasusPlatform).IsWindows) {
        [System.IO.File]::SetUnixFileMode(
            $Path,
            [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite)
    }
}

function Read-PegasusDatabaseSecretFile {
    param([Parameter(Mandatory)][string]$Path)

    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ($line.StartsWith('MSSQL_SA_PASSWORD=')) {
            return $line.Substring('MSSQL_SA_PASSWORD='.Length)
        }
    }

    throw "Database secret file '$Path' does not contain MSSQL_SA_PASSWORD."
}

function Get-PegasusDatabaseState {
    <#
        .SYNOPSIS
        Returns Missing, Stopped, Running or Unknown for the local instance.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$InstanceName,
        [Parameter(Mandatory)]
        [string]$Command,
        [string]$ContainerName
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        $output = (& $Command info $InstanceName 2>&1 | Out-String)
        if ($LASTEXITCODE -ne 0) {
            return 'Missing'
        }

        $state = [regex]::Match(
            $output,
            '^\s*State:\s*(?<state>Running|Stopped)\s*$',
            [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
                [System.Text.RegularExpressions.RegexOptions]::Multiline)
        $missingPattern = '(?im)^[ \t]*LocalDB[ \t]+instance[ \t]+"' +
            [regex]::Escape($InstanceName) +
            '"[ \t]+doesn''t[ \t]+exist![ \t]*\r?$'
        $isExplicitlyMissing = [regex]::IsMatch($output, $missingPattern)

        if ($state.Success -and -not $isExplicitlyMissing) {
            return $state.Groups['state'].Value
        }
        if ($isExplicitlyMissing -and -not $state.Success) {
            return 'Missing'
        }
        return 'Unknown'
    }

    $status = (& $Command inspect -f '{{.State.Status}}' $ContainerName 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        return 'Missing'
    }

    switch ($status) {
        'running' { return 'Running' }
        'created' { return 'Stopped' }
        'exited' { return 'Stopped' }
        'paused' { return 'Stopped' }
        default { return 'Unknown' }
    }
}

function Assert-PegasusDatabaseContainerOwnership {
    <#
        .SYNOPSIS
        Refuses to act on a container that this repository did not create.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [Parameter(Mandatory)]
        [string]$ContainerName,
        [Parameter(Mandatory)]
        [string]$RunId
    )

    $label = (& $Command inspect -f '{{index .Config.Labels "com.pegasus.runId"}}' $ContainerName 2>&1 |
        Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        return
    }

    if ($label -ne $RunId) {
        throw "Container '$ContainerName' is not owned by run $RunId (found '$label'). Refusing to act on it."
    }
}

function Invoke-PegasusDatabaseCommand {
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [Parameter(Mandatory)]
        [string[]]$Arguments,
        [Parameter(Mandatory)]
        [string]$Description
    )

    & $Command @Arguments *> $null
    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE."
    }
}

function New-PegasusDatabaseInstance {
    param(
        [Parameter(Mandatory)]
        [string]$InstanceName,
        [Parameter(Mandatory)]
        [string]$Command,
        [string]$ContainerName,
        [string]$RunId,
        [string]$RepositoryRoot,
        [string]$SecretPath,
        [int]$Port
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        Invoke-PegasusDatabaseCommand `
            -Command $Command `
            -Arguments @('create', $InstanceName) `
            -Description "Creating LocalDB instance $InstanceName"
        return
    }

    # Bind to loopback explicitly. A bare published port listens on every
    # interface, and container publishing bypasses host firewall rules.
    Invoke-PegasusDatabaseCommand `
        -Command $Command `
        -Arguments @(
            'create',
            '--name', $ContainerName,
            '--publish', "127.0.0.1:${Port}:1433",
            '--env-file', $SecretPath,
            '--env', "MSSQL_MEMORY_LIMIT_MB=$script:PegasusDatabaseMemoryLimitMb",
            '--label', "com.pegasus.runId=$RunId",
            '--label', "com.pegasus.repositoryRoot=$RepositoryRoot",
            (Get-PegasusDatabaseImageReference)
        ) `
        -Description "Creating database container $ContainerName"
}

function Start-PegasusDatabaseInstance {
    param(
        [Parameter(Mandatory)]
        [string]$InstanceName,
        [Parameter(Mandatory)]
        [string]$Command,
        [string]$ContainerName,
        [string]$RunId
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        Invoke-PegasusDatabaseCommand `
            -Command $Command `
            -Arguments @('start', $InstanceName) `
            -Description "Starting LocalDB instance $InstanceName"
        return
    }

    Assert-PegasusDatabaseContainerOwnership `
        -Command $Command -ContainerName $ContainerName -RunId $RunId
    Invoke-PegasusDatabaseCommand `
        -Command $Command `
        -Arguments @('start', $ContainerName) `
        -Description "Starting database container $ContainerName"
}

function Stop-PegasusDatabaseInstance {
    param(
        [Parameter(Mandatory)]
        [string]$InstanceName,
        [Parameter(Mandatory)]
        [string]$Command,
        [string]$ContainerName,
        [string]$RunId
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        Invoke-PegasusDatabaseCommand `
            -Command $Command `
            -Arguments @('stop', $InstanceName, '-k') `
            -Description "Stopping LocalDB instance $InstanceName"
        return
    }

    Assert-PegasusDatabaseContainerOwnership `
        -Command $Command -ContainerName $ContainerName -RunId $RunId
    Invoke-PegasusDatabaseCommand `
        -Command $Command `
        -Arguments @('stop', '--time', '10', $ContainerName) `
        -Description "Stopping database container $ContainerName"
}

function Remove-PegasusDatabaseInstance {
    param(
        [Parameter(Mandatory)]
        [string]$InstanceName,
        [Parameter(Mandatory)]
        [string]$Command,
        [string]$ContainerName,
        [string]$RunId
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        Invoke-PegasusDatabaseCommand `
            -Command $Command `
            -Arguments @('delete', $InstanceName) `
            -Description "Deleting LocalDB instance $InstanceName"
        return
    }

    Assert-PegasusDatabaseContainerOwnership `
        -Command $Command -ContainerName $ContainerName -RunId $RunId
    # Removing the container discards its writable layer, which is what deletes
    # the databases. This mirrors 'sqllocaldb delete'.
    Invoke-PegasusDatabaseCommand `
        -Command $Command `
        -Arguments @('rm', '--force', '--volumes', $ContainerName) `
        -Description "Removing database container $ContainerName"
}

function Test-PegasusDatabaseReady {
    <#
        .SYNOPSIS
        Returns $true when the local database accepts an authenticated query.

        .DESCRIPTION
        LocalDB start is synchronous, so it reports ready immediately. The
        container is not, so this performs a real login. The password is read
        from the container's own environment inside the container, so it never
        appears in a host command line.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [string]$ContainerName,
        [int]$Port,
        # When a run database already exists, ready means that database itself
        # accepts a login: after a container restart the engine answers on
        # master, and even reports the database ONLINE, a moment before it
        # admits connections to it. A migration that cannot open it in that
        # window concludes it is absent and tries to create it over itself.
        [string]$Database
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        return $true
    }

    $running = (& $Command inspect -f '{{.State.Running}}' $ContainerName 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $running -ne 'true') {
        return $false
    }

    try {
        $client = [System.Net.Sockets.TcpClient]::new()
        $connect = $client.ConnectAsync('127.0.0.1', $Port)
        if (-not $connect.Wait(1000)) {
            return $false
        }
    }
    catch {
        return $false
    }
    finally {
        if ($null -ne $client) { $client.Dispose() }
    }

    # Ready means the engine answers and no run database is still recovering:
    # after a container restart SQL Server accepts connections to master while
    # it brings user databases back, and a migration that cannot open the run
    # database in that window would try to create it over the existing one.
    $probe = 'exec 2>/dev/null; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -l 5 -Q "SET NOCOUNT ON; IF EXISTS (SELECT 1 FROM sys.databases WHERE name LIKE N''PegasusDevelopment[_]%'' AND state_desc <> N''ONLINE'') RAISERROR(N''A run database is still recovering.'', 16, 1); SELECT 1"'
    & $Command exec $ContainerName bash -c $probe *> $null
    if ($LASTEXITCODE -ne 0) {
        return $false
    }
    if ([string]::IsNullOrWhiteSpace($Database)) {
        return $true
    }
    if ($Database -notmatch '^PegasusDevelopment_[0-9a-f]{32}$') {
        throw "Unexpected run database name '$Database'."
    }

    # A run whose database the migration has not created yet is ready once the
    # engine answers; one whose database exists is ready only when that
    # database admits a login.
    $existsProbe = 'exec 2>/dev/null; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -l 5 -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_ID(N''' + $Database + ''') IS NULL THEN 0 ELSE 1 END"'
    $exists = (& $Command exec $ContainerName bash -c $existsProbe 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0) {
        return $false
    }
    if ($exists -eq '0') {
        return $true
    }

    $databaseProbe = 'exec 2>/dev/null; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -l 5 -d "' + $Database + '" -Q "SET NOCOUNT ON; SELECT 1"'
    & $Command exec $ContainerName bash -c $databaseProbe *> $null
    return $LASTEXITCODE -eq 0
}

function Get-PegasusDatabaseDiagnostics {
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [string]$ContainerName,
        [int]$TailLines = 50
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        return ''
    }

    $exitCode = (& $Command inspect -f '{{.State.ExitCode}}' $ContainerName 2>&1 | Out-String).Trim()
    $logs = (& $Command logs --tail $TailLines $ContainerName 2>&1 | Out-String).Trim()
    return "container exit code: $exitCode`n$logs"
}

function Get-PegasusDatabaseConnectionString {
    param(
        [Parameter(Mandatory)]
        [string]$InstanceName,
        [Parameter(Mandatory)]
        [string]$DatabaseName,
        [int]$Port,
        [string]$Password
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        return "Server=(localdb)\$InstanceName;Database=$DatabaseName;Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True"
    }

    # The container presents a self-signed certificate, so the connection is
    # encrypted but the certificate is not validated.
    return "Server=127.0.0.1,$Port;Database=$DatabaseName;User ID=sa;Password=$Password;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True"
}

# ---------------------------------------------------------------------------
# Local live-integration settings
# ---------------------------------------------------------------------------
#
# An operator-supplied, ignored KEY=VALUE file that opts a DevelopmentOffline
# run into real vendor integrations (live DVLA/DVSA, Box custody, Glass's) and
# password sign-in. Its values reach only the Web and Worker process
# environments; the run manifest records the derived boolean flags and never
# a value. The lifecycle owns the keys named in Get-PegasusReservedLocalSettingKeys.

$script:PegasusLocalSettingsFileName = 'local.settings.env'

$script:PegasusLocalSettingsHostPrefixes = @{
    Web = @('Features__', 'Box__', 'Glass__', 'GitHub__', 'DevelopmentOffline__', 'AutomationMcp__')
    Worker = @('Features__', 'Box__', 'Dvla__', 'Dvsa__')
}

$script:PegasusLiveIntegrationFlagKeys = [ordered]@{
    vehicleLookup = 'Features__LiveVehicleLookup'
    boxCustody = 'Features__LiveBoxCustody'
    glass = 'Features__LiveGlass'
    passwordSignIn = 'Features__PasswordSignIn'
    automationMcp = 'Features__AutomationMcp'
    principalApi = 'Features__PrincipalApi'
}

function Get-PegasusLocalSettingsPath {
    <#
        .SYNOPSIS
        The settings file's owned location beneath the ignored local-development root.
    #>
    param([Parameter(Mandatory)][string]$LocalDevelopmentRoot)

    return Join-Path $LocalDevelopmentRoot $script:PegasusLocalSettingsFileName
}

function Get-PegasusReservedLocalSettingKeys {
    <#
        .SYNOPSIS
        Environment names the lifecycle decides itself; a settings file may not set them.
    #>
    return @(
        'Runtime__Profile',
        'ConnectionStrings__Pegasus',
        'ASPNETCORE_URLS',
        'ASPNETCORE_ENVIRONMENT',
        'DOTNET_ENVIRONMENT',
        'AZURE_FUNCTIONS_ENVIRONMENT',
        'FUNCTIONS_WORKER_RUNTIME',
        'AzureWebJobsStorage',
        'IntakeStorage__ConnectionString',
        'Intake__LocalArtifactPath',
        'Features__LocalIntake',
        'Features__LocalDocumentCustody',
        'Glass__CallbackBaseUri',
        'PendingWorkRecoverySchedule',
        'IntakeStagedArtifactReconciliationSchedule',
        'SentEvidencePollSchedule'
    )
}

function Test-PegasusReservedLocalSettingKey {
    param([Parameter(Mandatory)][string]$Key)

    if ($Key -in (Get-PegasusReservedLocalSettingKeys)) {
        return $true
    }
    return $Key.StartsWith('ApprovedInbox__', [System.StringComparison]::Ordinal) -or
        $Key.StartsWith('ApprovedSent__', [System.StringComparison]::Ordinal)
}

function Assert-PegasusOwnerOnlyFile {
    <#
        .SYNOPSIS
        On Linux, refuses a file that any account other than its owner can read or write.
    #>
    param([Parameter(Mandatory)][string]$Path)

    if ((Get-PegasusPlatform).IsWindows) {
        return
    }

    $mode = [System.IO.File]::GetUnixFileMode($Path)
    $shared = [System.IO.UnixFileMode]::GroupRead -bor [System.IO.UnixFileMode]::GroupWrite -bor
        [System.IO.UnixFileMode]::GroupExecute -bor [System.IO.UnixFileMode]::OtherRead -bor
        [System.IO.UnixFileMode]::OtherWrite -bor [System.IO.UnixFileMode]::OtherExecute
    if (($mode -band $shared) -ne 0) {
        throw "The local settings file must be readable only by its owner (chmod 600): $Path"
    }
}

function Read-PegasusLocalSettingsFile {
    <#
        .SYNOPSIS
        Parses the KEY=VALUE settings file into an ordered hashtable.

        .DESCRIPTION
        Blank lines and lines starting with '#' are ignored. The first '=' splits
        a line; the value is taken verbatim after it (no quoting, no escaping),
        so a Box configuration JSON travels as one compact line. An absent file
        yields an empty table. Every key must be a plain environment name, be
        unique, match a host allowlist and not be a lifecycle-owned key.
    #>
    param([Parameter(Mandatory)][string]$Path)

    $settings = [ordered]@{}
    if (-not [System.IO.File]::Exists($Path)) {
        return $settings
    }

    Assert-PegasusOwnerOnlyFile -Path $Path
    $allowedPrefixes = @(
        $script:PegasusLocalSettingsHostPrefixes.Web + $script:PegasusLocalSettingsHostPrefixes.Worker |
            Select-Object -Unique
    )
    $lineNumber = 0
    foreach ($rawLine in [System.IO.File]::ReadAllLines($Path)) {
        $lineNumber++
        $line = $rawLine.TrimEnd("`r")
        if ([string]::IsNullOrWhiteSpace($line) -or $line.TrimStart().StartsWith('#')) {
            continue
        }

        $separator = $line.IndexOf('=')
        if ($separator -le 0) {
            throw "Local settings line $lineNumber is not KEY=VALUE."
        }

        $key = $line.Substring(0, $separator).Trim()
        $value = $line.Substring($separator + 1)
        if ($key -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
            throw "Local settings line $lineNumber has an invalid key '$key'."
        }
        if ($settings.Contains($key)) {
            throw "Local settings key '$key' is set more than once."
        }
        if (Test-PegasusReservedLocalSettingKey -Key $key) {
            throw "Local settings key '$key' is owned by the run lifecycle and cannot be supplied."
        }
        $prefixed = @($allowedPrefixes | Where-Object { $key.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
        if (-not $prefixed) {
            throw "Local settings key '$key' is not a recognised Web or Worker setting prefix."
        }

        $settings[$key] = $value
    }

    return $settings
}

function Split-PegasusLocalSettings {
    <#
        .SYNOPSIS
        The subset of the settings one host receives, by its prefix allowlist.
    #>
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [System.Collections.IDictionary]$Settings,
        [Parameter(Mandatory)]
        [ValidateSet('Web', 'Worker')]
        [string]$HostKind
    )

    $prefixes = $script:PegasusLocalSettingsHostPrefixes[$HostKind]
    $subset = [ordered]@{}
    foreach ($key in $Settings.Keys) {
        if (Test-PegasusReservedLocalSettingKey -Key $key) {
            throw "Local settings key '$key' is owned by the run lifecycle and cannot be supplied."
        }
        $matched = @($prefixes | Where-Object { $key.StartsWith($_, [System.StringComparison]::Ordinal) }).Count -gt 0
        if ($matched) {
            $subset[$key] = [string]$Settings[$key]
        }
    }
    return $subset
}

function Get-PegasusLiveIntegrationFlags {
    <#
        .SYNOPSIS
        The boolean opt-ins a settings file expresses, by name only. Never a value.
    #>
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [System.Collections.IDictionary]$Settings
    )

    $flags = [ordered]@{}
    foreach ($name in $script:PegasusLiveIntegrationFlagKeys.Keys) {
        $key = $script:PegasusLiveIntegrationFlagKeys[$name]
        $flags[$name] = $Settings.Contains($key) -and
            ([string]$Settings[$key]).Trim().Equals('true', [System.StringComparison]::OrdinalIgnoreCase)
    }
    $flags['problemReports'] = $Settings.Contains('GitHub__ProblemReports__Token') -and
        -not [string]::IsNullOrWhiteSpace([string]$Settings['GitHub__ProblemReports__Token'])
    return $flags
}

function Format-PegasusLiveIntegrationFlags {
    <#
        .SYNOPSIS
        'none', or the comma-joined names of the flags that are on.
    #>
    param([AllowNull()][object]$Flags)

    if ($null -eq $Flags) {
        return 'none'
    }
    $names = [System.Collections.Generic.List[string]]::new()
    if ($Flags -is [System.Collections.IDictionary]) {
        foreach ($name in $Flags.Keys) {
            if ([bool]$Flags[$name]) { $names.Add([string]$name) }
        }
    }
    else {
        foreach ($property in $Flags.PSObject.Properties) {
            if ([bool]$property.Value) { $names.Add([string]$property.Name) }
        }
    }
    if ($names.Count -eq 0) {
        return 'none'
    }
    return ($names -join ',')
}

# ---------------------------------------------------------------------------
# Repair hints
# ---------------------------------------------------------------------------

$script:PegasusRepairHints = @{
    'powershell' = @{
        Windows = 'winget install --exact --id Microsoft.PowerShell --version 7.6.3 --scope user'
        Linux = 'Install PowerShell 7.6.3 or later from https://github.com/PowerShell/PowerShell/releases'
    }
    'git' = @{
        Windows = 'winget install --exact --id Git.Git --scope user'
        Linux = 'sudo apt-get install --yes git'
    }
    'dotnet-sdk' = @{
        Windows = 'winget install --exact --id Microsoft.DotNet.SDK.10 --version 10.0.302 --scope user'
        Linux = 'curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --version 10.0.302 --install-dir "$HOME/.dotnet"; then export DOTNET_ROOT="$HOME/.dotnet" and add it to PATH'
    }
    'node' = @{
        Windows = 'winget install --exact --id OpenJS.NodeJS --version 24.0.0 --scope user'
        Linux = 'Install Node.js 24 with nvm: nvm install 24'
    }
    'npm' = @{
        Windows = 'npm install --global npm@11'
        Linux = 'npm install --global npm@11'
    }
    'python' = @{
        Windows = 'winget install --exact --id Python.Python.3.14 --scope user'
        Linux = 'sudo apt-get install --yes python3'
    }
    'func' = @{
        Windows = 'winget install --exact --id Microsoft.Azure.FunctionsCoreTools --version 4.12.1 --scope user'
        Linux = 'npm install --global azure-functions-core-tools@4'
    }
    'database-engine' = @{
        Windows = 'winget install --exact --id Microsoft.SQLServer.2022.Express --override "/ACTION=Install /QUIET /IACCEPTSQLSERVERLICENSETERMS /FEATURES=LocalDB"'
        Linux = "docker pull $script:PegasusDatabaseImage"
    }
    'container-runtime' = @{
        Windows = 'Install Docker Desktop and select Linux containers.'
        Linux = 'sudo apt-get install --yes docker.io && sudo usermod --append --groups docker "$USER" (log out and back in)'
    }
    'module-sqlserver' = @{
        Windows = 'Install-Module SqlServer -Scope CurrentUser -RequiredVersion 22.4.5.1 -Force -AllowClobber -Repository PSGallery'
        Linux = 'Install-Module SqlServer -Scope CurrentUser -RequiredVersion 22.4.5.1 -Force -AllowClobber -Repository PSGallery'
    }
    'module-exchange' = @{
        Windows = 'Install-Module ExchangeOnlineManagement -Scope CurrentUser -RequiredVersion 3.10.0 -Force -AllowClobber -Repository PSGallery'
        Linux = 'Install-Module ExchangeOnlineManagement -Scope CurrentUser -RequiredVersion 3.10.0 -Force -AllowClobber -Repository PSGallery'
    }
    'dev-certs' = @{
        Windows = 'dotnet dev-certs https --trust'
        Linux = 'dotnet dev-certs https'
    }
    'dev-certs-trust' = @{
        Windows = 'dotnet dev-certs https --trust'
        Linux = 'sudo apt-get install --yes libnss3-tools, then dotnet dev-certs https --trust (required for interactive browser clients)'
    }
    'az' = @{
        Windows = 'winget install --exact --id Microsoft.AzureCLI --version 2.88.0 --scope user'
        Linux = 'curl -sSL https://aka.ms/InstallAzureCLIDeb | sudo bash'
    }
    'azd' = @{
        Windows = 'winget install --exact --id Microsoft.Azd --version 1.28.0 --scope user'
        Linux = 'curl -fsSL https://aka.ms/install-azd.sh | sudo bash'
    }
    'bicep' = @{
        Windows = 'winget install --exact --id Microsoft.Bicep --version 0.45.15 --scope user'
        Linux = 'az bicep install'
    }
    'gh' = @{
        Windows = 'winget install --exact --id GitHub.cli --version 2.88.0 --scope user'
        Linux = 'sudo apt-get install --yes gh'
    }
    'infisical' = @{
        Windows = 'winget install --exact --id Infisical.cli --version 0.43.104 --scope user'
        Linux = 'npm install --global @infisical/cli@0.43.104'
    }
    'box' = @{
        Windows = 'npm install --global @box/cli@4.9.2'
        Linux = 'npm install --global @box/cli@4.9.2'
    }
    'sqlcmd' = @{
        Windows = 'winget install --exact --id Microsoft.Sqlcmd --version 1.10.0 --scope user'
        Linux = 'Download go-sqlcmd 1.10.0 from https://github.com/microsoft/go-sqlcmd/releases and place sqlcmd on PATH'
    }
    'platform' = @{
        Windows = 'Use the approved workstation-administration route to update this workstation to Windows 11.'
        Linux = 'Use a supported Linux distribution with PowerShell 7 and a reachable Docker daemon.'
    }
}

function Get-PegasusRepairHint {
    <#
        .SYNOPSIS
        Returns the platform-appropriate repair instruction for a check.
    #>
    param([Parameter(Mandatory)][string]$Id)

    if (-not $script:PegasusRepairHints.ContainsKey($Id)) {
        throw "No repair hint is defined for '$Id'."
    }

    return [string]$script:PegasusRepairHints[$Id][(Get-PegasusPlatform).Kind]
}
