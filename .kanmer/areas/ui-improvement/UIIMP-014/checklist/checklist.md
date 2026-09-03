# Checklist — UIIMP-014 (2026-09-02; revised 2026-09-03 after plan review)

- [ ] Step 1: Confirm every prerequisite is merged on `origin/dev`, **PLAT-070
  included**, then record the eleven final `section-<key>` ids, `?section=`
  values, jump/lazy and edit/read-only markers, OperatorLabels-backed labels,
  the Assessment 301 (and whether its routed page survives), the Awaiting
  instruction state and the Operations notice.
- [ ] Step 1 gate D44: no `RequireStaffImageReviewBeforeEngineerAssignment`,
  `ImagesReviewedByStaff`, review checkbox, dialog or Workflow configuration
  review panel remains in `src/` or `tests/`; otherwise stop for PLAT-070.
- [ ] Step 1 gate D45: the Damage section, labels and report projection carry
  zone, severity and note only — no damage type; otherwise stop for ENG-036.
  Do not edit the FRD; report the discrepancy to its owner.
- [ ] Step 1 gate D46: the crop control is reachable from the Files image
  viewer and Report image cards without Edit Case; otherwise stop for ENG-031.
- [ ] Step 2: Extend `StateMatch` to require all of several markers, declare
  the 22 Case section/mode scenarios with `case-details--default` folded in as
  Overview read-only, add Awaiting instruction and partial-data matchers and
  focused captures, and remove the Assessment visual scenario.
- [ ] Step 2: Add manifest self-checks in `TestUiSnapshotTests` — non-visual
  entries carry no states; scenarios present and unique; every scenario this
  ticket adds or changes has an explicit matcher.
- [ ] Step 3: Extract one `internal` seeded-browser entry point in
  `OperatorJourneyTests.cs` (wrapping `BrowserCaseDataState`,
  `BrowserAcceptedCaseDataQueries`, `BrowserVehicleEvidenceQueries`,
  `ConfirmedVehicle`, `RepositoryEvaFixture`) — never a copy of the seed.
- [ ] Step 3: Extract `AssertLayoutIntegrityAsync` from the existing
  `LayoutIntegrityTests` body and have the existing route theory call it.
- [ ] Step 3: Add three theory cases (one browser per width, 1580/1100/760)
  walking a **Complete** case read-only (no editable Engineer control) and a
  **Review** case with the lease held (an enabled control per section) —
  66 section visits in total, jump-nav enumerated from the rendered page.
- [ ] Step 3: Assert jump activation, scroll-spy current item, lazy readiness,
  D30 section order and sticky chrome at each section.
- [ ] Step 3: Prove D46 — seed an image, crop reachable from Files and Report
  without Edit Case, saving a crop takes the lease, one curation record.
- [ ] Step 3: Run axe and focus checks once per width on the seeded read-only
  record; leave `AccessibilityTests.AuthenticatedRouteList` unseeded.
- [ ] Step 4: Update the catalogue to 22 Case states plus `unavailable` and
  `conflict`, add `queues--awaiting-instruction` and `operations--partial-data`,
  and either convert the Assessment entry to `redirect` or delete it if
  ENG-034 removed its routed page; generate the index and pages.
- [ ] Step 5: Complete and record the four-lens simplification pass.
- [ ] Step 5: Record the wall-clock time of the fresh `-Verify` run against
  CI's 75-minute `test-ui` budget; report any shortfall to UIIMP-013.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Browser" -- xUnit.MaxParallelThreads=2`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify` (fresh capture — the proof)
- [ ] `./scripts/Test-UiCatalogue.ps1`
- [ ] post-implementation report written
- [ ] PR opened with Kanmer: UIIMP-014
