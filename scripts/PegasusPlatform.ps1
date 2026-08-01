# Shared platform abstraction for Pegasus repository scripts.
#
# Dot-source this file; it defines functions and performs no other work:
#   . (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')
#
# Pegasus supports one platform per workstation: Windows with PowerShell 7, or
# Linux with PowerShell 7. Every function here resolves the current platform and
# refuses an unsupported one rather than degrading silently.
#
# This file is deliberately a .ps1 rather than a .psm1 so that it remains inside
# the repository language census and documentation link validation performed by
# scripts/Test-RepositoryPolicy.ps1.

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
        if ($output -match '(?im)^\s*State:\s*(?<state>Running|Stopped)\s*$') {
            return $Matches.state
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
        [int]$Port
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

    $probe = 'exec 2>/dev/null; /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "$MSSQL_SA_PASSWORD" -C -b -l 5 -Q "SET NOCOUNT ON; SELECT 1"'
    & $Command exec $ContainerName bash -c $probe *> $null
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

function Get-PegasusOrphanedDatabaseContainer {
    <#
        .SYNOPSIS
        Lists database containers this repository created whose run is gone.

        .DESCRIPTION
        Reports only. Removing a container that was not proved to belong to the
        current run is exactly the kind of unproved destructive act these
        scripts avoid.
    #>
    param(
        [Parameter(Mandatory)]
        [string]$Command,
        [Parameter(Mandatory)]
        [string]$RepositoryRoot,
        [string[]]$KnownRunIds = @()
    )

    if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
        return @()
    }

    $output = (& $Command ps --all `
        --filter "label=com.pegasus.repositoryRoot=$RepositoryRoot" `
        --format '{{.Names}}|{{.Label "com.pegasus.runId"}}' 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) {
        return @()
    }

    $orphans = [System.Collections.Generic.List[object]]::new()
    foreach ($line in ($output -split "`n")) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        $parts = $trimmed -split '\|', 2
        if ($parts.Count -ne 2 -or $KnownRunIds -contains $parts[1]) {
            continue
        }

        $orphans.Add([pscustomobject]@{
            ContainerName = $parts[0]
            RunId = $parts[1]
            RemoveCommand = "docker rm --force --volumes $($parts[0])"
        }) | Out-Null
    }

    return @($orphans)
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
        Linux = 'sudo apt-get install --yes libnss3-tools, then dotnet dev-certs https --trust (required only for the browser evidence lane)'
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
