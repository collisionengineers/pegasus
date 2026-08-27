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
| App Insights is empty | Check `workspaceCapping.dataIngestionStatus`; the workspace may be over its 0.1 GB daily cap. Use bounded Container App console-log polling when capped. |
| Build output is locked | Identify the host holding the assembly, then run `dotnet build-server shutdown`; do not delete another task's output. |
| GitHub checkout stalls on a stale merge ref | Close and reopen the affected PR. Re-running the same stale ref does not repair it. |

## Rollback

Rollback requires separate exact-target Azure approval. Use the retained previous
release manifest and artifacts:

1. Set `PEGASUS_WEB_IMAGE_DIGEST` and a valid unused 12-character revision
   suffix for the retained digest, run `PreProvision`, preview, then provision.
2. Redeploy the retained Worker ZIP with `config-zip`.
3. Do not reverse migrations. The runbook's additive-schema contract governs
   application rollback; a non-additive migration requires its recorded recovery
   decision.
4. Smoke the previous release's exact SHA, version and Worker activation.
5. Record the rollback and reason in both current-state documents.

Stop after one failed recovery attempt and report the exact read-back. Do not
improvise a rebase, force push, database rebuild, unqualified data reset, or a
second deployment with unreviewed inputs.
