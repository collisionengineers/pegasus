# Checklist — CASE-042 (2026-09-02; revised 2026-09-03 after plan review)

- [ ] Open question 3 (Create Case for an image-initiated record) answered by
      the operator, or the tab confirmed to ship with Add to an existing case
      only. Do not link to `/Cases/Create?receiptId=` — it refuses this receipt.
- [ ] Preconditions confirmed on `dev`: CASE-032 merged with the extended
      `ImageIntakeSummary` carrying aggregate image count, custody **and**
      source (its amendment), or those two projection fields handed to
      CASE-042. Received is `RegisteredAtUtc` and needs nothing from CASE-032.
- [ ] Governing-docs lock handed over for the FRD-12 edit (four sentences at
      `frd-12-operator-experience.md:150, 162-167, 169-171`); if it cannot be
      released, stop and report.
- [ ] Step 1: `CaseStageCounts` gains a required `AwaitingInstruction` placed
      **before** the optional `Complete`; the six callers checked against the
      plan's table (no edit to `Pages/Index.cshtml.cs`);
      `EfDashboardQueries.GetCaseStageCountsAsync` moves the image-intake addend
      out of `NotReady`; count and row read use the one predicate; remarks and
      the stale `Triage/Index.cshtml.cs` comment rewritten.
- [ ] Step 2: `awaiting` tab added to `Tabs` in the Pre-Case group with the
      inline literal label `"Awaiting instruction"` (no `OperatorLabels` edit);
      `Count` maps it; the `Queue switch` gains its `LoadAwaitingAsync` arm;
      `RailCountsPageFilter` adds it once; `LoadNotReadyAsync` returns formal
      Cases only with its dead image branch and doc comment deleted;
      `LoadAwaitingAsync` builds rows from the summary with no per-row
      `ListImagesAsync`; the Chase fact retained; Received = `RegisteredAtUtc`;
      awaiting rows link through `Href(selected:)` so every row's quick detail
      is reachable without script.
- [ ] Step 3: `Cases/IndexModel` derives from `UploadConfirmationPageModel` and
      supplies only `RedirectToSurface`; `OriginReceiptId` plumbed onto the row
      or quick detail; script-free Add to an existing case form; the
      `UploadConfirmationError` block added to `Cases/Index.cshtml` so a refused
      attach is visible; no Vehicle, no chip, no disabled control, no
      explanatory copy, no Create Case control.
- [ ] Step 4: all three Not-ready image tests repurposed
      (`NotReadyRailCountMatchesRowsAcrossBothOrigins`,
      `NotReadyImageRowRendersRetainedImageCountAndChaseState`,
      `NotReadyRendersOneMergedRowListAcrossOrigins`) — none deleted or
      weakened; new assertions for second-row selection without script, a
      successful attach, a refused attach surfacing its error, and the
      linked-but-unmerged count-equals-rows state;
      `QdosAllocationRecoveryTests` re-run unchanged; `/Cases?tab=awaiting`
      added to `Browser/AccessibilityTests`.
- [ ] `dotnet restore ./Pegasus.slnx --locked-mode`
- [ ] `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- [ ] `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus"`
      (or the runbook's two complementary integration filters, both exit codes
      recorded — never a Browser-excluded pass presented as the gate)
- [ ] `./scripts/Update-TestUiSnapshots.ps1` — commit only
      `docs/design/test-ui/pages/queues--default.html` and `queues--empty.html`;
      if it writes anything else under `docs/design/test-ui/**`, stop and report
- [ ] `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` green
- [ ] `./scripts/Test-UiCatalogue.ps1` green
- [ ] Simplification pass recorded in `plan/` under a dated heading.
- [ ] Post-implementation report written: names the Work Centre Not ready side
      effect for UIIMP-008, the `?tab=awaiting` key settled for UIIMP-014, the
      narrow snapshot and governing-docs handoffs taken, and any CASE-032
      projection field CASE-042 had to carry itself.
- [ ] PR opened with Kanmer: CASE-042
