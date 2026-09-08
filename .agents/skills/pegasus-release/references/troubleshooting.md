# Release troubleshooting and rollback

Use this reference only after the normal route fails or rollback is requested.

| Symptom | Cause and action |
| --- | --- |
| Existing or missing Web revision | The azd digest/suffix is stale or the suffix is not exactly 12 characters. Reset both inputs for this manifest, run `PreProvision`, provision once, then read back the digest and traffic. |
| Provision fails after changing Worker settings | Provision can partially update the Function App before failing on Web. Re-read Worker settings before deciding what remains. |
| Old code is serving | Compare the active revision digest with the approved manifest. Never trust `azd provision` success alone. |
| A Container App setting disappears | `infra/modules/platform.bicep` owns the complete environment array. Declare the setting there; do not repair drift with `az containerapp update --set-env-vars`. |
| Worker crash-loops | Redeploy the approved `worker.zip` with Function App `config-zip`. Never use `azd deploy worker`. |
| Migration host construction fails | Supply the complete Production environment and shape-valid placeholder Box JWT JSON described in the database-migration reference. |
| Runtime feature fails with SQL permission denial | Run the manifest-bound database bootstrap and compare the live runtime permission census before changing application code. |
| App Insights is empty | Check `workspaceCapping.dataIngestionStatus`; compare usage with the currently configured daily cap. Use bounded Container App console-log polling when capped. |
| Build output is locked | Identify the host holding the assembly, then run `dotnet build-server shutdown`; do not delete another task's output. |
| GitHub checkout stalls on a stale merge ref | Close and reopen the affected PR. Re-running the same stale ref does not repair it. |

## Rollback

Use the [canonical recovery procedure](../../../../docs/runbook.md#production-recovery)
for the selected retained artifact and schema. This troubleshooting reference
does not define a second rollback sequence. Stop on an unexpected read-back and
report it; do not improvise unreviewed inputs or data resets.
