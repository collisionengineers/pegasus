[CmdletBinding()]
param(
    [switch]$Execute,
    [switch]$ResetTestEstate
)

$ErrorActionPreference = 'Stop'

$storageAccount = 'pegcustody252ow37gij'
$container = 'transient-intake'
$subscriptionId = 'e6076573-23a5-46a8-acef-7e22d264e5db'
$sqlServer = 'pegasus-prod-sql-252ow37gij.database.windows.net'
$database = 'pegasus'
$resourceGroup = 'rg-pegasus-prod'
$workerApp = 'pegasus-prod-worker-252ow37gij'

$preserve = @(
    '__EFMigrationsHistory',
    'AspNetRoleClaims', 'AspNetRoles', 'AspNetUserClaims', 'AspNetUserLogins',
    'AspNetUserRoles', 'AspNetUsers', 'AspNetUserTokens',
    'OpenIddictApplications', 'OpenIddictAuthorizations', 'OpenIddictScopes', 'OpenIddictTokens',
    'ApprovedInboxPollStates', 'ApprovedMailboxes', 'ApprovedMailboxFolderBindings',
    'ApprovedOutlookCategories', 'ApprovedSentPollStates',
    'Organizations', 'OrganizationRoles', 'OrganizationAdministrationOperations',
    'ContactRoles', 'ContactPrincipalLinks',
    'Principals', 'PrincipalSequenceLineages',
    'ProviderDomainEvidence', 'ProviderDomainPackages', 'ProviderReferences',
    'WorkflowConfigurations', 'LabourRateCards', 'ImageTags', 'SendToAiControl', 'SecurityEvents',
    'CaseSequences', 'ImageIntakeSequences', 'TriageSequences', 'UnidentifiedSequences',
    'ValuationPresets'
)

Write-Output "=== Blob inventory: $storageAccount/$container ==="
$blobsJson = az storage blob list --account-name $storageAccount --container-name $container --auth-mode login --output json
if ($LASTEXITCODE -ne 0) { throw 'Blob inventory failed; refusing.' }
$blobs = $blobsJson | ConvertFrom-Json
$blobCount = $blobs.Count
$blobBytes = ($blobs | ForEach-Object { $_.properties.contentLength } | Measure-Object -Sum).Sum
Write-Output ("Blobs: {0}; total bytes: {1}" -f $blobCount, $blobBytes)

Write-Output "`n=== SQL inventory: $sqlServer/$database ==="
$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) { throw 'SQL authentication failed; refusing.' }
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

if ($missing.Count -gt 0) { $connection.Close(); throw 'Preserve list has missing tables; refusing.' }

$sequences = Invoke-Query "SELECT (SELECT MAX(LastAllocatedSequence) FROM CaseSequences) AS CaseSeq, (SELECT COUNT(*) FROM ImageIntakeSequences) AS ImageSeqRows, (SELECT MAX(LastAllocatedSequence) FROM TriageSequences) AS TriageSeq, (SELECT COUNT(*) FROM UnidentifiedSequences) AS UnidSeqRows"
$sequences | Format-Table | Out-String | Write-Output

$removedUserIds = @()
$qdosSequence = $null
if ($ResetTestEstate) {
    Write-Output "`n=== Test-estate reset inventory ==="
    $accounts = Invoke-Query "SELECT users.Id, users.UserName, users.NormalizedUserName,
    STRING_AGG(roles.Name, N',') AS Roles
FROM dbo.AspNetUsers AS users
LEFT JOIN dbo.AspNetUserRoles AS userRoles ON userRoles.UserId = users.Id
LEFT JOIN dbo.AspNetRoles AS roles ON roles.Id = userRoles.RoleId
GROUP BY users.Id, users.UserName, users.NormalizedUserName
ORDER BY users.UserName"
    $accounts | Format-Table Id, UserName, Roles -AutoSize | Out-String | Write-Output

    $alexAccounts = @($accounts | Where-Object { $_.NormalizedUserName -eq 'ALEX' })
    if ($alexAccounts.Count -ne 1 -or
        @($alexAccounts[0].Roles -split ',' | Where-Object { $_ -eq 'Administrator' }).Count -ne 1) {
        $connection.Close()
        throw 'Exactly one alex Administrator account is required; refusing.'
    }

    $removedAccounts = @($accounts | Where-Object { $_.NormalizedUserName -ne 'ALEX' })
    $removedUserIds = @($removedAccounts | ForEach-Object { $_.Id.ToString() })
    Write-Output ("Accounts to remove: {0}" -f $removedAccounts.Count)
    $removedAccounts | Format-Table Id, UserName, Roles -AutoSize | Out-String | Write-Output

    $accountTraceSql = @"
SELECT N'AspNetUserClaims' AS TraceTable, COUNT_BIG(*) AS Rows
FROM dbo.AspNetUserClaims WHERE UserId IN (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'AspNetUserLogins', COUNT_BIG(*)
FROM dbo.AspNetUserLogins WHERE UserId IN (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'AspNetUserRoles', COUNT_BIG(*)
FROM dbo.AspNetUserRoles WHERE UserId IN (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'AspNetUserTokens', COUNT_BIG(*)
FROM dbo.AspNetUserTokens WHERE UserId IN (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'OpenIddictAuthorizations', COUNT_BIG(*)
FROM dbo.OpenIddictAuthorizations WHERE Subject IN (SELECT CONVERT(nvarchar(36), Id) FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'OpenIddictTokens', COUNT_BIG(*)
FROM dbo.OpenIddictTokens WHERE Subject IN (SELECT CONVERT(nvarchar(36), Id) FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'SecurityEvents', COUNT_BIG(*)
FROM dbo.SecurityEvents WHERE SubjectId IN (SELECT CONVERT(nvarchar(36), Id) FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
    OR ActorSubjectId IN (SELECT CONVERT(nvarchar(36), Id) FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'CaseTasks', COUNT_BIG(*)
FROM dbo.CaseTasks WHERE AssigneeId IN (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'GlassRepairEstimateSessions', COUNT_BIG(*)
FROM dbo.GlassRepairEstimateSessions WHERE UserId IN (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
UNION ALL SELECT N'UserExternalCredentials', COUNT_BIG(*)
FROM dbo.UserExternalCredentials WHERE UserId IN (SELECT Id FROM dbo.AspNetUsers WHERE NormalizedUserName <> N'ALEX')
"@
    Invoke-Query $accountTraceSql | Format-Table TraceTable, Rows -AutoSize | Out-String | Write-Output

    $qdosRows = @(Invoke-Query "DECLARE @LondonYear int = DATEPART(year,
    SYSUTCDATETIME() AT TIME ZONE 'UTC' AT TIME ZONE 'GMT Standard Time');
SELECT principal.SequenceLineageId, @LondonYear AS [Year], sequence.LastAllocatedSequence
FROM dbo.Principals AS principal
LEFT JOIN dbo.CaseSequences AS sequence
    ON sequence.SequenceLineageId = principal.SequenceLineageId
    AND sequence.[Year] = @LondonYear
WHERE principal.Code = N'QDOS';")
    if ($qdosRows.Count -ne 1) {
        $connection.Close()
        throw 'Exactly one QDOS principal and sequence lineage is required; refusing.'
    }
    $qdosSequence = $qdosRows[0]
    $qdosSequence | Format-Table SequenceLineageId, Year, LastAllocatedSequence -AutoSize | Out-String | Write-Output
    Write-Output ("Next QDOS reference after reset: QDOS{0:00}001" -f ($qdosSequence.Year % 100))
}

$sequenceSnapshotSql = @"
SELECT N'CaseSequences' AS SequenceTable,
    CONVERT(nvarchar(36), SequenceLineageId) + N'/' + CONVERT(nvarchar(4), [Year]) AS SequenceKey,
    CONVERT(nvarchar(20), LastAllocatedSequence) AS SequenceValue
FROM dbo.CaseSequences
UNION ALL
SELECT N'ImageIntakeSequences', NormalizedVehicleRegistration,
    CONVERT(nvarchar(20), LastAllocatedSequence)
FROM dbo.ImageIntakeSequences
UNION ALL
SELECT N'TriageSequences', CONVERT(nvarchar(11), Id),
    CONVERT(nvarchar(20), LastAllocatedSequence)
FROM dbo.TriageSequences
UNION ALL
SELECT N'UnidentifiedSequences', CONVERT(nvarchar(11), Id),
    CONVERT(nvarchar(20), LastAllocatedSequence)
FROM dbo.UnidentifiedSequences
"@
$sequencesBefore = Invoke-Query $sequenceSnapshotSql
$sequenceBeforeValues = @($sequencesBefore | Where-Object {
    -not ($ResetTestEstate -and $_.SequenceTable -eq 'CaseSequences' -and
        $_.SequenceKey -eq ("{0}/{1}" -f $qdosSequence.SequenceLineageId, $qdosSequence.Year))
} | ForEach-Object {
    "{0}|{1}|{2}" -f $_.SequenceTable, $_.SequenceKey, $_.SequenceValue
} | Sort-Object)
$valuationPresetRowsBefore = (Invoke-Query 'SELECT COUNT(*) AS ValuationPresetRows FROM dbo.ValuationPresets').ValuationPresetRows
Write-Output ("Valuation preset rows before: {0}" -f $valuationPresetRowsBefore)

if (-not $Execute) {
    $resetSummary = if ($ResetTestEstate) { ' Accounts and the QDOS counter were not changed.' } else { '' }
    Write-Output ("Dry run only (-Execute not set).{0} Not touched: authentication-ring, box-links, pegtrans252ow37gij, Outlook, Box." -f $resetSummary)
    $connection.Close()
    return
}

$workerState = az resource show --subscription $subscriptionId --resource-group $resourceGroup --name $workerApp --resource-type 'Microsoft.Web/sites' --api-version 2024-04-01 --query properties.state --output tsv
$workerStateReadFailed = $LASTEXITCODE -ne 0
$workerState = "$workerState".Trim()
if ($workerStateReadFailed -or $workerState -ne 'Stopped') {
    $connection.Close()
    throw "Stop $workerApp for the approved maintenance window before executing a wipe."
}
$cutoffUtc = [DateTimeOffset]::UtcNow
$resetMailBoundarySql = Get-Content (Join-Path $PSScriptRoot 'Reset-IntakeMailBoundary.sql') -Raw
$resetTestEstateSql = if ($ResetTestEstate) {
    Get-Content (Join-Path $PSScriptRoot 'Reset-TestEstate.sql') -Raw
} else { '' }
Write-Output ("Mail received before {0:O} will remain excluded after this wipe." -f $cutoffUtc)

Write-Output "`n=== Deleting blobs ==="
if ($blobCount -gt 0) {
    az storage blob delete-batch --account-name $storageAccount --source $container --auth-mode login | Out-Null
    if ($LASTEXITCODE -ne 0) { $connection.Close(); throw 'Blob deletion failed; SQL was not wiped.' }
}
$afterBlobsJson = az storage blob list --account-name $storageAccount --container-name $container --auth-mode login --output json
if ($LASTEXITCODE -ne 0) { $connection.Close(); throw 'Post-delete blob inventory failed; SQL was not wiped.' }
$afterBlobCount = ($afterBlobsJson | ConvertFrom-Json).Count
Write-Output ("Blobs remaining in {0}: {1}" -f $container, $afterBlobCount)
if ($afterBlobCount -ne 0) { $connection.Close(); throw 'Intake blobs remain; SQL was not wiped.' }

Write-Output "`n=== Deleting SQL rows ==="
$names = $wipe | ForEach-Object { "[{0}].[{1}]" -f $_.SchemaName, $_.TableName }
$batch = @()
$batch += $names | ForEach-Object { "ALTER TABLE $_ NOCHECK CONSTRAINT ALL;" }
$batch += $names | ForEach-Object { "DELETE FROM $_;" }
$batch += $resetTestEstateSql
$batch += $names | ForEach-Object { "ALTER TABLE $_ WITH CHECK CHECK CONSTRAINT ALL;" }
$sql = "SET XACT_ABORT ON; BEGIN TRANSACTION;`n" + $resetMailBoundarySql + "`n" + ($batch -join "`n") + "`nCOMMIT TRANSACTION;"
$command = $connection.CreateCommand()
$command.CommandText = $sql
$command.CommandTimeout = 1200
$parameter = $command.Parameters.Add('@CutoffUtc', [System.Data.SqlDbType]::DateTimeOffset)
$parameter.Value = $cutoffUtc
if ($ResetTestEstate) {
    $parameter = $command.Parameters.Add('@QdosSequenceLineageId', [System.Data.SqlDbType]::UniqueIdentifier)
    $parameter.Value = [Guid]$qdosSequence.SequenceLineageId
    $parameter = $command.Parameters.Add('@QdosSequenceYear', [System.Data.SqlDbType]::Int)
    $parameter.Value = [int]$qdosSequence.Year
}
$affected = $command.ExecuteNonQuery()
Write-Output ("Wipe batch committed; rows affected reported: {0}" -f $affected)
Write-Output ("Committed mail cutoff: {0:O}; mailbox approval and activation times unchanged." -f $cutoffUtc)

$after = Invoke-Query "SELECT s.name AS SchemaName, t.name AS TableName, SUM(p.rows) AS Rows
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
JOIN sys.partitions p ON p.object_id = t.object_id AND p.index_id IN (0,1)
GROUP BY s.name, t.name"
$stillHasRows = @($after | Where-Object { $_.TableName -notin $preserveEffective -and $_.Rows -gt 0 })
Write-Output ("Wiped tables still holding rows: {0}" -f $stillHasRows.Count)
$stillHasRows | Format-Table TableName, Rows | Out-String | Write-Output
Write-Output ("Preserved rows after: {0}" -f (($after | Where-Object { $_.TableName -in $preserveEffective }) | Measure-Object -Property Rows -Sum).Sum)
$sequencesAfter = Invoke-Query "SELECT (SELECT MAX(LastAllocatedSequence) FROM CaseSequences) AS CaseSeq, (SELECT COUNT(*) FROM ImageIntakeSequences) AS ImageSeqRows, (SELECT MAX(LastAllocatedSequence) FROM TriageSequences) AS TriageSeq, (SELECT COUNT(*) FROM UnidentifiedSequences) AS UnidSeqRows"
$sequencesAfter | Format-Table | Out-String | Write-Output
$sequencesAfterSnapshot = Invoke-Query $sequenceSnapshotSql
$sequenceAfterValues = @($sequencesAfterSnapshot | Where-Object {
    -not ($ResetTestEstate -and $_.SequenceTable -eq 'CaseSequences' -and
        $_.SequenceKey -eq ("{0}/{1}" -f $qdosSequence.SequenceLineageId, $qdosSequence.Year))
} | ForEach-Object {
    "{0}|{1}|{2}" -f $_.SequenceTable, $_.SequenceKey, $_.SequenceValue
} | Sort-Object)
$sequenceChanges = @(Compare-Object -ReferenceObject $sequenceBeforeValues -DifferenceObject $sequenceAfterValues)
$valuationPresetRowsAfter = (Invoke-Query 'SELECT COUNT(*) AS ValuationPresetRows FROM dbo.ValuationPresets').ValuationPresetRows
Write-Output ("Reference sequence changes: {0}" -f $sequenceChanges.Count)
$sequenceChanges | Format-Table -AutoSize | Out-String | Write-Output
Write-Output ("Valuation preset rows before/after: {0}/{1}" -f $valuationPresetRowsBefore, $valuationPresetRowsAfter)
$resetVerificationFailed = $false
if ($ResetTestEstate) {
    $resetVerification = Invoke-Query "SELECT
    (SELECT COUNT(*) FROM dbo.AspNetUsers) AS UserCount,
    (SELECT COUNT(*) FROM dbo.AspNetUsers WHERE NormalizedUserName = N'ALEX') AS AlexCount,
    (SELECT COUNT(*) FROM dbo.OpenIddictAuthorizations WHERE Subject IN ('$($removedUserIds -join "','")'))
        + (SELECT COUNT(*) FROM dbo.OpenIddictTokens WHERE Subject IN ('$($removedUserIds -join "','")'))
        + (SELECT COUNT(*) FROM dbo.SecurityEvents WHERE SubjectId IN ('$($removedUserIds -join "','")')
            OR ActorSubjectId IN ('$($removedUserIds -join "','")')) AS RemovedAccountTraces,
    (SELECT LastAllocatedSequence FROM dbo.CaseSequences WHERE SequenceLineageId = '$($qdosSequence.SequenceLineageId)' AND [Year] = $($qdosSequence.Year)) AS QdosSequence"
    $resetVerification | Format-Table | Out-String | Write-Output
    $qdosReset = [DBNull]::Value.Equals($resetVerification.QdosSequence) -or $resetVerification.QdosSequence -eq 0
    $resetVerificationFailed = $resetVerification.UserCount -ne 1 -or
        $resetVerification.AlexCount -ne 1 -or
        $resetVerification.RemovedAccountTraces -ne 0 -or
        -not $qdosReset
}
$connection.Close()

if ($sequenceChanges.Count -gt 0 -or $valuationPresetRowsBefore -ne $valuationPresetRowsAfter -or $resetVerificationFailed) {
    throw 'Post-wipe protected-state verification failed; do not resume the Worker.'
}

Write-Output 'Not touched: authentication-ring, box-links, pegtrans252ow37gij, Outlook, Box.'
