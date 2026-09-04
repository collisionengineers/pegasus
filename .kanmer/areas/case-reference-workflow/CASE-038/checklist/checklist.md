# Checklist — CASE-038 (2026-09-02; revised 2026-09-03 after plan review; revised 2026-09-04 after PR review; revised 2026-09-04 snapshot regeneration)

- [x] Step 0: confirm merged PLAT-070 — `git grep -i "ReviewedByStaff\|RequireStaffImageReview\|staff-reviewed"` returns nothing on the branch; stop and report if it does not. (PLAT-070 merged as `60fc84dc0`/#649; the only remaining matches are immutable EF migration snapshots, PLAT-070's own drop migration and an absence assertion — recorded in the report.)
- [x] Step 1: replace the section contract with the one canonical ordered `Key`/`Label`/`Icon` descriptor in `OperatorLabels.CaseWorkspace`, and add the authorized `OnGetSectionAsync` fragment handler reusing the eager `IGetCase` load plus only the section's supplemental query (`IImageIntakeQueries`/`ICaseEvidenceImageQueries` for Files).
- [x] Step 2: render the sticky eleven-host Case frame inside the retained `case-workspace`/`case-context` grid, with the server-side addressed host, Engineer and Sign-off ribbon slots (using the new `OperatorLabels` absent-value member), the horizontal jump-nav, and the four heading-only shells `_CaseDamage`/`_CaseEstimate`/`_CaseSettlement`/`_CaseReport` composed with `model="Model"`.
- [x] Step 2a: ~~rename the inspection form to `case-inspection-address-form`~~ — superseded 2026-09-04 by review finding 3: the Inspection section renders no form of its own and contributes its address control to the one record form (`Pages/Cases/Shared/*` lock exception, now covering `_CaseInspectionAddress.cshtml`, `_CaseWorkflow.cshtml` and the deletion of `_CaseDataHiddenFields.cshtml`).
- [x] Step 2b: render every section server-side with no `data-lazy` placeholder while the viewer holds the edit lease.
- [x] Step 3: add measured sticky geometry, retire `case-section-nav` only, add lazy fragment mounting, make the dialog-open, evidence-viewer and dirty-guard binders one root-scoped idempotent `bind(root)`, narrow the existing Ctrl+S handler to the dirty Case form, and add query jump and scroll-spy.
- [x] Step 4: update Case Details proof and add the seeded three-width Browser scenario with its own local seed helper; assert a lazily mounted Files body opens its evidence viewer and dialogs, and that no staff-review control renders.
- [x] Step 5: apply only the declared mechanical query-key retargets in the six direct test consumers.
- [x] Step 6: regenerate default/conflict snapshots, preserve unavailable byte-identical, correct the Details `default` catalogue branch text, and drop `case-section-nav` from `docs/design/README.md` lines 810 and 436 (nothing else there). Done 2026-09-04 at head `b5f5ccda9`: `case-details--default.html` regenerated (41,040 bytes, was the 3,437-byte Files fragment); `case-details--conflict.html` and `case-details--unavailable.html` unchanged (already correct); `catalogue.json`'s Details `default` branch text already matched the real content and needed no further edit.
- [x] Complete the dated Simplification pass with findings and dispositions.
- [x] Run `dotnet restore ./Pegasus.slnx --locked-mode`. (exit 0, re-run 2026-09-04)
- [x] Run `dotnet build ./Pegasus.slnx --configuration Release --no-restore`. (exit 0, 0 warnings, re-run 2026-09-04)
- [x] Run `dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"`. (exit 0 at `dd72edd66`; 2026-09-04 re-ran Core 1219, Architecture 100 and `CaseDetailsWebTests` 68, all exit 0)
- [x] Run the Browser suite. (exit 0 at `dd72edd66`, 123 passed; 2026-09-04 `LayoutIntegrityTests` re-run 69 passed, exit 0; snapshot capture's browser phase 2026-09-04 also passed 123/123)
- [x] Run `./scripts/Update-TestUiSnapshots.ps1`. (2026-09-04, exit 0 — capture browser 123 passed, capture non-browser 310 passed, snapshot update 1 passed)
- [x] Run `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`. (2026-09-04, exit 0 — 1 passed; the earlier exit 1 root cause — the Files fragment wrongly selected as the Case page's default snapshot, review finding 1 — was already fixed at the cause in the fragment's own routing before this run)
- [x] Run `./scripts/Test-UiCatalogue.ps1`. (2026-09-04, exit 0 — "Test UI catalogue valid: 54 routed sources, 58 prototypes, 0 broken local references.")
- [x] Inspect the regenerated `docs/design/test-ui/pages/case-details--default.html` directly: doctype, `case-sticky`, eleven `id="section-*"` hosts, and record its byte size. (41,040 bytes; begins `<!DOCTYPE html>`; one `class="case-sticky"`; eleven non-title `id="section-<key>"` hosts — damage, engineer-notes, estimate, files, inspection, notes, overview, report, settlement, valuation, vehicle; zero `<img src="#">`. `case-details--conflict.html` likewise: 40,091 bytes, doctype, one `case-sticky`, eleven section hosts, zero `<img src="#">`. `case-details--unavailable.html`: `git diff --stat` shows no change.)
- [x] post-implementation report written (corrected 2026-09-04 for review finding 8; snapshot regeneration appended 2026-09-04)
- [x] PR opened with Kanmer: CASE-038 (#656)

- [x] Review round 2 fixes (2026-09-04): isolated the `form=`-association
  proof in `InspectionAddressOutsideEditFormIsGuardedAndSaved` (finding 1),
  merged `origin/dev` and regenerated the two conflicting Test UI snapshots
  under the capture lock (finding 2), pushed the new head `f3005ea66` so a
  fresh Actions run exists (finding 3). Findings 4 and 5 left accepted per
  the review disposition, no change.
