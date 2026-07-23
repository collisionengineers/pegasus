[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

foreach ($name in 'AZURE_SQL_SERVER_FQDN', 'AZURE_SQL_DATABASE_NAME', 'WEB_APP_NAME', 'WORKER_IDENTITY_NAME') {
    if (-not (Test-Path "Env:$name")) {
        throw "Required azd output $name is not available."
    }
}

if (-not (Get-Command Invoke-Sqlcmd -ErrorAction SilentlyContinue)) {
    throw 'Invoke-Sqlcmd is required. Install for the current user with: Install-Module SqlServer -Scope CurrentUser'
}

$token = az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv
if ($LASTEXITCODE -ne 0 -or -not $token) {
    throw 'Could not acquire an Azure SQL access token from Azure CLI.'
}

$serverInstance = "tcp:$env:AZURE_SQL_SERVER_FQDN,1433"
$database = $env:AZURE_SQL_DATABASE_NAME

foreach ($applicationName in $env:WEB_APP_NAME, $env:WORKER_IDENTITY_NAME) {
    $escapedName = $applicationName.Replace(']', ']]')
    $escapedLiteral = $applicationName.Replace("'", "''")
    $query = @"
IF DATABASE_PRINCIPAL_ID(N'$escapedLiteral') IS NULL
BEGIN
    CREATE USER [$escapedName] FROM EXTERNAL PROVIDER;
END;
IF IS_ROLEMEMBER(N'db_datareader', N'$escapedLiteral') <> 1
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [$escapedName];
END;
IF IS_ROLEMEMBER(N'db_datawriter', N'$escapedLiteral') <> 1
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [$escapedName];
END;
"@

    Invoke-Sqlcmd -ServerInstance $serverInstance -Database $database -AccessToken $token -Query $query -Encrypt Mandatory -AbortOnError
}

Write-Host 'Azure SQL runtime principals were granted reader/writer access.' -ForegroundColor Green
