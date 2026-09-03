# Checklist — UIIMP-014 (2026-09-02)

- [ ] Step 1: Verify every wave-5 dependency on merged `origin/dev`, the eleven
  `section-<key>` IDs, `?section=` values, mode markers, OperatorLabels-backed
  labels, Assessment 301, Awaiting instruction state, and Operations notice.
- [ ] Step 2: Add deterministic semantic matchers and focused captures for all
  Case section/mode, Awaiting instruction, and partial-data scenarios; remove
  the Assessment visual scenario.
- [ ] Step 3: Add the seeded Case-record browser walk at 1580, 1100, and 760,
  including every section and the single edit mode (reuse the
  `OperatorJourneyTests` seed by widening it to `internal`, never a copy).
- [ ] Step 4: Update the catalogue, generate the Test UI index/pages, add the
  required states, and convert Assessment to a redirect.
- [ ] Step 5: Complete and record the four-lens simplification pass.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Browser" -- xUnit.MaxParallelThreads=2`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: UIIMP-014
