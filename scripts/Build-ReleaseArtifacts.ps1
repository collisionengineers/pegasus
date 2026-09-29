[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+-alpha\.\d+$')][string] $Version,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceRevision
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')
$migrationBundle = Get-PegasusMigrationBundle
$migrationRuntimeIdentifier = $migrationBundle.RuntimeIdentifier
$migrationBundleName = $migrationBundle.Name

# The assembly's informational version, read from its metadata without
# loading it, so an image compiled for another platform can be checked.
function Get-PegasusInformationalVersion {
    param([Parameter(Mandatory)][string] $AssemblyPath)
    $stream = [IO.File]::OpenRead($AssemblyPath)
    try {
        $image = [Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $metadata = [Reflection.Metadata.PEReaderExtensions]::GetMetadataReader($image)
            foreach ($handle in $metadata.GetAssemblyDefinition().GetCustomAttributes()) {
                $attribute = $metadata.GetCustomAttribute($handle)
                if ($attribute.Constructor.Kind -ne [Reflection.Metadata.HandleKind]::MemberReference) { continue }
                $constructor = $metadata.GetMemberReference([Reflection.Metadata.MemberReferenceHandle]$attribute.Constructor)
                if ($constructor.Parent.Kind -ne [Reflection.Metadata.HandleKind]::TypeReference) { continue }
                $type = $metadata.GetTypeReference([Reflection.Metadata.TypeReferenceHandle]$constructor.Parent)
                if ($metadata.GetString($type.Namespace) -cne 'System.Reflection' -or
                    $metadata.GetString($type.Name) -cne 'AssemblyInformationalVersionAttribute') { continue }
                $value = $metadata.GetBlobReader($attribute.Value)
                if ($value.ReadUInt16() -ne 1) { throw "$AssemblyPath has a malformed informational version attribute." }
                return $value.ReadSerializedString()
            }
        }
        finally { $image.Dispose() }
    }
    finally { $stream.Dispose() }
    throw "$AssemblyPath carries no informational version."
}

function Test-PegasusReadyToRunImage {
    param([Parameter(Mandatory)][string] $AssemblyPath)
    $stream = [IO.File]::OpenRead($AssemblyPath)
    try {
        $image = [Reflection.PortableExecutable.PEReader]::new($stream)
        try {
            $header = $image.PEHeaders.CorHeader
            return ($null -ne $header -and $header.ManagedNativeHeaderDirectory.Size -gt 0)
        }
        finally { $image.Dispose() }
    }
    finally { $stream.Dispose() }
}

Push-Location $repositoryRoot
try {
    $head = (git rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0 -or $head -ne $SourceRevision) {
        throw 'SourceRevision must equal the current exact Git HEAD.'
    }
    $sourceStatus = @(git status --porcelain=v1 --untracked-files=all)
    if ($LASTEXITCODE -ne 0 -or $sourceStatus.Count -ne 0) {
        throw 'Release artifacts require a clean exact source revision.'
    }

    $releaseRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts/releases/$Version"))
    $allowedRoot = [IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts/releases'))
    if (-not $releaseRoot.StartsWith($allowedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The release output escaped artifacts/releases.'
    }
    if (Test-Path -LiteralPath $releaseRoot) {
        Remove-Item -LiteralPath $releaseRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $releaseRoot | Out-Null
    $stagingRoot = Join-Path $releaseRoot '.staging'
    $webPublish = Join-Path $stagingRoot 'web'
    $workerPublish = Join-Path $stagingRoot 'worker'

    $buildProperties = @(
        "-p:Version=$Version",
        "-p:InformationalVersion=$Version+$SourceRevision",
        '-p:IncludeSourceRevisionInInformationalVersion=false',
        '-p:ContinuousIntegrationBuild=true'
    )
    # The Web restore asks for ReadyToRun, so the publish below finds the
    # runtime and compiler packs it needs (NETSDK1094 otherwise). The project
    # already lists linux-x64, so the locked graph is unchanged.
    & dotnet restore ./src/Pegasus.Web/Pegasus.Web.csproj --locked-mode -p:PublishReadyToRun=true
    if ($LASTEXITCODE -ne 0) { throw 'Locked Web runtime restore failed.' }
    & dotnet restore ./src/Pegasus.Worker/Pegasus.Worker.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked Worker runtime restore failed.' }
    # ADR-0049: web.zip is the Web release artifact. It is a framework-dependent
    # Linux x64 publish for the App Service DOTNETCORE|10.0 stack, run from
    # package; no container image, registry or OCI tooling is involved. It is
    # compiled ReadyToRun, so a fresh instance does not JIT every first request.
    & dotnet publish ./src/Pegasus.Web/Pegasus.Web.csproj -c Release -r linux-x64 --self-contained false -p:PublishReadyToRun=true --no-restore -o $webPublish @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Web publish failed.' }
    # The Web reports its identity from this attribute (Program.cs). A
    # ReadyToRun image compiled for Linux cannot be loaded on a Windows
    # workstation, so the attribute is read from the published metadata rather
    # than by running the assembly. The live smoke proves what the running
    # bytes report.
    $webInformationalVersion = Get-PegasusInformationalVersion -AssemblyPath (Join-Path $webPublish 'Pegasus.Web.dll')
    if ($webInformationalVersion -cne "$Version+$SourceRevision") {
        throw 'Web publish informational version does not match the exact release version and source revision.'
    }
    # The Box SDK's FIPS BouncyCastle assemblies check their own bytes when they
    # start; a ReadyToRun copy fails "Module checksum failed" on the first Box
    # sign-in. Pegasus.Web.csproj excludes them; this proves it did.
    foreach ($fipsAssembly in @(Get-ChildItem -LiteralPath $webPublish -Filter '*fips*.dll')) {
        if (Test-PegasusReadyToRunImage -AssemblyPath $fipsAssembly.FullName) {
            throw "$($fipsAssembly.Name) was compiled ReadyToRun; its FIPS module check fails on load. Exclude it with PublishReadyToRunExclude in Pegasus.Web.csproj."
        }
    }
    foreach ($requiredWebFile in @('Pegasus.Web.dll', 'Pegasus.Web.runtimeconfig.json')) {
        if (-not (Test-Path -LiteralPath (Join-Path $webPublish $requiredWebFile) -PathType Leaf)) {
            throw "Web publish output is missing $requiredWebFile at its root."
        }
    }
    & dotnet publish ./src/Pegasus.Worker/Pegasus.Worker.csproj -c Release -r linux-x64 --self-contained false --no-restore -o $workerPublish @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Worker publish failed.' }
    & dotnet ef migrations bundle --self-contained -r $migrationRuntimeIdentifier --project ./src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj --startup-project ./src/Pegasus.Web/Pegasus.Web.csproj --configuration Release -o (Join-Path $releaseRoot $migrationBundleName) --force
    if ($LASTEXITCODE -ne 0) { throw 'EF migration bundle creation failed.' }

    [IO.Compression.ZipFile]::CreateFromDirectory(
        $webPublish,
        (Join-Path $releaseRoot 'web.zip'),
        [IO.Compression.CompressionLevel]::Optimal,
        $false)
    [IO.Compression.ZipFile]::CreateFromDirectory(
        $workerPublish,
        (Join-Path $releaseRoot 'worker.zip'),
        [IO.Compression.CompressionLevel]::Optimal,
        $false)

    $migrationIdentity = Get-ChildItem ./src/Pegasus.Infrastructure/Persistence/Migrations -Filter '*.cs' |
        Where-Object { $_.Name -notmatch '\.Designer\.cs$|ModelSnapshot\.cs$' } |
        Sort-Object Name |
        Select-Object -Last 1 -ExpandProperty BaseName
    $artifacts = @('web.zip', 'worker.zip', $migrationBundleName) | ForEach-Object {
        $path = Join-Path $releaseRoot $_
        $file = Get-Item -LiteralPath $path
        [ordered]@{
            name = $_
            sizeBytes = $file.Length
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
    $sdk = dotnet --version
    $azVersion = (az version | ConvertFrom-Json).'azure-cli'
    $azdVersion = ((azd version) -split ' ')[2]
    $manifest = [ordered]@{
        schemaVersion = 3
        releaseVersion = $Version
        sourceRevision = $SourceRevision
        sourceStatus = 'clean'
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        tools = [ordered]@{ dotnetSdk = $sdk.Trim(); azureCli = $azVersion; azureDeveloperCli = $azdVersion }
        migrationIdentity = $migrationIdentity
        webPackage = [ordered]@{
            name = 'web.zip'
            runtimeIdentifier = 'linux-x64'
            selfContained = $false
            readyToRun = $true
            hostStack = 'DOTNETCORE|10.0'
        }
        migrationRuntimeIdentifier = $migrationRuntimeIdentifier
        migrationBundleName = $migrationBundleName
        artifacts = $artifacts
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Encoding utf8NoBOM
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    Write-Output (Join-Path $releaseRoot 'release-manifest.json')
}
finally {
    Pop-Location
}
