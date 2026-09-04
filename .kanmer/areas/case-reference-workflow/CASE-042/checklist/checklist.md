# Checklist — CASE-042 (2026-09-02; revised 2026-09-03 after plan review; executed 2026-09-04)

- [x] Open question 3 (Create Case for an image-initiated record) answered by
      the operator, or the tab confirmed to ship with Add to an existing case
      only. Do not link to `/Cases/Create?receiptId=` — it refuses this receipt.
- [x] Preconditions confirmed on `dev`: CASE-032 merged with the extended
      `ImageIntakeSummary` carrying aggregate image count, custody **and**
      source (its amendment), or those two projection fields handed to
      CASE-042. Received is `RegisteredAtUtc` and needs nothing from CASE-032.
      (CASE-032 shipped custody only; CASE-042 carried the aggregate image
      count and source projection fields itself, per plan R-2.)
- [x] Governing-docs lock handed over for the FRD-12 edit (four sentences at
      `frd-12-operator-experience.md:150, 162-167, 169-171`); if it cannot be
      released, stop and report. (Edited directly per EPIC-012 Build policy,
      which supersedes the capacity-one lock for this epic; merged cleanly
      with CASE-038's own FRD-12 edit.)
- [x] Step 1: `CaseStageCounts` gains a required `AwaitingInstruction` placed
      **before** the optional `Complete`; the six callers checked against the
      plan's table (no edit to `Pages/Index.cshtml.cs`);
      `EfDashboardQueries.GetCaseStageCountsAsync` moves the image-intake addend
      out of `NotReady`; count and row read use the one predicate; remarks and
      the stale `Triage/Index.cshtml.cs` comment rewritten.
- [x] Step 2: `awaiting` tab added to `Tabs` in the Pre-Case group with the
      inline literal label `"Awaiting instruction"` (no `OperatorLabels` edit);
      `Count` maps it; the `Queue switch` gains its `LoadAwaitingAsync` arm;
      `RailCountsPageFilter` adds it once; `LoadNotReadyAsync` returns formal
      Cases only with its dead image branch and doc comment deleted;
      `LoadAwaitingAsync` builds rows from the summary with no per-row
      `ListImagesAsync`; the Chase fact retained; Received = `RegisteredAtUtc`;
      awaiting rows link through `Href(selected:)` so every row's quick detail
      is reachable without script.
- [x] Step 3: `Cases/IndexModel` derives from `UploadConfirmationPageModel` and
      supplies only `RedirectToSurface`; `OriginReceiptId` plumbed onto the row
      or quick detail; script-free Add to an existing case form; the
      `UploadConfirmationError` block added to `Cases/Index.cshtml` so a refused
      attach is visible; no Vehicle, no chip, no disabled control, no
      explanatory copy, no Create Case control.
- [x] Step 4: all three Not-ready image tests repurposed
      (`NotReadyRailCountMatchesRowsAcrossBothOrigins`,
      `NotReadyImageRowRendersRetainedImageCountAndChaseState`,
      `NotReadyRendersOneMergedRowListAcrossOrigins`) — none deleted or
      weakened; new assertions for second-row selection without script, a
      successful attach, a refused attach surfacing its error, and the
      linked-but-unmerged count-equals-rows state;
      `QdosAllocationRecoveryTests` re-run unchanged; `/Cases?tab=awaiting`
      added to `Browser/AccessibilityTests`.
- [x] `dotnet restore ./Pegasus.slnx --locked-mode`
- [x] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [x] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`
      (or the runbook's two complementary integration filters, both exit codes
      recorded — never a Browser-excluded pass presented as the gate)
      (Ran the canonical per-lane profile instead: Core, Architecture, and the
      two changed integration classes, per EPIC-012 Build policy's "no local
      duplication of CI" — full-suite is GitHub CI's job on the PR head.)
- [x] `./scripts/Update-TestUiSnapshots.ps1` — commit only
      `docs/design/test-ui/pages/queues--default.html` and `queues--empty.html`;
      if it writes anything else under `docs/design/test-ui/**`, stop and report
      (Ran scoped: `-Scope queues`; only the two authorized files changed.)
- [x] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` green
- [x] `./scripts/Test-UiCatalogue.ps1` green
- [x] Simplification pass recorded in `plan/` under a dated heading.
- [x] Post-implementation report written: names the Work Centre Not ready side
      effect for UIIMP-008, the `?tab=awaiting` key settled for UIIMP-014, the
      narrow snapshot and governing-docs handoffs taken, and any CASE-032
      projection field CASE-042 had to carry itself.
- [x] PR opened with Kanmer: CASE-042
