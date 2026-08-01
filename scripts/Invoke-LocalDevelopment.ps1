[CmdletBinding()]
param(
    [ValidateSet('Start', 'Status', 'Smoke', 'Stop', 'Reset')]
    [string]$Action = 'Status',
    [ValidatePattern('^[0-9a-f]{32}$')]
    [string]$RunId,
    [ValidateSet('None', 'AfterWeb', 'StoragePressure')]
    [string]$FailureMode = 'None',
    [ValidateRange(1, 1024)]
    [int]$StoragePressureMegabytes = 32,
    [ValidateRange(15, 600)]
    [int]$StartupTimeoutSeconds = 120
)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')
$localDevelopmentRoot = Join-Path $repositoryRoot 'artifacts/local-development'
$initializationPath = Join-Path $localDevelopmentRoot '.initialized.json'
$webDirectory = Join-Path $repositoryRoot 'src/Pegasus.Web'
$webAssembly = Join-Path $webDirectory 'bin/Debug/net10.0/Pegasus.Web.dll'
$workerDirectory = Join-Path $repositoryRoot 'src/Pegasus.Worker'
$workerAssembly = Join-Path $workerDirectory 'bin/Debug/net10.0/Pegasus.Worker.dll'
$azuriteProgram = Join-Path $repositoryRoot 'node_modules/azurite/dist/src/azurite.js'
$manifestName = 'run-manifest.json'
$azuriteAccountName = 'devstoreaccount1'
$azuriteAccountKey = 'Eby8vdM02xNOcqFeqCnf2w+X8m0y0V3O52SxG9uM91m0YF/XwoK+WkKzQAE3WZcX0Dne6ZaZR7sCMZ8DlQ=='

function ConvertTo-DeterministicJson {
    param([Parameter(Mandatory)][object]$Value)

    $json = $Value | ConvertTo-Json -Depth 30
    return (($json -replace "`r`n?", "`n").TrimEnd([char[]]@("`n")) + "`n")
}

function Write-AtomicJson {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [object]$Value
    )

    $parent = Split-Path -Parent $Path
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    $temporaryPath = Join-Path $parent (".{0}.{1}.tmp" -f
        [System.IO.Path]::GetFileName($Path),
        [Guid]::NewGuid().ToString('N'))
    try {
        [System.IO.File]::WriteAllText(
            $temporaryPath,
            (ConvertTo-DeterministicJson -Value $Value),
            [System.Text.UTF8Encoding]::new($false))
        [System.IO.File]::Move($temporaryPath, $Path, $true)
    }
    finally {
        if ([System.IO.File]::Exists($temporaryPath)) {
            [System.IO.File]::Delete($temporaryPath)
        }
    }
}

function Get-RequiredApplication {
    param(
        [Parameter(Mandatory)]
        [string]$Name,
        [string]$Repair = 'Run pwsh ./scripts/Invoke-Doctor.ps1 -Profile Offline.'
    )

    $command = Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($null -eq $command) {
        throw "$Name is required. Repair: $Repair"
    }

    return [System.IO.Path]::GetFullPath([string]$command.Source)
}

function ConvertTo-OwnedTimestamp {
    <#
        .SYNOPSIS
        Returns an ownership timestamp in its canonical round-trip form.

        .DESCRIPTION
        ConvertFrom-Json converts an ISO-8601 string to [datetime], so a
        timestamp read back from a manifest is no longer the string that was
        written. Casting that value to [string] yields a culture-dependent form
        that neither the manifest contract nor the ownership comparison can
        match. Normalise instead of comparing whatever the parser produced.
    #>
    param([object]$Value)

    if ($Value -is [datetime]) {
        return ([datetime]$Value).ToUniversalTime().ToString('O')
    }

    return [string]$Value
}

function Get-Sha256 {
    param([Parameter(Mandatory)][string]$Path)

    return [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData(
            [System.IO.File]::ReadAllBytes($Path)))
}

function Assert-ExactPath {
    param(
        [Parameter(Mandatory)]
        [string]$Actual,
        [Parameter(Mandatory)]
        [string]$Expected,
        [Parameter(Mandatory)]
        [string]$Label
    )

    $actualFullPath = [System.IO.Path]::GetFullPath($Actual)
    $expectedFullPath = [System.IO.Path]::GetFullPath($Expected)
    if (-not $actualFullPath.Equals($expectedFullPath, (Get-PegasusPathComparison))) {
        throw "$Label does not match its run-owned path."
    }
}

function Get-Initialization {
    if (-not [System.IO.File]::Exists($initializationPath)) {
        throw 'Local development is not initialized. Run pwsh ./scripts/Initialize-LocalDevelopment.ps1.'
    }

    try {
        $initialization = [System.IO.File]::ReadAllText($initializationPath) |
            ConvertFrom-Json -Depth 20
    }
    catch {
        throw "The local initialization marker is invalid: $initializationPath"
    }

    if ($initialization.schemaVersion -ne 2 -or
        $initialization.kind -ne 'Pegasus.LocalDevelopment.Initialization' -or
        $initialization.profile -ne 'Offline' -or
        $initialization.sdkVersion -ne '10.0.302' -or
        $initialization.azuriteVersion -ne '3.36.0' -or
        $initialization.functionsCoreToolsVersion -ne '4.12.1' -or
        [string]$initialization.sourceSha -notmatch '^[0-9a-f]{40}$') {
        throw 'The local initialization marker does not satisfy the Offline profile contract. Re-run Initialize-LocalDevelopment.ps1.'
    }

    $packageLockPath = Join-Path $repositoryRoot 'package-lock.json'
    if (-not [System.IO.File]::Exists($packageLockPath) -or
        (Get-Sha256 -Path $packageLockPath) -ne [string]$initialization.packageLockSha256) {
        throw 'The root package lock changed after initialization. Re-run Initialize-LocalDevelopment.ps1.'
    }

    $git = Get-RequiredApplication -Name 'git'
    $sourceRevision = (& $git -C $repositoryRoot rev-parse --verify HEAD 2>$null | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or
        -not $sourceRevision.Equals(
            [string]$initialization.sourceSha,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'The checked-out source revision changed after initialization. Re-run Initialize-LocalDevelopment.ps1.'
    }

    foreach ($requiredPath in @($webAssembly, $workerAssembly, $azuriteProgram)) {
        if (-not [System.IO.File]::Exists($requiredPath)) {
            throw "Initialized local dependency is missing: $requiredPath. Re-run Initialize-LocalDevelopment.ps1."
        }
    }

    $runtimeArtifactPaths = [ordered]@{
        web = $webAssembly
        worker = $workerAssembly
    }
    foreach ($runtimeArtifact in $runtimeArtifactPaths.GetEnumerator()) {
        $record = $initialization.runtimeArtifacts.PSObject.Properties[$runtimeArtifact.Key].Value
        if ($null -eq $record -or
            [string]::IsNullOrWhiteSpace([string]$record.relativePath) -or
            $null -eq $record.byteLength -or
            [string]::IsNullOrWhiteSpace([string]$record.sha256)) {
            throw 'The local initialization marker does not contain complete runtime artifact evidence. Re-run Initialize-LocalDevelopment.ps1.'
        }

        Assert-ExactPath `
            -Actual (Join-Path $repositoryRoot ([string]$record.relativePath)) `
            -Expected $runtimeArtifact.Value `
            -Label "Initialized $($runtimeArtifact.Key) runtime artifact"

        $currentArtifact = [System.IO.FileInfo]::new($runtimeArtifact.Value)
        if ($currentArtifact.Length -ne [int64]$record.byteLength -or
            -not (Get-Sha256 -Path $currentArtifact.FullName).Equals(
                [string]$record.sha256,
                [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "The initialized runtime artifact changed after the clean build: $($record.relativePath). Re-run Initialize-LocalDevelopment.ps1."
        }
    }

    return $initialization
}

function Get-ToolPaths {
    return [pscustomobject][ordered]@{
        PowerShell = Get-RequiredApplication -Name 'pwsh'
        DotNet = Get-RequiredApplication -Name 'dotnet'
        Node = Get-RequiredApplication -Name 'node'
        Functions = Get-RequiredApplication -Name 'func'
        Database = Get-RequiredApplication -Name (Get-PegasusDatabaseCommandName)
    }
}
function Get-ControlToolPaths {
    return [pscustomobject][ordered]@{
        Database = Get-RequiredApplication -Name (Get-PegasusDatabaseCommandName)
    }
}


function Get-RunPaths {
    param([Parameter(Mandatory)][string]$Id)

    $runRoot = Join-Path $localDevelopmentRoot $Id
    return [pscustomobject][ordered]@{
        RunRoot = $runRoot
        Manifest = Join-Path $runRoot $manifestName
        State = Join-Path $runRoot 'state'
        Logs = Join-Path $runRoot 'logs'
        Azurite = Join-Path $runRoot 'azurite'
        Intake = Join-Path $runRoot 'intake'
        Mailbox = Join-Path $runRoot 'mailbox'
        MailboxInbox = Join-Path $runRoot 'mailbox/inbox'
        MailboxSent = Join-Path $runRoot 'mailbox/sent'
        CaseFiles = Join-Path $runRoot 'case-files'
    }
}

function Get-FreeTcpPort {
    # The first allocation of a run necessarily passes an empty set, and a
    # mandatory parameter rejects an empty collection unless this is declared.
    param(
        [Parameter(Mandatory)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.HashSet[int]]$Reserved
    )

    do {
        $listener = [System.Net.Sockets.TcpListener]::new(
            [System.Net.IPAddress]::Loopback,
            0)
        try {
            $listener.Start()
            $port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
        }
        finally {
            $listener.Stop()
        }
    } while (-not $Reserved.Add($port))

    return $port
}

function Test-TcpPortAvailable {
    param([Parameter(Mandatory)][int]$Port)

    $listener = [System.Net.Sockets.TcpListener]::new(
        [System.Net.IPAddress]::Loopback,
        $Port)
    try {
        $listener.Start()
        return $true
    }
    catch [System.Net.Sockets.SocketException] {
        return $false
    }
    finally {
        $listener.Stop()
    }
}

function New-RunManifest {
    param(
        [Parameter(Mandatory)]
        [string]$Id,
        [Parameter(Mandatory)]
        [object]$Initialization
    )

    $paths = Get-RunPaths -Id $Id
    if ([System.IO.Directory]::Exists($paths.RunRoot) -or
        [System.IO.File]::Exists($paths.RunRoot)) {
        throw "The requested run root already exists without an owned restart: $($paths.RunRoot)"
    }

    foreach ($path in @(
            $paths.State,
            $paths.Logs,
            $paths.Azurite,
            $paths.Intake,
            $paths.MailboxInbox,
            $paths.MailboxSent,
            $paths.CaseFiles)) {
        [System.IO.Directory]::CreateDirectory($path) | Out-Null
    }

    $reservedPorts = [System.Collections.Generic.HashSet[int]]::new()
    $webPort = Get-FreeTcpPort -Reserved $reservedPorts
    $functionsPort = Get-FreeTcpPort -Reserved $reservedPorts
    $blobPort = Get-FreeTcpPort -Reserved $reservedPorts
    $queuePort = Get-FreeTcpPort -Reserved $reservedPorts
    $tablePort = Get-FreeTcpPort -Reserved $reservedPorts
    # Allocated on both platforms so the manifest contract has no platform
    # branch. Only the container engine binds it.
    $databasePort = Get-FreeTcpPort -Reserved $reservedPorts
    $databaseName = "PegasusDevelopment_$Id"
    $now = [DateTimeOffset]::UtcNow.ToString('O')

    $manifest = [pscustomobject][ordered]@{
        schemaVersion = 2
        kind = 'Pegasus.LocalDevelopment.Run'
        runId = $Id
        state = 'Starting'
        startAttempt = 1
        createdUtc = $now
        updatedUtc = $now
        sourceSha = ([string]$Initialization.sourceSha).ToLowerInvariant()
        ownership = [ordered]@{
            repositoryRoot = $repositoryRoot
            runRoot = $paths.RunRoot
            cloudOperations = 'disabled'
        }
        runtime = [ordered]@{
            profile = 'DevelopmentOffline'
            environment = 'Development'
            artifacts = $Initialization.runtimeArtifacts
        }
        identity = [ordered]@{
            initializationCompleted = $false
            subjectId = 'd47fbbae-ea22-4ca6-b983-01e2ed1fbd13'
            userName = 'development-offline-administrator'
            role = 'Administrator'
        }
        verification = [ordered]@{
            readiness = $null
            smoke = $null
        }
        resources = [ordered]@{
            database = [ordered]@{
                provider = 'SqlServer'
                engine = Get-PegasusDatabaseEngineKind
                instanceName = $databaseName
                databaseName = $databaseName
                secretPath = $(if ((Get-PegasusDatabaseEngineKind) -eq 'LocalDb') {
                    $null
                }
                else {
                    Join-Path $paths.State 'mssql.env'
                })
                created = $false
            }
            ports = [ordered]@{
                webHttps = $webPort
                functions = $functionsPort
                azuriteBlob = $blobPort
                azuriteQueue = $queuePort
                azuriteTable = $tablePort
                database = $databasePort
            }
            paths = [ordered]@{
                state = $paths.State
                logs = $paths.Logs
                azurite = $paths.Azurite
                intake = $paths.Intake
                mailbox = $paths.Mailbox
                mailboxInbox = $paths.MailboxInbox
                mailboxSent = $paths.MailboxSent
                caseFiles = $paths.CaseFiles
            }
        }
        endpoints = [ordered]@{
            webBase = "https://localhost:$webPort"
            webLive = "https://localhost:$webPort/health/live"
            webReady = "https://localhost:$webPort/health/ready"
            webVersion = "https://localhost:$webPort/diagnostics/version"
            functionsStatus = "http://127.0.0.1:$functionsPort/admin/host/status"
            azuriteBlob = "http://127.0.0.1:$blobPort/$azuriteAccountName"
            azuriteQueue = "http://127.0.0.1:$queuePort/$azuriteAccountName"
            azuriteTable = "http://127.0.0.1:$tablePort/$azuriteAccountName"
        }
        processes = [ordered]@{
            azurite = $null
            web = $null
            worker = $null
        }
        failure = $null
    }

    Write-AtomicJson -Path $paths.Manifest -Value $manifest
    return $manifest
}

function Assert-OwnedManifest {
    param(
        [Parameter(Mandatory)]
        [object]$Manifest,
        [Parameter(Mandatory)]
        [string]$ManifestPath
    )

    $id = [string]$Manifest.runId
    if ($id -notmatch '^[0-9a-f]{32}$' -or
        $Manifest.schemaVersion -ne 2 -or
        $Manifest.kind -ne 'Pegasus.LocalDevelopment.Run' -or
        [string]$Manifest.state -notin @('Starting', 'Running', 'Stopped', 'Failed') -or
        $Manifest.startAttempt -lt 1 -or
        [string]$Manifest.sourceSha -notmatch '^[0-9a-f]{40}$' -or
        $Manifest.runtime.profile -ne 'DevelopmentOffline' -or
        $Manifest.runtime.environment -ne 'Development' -or
        $Manifest.ownership.cloudOperations -ne 'disabled' -or
        $Manifest.identity.initializationCompleted -isnot [bool]) {
        throw "The local development manifest has an invalid ownership contract: $ManifestPath"
    }

    $paths = Get-RunPaths -Id $id
    Assert-ExactPath -Actual $ManifestPath -Expected $paths.Manifest -Label 'Manifest path'
    Assert-ExactPath -Actual ([string]$Manifest.ownership.repositoryRoot) -Expected $repositoryRoot -Label 'Repository root'
    Assert-ExactPath -Actual ([string]$Manifest.ownership.runRoot) -Expected $paths.RunRoot -Label 'Run root'

    $expectedPaths = [ordered]@{
        state = $paths.State
        logs = $paths.Logs
        azurite = $paths.Azurite
        intake = $paths.Intake
        mailbox = $paths.Mailbox
        mailboxInbox = $paths.MailboxInbox
        mailboxSent = $paths.MailboxSent
        caseFiles = $paths.CaseFiles
    }
    foreach ($name in $expectedPaths.Keys) {
        $actual = [string]$Manifest.resources.paths.$name
        Assert-ExactPath -Actual $actual -Expected $expectedPaths[$name] -Label "Owned path '$name'"
    }

    $databaseName = "PegasusDevelopment_$id"
    # A manifest created on one platform names resources the other cannot act
    # on, so refuse it rather than silently no-op.
    if ($Manifest.resources.database.engine -ne (Get-PegasusDatabaseEngineKind)) {
        throw "Run $id was created with the $($Manifest.resources.database.engine) database engine and cannot be operated on this platform."
    }
    if ($Manifest.resources.database.provider -ne 'SqlServer' -or
        $Manifest.resources.database.instanceName -ne $databaseName -or
        $Manifest.resources.database.databaseName -ne $databaseName -or
        $Manifest.resources.database.created -isnot [bool]) {
        throw "The LocalDB identity does not match run '$id'."
    }

    $ports = @(
        $Manifest.resources.ports.webHttps,
        $Manifest.resources.ports.functions,
        $Manifest.resources.ports.azuriteBlob,
        $Manifest.resources.ports.azuriteQueue,
        $Manifest.resources.ports.azuriteTable,
        $Manifest.resources.ports.database
    )
    if (@($ports | Where-Object {
            ($_ -isnot [int] -and $_ -isnot [long]) -or $_ -lt 1024 -or $_ -gt 65535
        }).Count -gt 0 -or
        @($ports | Sort-Object -Unique).Count -ne $ports.Count) {
        throw "The loopback port allocation is invalid for run '$id'."
    }

    $webPort = [int]$Manifest.resources.ports.webHttps
    $functionsPort = [int]$Manifest.resources.ports.functions
    $blobPort = [int]$Manifest.resources.ports.azuriteBlob
    $queuePort = [int]$Manifest.resources.ports.azuriteQueue
    $tablePort = [int]$Manifest.resources.ports.azuriteTable
    $expectedEndpoints = [ordered]@{
        webBase = "https://localhost:$webPort"
        webLive = "https://localhost:$webPort/health/live"
        webReady = "https://localhost:$webPort/health/ready"
        webVersion = "https://localhost:$webPort/diagnostics/version"
        functionsStatus = "http://127.0.0.1:$functionsPort/admin/host/status"
        azuriteBlob = "http://127.0.0.1:$blobPort/$azuriteAccountName"
        azuriteQueue = "http://127.0.0.1:$queuePort/$azuriteAccountName"
        azuriteTable = "http://127.0.0.1:$tablePort/$azuriteAccountName"
    }
    foreach ($name in $expectedEndpoints.Keys) {
        if ([string]$Manifest.endpoints.$name -ne $expectedEndpoints[$name]) {
            throw "Endpoint '$name' does not match run '$id'."
        }
    }
    foreach ($role in @('azurite', 'web', 'worker')) {
        $record = $Manifest.processes.PSObject.Properties[$role].Value
        if ($null -eq $record) {
            continue
        }
        $expectedMarker = Join-Path $paths.State "start-$role-$($Manifest.startAttempt).ps1"
        if ($record.role -ne $role -or
            ($record.pid -isnot [int] -and $record.pid -isnot [long]) -or
            $record.pid -le 0 -or
            (ConvertTo-OwnedTimestamp -Value $record.startedUtc) -notmatch '^\d{4}-\d{2}-\d{2}T' -or
            [string]::IsNullOrWhiteSpace([string]$record.executable)) {
            throw "Process ownership record '$role' is invalid for run '$id'."
        }
        Assert-ExactPath `
            -Actual ([string]$record.commandMarker) `
            -Expected $expectedMarker `
            -Label "Process marker '$role'"
    }
}

function Read-OwnedManifest {
    param([Parameter(Mandatory)][string]$ManifestPath)

    if (-not [System.IO.File]::Exists($ManifestPath)) {
        throw "The owned run manifest does not exist: $ManifestPath"
    }
    try {
        $manifest = [System.IO.File]::ReadAllText($ManifestPath) |
            ConvertFrom-Json -Depth 30
    }
    catch {
        throw "The owned run manifest is invalid JSON: $ManifestPath"
    }
    Assert-OwnedManifest -Manifest $manifest -ManifestPath $ManifestPath
    return $manifest
}

function Write-OwnedManifest {
    param([Parameter(Mandatory)][object]$Manifest)

    $paths = Get-RunPaths -Id ([string]$Manifest.runId)
    $Manifest.updatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    Assert-OwnedManifest -Manifest $Manifest -ManifestPath $paths.Manifest
    Write-AtomicJson -Path $paths.Manifest -Value $Manifest
}

function Get-OwnedManifestPaths {
    if (-not [System.IO.Directory]::Exists($localDevelopmentRoot)) {
        return @()
    }

    return @(
        [System.IO.Directory]::EnumerateDirectories($localDevelopmentRoot) |
            Where-Object {
                [System.IO.Path]::GetFileName($_) -match '^[0-9a-f]{32}$' -and
                [System.IO.File]::Exists((Join-Path $_ $manifestName))
            } |
            ForEach-Object { Join-Path $_ $manifestName } |
            Sort-Object
    )
}

function Resolve-OwnedManifest {
    param([string]$RequestedRunId)

    if (-not [string]::IsNullOrWhiteSpace($RequestedRunId)) {
        return Read-OwnedManifest -ManifestPath (
            (Get-RunPaths -Id $RequestedRunId).Manifest)
    }

    # Wrap in an array subexpression: PowerShell unwraps a single-element array
    # returned from a function, which would make this a string and index the
    # first character of the path rather than the first element.
    $manifestPaths = @(Get-OwnedManifestPaths)
    if ($manifestPaths.Count -eq 0) {
        throw 'No owned local development run exists. Supply -RunId after starting a run.'
    }
    if ($manifestPaths.Count -ne 1) {
        throw "Run selection is ambiguous: found $($manifestPaths.Count) owned manifests. Supply -RunId."
    }

    return Read-OwnedManifest -ManifestPath $manifestPaths[0]
}

function Get-RunDatabaseContext {
    param([Parameter(Mandatory)][object]$Manifest)

    $instanceName = [string]$Manifest.resources.database.instanceName
    return [pscustomobject]@{
        InstanceName = $instanceName
        DatabaseName = [string]$Manifest.resources.database.databaseName
        RunId = [string]$Manifest.runId
        ContainerName = Get-PegasusDatabaseContainerName -RunId ([string]$Manifest.runId)
        Port = [int]$Manifest.resources.ports.database
        SecretPath = [string]$Manifest.resources.database.secretPath
    }
}

function Get-RunConnectionString {
    param([Parameter(Mandatory)][object]$Manifest)

    $context = Get-RunDatabaseContext -Manifest $Manifest
    $password = $null
    if ((Get-PegasusDatabaseEngineKind) -ne 'LocalDb') {
        $password = Read-PegasusDatabaseSecretFile -Path $context.SecretPath
    }

    return Get-PegasusDatabaseConnectionString `
        -InstanceName $context.InstanceName `
        -DatabaseName $context.DatabaseName `
        -Port $context.Port `
        -Password $password
}

function Get-AzuriteConnectionString {
    param([Parameter(Mandatory)][object]$Manifest)

    return @(
        'DefaultEndpointsProtocol=http',
        "AccountName=$azuriteAccountName",
        "AccountKey=$azuriteAccountKey",
        "BlobEndpoint=$($Manifest.endpoints.azuriteBlob)",
        "QueueEndpoint=$($Manifest.endpoints.azuriteQueue)",
        "TableEndpoint=$($Manifest.endpoints.azuriteTable)"
    ) -join ';'
}

function Get-WebEnvironment {
    param([Parameter(Mandatory)][object]$Manifest)

    $webBase = [string]$Manifest.endpoints.webBase
    return @{
        ASPNETCORE_ENVIRONMENT = 'Development'
        DOTNET_ENVIRONMENT = 'Development'
        ASPNETCORE_URLS = $webBase
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        Runtime__Profile = 'DevelopmentOffline'
        ConnectionStrings__Pegasus = Get-RunConnectionString -Manifest $Manifest
        Intake__LocalArtifactPath = [string]$Manifest.resources.paths.intake
        Custody__OfflineRootPath = [string]$Manifest.resources.paths.caseFiles
        Mailbox__LocalRootPath = [string]$Manifest.resources.paths.mailbox
        Features__LocalIntake = 'true'
        Features__LocalDocumentCustody = 'true'
    }
}

function Get-WorkerEnvironment {
    param([Parameter(Mandatory)][object]$Manifest)

    $storageConnection = Get-AzuriteConnectionString -Manifest $Manifest
    return @{
        AZURE_FUNCTIONS_ENVIRONMENT = 'Development'
        FUNCTIONS_WORKER_RUNTIME = 'dotnet-isolated'
        FUNCTIONS_CORE_TOOLS_TELEMETRY_OPTOUT = '1'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        Runtime__Profile = 'DevelopmentOffline'
        ConnectionStrings__Pegasus = Get-RunConnectionString -Manifest $Manifest
        AzureWebJobsStorage = $storageConnection
        IntakeStorage__ConnectionString = $storageConnection
        Custody__OfflineRootPath = [string]$Manifest.resources.paths.caseFiles
        Intake__LocalArtifactPath = [string]$Manifest.resources.paths.intake
        Mailbox__LocalRootPath = [string]$Manifest.resources.paths.mailbox
        IntakeWorkDispatchSchedule = '0 * * * * *'
        IntakeStagedArtifactReconciliationSchedule = '30 * * * * *'
        ExternalWorkDispatchSchedule = '15 * * * * *'
    }
}

function ConvertTo-SingleQuotedLiteral {
    param([Parameter(Mandatory)][string]$Value)

    return "'" + $Value.Replace("'", "''") + "'"
}

# Start-Process rejects -WindowStyle on non-Windows editions of PowerShell, so
# supply it only where it exists. On Linux the launched process also runs in its
# own process group, so it is no longer in the terminal's foreground group: any
# read from the inherited terminal would raise SIGTTIN, stop the process, and an
# orphaned stopped group is then sent SIGHUP. Detaching standard input avoids
# that entirely.
$hiddenWindowParameter = if ((Get-PegasusPlatform).IsWindows) {
    @{ WindowStyle = 'Hidden' }
}
else {
    @{ RedirectStandardInput = '/dev/null' }
}

function Write-Launcher {
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [string]$Command,
        [string[]]$Arguments = @()
    )

    $lines = [System.Collections.Generic.List[string]]::new()
    $lines.Add('$ErrorActionPreference = ''Stop''')
    $lines.Add('$PSNativeCommandUseErrorActionPreference = $false')
    # On Linux this places the launcher, and therefore every process it starts,
    # in its own process group so the whole tree can be reaped even after an
    # intermediate process exits and its children are reparented.
    $groupPreamble = Get-PegasusProcessGroupPreamble
    if (-not [string]::IsNullOrWhiteSpace($groupPreamble)) {
        foreach ($preambleLine in ($groupPreamble -split "`n")) {
            $lines.Add($preambleLine)
        }
    }
    $lines.Add('$commandArguments = @(')
    foreach ($argument in $Arguments) {
        $lines.Add("    $(ConvertTo-SingleQuotedLiteral -Value $argument)")
    }
    $lines.Add(')')
    $lines.Add("& $(ConvertTo-SingleQuotedLiteral -Value $Command) @commandArguments")
    $lines.Add('exit $LASTEXITCODE')
    [System.IO.File]::WriteAllText(
        $Path,
        (($lines -join "`n") + "`n"),
        [System.Text.UTF8Encoding]::new($false))
}

function Start-OwnedLauncher {
    param(
        [Parameter(Mandatory)]
        [object]$Manifest,
        [Parameter(Mandatory)]
        [ValidateSet('azurite', 'web', 'worker')]
        [string]$Role,
        [Parameter(Mandatory)]
        [string]$Command,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory)]
        [hashtable]$Environment,
        [Parameter(Mandatory)]
        [object]$Tools
    )

    $attempt = [int]$Manifest.startAttempt
    $launcher = Join-Path $Manifest.resources.paths.state "start-$Role-$attempt.ps1"
    Write-Launcher -Path $launcher -Command $Command -Arguments $Arguments
    $stdout = Join-Path $Manifest.resources.paths.logs "$Role-$attempt.stdout.log"
    $stderr = Join-Path $Manifest.resources.paths.logs "$Role-$attempt.stderr.log"
    $process = Start-Process `
        -FilePath $Tools.PowerShell `
        -ArgumentList @(
            '-NoLogo',
            '-NoProfile',
            '-NonInteractive',
            '-File',
            "`"$launcher`""
        ) `
        -WorkingDirectory $WorkingDirectory `
        -Environment $Environment `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        @hiddenWindowParameter `
        -PassThru
    try {
        $process.Refresh()
        return [pscustomobject][ordered]@{
            role = $Role
            pid = $process.Id
            startedUtc = $process.StartTime.ToUniversalTime().ToString('O')
            executable = [System.IO.Path]::GetFullPath($process.Path)
            commandMarker = $launcher
            stdout = $stdout
            stderr = $stderr
        }
    }
    catch {
        if (-not $process.HasExited) {
            $process.Kill($true)
            $process.WaitForExit()
        }
        throw
    }
}

function Invoke-OwnedOneShot {
    param(
        [Parameter(Mandatory)]
        [object]$Manifest,
        [Parameter(Mandatory)]
        [string]$Name,
        [Parameter(Mandatory)]
        [string]$Command,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)]
        [string]$WorkingDirectory,
        [Parameter(Mandatory)]
        [hashtable]$Environment,
        [Parameter(Mandatory)]
        [object]$Tools
    )

    $attempt = [int]$Manifest.startAttempt
    $launcher = Join-Path $Manifest.resources.paths.state "$Name-$attempt.ps1"
    Write-Launcher -Path $launcher -Command $Command -Arguments $Arguments
    $stdout = Join-Path $Manifest.resources.paths.logs "$Name-$attempt.stdout.log"
    $stderr = Join-Path $Manifest.resources.paths.logs "$Name-$attempt.stderr.log"
    $process = Start-Process `
        -FilePath $Tools.PowerShell `
        -ArgumentList @(
            '-NoLogo',
            '-NoProfile',
            '-NonInteractive',
            '-File',
            "`"$launcher`""
        ) `
        -WorkingDirectory $WorkingDirectory `
        -Environment $Environment `
        -RedirectStandardOutput $stdout `
        -RedirectStandardError $stderr `
        @hiddenWindowParameter `
        -Wait `
        -PassThru
    if ($process.ExitCode -ne 0) {
        throw "$Name failed with exit code $($process.ExitCode). See $stdout and $stderr."
    }
}

function Test-OwnedProcessIdentity {
    param([object]$Record)

    if ($null -eq $Record) {
        return [pscustomobject]@{
            Exists = $false
            Owned = $true
            Detail = 'not recorded'
        }
    }

    $process = Get-Process -Id ([int]$Record.pid) -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return [pscustomobject]@{
            Exists = $false
            Owned = $true
            Detail = 'not running'
        }
    }

    try {
        $process.Refresh()
        $actualStart = $process.StartTime.ToUniversalTime().ToString('O')
        $actualExecutable = [System.IO.Path]::GetFullPath($process.Path)
        $commandLine = Get-PegasusProcessCommandLine -ProcessId ([int]$Record.pid)
        $pathComparison = Get-PegasusPathComparison
        $expectedMarker = [System.IO.Path]::GetFullPath([string]$Record.commandMarker)
        $commandLineContainsMarker = -not [string]::IsNullOrWhiteSpace($commandLine) -and
            $commandLine.Contains($expectedMarker, $pathComparison)
        $owned = (Test-PegasusProcessStartTimeMatch `
                -Recorded (ConvertTo-OwnedTimestamp -Value $Record.startedUtc) `
                -Actual $actualStart) -and
            $actualExecutable.Equals(
                [System.IO.Path]::GetFullPath([string]$Record.executable),
                $pathComparison) -and
            $commandLineContainsMarker
        return [pscustomobject]@{
            Exists = $true
            Owned = $owned
            Detail = $(if ($owned) { 'owned process is running' } else { 'PID identity does not match ownership record' })
        }
    }
    catch {
        # Proving identity now reads several process sources, so a process that
        # exits mid-check raises here. That is not an ownership failure: treat a
        # vanished process as gone, and keep failing closed for anything else.
        if ($null -eq (Get-Process -Id ([int]$Record.pid) -ErrorAction SilentlyContinue)) {
            return [pscustomobject]@{
                Exists = $false
                Owned = $true
                Detail = 'not running'
            }
        }

        return [pscustomobject]@{
            Exists = $true
            Owned = $false
            Detail = 'PID identity could not be proved'
        }
    }
}

function Stop-OwnedProcessTree {
    param(
        [Parameter(Mandatory)]
        [object]$Manifest,
        [Parameter(Mandatory)]
        [ValidateSet('azurite', 'web', 'worker')]
        [string]$Role
    )

    $record = $Manifest.processes.PSObject.Properties[$Role].Value
    if ($null -eq $record) {
        return
    }

    $identity = Test-OwnedProcessIdentity -Record $record
    if ($identity.Exists -and -not $identity.Owned) {
        throw "Refusing to stop PID $($record.pid): $($identity.Detail)."
    }
    if (-not $identity.Exists) {
        $Manifest.processes.$Role = $null
        return
    }

    $rootPid = [int]$record.pid
    $result = Stop-PegasusProcessTree -RootProcessId $rootPid
    foreach ($warning in $result.Warnings) {
        Write-Warning $warning
    }
    if (-not $result.Stopped) {
        $residual = if ($result.ResidualProcessIds.Count -gt 0) {
            " Residual process identifiers: $($result.ResidualProcessIds -join ', ')."
        }
        else {
            ''
        }
        throw "Owned $Role process $rootPid did not stop.$residual"
    }

    $Manifest.processes.$Role = $null
}

function Get-RunDatabaseState {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][object]$Tools
    )

    $context = Get-RunDatabaseContext -Manifest $Manifest
    return Get-PegasusDatabaseState `
        -InstanceName $context.InstanceName `
        -Command $Tools.Database `
        -ContainerName $context.ContainerName
}

function Test-RunDatabaseExists {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][object]$Tools
    )

    return (Get-RunDatabaseState -Manifest $Manifest -Tools $Tools) -ne 'Missing'
}

function Wait-RunDatabaseReady {
    param(
        [Parameter(Mandatory)][object]$Manifest,
        [Parameter(Mandatory)][object]$Tools,
        [int]$TimeoutSeconds = 120
    )

    $context = Get-RunDatabaseContext -Manifest $Manifest
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        if (Test-PegasusDatabaseReady `
                -Command $Tools.Database `
                -ContainerName $context.ContainerName `
                -Port $context.Port) {
            return
        }
        Start-Sleep -Milliseconds 500
    }

    $diagnostics = Get-PegasusDatabaseDiagnostics `
        -Command $Tools.Database -ContainerName $context.ContainerName
    throw "Run-owned database '$($context.InstanceName)' did not become ready within $TimeoutSeconds seconds. $diagnostics"
}

function Stop-RunResources {
    param(
        [Parameter(Mandatory)]
        [object]$Manifest,
        [Parameter(Mandatory)]
        [object]$Tools
    )

    foreach ($role in @('worker', 'web', 'azurite')) {
        Stop-OwnedProcessTree -Manifest $Manifest -Role $role
    }

    $context = Get-RunDatabaseContext -Manifest $Manifest
    $instance = $context.InstanceName
    $instanceState = Get-RunDatabaseState -Manifest $Manifest -Tools $Tools
    if (-not $Manifest.resources.database.created) {
        if ($instanceState -ne 'Missing') {
            throw "Database instance '$instance' exists without completed run ownership."
        }
        return
    }
    if ($instanceState -eq 'Running') {
        Stop-PegasusDatabaseInstance `
            -InstanceName $instance `
            -Command $Tools.Database `
            -ContainerName $context.ContainerName `
            -RunId $context.RunId
    }
    elseif ($instanceState -eq 'Unknown') {
        throw "The state of run-owned database instance '$instance' could not be proved."
    }
}

function Invoke-LoopbackRequest {
    param(
        [Parameter(Mandatory)]
        [string]$Uri,
        [switch]$SkipCertificateCheck
    )

    $parsed = [Uri]$Uri
    if (-not $parsed.IsLoopback -or
        $parsed.Host -notin @('localhost', '127.0.0.1', '::1') -or
        $parsed.Scheme -notin @('http', 'https')) {
        throw "Refusing a non-loopback health request: $Uri"
    }

    return Invoke-WebRequest `
        -Uri $parsed `
        -Method Get `
        -TimeoutSec 3 `
        -SkipCertificateCheck:$SkipCertificateCheck `
        -SkipHttpErrorCheck
}

function Test-AzuriteReady {
    param([Parameter(Mandatory)][object]$Manifest)

    try {
        foreach ($endpoint in @(
                [string]$Manifest.endpoints.azuriteBlob,
                [string]$Manifest.endpoints.azuriteQueue,
                [string]$Manifest.endpoints.azuriteTable)) {
            $response = Invoke-LoopbackRequest -Uri $endpoint
            if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 500) {
                return $false
            }
        }
        return $true
    }
    catch {
        return $false
    }
}

function Test-WebHealth {
    param([Parameter(Mandatory)][object]$Manifest)

    try {
        $live = Invoke-LoopbackRequest `
            -Uri ([string]$Manifest.endpoints.webLive) `
            -SkipCertificateCheck
        $ready = Invoke-LoopbackRequest `
            -Uri ([string]$Manifest.endpoints.webReady) `
            -SkipCertificateCheck
        return $live.StatusCode -eq 200 -and $ready.StatusCode -eq 200
    }
    catch {
        return $false
    }
}

function Test-FunctionsRunning {
    param([Parameter(Mandatory)][object]$Manifest)

    try {
        $response = Invoke-LoopbackRequest -Uri ([string]$Manifest.endpoints.functionsStatus)
        if ($response.StatusCode -ne 200) {
            return $false
        }
        $status = $response.Content | ConvertFrom-Json
        return [string]$status.state -eq 'Running'
    }
    catch {
        return $false
    }
}

function Wait-ForReadiness {
    param(
        [Parameter(Mandatory)]
        [scriptblock]$Probe,
        [Parameter(Mandatory)]
        [object]$ProcessRecord,
        [Parameter(Mandatory)]
        [string]$Description
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($StartupTimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $identity = Test-OwnedProcessIdentity -Record $ProcessRecord
        if (-not $identity.Exists -or -not $identity.Owned) {
            throw "$Description exited before readiness. $($identity.Detail)."
        }
        if (& $Probe) {
            return
        }
        Start-Sleep -Milliseconds 250
    }

    throw "$Description did not become ready within $StartupTimeoutSeconds seconds."
}

function Test-RunStatus {
    param([Parameter(Mandatory)][object]$Manifest)

    $azuriteIdentity = Test-OwnedProcessIdentity -Record $Manifest.processes.azurite
    $webIdentity = Test-OwnedProcessIdentity -Record $Manifest.processes.web
    $workerIdentity = Test-OwnedProcessIdentity -Record $Manifest.processes.worker
    $azuriteReady = $azuriteIdentity.Exists -and
        $azuriteIdentity.Owned -and
        (Test-AzuriteReady -Manifest $Manifest)
    $webReady = $webIdentity.Exists -and
        $webIdentity.Owned -and
        (Test-WebHealth -Manifest $Manifest)
    $functionsRunning = $workerIdentity.Exists -and
        $workerIdentity.Owned -and
        (Test-FunctionsRunning -Manifest $Manifest)

    return [pscustomobject][ordered]@{
        RunId = [string]$Manifest.runId
        State = [string]$Manifest.state
        AzuriteProcess = $azuriteIdentity.Owned -and $azuriteIdentity.Exists
        AzuriteReady = $azuriteReady
        WebProcess = $webIdentity.Owned -and $webIdentity.Exists
        WebReady = $webReady
        WorkerProcess = $workerIdentity.Owned -and $workerIdentity.Exists
        FunctionsRunning = $functionsRunning
        WebUrl = [string]$Manifest.endpoints.webBase
        FunctionsStatusUrl = [string]$Manifest.endpoints.functionsStatus
    }
}

function Invoke-RunSmoke {
    param([Parameter(Mandatory)][object]$Manifest)
    if (-not $Manifest.identity.initializationCompleted) {
        throw "Run '$($Manifest.runId)' has no completed local identity initialization evidence."
    }


    $status = Test-RunStatus -Manifest $Manifest
    if ($Manifest.state -ne 'Running' -or
        -not $status.AzuriteReady -or
        -not $status.WebReady -or
        -not $status.FunctionsRunning) {
        throw "Run '$($Manifest.runId)' is not ready for smoke."
    }

    $versionResponse = Invoke-LoopbackRequest `
        -Uri ([string]$Manifest.endpoints.webVersion) `
        -SkipCertificateCheck
    if ($versionResponse.StatusCode -ne 200) {
        throw "Run '$($Manifest.runId)' version diagnostic returned HTTP $($versionResponse.StatusCode)."
    }
    $version = $versionResponse.Content | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace([string]$version.version) -or
        [string]$version.sourceSha -notmatch '^[0-9a-fA-F]{40}$' -or
        -not ([string]$version.sourceSha).Equals(
            [string]$Manifest.sourceSha,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Run '$($Manifest.runId)' version diagnostic does not match its initialized source."
    }

    $webBase = [string]$Manifest.endpoints.webBase
    $operationsResponse = Invoke-LoopbackRequest `
        -Uri "$webBase/Operations" `
        -SkipCertificateCheck
    if ($operationsResponse.StatusCode -ne 200) {
        throw "Run '$($Manifest.runId)' Operations route returned HTTP $($operationsResponse.StatusCode)."
    }


    $smokeEvidence = [pscustomobject][ordered]@{
        RunId = [string]$Manifest.runId
        Result = 'Passed'
        StartAttempt = [int]$Manifest.startAttempt
        ObservedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        WebReady = $true
        FunctionsRunning = $true
        Version = [string]$version.version
        SourceSha = ([string]$version.sourceSha).ToLowerInvariant()
        IdentityInitialized = [bool]$Manifest.identity.initializationCompleted
        HttpsOriginValidated = $true
        AdministratorRouteValidated = $true
        SubjectId = [string]$Manifest.identity.subjectId
        UserName = [string]$Manifest.identity.userName
    }
    $Manifest.verification.smoke = $smokeEvidence
    Write-OwnedManifest -Manifest $Manifest
    return $smokeEvidence
}

function Assert-NoReparsePoints {
    param([Parameter(Mandatory)][string]$RunRoot)

    $rootItem = Get-Item -LiteralPath $RunRoot -Force
    if (($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw "Reset refuses the run-root reparse point: $RunRoot"
    }
    $reparsePoint = Get-ChildItem -LiteralPath $RunRoot -Force -Recurse |
        Where-Object {
            ($_.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0
        } |
        Select-Object -First 1
    if ($null -ne $reparsePoint) {
        throw "Reset refuses the run-owned reparse point: $($reparsePoint.FullName)"
    }
}

function Enter-LifecycleMutex {
    $rootBytes = [System.Text.Encoding]::UTF8.GetBytes($repositoryRoot.ToUpperInvariant())
    $rootHash = [System.Convert]::ToHexString(
        [System.Security.Cryptography.SHA256]::HashData($rootBytes))
    $mutex = [System.Threading.Mutex]::new(
        $false,
        "Local\Pegasus.LocalDevelopment.$($rootHash.Substring(0, 32))")
    try {
        if (-not $mutex.WaitOne([TimeSpan]::FromSeconds(30))) {
            $mutex.Dispose()
            throw 'Another local lifecycle mutation is in progress.'
        }
    }
    catch [System.Threading.AbandonedMutexException] {
        # The abandoned owner is gone; this caller now owns the mutex.
    }
    return $mutex
}

function Start-LocalRun {
    $initialization = Get-Initialization
    $tools = Get-ToolPaths

    if ([string]::IsNullOrWhiteSpace($RunId)) {
        do {
            $id = [Guid]::NewGuid().ToString('N')
            $paths = Get-RunPaths -Id $id
        } while ([System.IO.Directory]::Exists($paths.RunRoot))
        $manifest = New-RunManifest -Id $id -Initialization $initialization
    }
    else {
        $paths = Get-RunPaths -Id $RunId
        if ([System.IO.File]::Exists($paths.Manifest)) {
            $manifest = Read-OwnedManifest -ManifestPath $paths.Manifest
            if ($manifest.state -notin @('Stopped', 'Failed')) {
                throw "Run '$RunId' is in state '$($manifest.state)' and cannot be started."
            }
            foreach ($role in @('azurite', 'web', 'worker')) {
                $record = $manifest.processes.PSObject.Properties[$role].Value
                $identity = Test-OwnedProcessIdentity -Record $record
                if ($identity.Exists) {
                    throw "Run '$RunId' still has a recorded $role process; stop it before restart."
                }
                $manifest.processes.$role = $null
            }
            $manifest.startAttempt = [int]$manifest.startAttempt + 1
            $manifest.state = 'Starting'
            $manifest.failure = $null
            Write-OwnedManifest -Manifest $manifest
        }
        else {
            $manifest = New-RunManifest -Id $RunId -Initialization $initialization
        }
    }

    $paths = Get-RunPaths -Id ([string]$manifest.runId)
    $databaseContext = Get-RunDatabaseContext -Manifest $manifest
    $instance = $databaseContext.InstanceName
    $databaseExists = Test-RunDatabaseExists -Manifest $manifest -Tools $tools

    try {
        foreach ($port in @(
                $manifest.resources.ports.webHttps,
                $manifest.resources.ports.functions,
                $manifest.resources.ports.azuriteBlob,
                $manifest.resources.ports.azuriteQueue,
                $manifest.resources.ports.azuriteTable,
                $manifest.resources.ports.database)) {
            if (-not (Test-TcpPortAvailable -Port ([int]$port))) {
                throw "Run '$($manifest.runId)' cannot bind owned loopback port $port."
            }
        }


        $azuriteEnvironment = @{
            AZURITE_ACCOUNTS = "$azuriteAccountName`:$azuriteAccountKey"
            FUNCTIONS_CORE_TOOLS_TELEMETRY_OPTOUT = '1'
        }
        $manifest.processes.azurite = Start-OwnedLauncher `
            -Manifest $manifest `
            -Role 'azurite' `
            -Command $tools.Node `
            -Arguments @(
                $azuriteProgram,
                '--location',
                [string]$manifest.resources.paths.azurite,
                '--debug',
                (Join-Path $manifest.resources.paths.logs "azurite-$($manifest.startAttempt).debug.log"),
                '--blobHost',
                '127.0.0.1',
                '--blobPort',
                [string]$manifest.resources.ports.azuriteBlob,
                '--queueHost',
                '127.0.0.1',
                '--queuePort',
                [string]$manifest.resources.ports.azuriteQueue,
                '--tableHost',
                '127.0.0.1',
                '--tablePort',
                [string]$manifest.resources.ports.azuriteTable
            ) `
            -WorkingDirectory $repositoryRoot `
            -Environment $azuriteEnvironment `
            -Tools $tools
        Write-OwnedManifest -Manifest $manifest
        Wait-ForReadiness `
            -Description 'Azurite' `
            -ProcessRecord $manifest.processes.azurite `
            -Probe { Test-AzuriteReady -Manifest $manifest }

        if (-not $manifest.resources.database.created) {
            if ($databaseExists) {
                throw "Database instance '$instance' exists without completed run ownership."
            }
            if ((Get-PegasusDatabaseEngineKind) -ne 'LocalDb') {
                # The credential reaches the engine through an owner-only file
                # and the started process environment, never through a command
                # line, which is world-readable on Linux.
                Assert-ExactPath `
                    -Actual $databaseContext.SecretPath `
                    -Expected (Join-Path $paths.State 'mssql.env') `
                    -Label 'Run-owned database secret'
                Write-PegasusDatabaseSecretFile `
                    -Path $databaseContext.SecretPath `
                    -Password (New-PegasusDatabasePassword)
            }
            New-PegasusDatabaseInstance `
                -InstanceName $instance `
                -Command $tools.Database `
                -ContainerName $databaseContext.ContainerName `
                -RunId $databaseContext.RunId `
                -RepositoryRoot $repositoryRoot `
                -SecretPath $databaseContext.SecretPath `
                -Port $databaseContext.Port
            $manifest.resources.database.created = $true
            Write-OwnedManifest -Manifest $manifest
        }
        elseif (-not $databaseExists) {
            throw "Run '$($manifest.runId)' lost its owned database instance; restart ownership is ambiguous."
        }
        Start-PegasusDatabaseInstance `
            -InstanceName $instance `
            -Command $tools.Database `
            -ContainerName $databaseContext.ContainerName `
            -RunId $databaseContext.RunId
        # LocalDB start is synchronous; a container start is not.
        Wait-RunDatabaseReady -Manifest $manifest -Tools $tools -TimeoutSeconds $StartupTimeoutSeconds

        $webEnvironment = Get-WebEnvironment -Manifest $manifest
        Invoke-OwnedOneShot `
            -Manifest $manifest `
            -Name 'initialize-web' `
            -Command $tools.DotNet `
            -Arguments @(
                $webAssembly,
                '--initialize-development',
                "--Lifecycle:RunId=$($manifest.runId)"
            ) `
            -WorkingDirectory $webDirectory `
            -Environment $webEnvironment `
            -Tools $tools

        $manifest.identity.initializationCompleted = $true
        Write-OwnedManifest -Manifest $manifest
        $manifest.processes.web = Start-OwnedLauncher `
            -Manifest $manifest `
            -Role 'web' `
            -Command $tools.DotNet `
            -Arguments @(
                $webAssembly,
                "--Lifecycle:RunId=$($manifest.runId)"
            ) `
            -WorkingDirectory $webDirectory `
            -Environment $webEnvironment `
            -Tools $tools
        Write-OwnedManifest -Manifest $manifest
        Wait-ForReadiness `
            -Description 'Pegasus Web' `
            -ProcessRecord $manifest.processes.web `
            -Probe { Test-WebHealth -Manifest $manifest }

        if ($FailureMode -eq 'AfterWeb') {
            throw 'Injected run-scoped failure after Web readiness.'
        }
        if ($FailureMode -eq 'StoragePressure') {
            $pressurePath = Join-Path $manifest.resources.paths.state 'storage-pressure.bin'
            $stream = [System.IO.File]::Open(
                $pressurePath,
                [System.IO.FileMode]::CreateNew,
                [System.IO.FileAccess]::Write,
                [System.IO.FileShare]::None)
            try {
                $stream.SetLength([int64]$StoragePressureMegabytes * 1MB)
            }
            finally {
                $stream.Dispose()
            }
            throw "Injected run-scoped bounded storage pressure ($StoragePressureMegabytes MiB)."
        }

        $workerEnvironment = Get-WorkerEnvironment -Manifest $manifest
        $manifest.processes.worker = Start-OwnedLauncher `
            -Manifest $manifest `
            -Role 'worker' `
            -Command $tools.Functions `
            -Arguments @(
                'start',
                '--port',
                [string]$manifest.resources.ports.functions,
                '--no-build'
            ) `
            -WorkingDirectory $workerDirectory `
            -Environment $workerEnvironment `
            -Tools $tools
        Write-OwnedManifest -Manifest $manifest
        Wait-ForReadiness `
            -Description 'Pegasus Functions host' `
            -ProcessRecord $manifest.processes.worker `
            -Probe { Test-FunctionsRunning -Manifest $manifest }

        $manifest.verification.readiness = [pscustomobject][ordered]@{
            Result = 'Passed'
            StartAttempt = [int]$manifest.startAttempt
            ObservedUtc = [DateTimeOffset]::UtcNow.ToString('O')
            AzuriteReady = $true
            WebReady = $true
            FunctionsRunning = $true
        }
        $manifest.state = 'Running'
        $manifest.failure = $null
        Write-OwnedManifest -Manifest $manifest
        return [pscustomobject][ordered]@{
            RunId = [string]$manifest.runId
            State = 'Running'
            WebReadyUrl = [string]$manifest.endpoints.webReady
            FunctionsStatusUrl = [string]$manifest.endpoints.functionsStatus
            ManifestPath = $paths.Manifest
        }
    }
    catch {
        $originalError = $_
        $cleanupErrors = [System.Collections.Generic.List[string]]::new()
        foreach ($role in @('worker', 'web', 'azurite')) {
            try {
                Stop-OwnedProcessTree -Manifest $manifest -Role $role
            }
            catch {
                $cleanupErrors.Add($_.Exception.Message)
            }
        }
        try {
            $failedContext = Get-RunDatabaseContext -Manifest $manifest
            $instanceState = Get-RunDatabaseState -Manifest $manifest -Tools $tools
            if ($instanceState -eq 'Running') {
                Stop-PegasusDatabaseInstance `
                    -InstanceName $instance `
                    -Command $tools.Database `
                    -ContainerName $failedContext.ContainerName `
                    -RunId $failedContext.RunId
            }
        }
        catch {
            $cleanupErrors.Add($_.Exception.Message)
        }

        $detail = $originalError.Exception.Message -replace '[\r\n]+', ' '
        if ($detail.Length -gt 500) {
            $detail = $detail.Substring(0, 500)
        }
        $manifest.state = 'Failed'
        $manifest.failure = [pscustomobject][ordered]@{
            code = 'START_FAILED'
            detail = $detail
            cleanup = @($cleanupErrors)
        }
        try {
            Write-OwnedManifest -Manifest $manifest
        }
        catch {
            $cleanupErrors.Add("Manifest update failed: $($_.Exception.Message)")
        }

        throw "Local run '$($manifest.runId)' failed. Diagnostics remain at $($paths.RunRoot). $detail"
    }
}

Get-PegasusPlatform | Out-Null
if ($Action -ne 'Start' -and $FailureMode -ne 'None') {
    throw '-FailureMode is valid only with -Action Start.'
}
if ($Action -eq 'Status' -and -not [string]::IsNullOrWhiteSpace($RunId)) {
    throw 'Status enumerates all owned runs and does not accept -RunId.'
}

$mutex = $null
try {
    switch ($Action) {
        'Start' {
            $mutex = Enter-LifecycleMutex
            Start-LocalRun
        }
        'Status' {
            $manifestPaths = @(Get-OwnedManifestPaths)
            if ($manifestPaths.Count -eq 0) {
                Write-Host 'No owned local development runs exist.'
                break
            }

            $invalid = $false
            foreach ($manifestPath in $manifestPaths) {
                try {
                    $manifest = Read-OwnedManifest -ManifestPath $manifestPath
                    Test-RunStatus -Manifest $manifest
                }
                catch {
                    $invalid = $true
                    [pscustomobject][ordered]@{
                        RunId = [System.IO.Path]::GetFileName(
                            [System.IO.Path]::GetDirectoryName($manifestPath))
                        State = 'Invalid'
                        Detail = $_.Exception.Message
                    }
                }
            }
            if ($invalid) {
                throw 'One or more owned local development manifests are invalid.'
            }
        }
        'Smoke' {
            $manifest = Resolve-OwnedManifest -RequestedRunId $RunId
            Invoke-RunSmoke -Manifest $manifest
        }
        'Stop' {
            $mutex = Enter-LifecycleMutex
            $manifest = Resolve-OwnedManifest -RequestedRunId $RunId
            $tools = Get-ControlToolPaths
            Stop-RunResources -Manifest $manifest -Tools $tools
            $manifest.state = 'Stopped'
            Write-OwnedManifest -Manifest $manifest
            [pscustomobject][ordered]@{
                RunId = [string]$manifest.runId
                State = 'Stopped'
                ManifestPath = (Get-RunPaths -Id ([string]$manifest.runId)).Manifest
            }
        }
        'Reset' {
            $mutex = Enter-LifecycleMutex
            $manifest = Resolve-OwnedManifest -RequestedRunId $RunId
            $paths = Get-RunPaths -Id ([string]$manifest.runId)
            Assert-NoReparsePoints -RunRoot $paths.RunRoot
            $tools = Get-ControlToolPaths
            Stop-RunResources -Manifest $manifest -Tools $tools
            $resetContext = Get-RunDatabaseContext -Manifest $manifest
            $instance = $resetContext.InstanceName
            if ($manifest.resources.database.created -and
                (Test-RunDatabaseExists -Manifest $manifest -Tools $tools)) {
                # Removing the instance discards its databases. For the
                # container engine that is the writable layer, which is why
                # Reset needs no DROP DATABASE and no SQL client.
                Remove-PegasusDatabaseInstance `
                    -InstanceName $instance `
                    -Command $tools.Database `
                    -ContainerName $resetContext.ContainerName `
                    -RunId $resetContext.RunId
            }
            Remove-Item -LiteralPath $paths.RunRoot -Recurse -Force
            [pscustomobject][ordered]@{
                RunId = [string]$manifest.runId
                State = 'Reset'
                RemovedPath = $paths.RunRoot
            }
        }
    }
}
finally {
    if ($null -ne $mutex) {
        try {
            $mutex.ReleaseMutex()
        }
        finally {
            $mutex.Dispose()
        }
    }
}
