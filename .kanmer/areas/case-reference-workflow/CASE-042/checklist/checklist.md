# Checklist — CASE-042 (2026-09-02)

- [ ] Preconditions confirmed on `dev`: CASE-032 merged with the extended
      `ImageIntakeSummary` (image count); CASE-038 has landed the
      `OperatorLabels` "Awaiting instruction" entry; Received/Source
      availability recorded (CASE-032 amendment or absent).
- [ ] Step 1: `CaseStageCounts` gains required `AwaitingInstruction`;
      `EfDashboardQueries.GetCaseStageCountsAsync` moves the image-intake
      addend out of `NotReady`; `DashboardBoundaryTests` fake updated; remarks
      and comment rewritten.
- [ ] Step 2: `awaiting` tab added to `Tabs` in the Pre-Case group with the
      central label; `Count` maps it; `RailCountsPageFilter` adds it once;
      `LoadNotReadyAsync` returns formal Cases only; `LoadAwaitingAsync` builds
      rows from the summary with no per-row `ListImagesAsync`.
- [ ] Step 3: Create Case links to `/Cases/Create?receiptId=`; script-free
      Add to an existing case form posts to `OnPostAttachAsync` calling
      `IUploadCaseDecision.AttachAsync`; no Vehicle, no chip, no disabled
      control, no explanatory copy.
- [ ] Step 4: `TriageQueuesWebTests` replaces the INTK-013 merged-count test
      with the split-count, shell-total, Create link and attach assertions;
      `QdosAllocationRecoveryTests` re-run.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`
- [ ] `./scripts/Update-TestUiSnapshots.ps1`
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` (green only
      with UIIMP-014's `queues--awaiting` states; otherwise reported)
- [ ] `./scripts/Test-UiCatalogue.ps1` (same condition)
- [ ] Simplification pass recorded in `plan/` under a dated heading.
- [ ] post-implementation report written (names the Work Centre Not ready
      side effect for UIIMP-008 and any absent Received/Source columns)
- [ ] PR opened with Kanmer: CASE-042
