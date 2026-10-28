[CmdletBinding(DefaultParameterSetName = 'ConnectionString')]
param(
    # A SQL Server connection string the current Windows identity can open,
    # such as the LocalDB one the CI job uses.
    [Parameter(Mandatory, ParameterSetName = 'ConnectionString')][string] $ConnectionString,
    # Or an Azure SQL database read with an access token, as the release
    # bootstrap connects (az account get-access-token --resource https://database.windows.net/).
    [Parameter(Mandatory, ParameterSetName = 'AccessToken')][string] $ServerInstance,
    [Parameter(Mandatory, ParameterSetName = 'AccessToken')][string] $Database,
    [Parameter(Mandatory, ParameterSetName = 'AccessToken')][string] $AccessToken,
    [Parameter(Mandatory)][string] $OutputDirectory
)

# Writes a sorted, normalised catalog of one database, one fact per line, so
# two databases can be compared with a plain diff (the schema baseline, R481):
# permissions.csv (the runtime-role permission record), tables, columns,
# defaults, indexes, foreign keys, check constraints, identity columns, roles,
# every non-table object, and the rows of every table that holds any. Names
# SQL Server generates (default-constraint names) are left out; column order
# is written to its own file and is not part of the comparison.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$usesAccessToken = $PSCmdlet.ParameterSetName -eq 'AccessToken'

function Invoke-CatalogQuery([string] $Query) {
    if ($usesAccessToken) {
        $rows = @(Invoke-Sqlcmd -ServerInstance $ServerInstance -Database $Database -AccessToken $AccessToken -Query $Query -AbortOnError -OutputAs DataRows -ErrorAction Stop |
            ForEach-Object { [string]$_[0] })
    }
    elseif (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue) {
        $rows = @(Invoke-Sqlcmd -ConnectionString $ConnectionString -Query $Query -AbortOnError -OutputAs DataRows -ErrorAction Stop |
            ForEach-Object { [string]$_[0] })
    }
    else {
        $server = ($ConnectionString -split ';' | Where-Object { $_ -match '^\s*(Server|Data Source)\s*=' }) -replace '^[^=]*=\s*', ''
        $database = ($ConnectionString -split ';' | Where-Object { $_ -match '^\s*(Database|Initial Catalog)\s*=' }) -replace '^[^=]*=\s*', ''
        $rows = @(& sqlcmd -S $server -d $database -E -Q "SET NOCOUNT ON; $Query" -h -1 -W -w 65535 |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        if ($LASTEXITCODE -ne 0) { throw "sqlcmd failed for: $Query" }
    }
    $sorted = [Collections.Generic.List[string]]::new()
    foreach ($row in $rows) { $sorted.Add(([string]$row).TrimEnd()) }
    $sorted.Sort([StringComparer]::Ordinal)
    # The comma keeps an empty result an array: a bare empty array returns as nothing.
    return , $sorted.ToArray()
}

function Write-Lines([string] $Name, [AllowEmptyCollection()][AllowNull()][string[]] $Lines) {
    $Lines = @($Lines | Where-Object { $null -ne $_ })
    $path = Join-Path $OutputDirectory $Name
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $path) | Out-Null
    [IO.File]::WriteAllText($path, (($Lines -join "`n") + "`n"), [Text.UTF8Encoding]::new($false))
    Write-Host "$Name`: $($Lines.Count) lines"
}

# 1. The runtime-role permission record. One row per permission the two roles
#    hold or are denied on a table, plus a `none` row for every user table on
#    which neither role holds anything, so the file names every table.
$permissionRows = Invoke-CatalogQuery @"
SELECT CONCAT(
       principal.name COLLATE DATABASE_DEFAULT, N',',
       permission.state COLLATE DATABASE_DEFAULT, N',',
       permission.permission_name COLLATE DATABASE_DEFAULT, N',',
       CASE
           WHEN permission.class = 1 AND permission.minor_id = 0 AND target.object_id IS NOT NULL
               THEN target.name COLLATE DATABASE_DEFAULT
           ELSE CONCAT(N'__UNAPPROVED_SCOPE_', permission.class, N'_', permission.major_id, N'_', permission.minor_id)
       END)
FROM sys.database_permissions AS permission
JOIN sys.database_principals AS principal ON principal.principal_id = permission.grantee_principal_id
LEFT JOIN sys.tables AS target ON target.object_id = permission.major_id
WHERE principal.name IN (N'pegasus_web_runtime_role', N'pegasus_worker_runtime_role')
"@
$userTables = Invoke-CatalogQuery "SELECT name FROM sys.tables WHERE is_ms_shipped = 0"
$coveredTables = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($row in $permissionRows) { [void]$coveredTables.Add($row.Split(',')[3]) }
$noneRows = @($userTables | Where-Object { -not $coveredTables.Contains($_) } | ForEach-Object { "none,,,$_" })
Write-Lines 'permissions.csv' (@('Role,State,Permission,Table') + $permissionRows + $noneRows)

# 2. Schema facts.
Write-Lines 'tables.txt' (Invoke-CatalogQuery "SELECT CONCAT(SCHEMA_NAME(schema_id), N'.', name) FROM sys.tables WHERE is_ms_shipped = 0")
Write-Lines 'columns.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(
    t.name COLLATE DATABASE_DEFAULT, N'|', c.name COLLATE DATABASE_DEFAULT, N'|', ty.name COLLATE DATABASE_DEFAULT, N'|', c.max_length, N'|', c.precision, N'|', c.scale, N'|', c.is_nullable,
    N'|', ISNULL(c.collation_name, N'') COLLATE DATABASE_DEFAULT, N'|', ISNULL(cc.definition, N'') COLLATE DATABASE_DEFAULT, N'|', ISNULL(cc.is_persisted, 0),
    N'|', c.is_identity, N'|', c.is_rowguidcol)
FROM sys.columns AS c
JOIN sys.tables AS t ON t.object_id = c.object_id AND t.is_ms_shipped = 0
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.computed_columns AS cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
"@)
Write-Lines 'column-order.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(t.name COLLATE DATABASE_DEFAULT, N'|', RIGHT(CONCAT(N'000', c.column_id), 3), N'|', c.name COLLATE DATABASE_DEFAULT)
FROM sys.columns AS c JOIN sys.tables AS t ON t.object_id = c.object_id AND t.is_ms_shipped = 0
"@)
Write-Lines 'defaults.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(t.name COLLATE DATABASE_DEFAULT, N'|', c.name COLLATE DATABASE_DEFAULT, N'|', d.definition COLLATE DATABASE_DEFAULT)
FROM sys.default_constraints AS d
JOIN sys.columns AS c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
JOIN sys.tables AS t ON t.object_id = d.parent_object_id
"@)
Write-Lines 'indexes.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(
    t.name COLLATE DATABASE_DEFAULT, N'|', i.name COLLATE DATABASE_DEFAULT, N'|', i.type_desc COLLATE DATABASE_DEFAULT, N'|', i.is_unique, N'|', i.is_unique_constraint, N'|', i.is_primary_key,
    N'|', ISNULL(i.filter_definition, N'') COLLATE DATABASE_DEFAULT,
    N'|', STUFF((SELECT CONCAT(N',', c.name COLLATE DATABASE_DEFAULT, CASE WHEN ic.is_descending_key = 1 THEN N' DESC' ELSE N'' END)
                 FROM sys.index_columns AS ic JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                 WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
                 ORDER BY ic.key_ordinal FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N'') COLLATE DATABASE_DEFAULT,
    N'|', ISNULL(STUFF((SELECT CONCAT(N',', c.name COLLATE DATABASE_DEFAULT)
                 FROM sys.index_columns AS ic JOIN sys.columns AS c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                 WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 1
                 ORDER BY c.name FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N''), N'') COLLATE DATABASE_DEFAULT)
FROM sys.indexes AS i
JOIN sys.tables AS t ON t.object_id = i.object_id AND t.is_ms_shipped = 0
WHERE i.type > 0
"@)
Write-Lines 'foreign-keys.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(
    fk.name COLLATE DATABASE_DEFAULT, N'|', pt.name COLLATE DATABASE_DEFAULT, N'|',
    STUFF((SELECT CONCAT(N',', pc.name COLLATE DATABASE_DEFAULT) FROM sys.foreign_key_columns AS fkc JOIN sys.columns AS pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
           WHERE fkc.constraint_object_id = fk.object_id ORDER BY fkc.constraint_column_id FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N'') COLLATE DATABASE_DEFAULT,
    N'|', rt.name COLLATE DATABASE_DEFAULT, N'|',
    STUFF((SELECT CONCAT(N',', rc.name COLLATE DATABASE_DEFAULT) FROM sys.foreign_key_columns AS fkc JOIN sys.columns AS rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
           WHERE fkc.constraint_object_id = fk.object_id ORDER BY fkc.constraint_column_id FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N'') COLLATE DATABASE_DEFAULT,
    N'|', fk.delete_referential_action_desc COLLATE DATABASE_DEFAULT, N'|', fk.update_referential_action_desc COLLATE DATABASE_DEFAULT, N'|', fk.is_not_trusted, N'|', fk.is_disabled)
FROM sys.foreign_keys AS fk
JOIN sys.tables AS pt ON pt.object_id = fk.parent_object_id
JOIN sys.tables AS rt ON rt.object_id = fk.referenced_object_id
"@)
Write-Lines 'check-constraints.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(t.name COLLATE DATABASE_DEFAULT, N'|', ck.name COLLATE DATABASE_DEFAULT, N'|', ck.definition COLLATE DATABASE_DEFAULT, N'|', ck.is_not_trusted, N'|', ck.is_disabled)
FROM sys.check_constraints AS ck JOIN sys.tables AS t ON t.object_id = ck.parent_object_id
"@)
Write-Lines 'identity.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(t.name COLLATE DATABASE_DEFAULT, N'|', ic.name COLLATE DATABASE_DEFAULT, N'|', CONVERT(nvarchar(40), ic.seed_value), N'|', CONVERT(nvarchar(40), ic.increment_value), N'|', ISNULL(CONVERT(nvarchar(40), ic.last_value), N''))
FROM sys.identity_columns AS ic JOIN sys.tables AS t ON t.object_id = ic.object_id AND t.is_ms_shipped = 0
"@)
Write-Lines 'objects.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(type_desc COLLATE DATABASE_DEFAULT, N'|', SCHEMA_NAME(schema_id) COLLATE DATABASE_DEFAULT, N'.', name COLLATE DATABASE_DEFAULT)
FROM sys.objects
WHERE is_ms_shipped = 0 AND type NOT IN ('U', 'PK', 'F', 'D', 'C', 'UQ', 'IT')
"@)
Write-Lines 'roles.txt' (Invoke-CatalogQuery @"
SELECT CONCAT(r.name COLLATE DATABASE_DEFAULT, N'|', ISNULL(o.name, N'') COLLATE DATABASE_DEFAULT, N'|',
    ISNULL(STUFF((SELECT CONCAT(N',', m.name COLLATE DATABASE_DEFAULT) FROM sys.database_role_members AS rm JOIN sys.database_principals AS m ON m.principal_id = rm.member_principal_id
                  WHERE rm.role_principal_id = r.principal_id ORDER BY m.name FOR XML PATH(N''), TYPE).value(N'.', N'nvarchar(max)'), 1, 1, N''), N'') COLLATE DATABASE_DEFAULT)
FROM sys.database_principals AS r
LEFT JOIN sys.database_principals AS o ON o.principal_id = r.owning_principal_id
WHERE r.type = 'R' AND r.is_fixed_role = 0 AND r.name <> N'public'
"@)

# 3. The rows of every table that holds any, so seeded reference data compares.
#    The history table is left out: its rows are the chain, not the schema.
#    Columns whose values differ between two correct builds are left out of
#    the row files and listed in data-ignored-columns.txt: rowversion, a
#    default that generates its value (NEWID, NEWSEQUENTIALID, SYSUTCDATETIME,
#    SYSDATETIMEOFFSET, GETUTCDATE, GETDATE), ConcurrencyStamp, and date-time
#    columns, which seeds stamp at seed time.
$countedTables = Invoke-CatalogQuery @"
SELECT CONCAT(t.name COLLATE DATABASE_DEFAULT, N'|', SUM(p.rows))
FROM sys.tables AS t JOIN sys.partitions AS p ON p.object_id = t.object_id AND p.index_id IN (0, 1)
WHERE t.is_ms_shipped = 0 AND t.name <> N'__EFMigrationsHistory'
GROUP BY t.name
HAVING SUM(p.rows) > 0
"@
Write-Lines 'row-counts.txt' $countedTables
$ignoredColumns = [Collections.Generic.List[string]]::new()
foreach ($entry in $countedTables) {
    $table = $entry.Split('|')[0]
    $columns = Invoke-CatalogQuery @"
SELECT CONCAT(c.name COLLATE DATABASE_DEFAULT, N'|', ty.name COLLATE DATABASE_DEFAULT, N'|', ISNULL(d.definition, N'') COLLATE DATABASE_DEFAULT)
FROM sys.columns AS c
JOIN sys.types AS ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.default_constraints AS d ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
WHERE c.object_id = OBJECT_ID(N'[dbo].[$table]')
"@
    $kept = foreach ($column in $columns) {
        $name, $type, $default = $column.Split('|', 3)
        $reason = if ($type -in @('timestamp', 'rowversion')) { 'rowversion' }
            elseif ($default -match 'NEWID|NEWSEQUENTIALID|SYSUTCDATETIME|SYSDATETIMEOFFSET|GETUTCDATE|GETDATE') { 'generated default' }
            elseif ($name -eq 'ConcurrencyStamp') { 'concurrency stamp' }
            elseif ($type -in @('datetime', 'datetime2', 'datetimeoffset', 'smalldatetime', 'date')) { 'date-time' }
        if ($reason) { $ignoredColumns.Add("$table|$name|$reason") } else { $name }
    }
    $projection = (@($kept) | ForEach-Object { "ISNULL(CONVERT(nvarchar(max), [$_], 1), N'<null>') COLLATE DATABASE_DEFAULT" }) -join ", N'|', "
    Write-Lines "data/$table.txt" (Invoke-CatalogQuery "SELECT CONCAT($projection) FROM [dbo].[$table]")
}
Write-Lines 'data-ignored-columns.txt' $ignoredColumns
Write-Host "Catalog written to $OutputDirectory"
