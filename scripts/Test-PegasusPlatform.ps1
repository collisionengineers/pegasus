[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

. (Join-Path $PSScriptRoot 'PegasusPlatform.ps1')

# Exercise the shared mapping without invoking a compiler, database or cloud.
$nativeBundle = Get-PegasusMigrationBundle
$expectedWorkerDisabledSettings = @(
    'AzureWebJobs.PendingWorkRecoveryFunction.Disabled',
    'AzureWebJobs.UnifiedWorkFunction.Disabled',
    'AzureWebJobs.UnifiedWorkPoisonFunction.Disabled',
    'AzureWebJobs.StagedArtifactReconciliationFunction.Disabled',
    'AzureWebJobs.SentEvidencePollFunction.Disabled'
)
$actualWorkerDisabledSettings = @(Get-PegasusWorkerDisabledSettingNames)
if ($actualWorkerDisabledSettings.Count -ne $expectedWorkerDisabledSettings.Count -or
    @($actualWorkerDisabledSettings | Where-Object { $_ -notin $expectedWorkerDisabledSettings }).Count -ne 0 -or
    @($actualWorkerDisabledSettings | Select-Object -Unique).Count -ne $actualWorkerDisabledSettings.Count) {
    throw 'Worker Disabled setting producer must return the exact distinct five-name census.'
}
# R1248 (#2165): the release route reads the Web host's Production required
# keys and the Web App's settings as text, at two SHAs. Both readers are held
# to the real files here, on every change, so a rewrite of either file fails
# this check and never a release.
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$webRequiredKeys = @(Get-PegasusWebRequiredKeys -ProgramText (Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'src/Pegasus.Web/Program.cs')))
if ($webRequiredKeys.Count -lt 5 -or 'ConnectionStrings:Pegasus' -notin $webRequiredKeys -or
    @($webRequiredKeys | Select-Object -Unique).Count -ne $webRequiredKeys.Count) {
    throw 'The Web required-key reader must return the distinct keys of the Production loop in src/Pegasus.Web/Program.cs.'
}
$webAppSettings = Get-PegasusWebAppSettings -TemplateText (Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot 'infra/modules/platform.bicep'))
foreach ($name in 'Runtime__Profile', 'WEBSITE_WARMUP_PATH', 'WEBSITE_WARMUP_STATUSES', 'WEBSITE_RUN_FROM_PACKAGE') {
    if (-not $webAppSettings.Contains($name)) { throw "The Web App settings reader did not find $name in infra/modules/platform.bicep." }
}
if (@($webAppSettings.Keys | Where-Object { $_ -like 'AzureWebJobs.*' }).Count -ne 0) {
    throw 'The Web App settings reader must stop at the webApp resource; it returned a Worker setting.'
}
$unprovisionedKeys = @($webRequiredKeys | Where-Object { -not $webAppSettings.Contains($_.Replace(':', '__')) })
if ($unprovisionedKeys.Count -ne 0) {
    throw "The Web host requires $($unprovisionedKeys -join ', ') at start-up, which the webApp resource in infra/modules/platform.bicep does not set."
}
if (@(Get-PegasusWebRequiredKeys -ProgramText 'var keys = new[] { "A:B" };').Count -ne 0 -or (Get-PegasusWebAppSettings -TemplateText 'resource workerApp x = {}').Count -ne 0) {
    throw 'The readers must return nothing when the loop or the webApp resource is absent.'
}
$settingsProbe = Get-PegasusWebAppSettings -TemplateText @"
resource webApp 'Microsoft.Web/sites@2024-04-01' = {
        { name: 'A__B', value: '@Microsoft.KeyVault(SecretUri=`${aSecretUri})' }
}
resource workerApp 'Microsoft.Web/sites@2024-04-01' = {
        { name: 'C__D', value: 'worker' }
}
"@
if ($settingsProbe.Count -ne 1 -or [string]$settingsProbe['A__B'] -cne "'@Microsoft.KeyVault(SecretUri=`${aSecretUri})'") {
    throw 'The Web App settings reader must return each webApp setting with its value expression as written.'
}
# The release identity is the newest migration source and nothing else that
# shares the folder: not a Designer file, the snapshot, or an .editorconfig.
$identityProbe = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-identity-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $identityProbe | Out-Null
try {
    foreach ($name in @(
        '20261028171919_Baseline.cs', '20261028171919_Baseline.Designer.cs',
        '20261101090000_LaterChange.cs', '20261101090000_LaterChange.Designer.cs',
        'PegasusDbContextModelSnapshot.cs', '.editorconfig', 'ZzzHelper.cs')) {
        Set-Content -LiteralPath (Join-Path $identityProbe $name) -Value '// probe'
    }
    if ((Get-PegasusMigrationIdentity -MigrationsDirectory $identityProbe) -cne '20261101090000_LaterChange') {
        throw 'The migration identity must be the newest timestamped migration source.'
    }
    Remove-Item -LiteralPath (Join-Path $identityProbe '20261101090000_LaterChange.cs')
    Remove-Item -LiteralPath (Join-Path $identityProbe '20261101090000_LaterChange.Designer.cs')
    if ((Get-PegasusMigrationIdentity -MigrationsDirectory $identityProbe) -cne '20261028171919_Baseline') {
        throw 'The migration identity must ignore the snapshot, Designer files and other sources.'
    }
}
finally {
    Remove-Item -LiteralPath $identityProbe -Recurse -Force
}

# A migration's class is read from the operations its Up runs: never its Down,
# a comment or the text of its SQL (engineering#migrations, R605, R761; #1845).
$classProbe = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-migration-class-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $classProbe | Out-Null
try {
    function Assert-MigrationClass {
        param(
            [Parameter(Mandatory)][string] $Case,
            [Parameter(Mandatory)][string] $Expected,
            [string[]] $Operations = @(),
            [Parameter(Mandatory)][string] $Source
        )
        $path = Join-Path $classProbe '20261108120000_Probe.cs'
        Set-Content -LiteralPath $path -Value $Source -Encoding utf8NoBOM
        $result = Get-PegasusMigrationClass -Path $path
        if ($result.Class -cne $Expected -or ($result.Operations -join ',') -cne ($Operations -join ',')) {
            throw "$Case expected $Expected [$($Operations -join ',')]; got $($result.Class) [$($result.Operations -join ',')]."
        }
    }

    # Generated style: block-scoped namespace. The comment and the string hold
    # a brace and a drop that are not code.
    Assert-MigrationClass -Case 'block-body additive' -Expected 'Additive' -Source @'
namespace Pegasus.Infrastructure.Persistence.Migrations
{
    public partial class Probe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropColumn(name: "Old", table: "Cases"); }
            migrationBuilder.AddColumn<string>(name: "New", table: "Cases", nullable: true);
            migrationBuilder.AddCheckConstraint(name: "CK_Cases_New", table: "Cases", sql: "[New] <> N'}migrationBuilder.DropTable('");
            migrationBuilder.DropIndex(name: "IX_Cases_New", table: "Cases");
            migrationBuilder.CreateIndex(name: "IX_Cases_New", table: "Cases", column: "New", filter: "[New] IS NOT NULL");
        }

        protected override void Down(MigrationBuilder migrationBuilder) =>
            throw new NotSupportedException("Forward only.");
    }
}
'@
    Assert-MigrationClass -Case 'block-body drop' -Expected 'NonAdditive' -Operations 'DropColumn' -Source @'
namespace Pegasus.Infrastructure.Persistence.Migrations
{
    public partial class Probe : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(name: "New", table: "Cases", nullable: true);
            migrationBuilder.DropColumn(name: "Old", table: "Cases");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "New", table: "Cases");
        }
    }
}
'@
    # Hand-written style: file-scoped namespace. The SQL's semicolons and
    # braces do not end the expression body, and the Down after it is not read.
    Assert-MigrationClass -Case 'expression-bodied Sql' -Expected 'Undetermined' -Operations 'Sql' -Source @'
namespace Pegasus.Infrastructure.Persistence.Migrations;

public partial class Probe : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.Sql(
            """
            UPDATE [Cases] SET [Value] = N'{"x":1}' WHERE [Value] IS NULL;
            DELETE FROM [CaseNotes];
            """);

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "Cases");
}
'@
    Assert-MigrationClass -Case 'Down drops, Up adds' -Expected 'Additive' -Source @'
namespace Pegasus.Infrastructure.Persistence.Migrations;

public partial class Probe : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Probes",
            columns: table => new { Id = table.Column<int>(type: "int", nullable: false) },
            constraints: table => { table.PrimaryKey("PK_Probes", x => x.Id); });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Id", table: "Probes");
        migrationBuilder.DropTable(name: "Probes");
    }
}
'@
    Assert-MigrationClass -Case 'Sql and a rename' -Expected 'NonAdditive' -Operations 'Sql', 'RenameColumn' -Source @'
namespace Pegasus.Infrastructure.Persistence.Migrations;

public partial class Probe : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(CopySql);
        migrationBuilder.RenameColumn(name: "Old", table: "Cases", newName: "New");
    }

    internal const string CopySql = "UPDATE [Cases] SET [Old] = [Old];";

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Forward only.");
}
'@
    Assert-MigrationClass -Case 'operations in a loop and a condition' -Expected 'NonAdditive' -Operations 'DropColumn', 'AlterColumn' -Source @'
namespace Pegasus.Infrastructure.Persistence.Migrations;

public partial class Probe : Migration
{
    private static readonly string[] Columns = ["First", "Second"];

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        foreach (var column in Columns)
        {
            migrationBuilder.DropColumn(name: column, table: "Principals");
        }

        if (ActiveProvider == "Microsoft.EntityFrameworkCore.SqlServer")
        {
            migrationBuilder.AlterColumn<string>(name: "Code", table: "Principals", maxLength: 8);
        }
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        throw new NotSupportedException("Forward only.");
}
'@
    Write-Output 'Migration class contract passed.'
}
finally {
    Remove-Item -LiteralPath $classProbe -Recurse -Force
}

# The hosted test instance's host (ADR-0088, R694): the origin is fixed, and
# only the Tailscale node that owns its name is the host.
if ((Get-PegasusTestInstanceOrigin) -cne 'https://cemattypc-1.tail432b53.ts.net/') {
    throw 'The hosted test instance origin must be the one ADR-0088 records, with one trailing slash.'
}
foreach ($accepted in @('https://cemattypc-1.tail432b53.ts.net', 'https://cemattypc-1.tail432b53.ts.net/', 'HTTPS://CEMATTYPC-1.tail432b53.ts.net/')) {
    if (-not (Test-PegasusTestInstanceOrigin -Origin $accepted)) { throw "The instance origin '$accepted' must be accepted." }
}
foreach ($refused in @('', 'https://other.tail432b53.ts.net/', 'https://cemattypc-1.tail432b53.ts.net:8443/', 'http://cemattypc-1.tail432b53.ts.net/', 'https://localhost:7139/')) {
    if (Test-PegasusTestInstanceOrigin -Origin $refused) { throw "The origin '$refused' is not the instance's and must be refused." }
}
foreach ($owner in @('cemattypc-1.tail432b53.ts.net.', 'cemattypc-1.tail432b53.ts.net', 'CEMATTYPC-1.tail432b53.ts.net.')) {
    if (-not (Test-PegasusTestInstanceHost -NodeName $owner)) { throw "The Tailscale node '$owner' owns the instance origin and must be the host." }
}
foreach ($other in @('', 'cemattypc.tail432b53.ts.net.', 'cemattypc-1.other.ts.net.', 'cemattypc-1')) {
    if (Test-PegasusTestInstanceHost -NodeName $other) { throw "The Tailscale node '$other' does not own the instance origin and must not be the host." }
}

$platformResolver = ${function:Get-PegasusPlatform}
try {
    foreach ($hostKind in @('Windows', 'Linux')) {
        function Get-PegasusPlatform {
            [pscustomobject]@{ Kind = $hostKind; IsWindows = $hostKind -eq 'Windows'; IsLinux = $hostKind -eq 'Linux' }
        }
        $bundle = Get-PegasusMigrationBundle
        $expectedRuntime = if ($hostKind -eq 'Windows') { 'win-x64' } else { 'linux-x64' }
        $expectedName = if ($hostKind -eq 'Windows') { 'efbundle.exe' } else { 'efbundle' }
        if ($bundle.RuntimeIdentifier -cne $expectedRuntime -or $bundle.Name -cne $expectedName -or
            $bundle.IsLinux -ne ($hostKind -eq 'Linux')) {
            throw "Incorrect migration bundle identity for $hostKind."
        }
        foreach ($cloudTool in @('az', 'azd', 'bicep')) {
            if ([string]::IsNullOrWhiteSpace((Get-PegasusRepairHint -Id $cloudTool))) {
                throw "Missing $cloudTool installation guidance for $hostKind."
            }
        }
    }
}
finally {
    ${function:Get-PegasusPlatform} = $platformResolver
}

$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-release-contract-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    # ADR-0049: the manifest validator must never shell out; web.zip is a plain
    # publish and there is no image tooling to call.
    $script:NativeCalls = 0
    function oras { $script:NativeCalls++; throw 'ORAS must not be invoked.' }
    function docker { $script:NativeCalls++; throw 'docker must not be invoked.' }

    function New-ZipFixture {
        param(
            [Parameter(Mandatory)][string] $Source,
            [Parameter(Mandatory)][string] $Archive
        )

        if ([IO.File]::Exists($Archive)) {
            [IO.File]::Delete($Archive)
        }
        [IO.Compression.ZipFile]::CreateFromDirectory($Source, $Archive)
    }

    $webArchiveSource = Join-Path $fixtureRoot 'web-archive'
    $workerArchiveSource = Join-Path $fixtureRoot 'worker-archive'
    $classifierArchiveSource = Join-Path $fixtureRoot 'classifier-archive'
    $missingRootSource = Join-Path $fixtureRoot 'missing-root-archive'
    $nestedWebSource = Join-Path $fixtureRoot 'nested-web-archive'
    New-Item -ItemType Directory -Path $webArchiveSource -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $workerArchiveSource '.azurefunctions') -Force | Out-Null
    $classifierNativePath = Join-Path $classifierArchiveSource (Get-PegasusClassifierNativeEntry)
    New-Item -ItemType Directory -Path (Split-Path -Parent $classifierNativePath) -Force | Out-Null
    New-Item -ItemType Directory -Path $missingRootSource -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $nestedWebSource 'web') -Force | Out-Null
    Set-Content -LiteralPath (Join-Path $webArchiveSource 'Pegasus.Web.dll') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $webArchiveSource 'Pegasus.Web.runtimeconfig.json') -Value '{}' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $workerArchiveSource '.azurefunctions/fixture.txt') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $classifierArchiveSource 'host.json') -Value '{}' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $classifierArchiveSource 'package.json') -Value '{}' -Encoding utf8NoBOM
    Set-Content -LiteralPath $classifierNativePath -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $missingRootSource 'fixture.txt') -Value 'fixture' -Encoding utf8NoBOM
    # A publish zipped one directory too high: the files exist but not at the root.
    Set-Content -LiteralPath (Join-Path $nestedWebSource 'web/Pegasus.Web.dll') -Value 'fixture' -Encoding utf8NoBOM
    Set-Content -LiteralPath (Join-Path $nestedWebSource 'web/Pegasus.Web.runtimeconfig.json') -Value '{}' -Encoding utf8NoBOM
    $webZipPath = Join-Path $fixtureRoot 'web.zip'
    $workerZipPath = Join-Path $fixtureRoot 'worker.zip'
    $classifierZipPath = Join-Path $fixtureRoot 'classifier.zip'
    New-ZipFixture -Source $webArchiveSource -Archive $webZipPath
    New-ZipFixture -Source $workerArchiveSource -Archive $workerZipPath
    New-ZipFixture -Source $classifierArchiveSource -Archive $classifierZipPath
    Set-Content -LiteralPath (Join-Path $fixtureRoot $nativeBundle.Name) -Value 'artifact contract fixture' -Encoding utf8NoBOM
    $artifacts = @('web.zip', 'worker.zip', 'classifier.zip', $nativeBundle.Name) | ForEach-Object {
        $path = Join-Path $fixtureRoot $_
        @{ name = $_; sizeBytes = (Get-Item -LiteralPath $path).Length;
            sha256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash }
    }
    $bundlePath = Join-Path $fixtureRoot $nativeBundle.Name
    if ($IsLinux) {
        [IO.File]::SetUnixFileMode($bundlePath, [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserExecute)
    }
    $manifest = @{
        schemaVersion = 3; sourceRevision = 'a' * 40; sourceStatus = 'clean'; artifacts = $artifacts
        migrationRuntimeIdentifier = $nativeBundle.RuntimeIdentifier; migrationBundleName = $nativeBundle.Name
        webPackage = @{ name = 'web.zip'; runtimeIdentifier = 'linux-x64'; selfContained = $false;
            readyToRun = $true; hostStack = 'DOTNETCORE|10.0' }
    }
    $manifestPath = Join-Path $fixtureRoot 'release-manifest.json'
    function Assert-Manifest {
        param([string]$ExpectedError)
        $manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
        $script:NativeCalls = 0
        $failure = $null
        try { Test-PegasusArtifactManifest -Path $manifestPath }
        catch { $failure = $_.Exception.Message }
        if ($script:NativeCalls -ne 0) {
            throw "The manifest validator invoked image tooling ($script:NativeCalls calls)."
        }
        if ($ExpectedError) {
            if (-not $failure -or -not $failure.Contains($ExpectedError)) {
                throw "Expected rejection '$ExpectedError'; got '$failure'."
            }
        }
        elseif ($failure) {
            throw "Expected a valid manifest; got '$failure'."
        }
    }

    function Set-ArtifactIdentity {
        param([Parameter(Mandatory)][int] $Index, [Parameter(Mandatory)][string] $Path)
        $manifest.artifacts[$Index].sizeBytes = (Get-Item -LiteralPath $Path).Length
        $manifest.artifacts[$Index].sha256 = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    }

    Assert-Manifest
    New-ZipFixture -Source $missingRootSource -Archive $workerZipPath
    Set-ArtifactIdentity -Index 1 -Path $workerZipPath
    Assert-Manifest -ExpectedError 'worker.zip must contain .azurefunctions/'
    New-ZipFixture -Source $workerArchiveSource -Archive $workerZipPath
    Set-ArtifactIdentity -Index 1 -Path $workerZipPath
    New-ZipFixture -Source $missingRootSource -Archive $webZipPath
    Set-ArtifactIdentity -Index 0 -Path $webZipPath
    Assert-Manifest -ExpectedError 'web.zip must contain Pegasus.Web.dll at its root'
    New-ZipFixture -Source $nestedWebSource -Archive $webZipPath
    Set-ArtifactIdentity -Index 0 -Path $webZipPath
    Assert-Manifest -ExpectedError 'web.zip must contain Pegasus.Web.dll at its root'
    New-ZipFixture -Source $webArchiveSource -Archive $webZipPath
    Set-ArtifactIdentity -Index 0 -Path $webZipPath
    $manifest.webPackage.hostStack = 'DOTNETCORE|9.0'
    Assert-Manifest -ExpectedError 'Web package identity is incomplete or invalid'
    $manifest.webPackage.hostStack = 'DOTNETCORE|10.0'
    $manifest.webPackage.selfContained = $true
    Assert-Manifest -ExpectedError 'Web package identity is incomplete or invalid'
    $manifest.webPackage.selfContained = $false
    $manifest.webPackage.readyToRun = $false
    Assert-Manifest -ExpectedError 'Web package identity is incomplete or invalid'
    $manifest.webPackage.readyToRun = $true
    $manifest.artifacts = @($manifest.artifacts) + @(@{ name = 'web-image.tar.gz'; sizeBytes = 1; sha256 = 'c' * 64 })
    Assert-Manifest -ExpectedError 'exactly the Web ZIP, Worker ZIP, classifier ZIP, and migration bundle'
    $manifest.artifacts = @($manifest.artifacts | Select-Object -First 4)
    # A classifier ZIP whose node_modules were installed for the workstation, not Linux x64.
    Remove-Item -LiteralPath $classifierNativePath
    New-ZipFixture -Source $classifierArchiveSource -Archive $classifierZipPath
    Set-ArtifactIdentity -Index 2 -Path $classifierZipPath
    Assert-Manifest -ExpectedError "classifier.zip must contain $(Get-PegasusClassifierNativeEntry) at its root"
    Set-Content -LiteralPath $classifierNativePath -Value 'fixture' -Encoding utf8NoBOM
    New-ZipFixture -Source $classifierArchiveSource -Archive $classifierZipPath
    Set-ArtifactIdentity -Index 2 -Path $classifierZipPath
    foreach ($wrongName in @('../efbundle', 'wrong.exe')) {
        $manifest.migrationBundleName = $wrongName
        Assert-Manifest -ExpectedError 'for this workstation'
    }
    $manifest.migrationBundleName = $nativeBundle.Name
    $manifest.migrationRuntimeIdentifier = if ($IsWindows) { 'linux-x64' } else { 'win-x64' }
    Assert-Manifest -ExpectedError 'for this workstation'
    $manifest.migrationBundleName = if ($IsWindows) { 'efbundle' } else { 'efbundle.exe' }
    Assert-Manifest -ExpectedError 'for this workstation'
    $manifest.migrationRuntimeIdentifier = $nativeBundle.RuntimeIdentifier
    $manifest.migrationBundleName = $nativeBundle.Name
    $manifest.artifacts[0].sha256 = 'b' * 64
    Assert-Manifest -ExpectedError 'Release artifact identity mismatch'
    $manifest.artifacts[0].sha256 = (Get-FileHash -LiteralPath (Join-Path $fixtureRoot 'web.zip') -Algorithm SHA256).Hash
    if ($IsLinux) {
        [IO.File]::SetUnixFileMode($bundlePath, [IO.UnixFileMode]::UserRead)
        Assert-Manifest -ExpectedError 'must be executable by its owner'
    }
    Write-Output "Release workstation and manifest contract passed ($($nativeBundle.RuntimeIdentifier)); Windows/Linux mappings checked."
}
finally {
    $resolvedFixtureRoot = (Resolve-Path -LiteralPath $fixtureRoot).Path
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (-not $resolvedFixtureRoot.StartsWith($temporaryRoot + [IO.Path]::DirectorySeparatorChar, (Get-PegasusPathComparison)) -or
        [IO.Path]::GetFileName($resolvedFixtureRoot) -notmatch '^pegasus-release-contract-[0-9a-f]{32}$') {
        throw 'Refusing to remove an unexpected fixture path.'
    }
    Remove-Item -LiteralPath $resolvedFixtureRoot -Recurse -Force
}

# Local live-integration settings: the parser, the host split and the flag
# derivation, without a run, a vendor or a secret.
$settingsRoot = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-local-settings-" + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $settingsRoot | Out-Null
try {
    function Write-SettingsFixture {
        param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][AllowEmptyString()][string[]]$Lines, [switch]$Shared)
        $path = Join-Path $settingsRoot $Name
        [IO.File]::WriteAllText($path, (($Lines -join "`n") + "`n"))
        if ($IsLinux) {
            $mode = [IO.UnixFileMode]::UserRead -bor [IO.UnixFileMode]::UserWrite
            if ($Shared) { $mode = $mode -bor [IO.UnixFileMode]::GroupRead }
            [IO.File]::SetUnixFileMode($path, $mode)
        }
        return $path
    }
    function Assert-SettingsRejected {
        param([Parameter(Mandatory)][string]$Case, [Parameter(Mandatory)][string]$Path, [Parameter(Mandatory)][string]$ExpectedError)
        $failure = $null
        try { Read-PegasusLocalSettingsFile -Path $Path | Out-Null }
        catch { $failure = $_.Exception.Message }
        if (-not $failure -or -not $failure.Contains($ExpectedError)) {
            throw "$Case expected rejection '$ExpectedError'; got '$failure'."
        }
    }

    $absent = Read-PegasusLocalSettingsFile -Path (Join-Path $settingsRoot 'absent.env')
    if ($absent.Count -ne 0) { throw 'An absent settings file must read as empty.' }
    $emptyFlags = Get-PegasusLiveIntegrationFlags -Settings $absent
    if ((Format-PegasusLiveIntegrationFlags -Flags $emptyFlags) -ne 'none' -or @($emptyFlags.Values | Where-Object { $_ }).Count -ne 0) {
        throw 'No settings must derive no live integration flags.'
    }

    $good = Write-SettingsFixture -Name 'good.env' -Lines @(
        '# comment line',
        '',
        'Features__LiveVehicleLookup=true',
        'Features__LiveBoxCustody=TRUE',
        'Features__LiveGlass=false',
        'Features__LiveGraphMail=true',
        'Features__LiveOcr=true',
        'DocumentIntelligence__Endpoint=https://ocr.example.test/',
        'Box__ConfigJson={"boxAppSettings":{"clientID":"a=b"},"enterpriseID":"1"}',
        'Box__RootFolderId=425169015650',
        'Dvla__ApiKey=key=with=equals',
        'Glass__RepairProfileId=4063',
        'Brego__ValuationAccount__Email=valuer@example.test',
        'Graph__TenantId=858cf5b3-aa0a-47a6-9b40-4851fd0afa94',
        'DevelopmentOffline__AdministratorPassword=p#ss=word',
        'GitHub__ProblemReports__Token=')
    $settings = Read-PegasusLocalSettingsFile -Path $good
    if ($settings.Count -ne 14) { throw "Expected 14 parsed settings; got $($settings.Count)." }
    if ($settings['Box__ConfigJson'] -cne '{"boxAppSettings":{"clientID":"a=b"},"enterpriseID":"1"}' -or
        $settings['Dvla__ApiKey'] -cne 'key=with=equals') {
        throw 'The first = must split a settings line and the rest is the verbatim value.'
    }
    $flags = Get-PegasusLiveIntegrationFlags -Settings $settings
    if (-not $flags.vehicleLookup -or -not $flags.boxCustody -or -not $flags.graphMail -or -not $flags.ocr -or $flags.glass -or $flags.guideValuation -or $flags.passwordSignIn -or
        $flags.automationMcp -or $flags.principalApi -or $flags.problemReports) {
        throw 'Flag derivation does not follow the Features__ names (case-insensitive true, token presence).'
    }
    if ((Format-PegasusLiveIntegrationFlags -Flags $flags) -cne 'vehicleLookup,boxCustody,graphMail,ocr') {
        throw "Unexpected flag summary '$(Format-PegasusLiveIntegrationFlags -Flags $flags)'."
    }
    if ((Format-PegasusLiveIntegrationFlags -Flags ([pscustomobject]$flags)) -cne 'vehicleLookup,boxCustody,graphMail,ocr') {
        throw 'The flag summary must read a manifest-shaped (PSCustomObject) flag set too.'
    }
    $web = Split-PegasusLocalSettings -Settings $settings -HostKind Web
    $worker = Split-PegasusLocalSettings -Settings $settings -HostKind Worker
    if ($web.Contains('Dvla__ApiKey') -or $web.Contains('DocumentIntelligence__Endpoint') -or -not $web.Contains('Glass__RepairProfileId') -or -not $web.Contains('Graph__TenantId') -or
        -not $web.Contains('DevelopmentOffline__AdministratorPassword') -or -not $web.Contains('Box__ConfigJson') -or
        -not $web.Contains('Brego__ValuationAccount__Email')) {
        throw 'The Web split must carry Features, Box, Glass, Cazana, Brego, Experian, Graph, GitHub, DevelopmentOffline and AutomationMcp keys only.'
    }
    if ($worker.Contains('Glass__RepairProfileId') -or $worker.Contains('DevelopmentOffline__AdministratorPassword') -or
        $worker.Contains('Brego__ValuationAccount__Email') -or
        -not $worker.Contains('Dvla__ApiKey') -or -not $worker.Contains('Box__ConfigJson') -or -not $worker.Contains('Features__LiveVehicleLookup') -or
        -not $worker.Contains('Graph__TenantId') -or -not $worker.Contains('DocumentIntelligence__Endpoint')) {
        throw 'The Worker split must carry Features, Box, Dvla, Dvsa, Graph and DocumentIntelligence keys only.'
    }

    Assert-SettingsRejected -Case 'unknown prefix' -ExpectedError 'not a recognised Web or Worker setting prefix' -Path (
        Write-SettingsFixture -Name 'unknown.env' -Lines @('Eva__ClientId=x'))
    Assert-SettingsRejected -Case 'reserved key' -ExpectedError 'owned by the run lifecycle' -Path (
        Write-SettingsFixture -Name 'reserved.env' -Lines @('Glass__CallbackBaseUri=https://example.test/'))
    Assert-SettingsRejected -Case 'reserved local custody key' -ExpectedError 'owned by the run lifecycle' -Path (
        Write-SettingsFixture -Name 'reserved2.env' -Lines @('Features__LocalDocumentCustody=true'))
    Assert-SettingsRejected -Case 'reserved public origin' -ExpectedError 'owned by the run lifecycle' -Path (
        Write-SettingsFixture -Name 'reserved3.env' -Lines @('AutomationMcp__PublicOrigin=https://example.test/'))
    Assert-SettingsRejected -Case 'duplicate key' -ExpectedError 'set more than once' -Path (
        Write-SettingsFixture -Name 'dup.env' -Lines @('Features__LiveGlass=true', 'Features__LiveGlass=false'))
    Assert-SettingsRejected -Case 'not KEY=VALUE' -ExpectedError 'is not KEY=VALUE' -Path (
        Write-SettingsFixture -Name 'bare.env' -Lines @('Features__LiveGlass'))
    Assert-SettingsRejected -Case 'invalid key' -ExpectedError 'has an invalid key' -Path (
        Write-SettingsFixture -Name 'badkey.env' -Lines @('Features:LiveGlass=true'))
    if ($IsLinux) {
        Assert-SettingsRejected -Case 'group-readable file' -ExpectedError 'readable only by its owner' -Path (
            Write-SettingsFixture -Name 'shared.env' -Lines @('Features__LiveGlass=true') -Shared)
    }
    Write-Output 'Local live-integration settings contract passed.'
}
finally {
    Remove-Item -LiteralPath $settingsRoot -Recurse -Force
}

if ($IsLinux) {
    # A local run's launcher is started through /bin/sh, which execs PowerShell,
    # and the launcher then makes itself a process group leader. Its ownership
    # record is handed over only once it leads that group (#2093), and a group
    # whose launcher has exited is still stopped through the group (R978).
    $launcherRoot = Join-Path ([IO.Path]::GetTempPath()) ("pegasus-launcher-" + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $launcherRoot | Out-Null
    $started = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
    $leftChildPidFile = Join-Path $launcherRoot 'left-child.pid'
    $powerShell = [Environment]::ProcessPath

    function Start-ShellFixture {
        param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Body)

        $wrapper = Join-Path $launcherRoot "$Name.sh"
        [IO.File]::WriteAllText($wrapper, $Body + "`n", [Text.UTF8Encoding]::new($false))
        $process = Start-Process -FilePath '/bin/sh' -ArgumentList @("`"$wrapper`"") -PassThru
        $started.Add($process)
        return $process
    }

    function New-LauncherFixture {
        param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][string]$Body)

        $launcher = Join-Path $launcherRoot "$Name.ps1"
        [IO.File]::WriteAllText(
            $launcher,
            (Get-PegasusProcessGroupPreamble) + "`n" + $Body + "`n",
            [Text.UTF8Encoding]::new($false))
        return "exec '$powerShell' -NoLogo -NoProfile -NonInteractive -File '$launcher' </dev/null >/dev/null 2>&1"
    }

    function Assert-GroupStopped {
        param([Parameter(Mandatory)][string]$Case, [Parameter(Mandatory)][int]$RootProcessId)

        $result = Stop-PegasusProcessTree -RootProcessId $RootProcessId
        if (-not $result.Stopped -or @($result.ResidualProcessIds).Count -ne 0) {
            throw "$Case did not stop the group; residual: $(@($result.ResidualProcessIds) -join ', ')."
        }
    }

    try {
        # Handover: until the exec and the preamble have run, the identifier is
        # the shell's and leads no group of its own.
        $hold = Start-ShellFixture -Name 'hold' -Body (
            "sleep 1`n" + (New-LauncherFixture -Name 'hold' -Body 'Start-Sleep -Seconds 60'))
        $before = Read-PegasusProcessStat -ProcessId $hold.Id
        if ($null -eq $before -or $before.ProcessGroupId -eq $hold.Id) {
            throw 'A launcher still in its shell must not lead its own process group.'
        }
        $handover = Wait-PegasusProcessGroupLeader -ProcessId $hold.Id -TimeoutSeconds 60
        if ($handover -ne 'Leader') {
            throw "A launcher that ran its preamble must lead its group; got $handover."
        }
        if ((Get-Item -LiteralPath "/proc/$($hold.Id)/exe").LinkTarget -ne $powerShell) {
            throw 'A launcher that leads its group must already be PowerShell, not the shell.'
        }
        Assert-GroupStopped -Case 'Stopping a running launcher' -RootProcessId $hold.Id

        # A launcher that exits leaves its child in the group; the group still
        # proves ownership and is stopped (R978).
        $leave = Start-ShellFixture -Name 'leave' -Body (New-LauncherFixture -Name 'leave' -Body (
                "`$child = Start-Process -FilePath '/bin/sleep' -ArgumentList '60' -PassThru`n" +
                "Set-Content -LiteralPath '$leftChildPidFile' -Value `$child.Id`n" +
                "Start-Sleep -Seconds 2`nexit 0"))
        $handover = Wait-PegasusProcessGroupLeader -ProcessId $leave.Id -TimeoutSeconds 60
        if ($handover -ne 'Leader') {
            throw "The exiting launcher must lead its group before it exits; got $handover."
        }
        if (-not $leave.WaitForExit(60000)) {
            throw 'The exiting launcher fixture did not exit.'
        }
        $leftChild = [int](Get-Content -LiteralPath $leftChildPidFile -Raw)
        if (-not (Test-PegasusProcessTreeAlive -RootProcessId $leave.Id)) {
            throw 'A group whose launcher exited must still count as alive while its child runs.'
        }
        Assert-GroupStopped -Case 'Stopping a group whose launcher exited' -RootProcessId $leave.Id
        if ($null -ne (Get-Process -Id $leftChild -ErrorAction SilentlyContinue)) {
            throw 'The child left in the group must be stopped with it.'
        }

        $exited = Start-ShellFixture -Name 'exited' -Body 'exit 3'
        $handover = Wait-PegasusProcessGroupLeader -ProcessId $exited.Id -TimeoutSeconds 60
        if ($handover -ne 'Exited') {
            throw "A launcher that exits before its preamble must be reported Exited; got $handover."
        }

        $stalled = Start-ShellFixture -Name 'stalled' -Body 'exec sleep 30'
        $handover = Wait-PegasusProcessGroupLeader -ProcessId $stalled.Id -TimeoutSeconds 1
        if ($handover -ne 'TimedOut') {
            throw "A process that never leads its group must time out; got $handover."
        }
        Write-Output 'Linux launcher handover and process-group stop passed.'
    }
    finally {
        foreach ($process in $started) {
            if (-not $process.HasExited) {
                $process.Kill($true)
                $process.WaitForExit()
            }
        }
        if (Test-Path -LiteralPath $leftChildPidFile) {
            Stop-Process -Id ([int](Get-Content -LiteralPath $leftChildPidFile -Raw)) -Force -ErrorAction SilentlyContinue
        }
        Remove-Item -LiteralPath $launcherRoot -Recurse -Force
    }
}

# The LocalDB response classifier is text parsing, so it runs on every host;
# the engine kind is pinned to LocalDb for these cases.
$engineKindResolver = ${function:Get-PegasusDatabaseEngineKind}
function Get-PegasusDatabaseEngineKind { 'LocalDb' }

$script:RequestedInstance = 'PegasusDevelopment_PLAT014_contract'
$script:FixtureOutput = ''
$script:FixtureExitCode = 0

function Invoke-LocalDbFixture {
    $script:FixtureOutput
    $global:LASTEXITCODE = $script:FixtureExitCode
}

function Assert-DatabaseState {
    param(
        [Parameter(Mandatory)][string]$Case,
        [Parameter(Mandatory)][string]$Output,
        [Parameter(Mandatory)][int]$ExitCode,
        [Parameter(Mandatory)][string]$Expected
    )

    $script:FixtureOutput = $Output
    $script:FixtureExitCode = $ExitCode
    $actual = Get-PegasusDatabaseState `
        -InstanceName $script:RequestedInstance `
        -Command 'Invoke-LocalDbFixture'

    if ($actual -ne $Expected) {
        throw "$Case expected '$Expected'; got '$actual'."
    }
}

$missingFixture = "Printing of LocalDB instance `"$script:RequestedInstance`" information failed because of the following error:`r`n`r`nLocalDB instance `"$script:RequestedInstance`" doesn't exist! "

try {
    Assert-DatabaseState -Case 'zero-exit explicit missing instance' -Output $missingFixture -ExitCode 0 -Expected 'Missing'
    Assert-DatabaseState -Case 'zero-exit missing different instance' -Output 'LocalDB instance "PegasusDevelopment_other" doesn''t exist! ' -ExitCode 0 -Expected 'Unknown'
    Assert-DatabaseState -Case 'zero-exit wrapper-only failure' -Output "Printing of LocalDB instance `"$script:RequestedInstance`" information failed because of the following error:" -ExitCode 0 -Expected 'Unknown'
    Assert-DatabaseState -Case 'zero-exit unrecognized response' -Output 'LocalDB returned an unrecognized diagnostic.' -ExitCode 0 -Expected 'Unknown'
    Assert-DatabaseState -Case 'running state' -Output 'State: Running' -ExitCode 0 -Expected 'Running'
    Assert-DatabaseState -Case 'stopped state' -Output 'State: Stopped' -ExitCode 0 -Expected 'Stopped'
    Assert-DatabaseState -Case 'contradictory state and missing response' -Output "State: Running`r`nLocalDB instance `"$script:RequestedInstance`" doesn't exist! " -ExitCode 0 -Expected 'Unknown'
    Assert-DatabaseState -Case 'non-zero response' -Output 'LocalDB command failed.' -ExitCode 1 -Expected 'Missing'
}
finally {
    ${function:Get-PegasusDatabaseEngineKind} = $engineKindResolver
}

# #2206: the test instance's classifier build is current only when its stamp
# names the checkout's classifier tree and the output it stamps exists.
$classifierFixture = Join-Path ([System.IO.Path]::GetTempPath()) "pegasus-classifier-build-$([guid]::NewGuid().ToString('N'))"
try {
    $tree = 'a' * 40
    [System.IO.Directory]::CreateDirectory((Join-Path $classifierFixture 'dist')) | Out-Null
    $stampPath = Get-PegasusClassifierBuildStampPath -ClassifierRoot $classifierFixture
    $cases = @(
        @{ Case = 'no output, no stamp'; Cli = $false; Stamp = $null; Expected = 'not built' },
        @{ Case = 'stamp without output'; Cli = $false; Stamp = $tree; Expected = 'not built' },
        @{ Case = 'output without stamp'; Cli = $true; Stamp = $null; Expected = 'not built' },
        @{ Case = 'stamp names the checkout'; Cli = $true; Stamp = "$tree`n"; Expected = 'current' },
        @{ Case = 'stamp names another tree'; Cli = $true; Stamp = 'b' * 40; Expected = 'stale' }
    )
    foreach ($case in $cases) {
        $cliPath = Join-Path $classifierFixture 'dist/cli.js'
        if ($case.Cli) { [System.IO.File]::WriteAllText($cliPath, '') } elseif ([System.IO.File]::Exists($cliPath)) { [System.IO.File]::Delete($cliPath) }
        if ($null -ne $case.Stamp) { [System.IO.File]::WriteAllText($stampPath, $case.Stamp) } elseif ([System.IO.File]::Exists($stampPath)) { [System.IO.File]::Delete($stampPath) }
        $state = Get-PegasusClassifierBuildState -ClassifierRoot $classifierFixture -SourceTree $tree
        if ($state.State -ne $case.Expected) {
            throw "Classifier build state '$($case.Case)' expected '$($case.Expected)'; got '$($state.State)'."
        }
    }
    $stale = Format-PegasusClassifierBuildState -BuildState (Get-PegasusClassifierBuildState -ClassifierRoot $classifierFixture -SourceTree $tree)
    if ($stale -cne "stale (built $('b' * 9) / checkout $('a' * 9))") {
        throw "Classifier build state formats a stale build as '$stale'."
    }
}
finally {
    if ([System.IO.Directory]::Exists($classifierFixture)) { [System.IO.Directory]::Delete($classifierFixture, $true) }
}

$global:LASTEXITCODE = 0
Write-Output 'Pegasus platform LocalDB state classification passed.'
