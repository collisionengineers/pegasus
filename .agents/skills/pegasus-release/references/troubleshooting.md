# Release troubleshooting and rollback

Use this reference only after the normal route fails or rollback is requested.

| Symptom | Cause and action |
| --- | --- |
| Provision fails creating the Web plan or Web App | Read the App Service quota for the effective Web region. A `B1` or aggregate limit of 0 is a quota block, not a transient failure: request the increase, or set `PEGASUS_WEB_LOCATION` to a region whose read-back shows quota when the approval names it, then provision once more. |
| Provision fails after changing Worker settings | Provision can partially update the Function App before failing on Web. Re-read Worker settings before deciding what remains. |
| Old code is serving | `/diagnostics/version` reports the previous SHA: the package deployment did not complete or the site did not restart onto it. Re-run `az webapp deploy ... --restart true` with the approved `web.zip` and wait for the exact release read-back. Never trust `azd provision` success alone; provision changes settings, not the served bytes. |
| `az webapp deploy` is refused (401/403) | Basic publishing credentials are disabled by design. The caller's Entra identity needs write access to the Web App (Contributor or Website Contributor on the site); do not enable basic auth to get past it. |
| Web App is `Stopped` after a destructive release | The route starts it only after the new package is deployed and provision succeeded. Run the same `-Execute` line again: the script reads the migration head and resumes from the stopped site, deploying the approved `web.zip` before any start. |
| A Web App setting disappears | `infra/modules/platform.bicep` owns the complete `appSettings` array. Declare the setting there; do not repair drift with `az webapp config appsettings set`. |
| Health check recycles the site | `/health/ready` failed repeatedly. Read `AppServiceConsoleLogs` and `AppServiceAppLogs` in Log Analytics for the startup failure; a missing Production required key stops the host naming the key. |
| Worker crash-loops | Redeploy the approved `worker.zip` with Function App `config-zip`. Never use `azd deploy worker`. |
| Migration host construction fails | The script builds the complete Production environment with shape-valid placeholders; a missing azd value is named in its refusal (`GLASS_*` base URIs must be set with `azd env set`, since they fall back to `main.parameters.json` defaults when unset). |
| Runtime feature fails with SQL permission denial | Run the manifest-bound database bootstrap and compare the live runtime permission census before changing application code. |
| App Insights is empty | Check `workspaceCapping.dataIngestionStatus`; compare usage with the currently configured daily cap. Use bounded `az webapp log tail` or the `AppServiceConsoleLogs` table when capped. |
| Build output is locked | Identify the host holding the assembly, then run `dotnet build-server shutdown`; do not delete another task's output. |
| GitHub checkout stalls on a stale merge ref | Close and reopen the affected PR. Re-running the same stale ref does not repair it. |

## Rollback

Use the [canonical recovery procedure](../../../../docs/runbook.md#production-recovery)
for the selected retained artifact and schema. This troubleshooting reference
does not define a second rollback sequence. Stop on an unexpected read-back and
report it; do not improvise unreviewed inputs or data resets.
