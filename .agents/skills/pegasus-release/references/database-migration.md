# Database migration

Use this route only when the approved manifest carries a migration identity not
present in the deployed release.

1. Run the manifest- and environment-bound gate:

   ```powershell
   pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode PreMigration `
     -Environment $releaseEnvironment -ManifestPath $manifestPath `
     -ManifestSha256 $manifestSha256
   ```

2. Run the approved manifest's `efbundle` from `src/Pegasus.Web` with the
   Production process
   environment required by `docs/runbook.md` under **Release artifacts and
   bootstrap**. Use `AZURE_TOKEN_CREDENTIALS=AzureCliCredential` and the
   approved azd environment values. The current Web host also requires
   `Graph__BaseUri=https://graph.microsoft.com/v1.0/`, the approved tenant as
   `Graph__TenantId`, and a non-empty placeholder
   `Graph__ChangeNotificationClientState`; the bundle constructs the host but
   does not start the webhook. `Box__ConfigJson` must be shape-valid Box JWT
   JSON even though placeholder strings are used. Pass only `--connection` to
   the bundle.

   The complete key list is the Production fail-fast array in
   `src/Pegasus.Web/Program.cs` (`ConnectionStrings:Pegasus`,
   `AzureIdentity:WebClientId`, the transport, intake-queue and custody
   storage settings, the three `Graph:*` keys, the six `Box:*` keys including
   `Box:HoldingFolderId`, and the six `Eva:*` keys). Take real values from the
   azd environment and the live Web container's non-secret environment; use
   placeholders only for secrets. The connection string is the live one with
   `Authentication=Active Directory Default` in place of the managed-identity
   clause, so the operator's CLI token is used.

   Each migration runs in its own transaction. A failure inside migration N
   leaves migrations 1..N-1 committed and the still-running previous release
   facing the new schema; read the migration history and the live telemetry
   before deciding how to proceed, and never re-run with altered inputs
   without recording why.

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
