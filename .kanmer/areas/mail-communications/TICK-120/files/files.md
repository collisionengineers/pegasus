- `src/Pegasus.Core/Tasks/CaseWorkScheduling.cs` — due-work contract, seven-day chase schedule policy (existing, unchanged)
- `src/Pegasus.Core/Tasks/RunDueChasers.cs` — chaser-draft generation (existing, unchanged)
- `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs` — `OnPostRecordManualChaseAsync` staff caller (existing, unchanged)
- `src/Pegasus.Worker/EmailEvidenceFunctions.cs` — `DueWorkSweepFunction` timer composition (existing, unchanged)
- `infra/modules/platform.bicep` — `DueWorkSweepSchedule` app setting (existing, unchanged)

No files changed by this ticket — verification-only backfill against already-shipped code.
