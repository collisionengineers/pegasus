[CmdletBinding()]
param(
    [ValidatePattern('^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?$')]
    [string]$Version = '0.1.0-alpha.1',

    [string]$OutputRoot
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repositoryRoot "artifacts/packages/$Version"
}
$output = [System.IO.Path]::GetFullPath($OutputRoot)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot 'artifacts'))
if (-not $output.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Release-candidate output must be beneath the repository artifacts directory.'
}
if (Test-Path -LiteralPath $output) {
    throw "Release-candidate output already exists: $output"
}

$solution = Join-Path $repositoryRoot 'CollisionDocNet.slnx'
$packages = Join-Path $output 'nuget'
$cli = Join-Path $output 'cli-framework-dependent'
$schemas = Join-Path $repositoryRoot 'docs/schemas'
$packProjects = @(
    'CollisionDocNet.Core',
    'CollisionDocNet.Storage',
    'CollisionDocNet.Model',
    'CollisionDocNet.Email',
    'CollisionDocNet.Outlook',
    'CollisionDocNet.Pdf',
    'CollisionDocNet.Writer',
    'CollisionDocNet.Writer.OpenXml',
    'CollisionDocNet.Conversion'
)

function Invoke-NativeStep {
    param([string]$Name, [scriptblock]$Action)
    Write-Output "[$Name]"
    & $Action
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE."
    }
}

function New-DeterministicZip {
    param([string]$Source, [string]$Destination)
    Add-Type -AssemblyName System.IO.Compression
    $stream = [System.IO.File]::Open($Destination, [System.IO.FileMode]::CreateNew)
    try {
        $archive = [System.IO.Compression.ZipArchive]::new(
            $stream,
            [System.IO.Compression.ZipArchiveMode]::Create,
            $false)
        try {
            $files = Get-ChildItem -LiteralPath $Source -Recurse -File |
                Sort-Object { [System.IO.Path]::GetRelativePath($Source, $_.FullName) }
            foreach ($file in $files) {
                $relative = [System.IO.Path]::GetRelativePath($Source, $file.FullName).Replace('\', '/')
                $entry = $archive.CreateEntry($relative, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = [DateTimeOffset]::new(1980, 1, 1, 0, 0, 0, [TimeSpan]::Zero)
                $entryStream = $entry.Open()
                try {
                    $sourceStream = [System.IO.File]::OpenRead($file.FullName)
                    try { $sourceStream.CopyTo($entryStream) } finally { $sourceStream.Dispose() }
                }
                finally { $entryStream.Dispose() }
            }
        }
        finally { $archive.Dispose() }
    }
    finally { $stream.Dispose() }
}

function Write-DependencyManifest {
    param([string]$Destination)
    $consumersByPackage = @{}
    $dependencyRoots = @(
        [pscustomobject]@{ Path = (Join-Path $repositoryRoot 'src'); Scope = 'production' },
        [pscustomobject]@{ Path = (Join-Path $repositoryRoot 'tests'); Scope = 'test-or-tooling' }
    )
    $projectLocks = @()
    foreach ($dependencyRoot in $dependencyRoots) {
        $rootDirectory = Get-Item -LiteralPath $dependencyRoot.Path -Force
        if (($rootDirectory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Dependency root must not be a reparse point: $($dependencyRoot.Path)"
        }

        $projectParents = if ($dependencyRoot.Scope -eq 'production') {
            @(Get-ChildItem -LiteralPath $rootDirectory.FullName -Directory -Force)
        }
        else {
            @(
                foreach ($testArea in (Get-ChildItem -LiteralPath $rootDirectory.FullName -Directory -Force)) {
                    if (($testArea.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                        throw "Test area must not be a reparse point: $($testArea.FullName)"
                    }
                    Get-ChildItem -LiteralPath $testArea.FullName -Directory -Force
                }
            )
        }

        foreach ($projectDirectory in $projectParents) {
            if (($projectDirectory.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Project directory must not be a reparse point: $($projectDirectory.FullName)"
            }
            $projectFiles = @(Get-ChildItem -LiteralPath $projectDirectory.FullName -Filter '*.csproj' -File -Force)
            if ($projectFiles.Count -eq 0) {
                continue
            }
            if ($projectFiles.Count -ne 1) {
                throw "Expected exactly one project file beneath $($projectDirectory.FullName)."
            }

            $lockFiles = @(Get-ChildItem -LiteralPath $projectDirectory.FullName -Filter 'packages.lock.json' -File -Force)
            if ($lockFiles.Count -eq 0) {
                continue
            }
            if ($lockFiles.Count -ne 1) {
                throw "Expected exactly one dependency lock beneath $($projectDirectory.FullName)."
            }
            $lockFile = $lockFiles[0]
            if (($lockFile.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Dependency lock must not be a reparse point: $($lockFile.FullName)"
            }
            $projectLocks += [pscustomobject]@{
                File = $lockFile
                Scope = $dependencyRoot.Scope
                Consumer = [System.IO.Path]::GetRelativePath(
                    $repositoryRoot,
                    $projectDirectory.FullName).Replace('\', '/')
            }
        }
    }

    foreach ($projectLock in ($projectLocks | Sort-Object Consumer)) {
        $lock = Get-Content -Raw -LiteralPath $projectLock.File.FullName | ConvertFrom-Json -AsHashtable
        foreach ($framework in ($lock.dependencies.Keys | Sort-Object)) {
            foreach ($packageId in ($lock.dependencies[$framework].Keys | Sort-Object)) {
                $dependency = $lock.dependencies[$framework][$packageId]
                if ([string]$dependency.type -eq 'Project') {
                    continue
                }
                $resolved = [string]$dependency.resolved
                $key = "$($packageId.ToLowerInvariant())@$resolved"
                if (-not $consumersByPackage.ContainsKey($key)) {
                    $consumersByPackage[$key] = [ordered]@{
                        id = $packageId
                        version = $resolved
                        scope = $projectLock.Scope
                        consumers = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
                    }
                }
                if ($projectLock.Scope -eq 'production') {
                    $consumersByPackage[$key].scope = 'production'
                }
                [void]$consumersByPackage[$key].consumers.Add($projectLock.Consumer)
            }
        }
    }
    $sdk = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'global.json') | ConvertFrom-Json
    $document = [ordered]@{
        schemaVersion = 'collisiondocnet-dependencies/1'
        sdkVersion = $sdk.sdk.version
        targetFramework = 'net10.0'
        source = 'NuGet packages.lock.json files produced by locked restore'
        packages = @($consumersByPackage.Keys | Sort-Object | ForEach-Object {
            $item = $consumersByPackage[$_]
            [ordered]@{
                id = $item.id
                version = $item.version
                scope = $item.scope
                consumers = @($item.consumers)
            }
        })
    }
    $document | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $Destination -Encoding utf8NoBOM
}

Push-Location -LiteralPath $repositoryRoot
try {
    New-Item -ItemType Directory -Path $packages, $cli | Out-Null
    Invoke-NativeStep 'restore (locked)' { dotnet restore $solution --locked-mode }
    Invoke-NativeStep 'build (Release)' { dotnet build $solution --configuration Release --no-restore }
    Invoke-NativeStep 'test (Release, MTP)' { dotnet test --solution $solution --configuration Release --no-build }
    foreach ($projectName in $packProjects) {
        $project = Join-Path $repositoryRoot "src\$projectName\$projectName.csproj"
        Invoke-NativeStep "pack $projectName" {
            dotnet pack $project --configuration Release --no-build --no-restore --output $packages "/p:PackageVersion=$Version"
        }
    }
    Invoke-NativeStep 'publish framework-dependent CLI' {
        dotnet publish src\CollisionDocNet.Cli\CollisionDocNet.Cli.csproj `
            --configuration Release --no-build --no-restore --no-self-contained --output $cli "/p:Version=$Version"
    }
    Copy-Item -LiteralPath (Join-Path $repositoryRoot 'PACKAGE.md') -Destination $cli
    Copy-Item -LiteralPath (Join-Path $schemas 'extraction-result.v1.schema.json') -Destination $cli
    Copy-Item -LiteralPath (Join-Path $schemas 'evidence-bundle-manifest.v1.schema.json') -Destination $cli
    Invoke-NativeStep 'CLI Windows framework-dependent smoke' {
        $cliExecutable = if ($IsWindows) { 'collisiondocnet.exe' } else { 'collisiondocnet' }
        $versionDocument = & (Join-Path $cli $cliExecutable) version | ConvertFrom-Json
        if ($versionDocument.product -ne 'collisiondocnet' -or
            $versionDocument.schemaVersion -ne 'collisiondocnet-result/1') {
            throw 'The published CLI version contract did not match the packaged product and schema.'
        }
    }
    $dependencyManifestPath = Join-Path $output 'dependency-manifest.v1.json'
    Write-DependencyManifest -Destination $dependencyManifestPath
    $dependencyManifest = Get-Content -Raw -LiteralPath $dependencyManifestPath | ConvertFrom-Json
    if (($dependencyManifest.packages | Where-Object scope -eq 'production').Count -ne 0) {
        throw 'A production NuGet dependency was found; update the dependency/licence review before packaging.'
    }
    New-DeterministicZip -Source $cli -Destination (Join-Path $output "collisiondocnet-cli-$Version-framework-dependent.zip")

    $binaryPackages = @(Get-ChildItem -LiteralPath $packages -Filter '*.nupkg' -File)
    $symbolPackages = @(Get-ChildItem -LiteralPath $packages -Filter '*.snupkg' -File)
    if ($binaryPackages.Count -ne $packProjects.Count -or $symbolPackages.Count -ne $packProjects.Count) {
        throw 'The NuGet package set was incomplete or contained an unexpected package.'
    }

    $manifestEntries = Get-ChildItem -LiteralPath $output -Recurse -File |
        Where-Object { $_.Name -ne 'package-manifest.v1.json' } |
        Sort-Object { [System.IO.Path]::GetRelativePath($output, $_.FullName) } |
        ForEach-Object {
            [ordered]@{
                path = [System.IO.Path]::GetRelativePath($output, $_.FullName).Replace('\', '/')
                length = $_.Length
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
    [ordered]@{
        schemaVersion = 'collisiondocnet-package-manifest/1'
        packageVersion = $Version
        targetFramework = 'net10.0'
        deployment = 'framework-dependent'
        files = @($manifestEntries)
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'package-manifest.v1.json') -Encoding utf8NoBOM
    Write-Output "Local release candidate created at $output"
}
finally {
    Pop-Location
}
