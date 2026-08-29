# Checklist — ENG-027

- [x] `CaseValuation` record with source/date/time/mileage/retail/trade
- [x] Closed source vocabulary (Glass's, Cazana, Engineer's Value) in one list
- [x] Save / edit / list-for-case ports + Core policy validation
- [x] Current Engineer's Value query (named seam for ENG-028)
- [x] Infrastructure EF adapter with version/lease/archived/replay guards
- [x] Migration + Web runtime grant in the same diff
- [x] Bootstrap permission census updated in the same diff
- [x] Composition-root registration for all ports
- [x] Core unit tests
- [x] Persistence integration tests (round trip + production composition)
- [x] `Test-MigrationGrants.ps1` passes
- [x] Build: `dotnet build ./Pegasus.slnx --configuration Release` — 0
      warnings, 0 errors (re-run independently)
- [x] No UI, no `OperatorLabels`, no other migration touched
- [x] Simplification pass — see plan's "Disposition" section
