#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('ValidateAccess', 'Plan', 'Execute', 'Verify')]
    [string] $Operation,

    [Parameter(Mandatory)]
    [guid] $TenantId,

    [Parameter(Mandatory)]
    [guid] $SubscriptionId,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $ResourceGroup,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $SqlServer,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $SqlDatabase,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $StorageAccount,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $BlobContainer,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $MailboxIdentity,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $InboxFolderIdentity,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $NonTargetMailboxIdentity,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $OperatorUpn,

    [Parameter(Mandatory)]
    [guid] $PublicClientId,

    [Parameter(Mandatory)]
    [ValidateNotNullOrEmpty()]
    [string] $AccessEvidencePath,

    [Parameter(Mandatory)]
    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string] $AccessEvidenceSha256,

    [string] $ManifestPath,

    [ValidatePattern('^[0-9a-fA-F]{64}$')]
    [string] $ManifestSha256,

    [string] $ExecutionReceiptPath,

    [datetimeoffset] $PreTestCutoffUtc
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$hasPreTestCutoff = $PSBoundParameters.ContainsKey('PreTestCutoffUtc')

foreach ($credentialVariable in @(
    'AZURE_CLIENT_SECRET',
    'AZURE_CLIENT_CERTIFICATE_PATH',
    'AZURE_CLIENT_CERTIFICATE_PASSWORD',
    'AZURE_USERNAME',
    'AZURE_PASSWORD',
    'AZURE_STORAGE_CONNECTION_STRING',
    'AZURE_STORAGE_KEY',
    'SQLCMDPASSWORD',
    'IDENTITY_ENDPOINT',
    'IDENTITY_HEADER',
    'MSI_ENDPOINT',
    'MSI_SECRET')) {
    if (-not [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($credentialVariable))) {
        throw "The maintenance CLI refuses reusable application, managed-identity, storage-key, or password credentials ($credentialVariable)."
    }
}

if ($PublicClientId -eq [guid]::Empty) {
    throw 'PublicClientId must name the dedicated non-empty Entra public-client registration.'
}
if ($Operation -eq 'Plan' -and (-not $hasPreTestCutoff -or [string]::IsNullOrWhiteSpace($ManifestPath))) {
    throw 'Plan requires PreTestCutoffUtc and a new ignored ManifestPath.'
}
if ($Operation -in @('Execute', 'Verify') -and
    ([string]::IsNullOrWhiteSpace($ManifestPath) -or [string]::IsNullOrWhiteSpace($ManifestSha256))) {
    throw "$Operation requires ManifestPath and the operator-approved ManifestSha256."
}
if ($Operation -in @('Execute', 'Verify') -and [string]::IsNullOrWhiteSpace($ExecutionReceiptPath)) {
    throw "$Operation requires the ignored, content-safe ExecutionReceiptPath."
}

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assemblyPath = Join-Path $repositoryRoot 'src/Pegasus.Infrastructure/bin/Release/net10.0/Pegasus.Infrastructure.dll'
if (-not (Test-Path -LiteralPath $assemblyPath -PathType Leaf)) {
    throw 'The Release maintenance assembly is absent. Run the locked Release build from docs/runbook.md before this command.'
}

$assemblyDirectory = Split-Path -Parent $assemblyPath
foreach ($dependencyPath in Get-ChildItem -LiteralPath $assemblyDirectory -Filter '*.dll' -File |
    Where-Object FullName -ne $assemblyPath) {
    try {
        [void][System.Reflection.Assembly]::LoadFrom($dependencyPath.FullName)
    }
    catch [System.BadImageFormatException] {
        # Native runtime assets are resolved by their owning managed assembly.
    }
}
[void][System.Reflection.Assembly]::LoadFrom($assemblyPath)
$invocation = [ordered]@{
    operation = $Operation
    tenantId = $TenantId
    subscriptionId = $SubscriptionId
    resourceGroup = $ResourceGroup
    sqlServer = $SqlServer
    sqlDatabase = $SqlDatabase
    storageAccount = $StorageAccount
    blobContainer = $BlobContainer
    mailboxIdentity = $MailboxIdentity
    inboxFolderIdentity = $InboxFolderIdentity
    nonTargetMailboxIdentity = $NonTargetMailboxIdentity
    operatorUpn = $OperatorUpn
    publicClientId = $PublicClientId
    accessEvidencePath = (Resolve-Path -LiteralPath $AccessEvidencePath).Path
    accessEvidenceSha256 = $AccessEvidenceSha256.ToLowerInvariant()
    manifestPath = if ([string]::IsNullOrWhiteSpace($ManifestPath)) { $null } else { [IO.Path]::GetFullPath($ManifestPath) }
    manifestSha256 = if ([string]::IsNullOrWhiteSpace($ManifestSha256)) { $null } else { $ManifestSha256.ToLowerInvariant() }
    executionReceiptPath = if ([string]::IsNullOrWhiteSpace($ExecutionReceiptPath)) { $null } else { [IO.Path]::GetFullPath($ExecutionReceiptPath) }
    preTestCutoffUtc = if (-not $hasPreTestCutoff) { $null } else { $PreTestCutoffUtc.ToUniversalTime().ToString('O') }
}
$invocationJson = $invocation | ConvertTo-Json -Depth 4 -Compress
$result = [Pegasus.Infrastructure.Maintenance.ProductionIntakeCleanBaselineCommand]::RunJsonAsync(
    $invocationJson,
    [Threading.CancellationToken]::None).GetAwaiter().GetResult()

# The command result contains only scoped identities, counts, hashes and outcome codes.
# Tokens, Graph delta links, message bodies and row contents never cross this boundary.
$result
