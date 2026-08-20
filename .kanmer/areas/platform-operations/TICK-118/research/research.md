## Backfill research (VERIFY2, 2026-08-20)

Written retrospectively — this capability was implemented and released before this ticket was worked; this records what was found, not what was built.

**Capability rows (docs/capabilities.md):** CASE-13 (separate mandatory staff completeness judgements), CASE-14 (completeness gate before Engineers-queue eligibility), CASE-16 (`Not ready`/`Review`/`Held` workflow), UI-02 (case queues for the three states). All owner FRDs: frd-01 (Lifecycle closure and correspondence), frd-12 (operator experience).

**Core policy owner:** `Cases.InstructionComplete`/`ImagesComplete`/`InstructionConfirmedByStaff`/`ImagesConfirmedByStaff` — contract `src/Pegasus.Core/Cases/CaseContracts.cs:125-133,149`, policy `src/Pegasus.Core/Cases/CaseDataOperations.cs:73-105` (a staff confirmation cannot mark a field complete that Core has not independently determined complete — `InstructionConfirmedByStaff && !InstructionComplete` is rejected at :99/:105).

**Real caller (Web):** `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs:266` `OnPostConfirmCompletenessAsync`, form posted from `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml:258` (`asp-page-handler="ConfirmCompleteness"`). Routes through `IConfirmCompleteness` → the standard `ExecuteCaseCommandAsync` edit-lease/version/operation-key envelope used by every case command on this page.

**Persistence:** `Cases` table columns confirmed on the entity (`src/Pegasus.Infrastructure/Persistence/IntakeAllocationEntities.cs:14-17`) and read/written by `EfCaseDataStore.cs:98-106,201-235,618-621`, `EfCaseAcceptanceStore.cs:277-280,620-623`, `EfIntakeAllocationStore.cs:203-206,260-263`. First introduced in migration `20260729150000_DocumentCustodyAndRequests`.

**Queues:** `src/Pegasus.Web/Pages/Triage/Index.cshtml.cs` — Not ready / Review / Held / Triage / Unidentified tabs; badge from `IDashboardQueries.GetCaseStageCountsAsync` (`EfDashboardQueries.cs`). Confirmed live in production per prod-diagnostics §2 (2 real `CaseWorkflows` rows, both `NotReady`, both instruction-initiated: QDOS26001, QDOS26002).

**File-presence at production (release 13 = 2325ed4a, confirmed by `git show 2325ed4a:<path>`):** `Details.cshtml.cs:266` has `OnPostConfirmCompletenessAsync`; the entity columns and stores above are all present. This capability is deployed to production, not merely merged to `dev`.

**Live read-only SQL check (2026-08-20, prod `pegasus-prod-sql-252ow37gij/pegasus`, AAD access token, no writes):**
```sql
SELECT COUNT(*) AS CaseCount,
       SUM(CASE WHEN InstructionConfirmedByStaff=1 THEN 1 ELSE 0 END) AS InstrConfirmed,
       SUM(CASE WHEN ImagesConfirmedByStaff=1 THEN 1 ELSE 0 END) AS ImagesConfirmed,
       SUM(CASE WHEN InstructionComplete=1 THEN 1 ELSE 0 END) AS InstrComplete,
       SUM(CASE WHEN ImagesComplete=1 THEN 1 ELSE 0 END) AS ImagesComplete
FROM Cases;
```
Result: `CaseCount=2, InstrConfirmed=0, ImagesConfirmed=0, InstrComplete=0, ImagesComplete=0`.

**Gap named honestly:** the caller, policy, persistence, and queue presentation are all implemented and deployed, and match the exact capability text. But no member of staff has yet exercised `ConfirmCompleteness` against a real production case — both production cases (QDOS26001, QDOS26002) remain at `InstructionComplete=0`/`ImagesComplete=0` since creation. This is expected (both cases are recent, real instruction/image work is still outstanding on them) but it means "staff can record both completeness judgements through the live caller" is proved by code+composition, not yet by an observed live use. No further code work is implied — this is an operational residual, not a defect.
