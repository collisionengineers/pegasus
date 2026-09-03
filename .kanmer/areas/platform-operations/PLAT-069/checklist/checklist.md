# Checklist — PLAT-069 (2026-09-02; revised 2026-09-03 after plan review)

- [ ] Step 1: Refresh from `origin/dev`, confirm
  `src/Pegasus.Web/Pages/Administration/ServiceHealth/` is still absent
  (notice ships anchorless), and take the exact OperatorLabels and
  Operations-snapshot shared-path leases.
- [ ] Step 2: Add and unit-test the Core `Partial`/`Failed` notice predicate,
  including the excluded states and the ignored limit flag.
- [ ] Step 3: Remove the Operations Service health panel; render the
  administrator-only label-only health notice with no anchor; strip the limit
  notice's explanatory sentence; centralize both labels in `OperatorLabels`
  and retain the four `ServiceHealth*Name` helpers for PLAT-051.
- [ ] Step 4: Rewrite the Operations integration assertions — table absent,
  Administrator sees the notice, `[Theory]` proves Engineer and User do not,
  no `href=""` in the response, and a combined limit-plus-health test over a
  configurable `RecordingOperationsStore.LimitReached`.
- [ ] Step 5: Add `operations--partial-data` to the catalogue and its
  `StateMatches` marker, capture it, and commit only Operations snapshot files.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] Simplification pass run over the branch diff; findings and dispositions recorded in the plan under a dated "Simplification pass" heading, including the retention of the four `ServiceHealth*Name` label helpers
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: PLAT-069
