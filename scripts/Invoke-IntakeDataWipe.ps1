[CmdletBinding()]
param(
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'

$storageAccount = 'pegcustody252ow37gij'
$container = 'transient-intake'
$sqlServer = 'pegasus-prod-sql-252ow37gij.database.windows.net'
$database = 'pegasus'

$preserve = @(
    '__EFMigrationsHistory',
    'AspNetRoleClaims', 'AspNetRoles', 'AspNetUserClaims', 'AspNetUserLogins',
    'AspNetUserRoles', 'AspNetUsers', 'AspNetUserTokens',
    'OpenIddictApplications', 'OpenIddictAuthorizations', 'OpenIddictScopes', 'OpenIddictTokens',
    'ApprovedInboxPollStates', 'ApprovedMailboxes', 'ApprovedMailboxFolderBindings',
    'ApprovedOutlookCategories', 'ApprovedSentPollStates',
    'Organizations', 'OrganizationRoles', 'OrganizationAdministrationOperations',
    'Principals', 'PrincipalSequenceLineages',
    'ProviderDomainEvidence', 'ProviderDomainPackages', 'ProviderReferences',
    'WorkflowConfigurations', 'SendToAiControl', 'SecurityEvents',
    'CaseSequences', 'ImageIntakeSequences', 'UnidentifiedSequences'
)

Write-Output "=== Blob inventory: $storageAccount/$container ==="
$blobsJson = az storage blob list --account-name $storageAccount --container-name $container --auth-mode login --output json
$blobs = $blobsJson | ConvertFrom-Json
$blobCount = $blobs.Count
$blobBytes = ($blobs | ForEach-Object { $_.properties.contentLength } | Measure-Object -Sum).Sum
Write-Output ("Blobs: {0}; total bytes: {1}" -f $blobCount, $blobBytes)

Write-Output "`n=== SQL inventory: $sqlServer/$database ==="
$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
Add-Type -AssemblyName System.Data
$connection = New-Object System.Data.SqlClient.SqlConnection
$connection.ConnectionString = "Server=tcp:$sqlServer,1433;Database=$database;Encrypt=True;Connect Timeout=60;"
$connection.AccessToken = $token
$connection.Open()

function Invoke-Query([string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    $command.CommandTimeout = 600
    $adapter = New-Object System.Data.SqlClient.SqlDataAdapter $command
    $table = New-Object System.Data.DataTable
    [void]$adapter.Fill($table)
    return $table
}

$tables = Invoke-Query "SELECT s.name AS SchemaName, t.name AS TableName, SUM(p.rows) AS Rows
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY s.name, t.name ORDER BY t.name"

$all = @($tables | ForEach-Object { $_.TableName })
$preserveEffective = @($all | Where-Object { $_ -in $preserve -or $_ -like 'ApprovedMailbox*' })
$missing = @($preserve | Where-Object { $_ -notin $all })
$wipe = @($tables | Where-Object { $_.TableName -notin $preserveEffective })

Write-Output ("Tables total: {0}" -f $all.Count)
Write-Output ("Preserve list found: {0}/{1}; missing: {2}" -f ($preserve | Where-Object { $_ -in $all }).Count, $preserve.Count, ($missing -join ','))
Write-Output ("Preserved effective (incl. ApprovedMailbox*): {0}" -f $preserveEffective.Count)
Write-Output ("Tables to wipe: {0}; rows to delete: {1}" -f $wipe.Count, ($wipe | Measure-Object -Property Rows -Sum).Sum)
$wipe | Where-Object { $_.Rows -gt 0 } | Format-Table TableName, Rows -AutoSize | Out-String | Write-Output
$sequences = Invoke-Query "SELECT (SELECT MAX(LastAllocatedSequence) FROM CaseSequences) AS CaseSeq, (SELECT COUNT(*) FROM ImageIntakeSequences) AS ImageSeqRows, (SELECT COUNT(*) FROM UnidentifiedSequences) AS UnidSeqRows"
$sequences | Format-Table | Out-String | Write-Output

if ($missing.Count -gt 0) { $connection.Close(); throw 'Preserve list has missing tables; refusing.' }

if (-not $Execute) {
    Write-Output 'Dry run only (-Execute not set). Not touched: authentication-ring, box-links, pegtrans252ow37gij, Outlook, Box.'
    $connection.Close()
    return
}

Write-Output "`n=== Deleting blobs ==="
if ($blobCount -gt 0) {
    az storage blob delete-batch --account-name $storageAccount --source $container --auth-mode login | Out-Null
}
$afterBlobsJson = az storage blob list --account-name $storageAccount --container-name $container --auth-mode login --output json
$afterBlobCount = ($afterBlobsJson | ConvertFrom-Json).Count
Write-Output ("Blobs remaining in {0}: {1}" -f $container, $afterBlobCount)

Write-Output "`n=== Deleting SQL rows ==="
$names = $wipe | ForEach-Object { "[{0}].[{1}]" -f $_.SchemaName, $_.TableName }
$batch = @()
$batch += $names | ForEach-Object { "ALTER TABLE $_ NOCHECK CONSTRAINT ALL;" }
$batch += $names | ForEach-Object { "DELETE FROM $_;" }
$batch += $names | ForEach-Object { "ALTER TABLE $_ WITH CHECK CHECK CONSTRAINT ALL;" }
$sql = "SET XACT_ABORT ON; BEGIN TRANSACTION;`n" + ($batch -join "`n") + "`nCOMMIT TRANSACTION;"
$command = $connection.CreateCommand()
$command.CommandText = $sql
$command.CommandTimeout = 1200
$affected = $command.ExecuteNonQuery()
Write-Output ("Wipe batch committed; rows affected reported: {0}" -f $affected)

$after = Invoke-Query "SELECT s.name AS SchemaName, t.name AS TableName, SUM(p.rows) AS Rows
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY s.name, t.name"
$stillHasRows = @($after | Where-Object { $_.TableName -notin $preserveEffective -and $_.Rows -gt 0 })
Write-Output ("Wiped tables still holding rows: {0}" -f $stillHasRows.Count)
$stillHasRows | Format-Table TableName, Rows | Out-String | Write-Output
Write-Output ("Preserved rows after: {0}" -f (($after | Where-Object { $_.TableName -in $preserveEffective }) | Measure-Object -Property Rows -Sum).Sum)
Invoke-Query "SELECT (SELECT MAX(LastAllocatedSequence) FROM CaseSequences) AS CaseSeq, (SELECT COUNT(*) FROM ImageIntakeSequences) AS ImageSeqRows, (SELECT COUNT(*) FROM UnidentifiedSequences) AS UnidSeqRows" | Format-Table | Out-String | Write-Output
$connection.Close()

Write-Output 'Not touched: authentication-ring, box-links, pegtrans252ow37gij, Outlook, Box.'
