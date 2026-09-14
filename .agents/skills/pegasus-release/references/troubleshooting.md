# Release troubleshooting and rollback

Use this reference only after the normal route fails or rollback is requested.

| Symptom | Cause and action |
| --- | --- |
| Provision fails creating the Web plan or Web App | Read the App Service quota for the effective Web region (`PreProvision` prints it). A `B1` or aggregate limit of 0 is a quota block, not a transient failure: request the increase or set `PEGASUS_WEB_LOCATION` per the skill's cutover section, then provision once more. |
| Provision fails after changing Worker settings | Provision can partially update the Function App before failing on Web. Re-read Worker settings before deciding what remains. |
| Old code is serving | `/diagnostics/version` reports the previous SHA: the package deployment did not complete or the site did not restart onto it. Re-run `az webapp deploy ... --restart true` with the approved `web.zip` and wait for the exact release read-back. Never trust `azd provision` success alone; provision changes settings, not the served bytes. |
| `az webapp deploy` is refused (401/403) | Basic publishing credentials are disabled by design. The caller's Entra identity needs write access to the Web App (Contributor or Website Contributor on the site); do not enable basic auth to get past it. |
| Web App is `Stopped` after a destructive release | Section 10 starts it only after the new package is deployed and provision succeeded. Read `az webapp show --query state`; if the package deployment record is not the approved `web.zip`, deploy it before starting. |
| A Web App setting disappears | `infra/modules/platform.bicep` owns the complete `appSettings` array. Declare the setting there; do not repair drift with `az webapp config appsettings set`. |
| Health check recycles the site | `/health/ready` failed repeatedly. Read `AppServiceConsoleLogs` and `AppServiceAppLogs` in Log Analytics for the startup failure; a missing Production required key stops the host naming the key. |
| Worker crash-loops | Redeploy the approved `worker.zip` with Function App `config-zip`. Never use `azd deploy worker`. |
| Migration host construction fails | Supply the complete Production environment and shape-valid placeholder Box JWT JSON described in the database-migration reference. |
| Runtime feature fails with SQL permission denial | Run the manifest-bound database bootstrap and compare the live runtime permission census before changing application code. |
| App Insights is empty | Check `workspaceCapping.dataIngestionStatus`; compare usage with the currently configured daily cap. Use bounded `az webapp log tail` or the `AppServiceConsoleLogs` table when capped. |
| Build output is locked | Identify the host holding the assembly, then run `dotnet build-server shutdown`; do not delete another task's output. |
| GitHub checkout stalls on a stale merge ref | Close and reopen the affected PR. Re-running the same stale ref does not repair it. |

## Rollback

Use the [canonical recovery procedure](../../../../docs/runbook.md#production-recovery)
for the selected retained artifact and schema. This troubleshooting reference
does not define a second rollback sequence. Stop on an unexpected read-back and
report it; do not improvise unreviewed inputs or data resets.
