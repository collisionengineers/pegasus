[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Guards against the class of defect fixed by DELIV-012: a migration that
# creates a table but never grants the runtime roles access to it, so the
# first real save fails in production with a SQL permission error. A table
# is satisfied when ANY migration file in this folder contains a GRANT
# statement naming it (the grant does not have to live in the same file as
# the CreateTable( -- a follow-up grant-only migration is exactly how
# DELIV-012 itself closed one of these), or when the file that creates it
# carries an explicit "// no-runtime-grant: <Table>" opt-out with a reason
# (for a table a runtime role legitimately never touches).

$migrationsDir = Join-Path $PSScriptRoot '../src/Pegasus.Infrastructure/Persistence/Migrations'
$migrationFiles = Get-ChildItem -LiteralPath $migrationsDir -Filter '*.cs' |
    Where-Object { $_.Name -notlike '*.Designer.cs' -and $_.Name -notlike '*ModelSnapshot.cs' }

# Read every file once. The GRANT search runs over the whole folder, because
# the grant that satisfies a table does not have to live in the same
# migration file that created the table.
$fileContents = [Ordered]@{}
foreach ($file in $migrationFiles) {
    $fileContents[$file.FullName] = Get-Content -Raw -LiteralPath $file.FullName
}

function Test-TableGranted([string] $Table) {
    $tablePattern = [regex]::Escape($Table)
    foreach ($content in $fileContents.Values) {
        if ($content -notmatch 'GRANT') {
            continue
        }

        foreach ($grantStatement in [regex]::Matches($content, 'GRANT[^;]*;')) {
            if ($grantStatement.Value -match "\[$tablePattern\]") {
                return $true
            }
        }

        # A few migrations build their GRANT text from a shared interpolated
        # helper (`$"GRANT {permissions} ON OBJECT::[dbo].[{table}] ..."`),
        # so the table name never appears literally next to the word GRANT.
        # There the table instead appears as the first element of a
        # (Table, Permissions) grant-tuple literal; accept that shape too.
        if ($content -match "\(\s*`"$tablePattern`"\s*,\s*`"[A-Z][A-Z, ]*`"\s*\)") {
            return $true
        }
    }
    return $false
}

$failures = [Collections.Generic.List[string]]::new()

foreach ($file in $migrationFiles) {
    $content = $fileContents[$file.FullName]

    # Only tables created by Up() need a grant. CreateTable( can also appear
    # in Down() when it reverses an earlier DropTable(); that recreated table
    # is not a new object in the live database and needs nothing here.
    $upMatch = [regex]::Match(
        $content,
        'protected override void Up\(MigrationBuilder migrationBuilder\)(?<body>.*?)(?=protected override void Down\(|\z)',
        [Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $upMatch.Success) {
        continue
    }
    $upBody = $upMatch.Groups['body'].Value

    $tables = [regex]::Matches($upBody, 'CreateTable\(\s*name:\s*"(?<table>[A-Za-z0-9]+)"') |
        ForEach-Object { $_.Groups['table'].Value } |
        Select-Object -Unique

    foreach ($table in $tables) {
        $tablePattern = [regex]::Escape($table)

        # The opt-out marker is a deliberate declaration by the migration
        # that creates the table, so it is only honoured in that same file --
        # a marker elsewhere could not speak to whether this table needs one.
        if ($content -match "// no-runtime-grant:\s*$tablePattern\b") {
            continue
        }

        if (Test-TableGranted $table) {
            continue
        }

        $failures.Add(
            "$($file.Name): table '$table' is created by CreateTable( in Up() but no migration in this folder GRANTs it, " +
            "and there is no '// no-runtime-grant: $table' opt-out marker in this file.")
    }
}

if ($failures.Count -gt 0) {
    throw "Migration runtime-grant check failed:`n$($failures -join "`n")"
}

Write-Output "Test-MigrationGrants: $($migrationFiles.Count) migration files checked, every created table is granted or exempted."
