# Files — MAIL-14 (backfill; all present on origin/main = 2325ed4a ancestor path)

- `src/Pegasus.Core/Workflow/PollSentEvidence.cs` — poll, approval check, exact detection, outcome recording
- `src/Pegasus.Infrastructure/Persistence/EfSentEvidencePollStore.cs` — lease/cursor/poll state
- `src/Pegasus.Infrastructure/Persistence/EfSentEvidencePollOutcomeQueries.cs` — outcome queries
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260729160000_CaseWorkflowRuntime.cs` — `CaseReportSentEvidence` table
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260729183000_SentEvidencePolling.cs` — poll state/outcome tables
- `src/Pegasus.Worker/EmailEvidenceFunctions.cs` — `SentEvidencePollFunction` timer caller (schedule `15 * * * * *`)
- `tests/Pegasus.Core.Tests/Workflow/PollSentEvidenceTests.cs`
- `tests/Pegasus.IntegrationTests/SentEvidencePollPersistenceTests.cs`

Post-release hardening on dev only (not on main yet): commit `c432bc9a` touching `PollSentEvidence.cs` (MAIL-003).
