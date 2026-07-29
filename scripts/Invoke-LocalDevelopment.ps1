[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Start', 'Status', 'Smoke', 'Stop', 'Reset')]
    [string]$Action,

    [ValidatePattern('^[a-f0-9]{32}$')]
    [string]$RunId,

    [ValidateRange(15, 300)]
    [int]$ReadinessTimeoutSeconds = 90,

    [ValidateSet('None', 'AfterAzurite', 'AfterWeb', 'AfterWorker', 'StoragePressure')]
    [string]$FailureMode = 'None',

    [ValidateRange(1, 256)]
    [int]$StoragePressureMegabytes = 32
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runRootBase = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/local-development'))
$manifestName = 'ownership-manifest.json'

function Get-CanonicalPath {
    param([Parameter(Mandatory)] [string]$Path)
    return [System.IO.Path]::GetFullPath($Path)
}

function Test-DescendantPath {
    param(
        [Parameter(Mandatory)] [string]$Path,
        [Parameter(Mandatory)] [string]$Parent
    )

    $fullPath = Get-CanonicalPath $Path
    $fullParent = (Get-CanonicalPath $Parent).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    return $fullPath.StartsWith($fullParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)
}

function Write-Manifest {
    param(
        [Parameter(Mandatory)] [hashtable]$Manifest,
        [Parameter(Mandatory)] [string]$Path
    )

    $Manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Path -Encoding utf8NoBOM
}

function Read-Manifest {
    param([Parameter(Mandatory)] [string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "No ownership manifest exists at '$Path'."
    }
    return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json -AsHashtable
}

function Assert-OwnedManifest {
    param([Parameter(Mandatory)] [hashtable]$Manifest)

    if ($Manifest.schemaVersion -ne 1 -or $Manifest.profile -ne 'DevelopmentOffline') {
        throw 'The ownership manifest does not describe a supported DevelopmentOffline run.'
    }
    if ($Manifest.runId -notmatch '^[a-f0-9]{32}$') {
        throw 'The ownership manifest has an invalid run identifier.'
    }
    $expectedRunRoot = Get-CanonicalPath (Join-Path $runRootBase $Manifest.runId)
    if ((Get-CanonicalPath $Manifest.runRoot) -ne $expectedRunRoot) {
        throw 'The ownership manifest run path does not match its run identifier.'
    }
    if (-not (Test-DescendantPath -Path $Manifest.runRoot -Parent $runRootBase)) {
        throw 'The ownership manifest is outside artifacts/local-development.'
    }
    if ($Manifest.database -ne "PegasusDevelopment_$($Manifest.runId)") {
        throw 'The ownership manifest database does not match its run identifier.'
    }
    foreach ($path in @($Manifest.paths.Values)) {
        if (-not (Test-DescendantPath -Path $path -Parent $Manifest.runRoot)) {
            throw 'The ownership manifest contains a path outside its run directory.'
        }
    }
}

function Get-RunManifestPaths {
    if (-not (Test-Path -LiteralPath $runRootBase -PathType Container)) {
        return @()
    }
    return @(Get-ChildItem -LiteralPath $runRootBase -Directory | ForEach-Object {
            Join-Path $_.FullName $manifestName
        } | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf })
}

function Resolve-TargetManifest {
    if (-not [string]::IsNullOrWhiteSpace($RunId)) {
        $path = Join-Path (Join-Path $runRootBase $RunId) $manifestName
        $manifest = Read-Manifest $path
        Assert-OwnedManifest $manifest
        return $manifest
    }

    $candidatePaths = Get-RunManifestPaths
    if ($candidatePaths.Count -ne 1) {
        throw 'Specify -RunId when zero or multiple owned runs exist; ambiguity is not safe to resolve automatically.'
    }
    $manifest = Read-Manifest $candidatePaths[0]
    Assert-OwnedManifest $manifest
    return $manifest
}

function Get-FreeLoopbackPort {
    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
    try {
        $listener.Start()
        return ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Get-ProcessOwnershipState {
    param([Parameter(Mandatory)] [hashtable]$Process)

    $runningProcess = Get-Process -Id ([int]$Process.processId) -ErrorAction SilentlyContinue
    if ($null -eq $runningProcess) {
        return 'Exited'
    }
    $observedStart = $runningProcess.StartTime.ToUniversalTime().ToString('O')
    if ($observedStart -ne $Process.startedUtc) {
        return 'PidReused'
    }
    return 'Running'
}

function Stop-OwnedProcesses {
    param([Parameter(Mandatory)] [hashtable]$Manifest)

    foreach ($process in @($Manifest.processes)) {
        if ((Get-ProcessOwnershipState $process) -eq 'Running') {
            Stop-Process -Id ([int]$process.processId) -ErrorAction Stop
            Wait-Process -Id ([int]$process.processId) -Timeout 15 -ErrorAction SilentlyContinue
        }
    }
}

function Start-TrackedProcess {
    param(
        [Parameter(Mandatory)] [string]$Name,
        [Parameter(Mandatory)] [string]$FilePath,
        [Parameter(Mandatory)] [string[]]$ArgumentList,
        [Parameter(Mandatory)] [string]$WorkingDirectory,
        [Parameter(Mandatory)] [string]$LogDirectory,
        [Parameter(Mandatory)] [hashtable]$Environment
    )

    $previousValues = @{}
    try {
        foreach ($key in $Environment.Keys) {
            $previousValues[$key] = [Environment]::GetEnvironmentVariable($key, 'Process')
            Set-Item -Path "Env:$key" -Value $Environment[$key]
        }
        $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -WorkingDirectory $WorkingDirectory -RedirectStandardOutput (Join-Path $LogDirectory "$Name.stdout.log") -RedirectStandardError (Join-Path $LogDirectory "$Name.stderr.log") -PassThru
        return @{
            name = $Name
            processId = $process.Id
            startedUtc = $process.StartTime.ToUniversalTime().ToString('O')
            command = $FilePath
        }
    }
    finally {
        foreach ($key in $Environment.Keys) {
            if ($null -eq $previousValues[$key]) {
                Remove-Item -Path "Env:$key" -ErrorAction SilentlyContinue
            }
            else {
                Set-Item -Path "Env:$key" -Value $previousValues[$key]
            }
        }
    }
}

function Wait-ForTcpReady {
    param(
        [Parameter(Mandatory)] [int]$Port,
        [Parameter(Mandatory)] [int]$TimeoutSeconds
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        $client = [System.Net.Sockets.TcpClient]::new()
        try {
            $connectTask = $client.ConnectAsync([System.Net.IPAddress]::Loopback, $Port)
            if ($connectTask.Wait(1000) -and $client.Connected) {
                return
            }
        }
        catch {
        }
        finally {
            $client.Dispose()
        }
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for loopback port $Port."
}

function Wait-ForHttpReady {
    param(
        [Parameter(Mandatory)] [string]$Uri,
        [Parameter(Mandatory)] [int]$TimeoutSeconds,
        [switch]$SkipCertificateCheck
    )

    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = if ($SkipCertificateCheck) {
                Invoke-WebRequest -Uri $Uri -SkipCertificateCheck -TimeoutSec 5
            }
            else {
                Invoke-WebRequest -Uri $Uri -TimeoutSec 5
            }
            if ($response.StatusCode -eq 200) {
                return $response
            }
        }
        catch {
            Start-Sleep -Milliseconds 500
        }
    } while ([DateTime]::UtcNow -lt $deadline)

    throw "Timed out waiting for '$Uri'."
}

function Test-RunReadiness {
    param([Parameter(Mandatory)] [hashtable]$Manifest)

    Assert-OwnedManifest $Manifest
    foreach ($process in @($Manifest.processes)) {
        $state = Get-ProcessOwnershipState $process
        if ($state -ne 'Running') {
            throw "$($process.name) is $state."
        }
    }

    $webResponse = Wait-ForHttpReady -Uri "https://127.0.0.1:$($Manifest.ports.webHttps)/health/ready" -TimeoutSeconds $ReadinessTimeoutSeconds -SkipCertificateCheck
    $workerResponse = Wait-ForHttpReady -Uri "http://127.0.0.1:$($Manifest.ports.worker)/admin/host/status" -TimeoutSeconds $ReadinessTimeoutSeconds
    if ($workerResponse.Content -notmatch '"state"\s*:\s*"Running"') {
        throw 'The Functions host did not report the Running state.'
    }
    return $webResponse
}

function Invoke-Start {
    if ($FailureMode -ne 'None' -and -not [string]::IsNullOrWhiteSpace($RunId)) {
        throw 'Failure injection creates its own run ID and does not accept -RunId.'
    }

    $newRunId = [Guid]::NewGuid().ToString('N')
    $runRoot = Join-Path $runRootBase $newRunId
    $paths = @{
        logs = Join-Path $runRoot 'logs'
        azurite = Join-Path $runRoot 'azurite'
        intake = Join-Path $runRoot 'intake'
        mailbox = Join-Path $runRoot 'mailbox'
        caseFiles = Join-Path $runRoot 'case-files'
        pressure = Join-Path $runRoot 'pressure'
    }
    foreach ($path in $paths.Values) {
        [System.IO.Directory]::CreateDirectory($path) | Out-Null
    }

    $ports = @{
        azuriteBlob = Get-FreeLoopbackPort
        azuriteQueue = Get-FreeLoopbackPort
        azuriteTable = Get-FreeLoopbackPort
        worker = Get-FreeLoopbackPort
        webHttp = Get-FreeLoopbackPort
        webHttps = Get-FreeLoopbackPort
    }
    if ((@($ports.Values) | Select-Object -Unique).Count -ne 6) {
        throw 'Loopback port allocation was not unique; no processes were started.'
    }

    $manifest = @{
        schemaVersion = 1
        profile = 'DevelopmentOffline'
        runId = $newRunId
        runRoot = Get-CanonicalPath $runRoot
        database = "PegasusDevelopment_$newRunId"
        createdUtc = [DateTime]::UtcNow.ToString('O')
        status = 'Starting'
        ports = $ports
        paths = $paths
        processes = @()
    }
    $manifestPath = Join-Path $runRoot $manifestName
    Write-Manifest -Manifest $manifest -Path $manifestPath

    $connectionString = "Server=(localdb)\MSSQLLocalDB;Database=$($manifest.database);Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True"
    $storageKeyBytes = [byte[]]::new(32)
    [System.Security.Cryptography.RandomNumberGenerator]::Fill($storageKeyBytes)
    $storageKey = [Convert]::ToBase64String($storageKeyBytes)
    $storageConnection = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=$storageKey;BlobEndpoint=http://127.0.0.1:$($ports.azuriteBlob)/devstoreaccount1;QueueEndpoint=http://127.0.0.1:$($ports.azuriteQueue)/devstoreaccount1;TableEndpoint=http://127.0.0.1:$($ports.azuriteTable)/devstoreaccount1"

    try {
        $azurite = Start-TrackedProcess -Name 'azurite' -FilePath 'npx' -ArgumentList @('--no-install', 'azurite', '--location', $paths.azurite, '--blobHost', '127.0.0.1', '--queueHost', '127.0.0.1', '--tableHost', '127.0.0.1', '--blobPort', $ports.azuriteBlob, '--queuePort', $ports.azuriteQueue, '--tablePort', $ports.azuriteTable, '--silent') -WorkingDirectory $repositoryRoot -LogDirectory $paths.logs -Environment @{ AZURITE_ACCOUNTS = "devstoreaccount1:$storageKey" }
        $manifest.processes += $azurite
        Write-Manifest -Manifest $manifest -Path $manifestPath
        Wait-ForTcpReady -Port $ports.azuriteBlob -TimeoutSeconds $ReadinessTimeoutSeconds
        if ($FailureMode -eq 'AfterAzurite') { throw 'Injected failure after Azurite readiness.' }

        $webEnvironment = @{
            ASPNETCORE_ENVIRONMENT = 'Development'
            Runtime__Profile = 'DevelopmentOffline'
            ConnectionStrings__Pegasus = $connectionString
            Intake__LocalArtifactPath = $paths.intake
            Features__LocalIntake = 'true'
        }
        $web = Start-TrackedProcess -Name 'web' -FilePath 'dotnet' -ArgumentList @('run', '--project', (Join-Path $repositoryRoot 'src/Pegasus.Web/Pegasus.Web.csproj'), '--no-launch-profile', '--', '--migrate-development', '--urls', "http://127.0.0.1:$($ports.webHttp);https://127.0.0.1:$($ports.webHttps)") -WorkingDirectory $repositoryRoot -LogDirectory $paths.logs -Environment $webEnvironment
        $manifest.processes += $web
        Write-Manifest -Manifest $manifest -Path $manifestPath
        Wait-ForHttpReady -Uri "https://127.0.0.1:$($ports.webHttps)/health/ready" -TimeoutSeconds $ReadinessTimeoutSeconds -SkipCertificateCheck | Out-Null
        if ($FailureMode -eq 'AfterWeb') { throw 'Injected failure after Web readiness.' }

        $workerEnvironment = @{
            ASPNETCORE_ENVIRONMENT = 'Development'
            Runtime__Profile = 'DevelopmentOffline'
            Database__Provider = 'SqlServer'
            Database__ConnectionStringName = 'Pegasus'
            ConnectionStrings__Pegasus = $connectionString
            AzureWebJobsStorage = $storageConnection
            FUNCTIONS_WORKER_RUNTIME = 'dotnet-isolated'
        }
        $worker = Start-TrackedProcess -Name 'worker' -FilePath 'func' -ArgumentList @('start', '--port', $ports.worker) -WorkingDirectory (Join-Path $repositoryRoot 'src/Pegasus.Worker') -LogDirectory $paths.logs -Environment $workerEnvironment
        $manifest.processes += $worker
        Write-Manifest -Manifest $manifest -Path $manifestPath
        Wait-ForHttpReady -Uri "http://127.0.0.1:$($ports.worker)/admin/host/status" -TimeoutSeconds $ReadinessTimeoutSeconds | Out-Null
        if ($FailureMode -eq 'AfterWorker') { throw 'Injected failure after Worker readiness.' }

        if ($FailureMode -eq 'StoragePressure') {
            $pressurePath = Join-Path $paths.pressure 'bounded-pressure.bin'
            $stream = [System.IO.File]::Open($pressurePath, [System.IO.FileMode]::CreateNew, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try {
                $stream.SetLength($StoragePressureMegabytes * 1MB)
            }
            finally {
                $stream.Dispose()
            }
            throw "Injected bounded storage-pressure failure after allocating $StoragePressureMegabytes MB inside this run only."
        }

        $manifest.status = 'Running'
        Write-Manifest -Manifest $manifest -Path $manifestPath
        Write-Output "RunId: $newRunId"
        Write-Output "Web readiness: https://127.0.0.1:$($ports.webHttps)/health/ready"
        Write-Output "Worker status: http://127.0.0.1:$($ports.worker)/admin/host/status"
    }
    catch {
        $manifest.status = 'Failed'
        $manifest.failure = $_.Exception.Message
        try { Stop-OwnedProcesses $manifest } catch { $manifest.stopFailure = $_.Exception.Message }
        Write-Manifest -Manifest $manifest -Path $manifestPath
        throw
    }
}

function Invoke-Status {
    $manifests = foreach ($path in Get-RunManifestPaths) {
        try {
            $manifest = Read-Manifest $path
            Assert-OwnedManifest $manifest
            $processStates = @($manifest.processes | ForEach-Object { "$($_.name)=$(Get-ProcessOwnershipState $_)" })
            [pscustomobject]@{
                RunId = $manifest.runId
                Status = $manifest.status
                Database = $manifest.database
                Processes = $processStates -join '; '
                WebReadiness = if ($manifest.status -eq 'Running') { try { (Test-RunReadiness $manifest).StatusCode } catch { $_.Exception.Message } } else { 'Not probed' }
            }
        }
        catch {
            [pscustomobject]@{ RunId = Split-Path (Split-Path $path -Parent) -Leaf; Status = 'Unsafe manifest'; Database = ''; Processes = $_.Exception.Message; WebReadiness = 'Not probed' }
        }
    }
    if ($null -eq $manifests -or $manifests.Count -eq 0) {
        Write-Output 'No owned local-development runs exist.'
        return
    }
    $manifests
}

function Invoke-Smoke {
    $manifest = Resolve-TargetManifest
    if ($manifest.status -ne 'Running') {
        throw "Run '$($manifest.runId)' is not marked Running."
    }
    $ready = Test-RunReadiness $manifest
    $version = Wait-ForHttpReady -Uri "https://127.0.0.1:$($manifest.ports.webHttps)/diagnostics/version" -TimeoutSeconds $ReadinessTimeoutSeconds -SkipCertificateCheck
    $diagnostic = $version.Content | ConvertFrom-Json
    if ([string]::IsNullOrWhiteSpace($diagnostic.version) -or [string]::IsNullOrWhiteSpace($diagnostic.sourceSha)) {
        throw 'Version diagnostics did not provide the non-sensitive version and source SHA.'
    }
    Write-Output "Smoke passed for run '$($manifest.runId)': readiness=$($ready.StatusCode), version=$($diagnostic.version), sourceSha=$($diagnostic.sourceSha)."
}

function Invoke-Stop {
    $manifest = Resolve-TargetManifest
    Stop-OwnedProcesses $manifest
    $manifest.status = 'Stopped'
    $manifest.stoppedUtc = [DateTime]::UtcNow.ToString('O')
    Write-Manifest -Manifest $manifest -Path (Join-Path $manifest.runRoot $manifestName)
    Write-Output "Stopped owned processes for run '$($manifest.runId)'. Run state and logs were retained."
}

function Invoke-Reset {
    $manifest = Resolve-TargetManifest
    Stop-OwnedProcesses $manifest

    $previousConnection = [Environment]::GetEnvironmentVariable('ConnectionStrings__Pegasus', 'Process')
    try {
        Set-Item -Path 'Env:ConnectionStrings__Pegasus' -Value "Server=(localdb)\MSSQLLocalDB;Database=$($manifest.database);Integrated Security=True;Encrypt=False;MultipleActiveResultSets=True"
        & dotnet ef database drop --force --no-build --project (Join-Path $repositoryRoot 'src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj') --startup-project (Join-Path $repositoryRoot 'src/Pegasus.Web/Pegasus.Web.csproj')
        if ($LASTEXITCODE -ne 0) {
            throw "Owned LocalDB database '$($manifest.database)' could not be dropped. Run state was retained."
        }
    }
    finally {
        if ($null -eq $previousConnection) { Remove-Item -Path 'Env:ConnectionStrings__Pegasus' -ErrorAction SilentlyContinue } else { Set-Item -Path 'Env:ConnectionStrings__Pegasus' -Value $previousConnection }
    }

    Assert-OwnedManifest $manifest
    Remove-Item -LiteralPath $manifest.runRoot -Recurse -Force
    Write-Output "Reset owned run '$($manifest.runId)'. No other run or resource was changed."
}

switch ($Action) {
    'Start' { Invoke-Start }
    'Status' { Invoke-Status }
    'Smoke' { Invoke-Smoke }
    'Stop' { Invoke-Stop }
    'Reset' { Invoke-Reset }
}
