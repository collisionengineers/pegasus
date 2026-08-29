# Checklist — ENG-027

- [x] `CaseValuation` record with source/date/time/mileage/retail/trade
- [x] Closed source vocabulary (Glass's, Cazana, Engineer's Value) in one list
- [x] Save / edit / list-for-case ports + Core policy validation
- [ ] ~~Current Engineer's Value query (named seam for ENG-028)~~ — **deleted in
      round 2** (`d9d32f48` removed `IGetCurrentEngineersValue` and
      `ValuationPolicy.CurrentEngineersValue`). ENG-028 reads the assessment
      projection instead. This box was ticked for a capability that no longer
      exists; unticked and struck through rather than removed, so the record
      shows what happened.
- [x] Infrastructure EF adapter with version/lease/archived/replay guards
- [x] Migration + Web runtime grant in the same diff
- [x] Bootstrap permission census updated in the same diff
- [x] Composition-root registration for all ports
- [x] Core unit tests
- [x] Persistence integration tests (round trip + production composition)
- [x] `Test-MigrationGrants.ps1` passes — 86 migration files checked, every
      created table granted or exempted
- [x] Build: `dotnet build ./Pegasus.slnx --configuration Release` — **corrected
      2026-08-29**. This box previously claimed 0 errors while the measured
      result was exit 1, on the `dev` CS1739 break that was not this lane's
      defect. Rule 20 makes a ticked box contradicting a measured result a record
      defect, so the history is stated rather than quietly overwritten:
      - at review time, on branch head `3ad69881`: **exit 1**, 1 error
        (`ProviderSubmissionTests.cs:284`, CS1739 — inherited from `origin/dev`)
      - after merging `origin/dev` at `55e23b02`, which carries [[DELIV-035]]'s
        fix: **Build succeeded, 0 Error(s)**, re-run by the orchestrator
- [x] No UI, no `OperatorLabels`, no other migration touched
- [x] Simplification pass — rounds 1–2 in the plan's "Disposition" section;
      round 3 added below

## Rule 14 status — merge-eligible, NOT Done-eligible

Recorded here so nobody walks this ticket to `done` by mistake.

Confirmed independently by the cross-model reviewer: of the capabilities this
ticket names, **only the table plus its runtime grant has a production caller
today**. `ISaveValuation`, `IEditValuation`, `IListCaseValuations` and the
`valuation_created` / `valuation_updated` history events are registered at
`src/Pegasus.Infrastructure/DependencyInjection.cs:340-342` with **no reachable
consumer**. The Engineer's Value write side fires only from those ports, so no
operator action populates `assessment.values.engineer` yet — while three real
production consumers already *read* it
(`AiJobOperations.cs:304`, `AssessmentReportProjection.cs:194`,
`Pages/Cases/Assessment/Index.cshtml.cs:165`).

Under decision D20 that is a "No" row: this ticket merges to `dev`, then waits in
`verifying` until **CASE-029** (Valuations tab) or **ENG-028** wires the entry
point.
