# Checklist — PLAT-069 (2026-09-02)

- [ ] Step 1: Refresh from `origin/dev`, verify PLAT-051's endpoint, and take
  the exact OperatorLabels and Operations-snapshot shared-path leases.
- [ ] Step 2: Add and unit-test the Core `Partial`/`Failed` notice predicate.
- [ ] Step 3: Remove the Operations Service health panel and render compliant
  administrator-only notices using centralized labels.
- [ ] Step 4: Replace Operations integration assertions with table absence,
  role gating, and live-link coverage.
- [ ] Step 5: Add, capture, and commit the Operations partial-data snapshot
  state only.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] Simplification pass run over the branch diff; findings and dispositions recorded in the plan under a dated "Simplification pass" heading
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: PLAT-069
