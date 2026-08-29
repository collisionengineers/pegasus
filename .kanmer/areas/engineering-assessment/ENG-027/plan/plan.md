# Plan — ENG-027 Case valuations

## Steps (all delivered in one commit `26c1bbab`)

1. **Core record and ports** — `src/Pegasus.Core/Assessment/Valuations.cs`,
   modelled on `Estimates.cs` (reuses `CaseMutationRequest`, `ActionActor`,
   `CaseLifecycleRules.ValidateMutation`, `RepairSpecificationPolicy
   .RequireEngineer`, `LondonCalendar`). New: the one `ValuationSource`
   vocabulary (Glass's / Cazana / Engineer's Value), `ValuationDetails`/
   `CaseValuation`, save/edit/list ports, and `IGetCurrentEngineersValue` as
   the ENG-028 seam.
2. **Infrastructure adapter** — `EfValuationStore.cs`, reusing the
   Serializable-transaction + operation-key-replay + version/lease/archived
   guard + triple history-write shape from `EfRepairSpecificationStore.cs`.
3. **EF wiring** — entity in `AssessmentEntities.cs`, Fluent config in
   `AssessmentModelConfiguration.cs` (check constraints generated from the
   one `ValuationSources.All` list, not hand-duplicated), `DbSet` in
   `PegasusDbContext.cs`.
4. **Migration + grants + census in the same diff** —
   `20260829095336_CaseValuations`, following `GrantAiJobs`'s
   `IsSqlServer()`/`RequireRuntimeRole` shape, plus the
   `Invoke-AzureDatabaseBootstrap.ps1` census entry (rule 16).
5. **Composition root** — five ports registered as `Scoped` in
   `DependencyInjection.cs`.
6. **Tests** — `ValuationTests.cs` (Core, 5 tests: vocabulary closure,
   validation, save/edit actor + forwarding, current-Engineer's-Value
   tie-break, empty-case-id rejection) and two additions to
   `AssessmentPersistenceIntegrationTests.cs` (persistence/replay/history/
   current-value round-trip, and production-composition resolution).

## Reuse named per step

Every step above names the existing file/pattern it copies; no new
abstraction was introduced beyond the one seam (`IGetCurrentEngineersValue`)
the epic's own wave plan names a concrete future caller for (ENG-028).

## Disposition — findings from my own independent verification pass (2026-08-29)

Verified independently (build, two focused test filters, migration-grants
script, full diff read) rather than trusting codex's self-report; all of its
reported numbers reproduced exactly. No findings required a fix:

1. **Naming**: ticket body says `IRecordCaseValuation`/`IAmendCaseValuation`;
   delivered `ISaveValuation`/`IEditValuation`. **Accept as correct** — matches
   the Estimates convention already in the codebase ("the existing convention
   wins" rail); behaviour is unaffected.
2. **`assessment.values.engineer` write-through** named in the ticket body's
   "What" is not delivered here. **Accept as correct** — it is explicitly
   ENG-028's job per the wave plan and this ticket's own task packet; only the
   read seam (`IGetCurrentEngineersValue`) was in scope, and it is delivered,
   registered, and covered by
   `ValuationPortsResolveFromProductionComposition`.
3. **No Pages/Web caller yet** for any of the five new ports. **Accept as
   correct, not a defect for this ticket** — this is a deliberate wave-3
   (backend) / wave-4 (UI) split named in `waves.md`; CASE-029 and ENG-028
   are the named forthcoming callers. Recorded here so the gap is visible,
   not silently carried.

No out-of-scope defect was found in any file this ticket touches or in
neighbouring files.
