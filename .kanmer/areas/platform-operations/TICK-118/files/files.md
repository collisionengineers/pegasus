- `src/Pegasus.Core/Cases/CaseContracts.cs` — completeness contract (existing, unchanged)
- `src/Pegasus.Core/Cases/CaseDataOperations.cs` — completeness confirmation policy (existing, unchanged)
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` — `OnPostConfirmCompletenessAsync` (existing, unchanged)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` — completeness confirmation form (existing, unchanged)
- `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs` / `.cshtml` — Review/Not ready/Held/Triage/Unidentified queues (existing, unchanged)
- `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`, `EfCaseAcceptanceStore.cs`, `EfIntakeAllocationStore.cs` — persistence (existing, unchanged)

No files changed by this ticket — it is a verification-only backfill against already-shipped code.
