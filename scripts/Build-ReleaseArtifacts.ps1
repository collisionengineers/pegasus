[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+-alpha\.\d+$')][string] $Version,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{40}$')][string] $SourceRevision
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
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
    $webImageArchive = Join-Path $releaseRoot 'web-image.tar.gz'

    $buildProperties = @(
        "-p:Version=$Version",
        "-p:InformationalVersion=$Version+$SourceRevision",
        '-p:ContinuousIntegrationBuild=true'
    )
    & dotnet restore ./src/Pegasus.Web/Pegasus.Web.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked Web runtime restore failed.' }
    & dotnet restore ./src/Pegasus.Worker/Pegasus.Worker.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Locked Worker runtime restore failed.' }
    & dotnet publish ./src/Pegasus.Web/Pegasus.Web.csproj -c Release -r linux-x64 --self-contained false --no-restore -o $webPublish @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Web publish failed.' }
    & dotnet publish ./src/Pegasus.Web/Pegasus.Web.csproj -c Release -r linux-x64 --self-contained false --no-restore /t:PublishContainer `
        -p:ContainerImageFormat=OCI `
        -p:ContainerArchiveOutputPath=$webImageArchive `
        -p:ContainerRepository=pegasus/web `
        -p:ContainerImageTag=$SourceRevision `
        @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Web OCI image publish failed.' }
    & dotnet publish ./src/Pegasus.Worker/Pegasus.Worker.csproj -c Release -r linux-x64 --self-contained false --no-restore -o $workerPublish @buildProperties
    if ($LASTEXITCODE -ne 0) { throw 'Worker publish failed.' }
    & dotnet ef migrations bundle --self-contained -r win-x64 --project ./src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj --startup-project ./src/Pegasus.Web/Pegasus.Web.csproj --configuration Release -o (Join-Path $releaseRoot 'efbundle.exe') --force
    if ($LASTEXITCODE -ne 0) { throw 'EF migration bundle creation failed.' }

    Compress-Archive -Path (Join-Path $webPublish '*') -DestinationPath (Join-Path $releaseRoot 'web.zip') -CompressionLevel Optimal
    Compress-Archive -Path (Join-Path $workerPublish '*') -DestinationPath (Join-Path $releaseRoot 'worker.zip') -CompressionLevel Optimal

    $migrationIdentity = Get-ChildItem ./src/Pegasus.Infrastructure/Persistence/Migrations -Filter '*.cs' |
        Where-Object { $_.Name -notmatch '\.Designer\.cs$|ModelSnapshot\.cs$' } |
        Sort-Object Name |
        Select-Object -Last 1 -ExpandProperty BaseName
    $imageDescriptor = & oras manifest fetch --oci-layout "${webImageArchive}:$SourceRevision" --descriptor | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $imageDescriptor.digest -notmatch '^sha256:[0-9a-f]{64}$') {
        throw 'ORAS could not read the Web OCI manifest digest.'
    }
    $imageManifest = & oras manifest fetch --oci-layout "${webImageArchive}:$SourceRevision" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $imageManifest.config.digest -notmatch '^sha256:[0-9a-f]{64}$') {
        throw 'ORAS could not read the Web OCI image config descriptor.'
    }
    $imageConfig = & oras blob fetch --oci-layout --output - "${webImageArchive}@$($imageManifest.config.digest)" | ConvertFrom-Json
    if ($LASTEXITCODE -ne 0 -or $imageConfig.os -ne 'linux' -or $imageConfig.architecture -ne 'amd64') {
        throw "The Web OCI image platform must be linux/amd64; found $($imageConfig.os)/$($imageConfig.architecture)."
    }
    $artifacts = @('web.zip', 'web-image.tar.gz', 'worker.zip', 'efbundle.exe') | ForEach-Object {
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
        schemaVersion = 2
        releaseVersion = $Version
        sourceRevision = $SourceRevision
        sourceStatus = 'clean'
        createdAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        tools = [ordered]@{ dotnetSdk = $sdk.Trim(); azureCli = $azVersion; azureDeveloperCli = $azdVersion }
        migrationIdentity = $migrationIdentity
        webImage = [ordered]@{
            repository = 'pegasus/web'
            tag = $SourceRevision
            digest = $imageDescriptor.digest
            mediaType = $imageDescriptor.mediaType
            platform = "$($imageConfig.os)/$($imageConfig.architecture)"
            archive = 'web-image.tar.gz'
        }
        artifacts = $artifacts
    }
    $manifest | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $releaseRoot 'release-manifest.json') -Encoding utf8NoBOM
    Remove-Item -LiteralPath $stagingRoot -Recurse -Force
    Write-Output (Join-Path $releaseRoot 'release-manifest.json')
}
finally {
    Pop-Location
}
