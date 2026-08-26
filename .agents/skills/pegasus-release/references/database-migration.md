# Database migration

Use this route only when the approved manifest carries a migration identity not
present in the deployed release.

1. Run the manifest- and environment-bound gate:

   ```powershell
   pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreMigration `
     -Environment $releaseEnvironment -ManifestPath $manifestPath `
     -ManifestSha256 $manifestSha256
   ```

2. Run `efbundle.exe` from `src/Pegasus.Web` with the Production process
   environment required by `docs/runbook.md` under **Release artifacts and
   bootstrap**. Use `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` and the
   approved azd environment values. The current Web host also requires
   `Graph__BaseUri=https://graph.microsoft.com/v1.0/`, the approved tenant as
   `Graph__TenantId`, and a non-empty placeholder
   `Graph__ChangeNotificationClientState`; the bundle constructs the host but
   does not start the webhook. `Box__ConfigJson` must be shape-valid Box JWT
   JSON even though placeholder strings are used. Pass only `--connection` to
   the bundle.

3. Reconcile and verify the runtime principals and exact permission census:

   ```powershell
   pwsh ./scripts/Invoke-AzureDatabaseBootstrap.ps1 `
     -Environment $releaseEnvironment -ManifestPath $manifestPath `
     -ManifestSha256 $manifestSha256
   ```

4. Verify the live migration head equals `migrationIdentity` in the manifest.
   Stop before Web/Worker deployment on any mismatch.

Run `Invoke-ProductionAdministratorBootstrap.ps1` only when the release changes
administrator bootstrap behavior or the approved task explicitly requires
administrator reconciliation. It is not a routine migration step.
