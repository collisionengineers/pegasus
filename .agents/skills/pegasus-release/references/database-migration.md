# Database migration

Use this route only when the approved manifest carries a migration identity not
present in the deployed release. For a destructive migration, the release skill
must already have recorded exact Worker `Stopped` and old Web inactive/zero-replica
read-backs immediately before this recipe. Unknown or stale containment blocks SQL.

1. Run the manifest- and environment-bound gate:

   ```powershell
   pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreMigration `
     -Environment $releaseEnvironment -ManifestPath $manifestPath `
     -ManifestSha256 $manifestSha256
   if ($LASTEXITCODE -ne 0) { throw 'Pre-migration validation failed.' }
   ```

2. Resolve the executable from the validated manifest, not a hardcoded name:

   ```powershell
   $repositoryRoot = (Resolve-Path -LiteralPath '.').Path
   $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
   $migrationBundlePath = Join-Path (Split-Path -Parent (Resolve-Path -LiteralPath $manifestPath)) $manifest.migrationBundleName
   ```

   Run `$migrationBundlePath` (`efbundle.exe` on Windows or `efbundle` on Linux)
   from `src/Pegasus.Web` with this process-local Production host environment.
   It reads the listed approved required azd values, derives the two Azure
   service URIs, and does not print or persist the environment output:

   ```powershell
   $environmentLines = @(& azd env get-values -e $releaseEnvironment --no-prompt)
   if ($LASTEXITCODE -ne 0) { throw 'azd env get-values failed.' }
   $values = @{}
   foreach ($line in $environmentLines) {
     if ($line -match '^([A-Z0-9_]+)=(.*)$') { $values[$Matches[1]] = $Matches[2].Trim('"') }
   }
   $required = @(
     'AZURE_SQL_SERVER_FQDN', 'AZURE_SQL_DATABASE_NAME', 'WEB_IDENTITY_CLIENT_ID',
     'TRANSPORT_STORAGE_ACCOUNT_NAME', 'CUSTODY_STORAGE_ACCOUNT_NAME',
     'AZURE_TENANT_ID', 'BOX_HOLDING_FOLDER_ID', 'EVA_BASE_URI',
     'EVA_REQUEST_FROM', 'EVA_INSPECTION_TYPE', 'EVA_INSTRUCTION_EMAIL')
   $missing = @($required | Where-Object { [string]::IsNullOrWhiteSpace($values[$_]) })
   if ($missing.Count) { throw "azd environment is missing: $($missing -join ', ')" }

   $migrationHost = @{
     ASPNETCORE_ENVIRONMENT = 'Production'
     Runtime__Profile = 'Production'
     AZURE_TOKEN_CREDENTIALS = 'AzureCliCredential'
     ConnectionStrings__Pegasus = "Server=tcp:$($values['AZURE_SQL_SERVER_FQDN']),1433;Database=$($values['AZURE_SQL_DATABASE_NAME']);Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
     AzureIdentity__WebClientId = $values['WEB_IDENTITY_CLIENT_ID']
     TransportStorage__AccountName = $values['TRANSPORT_STORAGE_ACCOUNT_NAME']
     IntakeQueue__ServiceUri = "https://$($values['TRANSPORT_STORAGE_ACCOUNT_NAME']).queue.core.windows.net/"
     CustodyStorage__AccountName = $values['CUSTODY_STORAGE_ACCOUNT_NAME']
     CustodyStorage__ServiceUri = "https://$($values['CUSTODY_STORAGE_ACCOUNT_NAME']).blob.core.windows.net/"
     Graph__BaseUri = 'https://graph.microsoft.com/v1.0/'
     Graph__TenantId = $values['AZURE_TENANT_ID']
     Graph__ChangeNotificationClientState = 'migration-host-placeholder'
     Box__BaseUri = 'https://api.box.com/2.0/'
     Box__UploadUri = 'https://upload.box.com/api/2.0/'
     Box__RootFolderId = '405543781910'
     Box__HoldingFolderId = $values['BOX_HOLDING_FOLDER_ID']
     Box__ConfigJson = '{"boxAppSettings":{"clientID":"migration-host-placeholder","appAuth":{"publicKeyID":"migration-host-placeholder","privateKey":"migration-host-placeholder","passphrase":"migration-host-placeholder"}},"enterpriseID":"migration-host-placeholder"}'
     Box__ClientSecret = 'migration-host-placeholder'
     Eva__BaseUri = $values['EVA_BASE_URI']
     Eva__ClientId = 'migration-host-placeholder'
     Eva__ClientSecret = 'migration-host-placeholder'
     Eva__RequestFrom = $values['EVA_REQUEST_FROM']
     Eva__InspectionType = $values['EVA_INSPECTION_TYPE']
     Eva__InstructionEmail = $values['EVA_INSTRUCTION_EMAIL']
   }
   foreach ($entry in $migrationHost.GetEnumerator()) {
     [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
   }
   ```

   `Graph__ChangeNotificationClientState`, `Box__ConfigJson`,
   `Box__ClientSecret`, `Eva__ClientId`, and `Eva__ClientSecret` above are
   intentionally non-empty process-only placeholders, not azd configuration
   or secrets. The Box value is shape-valid JWT JSON. The migration host builds
   its deferred Box and EVA factories but does not use their external routes.
   Run the resolved bundle with only `--connection`; its native exit code must
   succeed before bootstrap:

   ```powershell
   Push-Location (Join-Path $repositoryRoot 'src/Pegasus.Web')
   try {
     & $migrationBundlePath --connection $env:ConnectionStrings__Pegasus
     if ($LASTEXITCODE -ne 0) { throw 'Migration bundle failed.' }
   }
   finally {
     Pop-Location
   }
   ```

3. Reconcile and verify the runtime principals and exact permission census:

   ```powershell
   pwsh ./scripts/Invoke-AzureDatabaseBootstrap.ps1 `
     -Environment $releaseEnvironment -ManifestPath $manifestPath `
     -ManifestSha256 $manifestSha256
   if ($LASTEXITCODE -ne 0) { throw 'Database bootstrap failed.' }
   ```

4. Verify the live migration head equals `migrationIdentity` in the manifest.
   Stop before Web/Worker activation on any mismatch.

For additive migrations, finish this migration boundary before provisioning Web
or deploying the Worker package. The destructive route's disabled new-Worker
staging exception is defined solely in the release skill; it never authorizes an
old package or old Web revision to resume after SQL begins.

Run `Invoke-ProductionAdministratorBootstrap.ps1` only when the release changes
administrator bootstrap behavior or the approved task explicitly requires
administrator reconciliation. It is not a routine migration step.
