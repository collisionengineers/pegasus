[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Guards against the class of defect fixed by DELIV-012: a migration that
# creates a table but never grants the runtime roles access to it, so the
# first real save fails in production with a SQL permission error. A table
# is satisfied when the same migration file contains a GRANT statement
# naming it, or an explicit "// no-runtime-grant: <Table>" opt-out with a
# reason (for a table a runtime role legitimately never touches, or one
# already granted by an earlier consolidated least-privilege migration).

$migrationsDir = Join-Path $PSScriptRoot '../src/Pegasus.Infrastructure/Persistence/Migrations'
$migrationFiles = Get-ChildItem -LiteralPath $migrationsDir -Filter '*.cs' |
    Where-Object { $_.Name -notlike '*.Designer.cs' -and $_.Name -notlike '*ModelSnapshot.cs' }

$failures = [Collections.Generic.List[string]]::new()

foreach ($file in $migrationFiles) {
    $content = Get-Content -Raw -LiteralPath $file.FullName

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

        if ($content -match "// no-runtime-grant:\s*$tablePattern\b") {
            continue
        }

        $granted = $false
        foreach ($grantStatement in [regex]::Matches($content, 'GRANT[^;]*;')) {
            if ($grantStatement.Value -match "\[$tablePattern\]") {
                $granted = $true
                break
            }
        }

        # A few migrations build their GRANT text from a shared interpolated
        # helper (`$"GRANT {permissions} ON OBJECT::[dbo].[{table}] ..."`),
        # so the table name never appears literally next to the word GRANT.
        # There the table instead appears as the first element of a
        # (Table, Permissions) grant-tuple literal; accept that shape too.
        if (-not $granted -and $content -match 'GRANT') {
            if ($content -match "\(\s*`"$tablePattern`"\s*,\s*`"[A-Z][A-Z, ]*`"\s*\)") {
                $granted = $true
            }
        }

        if (-not $granted) {
            $failures.Add(
                "$($file.Name): table '$table' is created by CreateTable( in Up() but no GRANT in this file names it, " +
                "and there is no '// no-runtime-grant: $table' opt-out marker.")
        }
    }
}

if ($failures.Count -gt 0) {
    throw "Migration runtime-grant check failed:`n$($failures -join "`n")"
}

Write-Output "Test-MigrationGrants: $($migrationFiles.Count) migration files checked, every created table is granted or exempted."
