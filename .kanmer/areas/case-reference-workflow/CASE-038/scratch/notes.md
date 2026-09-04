2026-09-02 research wrapper (Claude + gpt-5.6-terra xhigh) started; Codex reads `.worktrees/research` at `cad00be9` (origin/dev).

2026-09-02 research wrapper finished: Codex (gpt-5.6-terra xhigh) exit 0, `.worktrees/research` clean at `cad00be9`; research/ and files/ written with wrapper corrections (Details snapshots ride in this PR; duplicate `case-edit-form` id; `?section=` no-script meaning). No operator questions. Next: kanmer-plan (plan + checklist) — gates for leaving Preparing: plan, checklist.

2026-09-02 plan wrapper (Claude + gpt-5.6-terra xhigh) started; Codex reads `.worktrees/research` at `897db953` (origin/dev, three DELIV-041 commits past the research's `cad00be9`; only `docs/design/README.md` changed among owned paths). Wrapper findings fed to the prompt: DELIV-041 already added the five vocabulary rows (README 808–816) so the README row in files.md is obsolete; D30 section keys (`inspection`, `files`, …) replace the legacy `inspection-address`/`case-files`/`valuations` keys (rule 6, no aliases) and the test callers are listed file:line; ENG-034 handler-host contract to be settled as a plan decision.

2026-09-02 plan wrapper re-entry (Claude): `plan/plan.md` and `checklist/checklist.md` already existed (written 2026-09-03T00:16Z, after the research), so Codex was not re-run. The earlier pass had drafted an ENG-034 reconciliation (ENG-034's plan, written one minute before ours, adopts option A: CASE-038's `DetailsModel` hosts the nine moved Assessment handlers) but was interrupted before writing it. This pass wrote the reconciled plan with the handler host **settled as option B** (CASE-038 supplies the host shape — hosts, fragment convention, four heading-only shells, lease handlers, `AssessmentIsReadOnly`; ENG-034 adds the nine handlers, `OnGetPreviewReportDraftAsync`, the assessment projection and estimates/editor state to `DetailsModel` in the PR that moves the forms), on AGENTS.md rules 1, 2, 8 and 14 and plan sizing. No open-questions doc: the decision is a repo-rule outcome, and CASE-038's deliverable is the same under either option except the handler transplant. **Orchestrator action:** amend ENG-034's plan Steps 1 and 3 (and its option table) so ENG-034 moves the handlers itself; a cross-reference was appended to ENG-034's scratch. Re-verified at `897db953` (clean): Assessment handlers at `Index.cshtml.cs` 529/579/604/668/757/778/817/865/1125 (1382 lines); lease handlers on `DetailsModel` 247–313; `CanOpenAssessment` at `Details.cshtml.cs` 24/165/210 and `Details.cshtml` 274; the four shells do not exist; `_CaseInspectionAddress.cshtml` 71–76 / `_CaseWorkflow.cshtml` 161 duplicate `case-edit-form`. `files/files.md` appended with the four shell rows and the withdrawn `IGetAssessmentWorkspace` reuse. Ticket left in `preparing`; feature leave-preparing gates (research, files, plan, checklist) all present. kanmer MCP calls still return only the project header; every write was confirmed on disk in `.worktrees/kanmer`.

### Option A contingency (not planned; reinstate only if the orchestrator overrides the handler-host decision to A)

### Step 1b — Host the moved Assessment handlers

- **Files:** `Details.cshtml.cs`, `CaseDetailsWebTests.cs`.
- **Reuse:** The handlers verbatim from `Assessment/Index.cshtml.cs`
  (`OnPostGenerateReportDraftAsync` 529, `OnGetPreviewReportDraftAsync`
  579, `OnPostSendToClaudeAsync` 604, `OnPostSaveEstimateAsync` 668,
  `OnPostEditLineAsync` 757, `OnPostDuplicateEstimateAsync` 778,
  `OnPostDiscardEstimateAsync` 817, `OnPostSetCurrentEstimateAsync` 865,
  `OnPostImportEstimateAsync` 1125) with their private helpers and
  constructor dependencies; the existing Core commands they call
  (`EstimatePolicy`, `EstimateTotals`, the import command, `ICreateAiJob`,
  report-draft ports) — no second implementation of any of them; the
  `CaseMutationPageModel` lease helpers already on `DetailsModel`.
- **Change:** Copy the nine handlers onto `DetailsModel`; every mutating
  result redirects to `/Cases/{id}?section=estimate`, carrying `estimate`
  or `dialog` query state where the existing flow requires it;
  `OnGetPreviewReportDraftAsync` keeps its `File(pdf, "application/pdf")`
  / `RedirectToPage` on `NotReady` shape. Lease claim/heartbeat/release are
  not duplicated — the Details ones serve. Add handler-reachability
  coverage to `CaseDetailsWebTests.cs` for each handler (antiforgery, lease
  token, version, operation key, redirect target), reusing
  `AssessmentWorkspaceTestData.Create`, `FakeGetAssessmentAccess` and the
  existing Case stores.
- **Do not:** edit or delete anything in `Pages/Cases/Assessment/**` (that
  is ENG-034's retirement step); render any form that posts to the new
  handlers (ENG-034's partial content).
- **Done when:** Each copied handler answers on `/Cases/{id}?handler=<name>`
  with the same outcome its Assessment twin gives, proven by the new tests;
  the plan records under Simplification pass that the duplication window
  closes in ENG-034.

## 2026-09-04 — orchestrator action needed: Test UI offline-render dependency

PR #656 is open and everything CASE-038 owns is green (restore, build, the
full non-Browser suite, the Browser suite, the snapshot update). One command
fails and it is not this lane's to fix:

`./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` → exit 1,
`Offline image failed to load: pages/case-details--default.html`.

The `default` Case snapshot is captured with the edit lease held, so the
record renders every section including Files. Its instruction-photograph
gallery renders `<img src="/Cases/{id}/Documents/{occ}/{ver}?inline=true">`,
correct in production. `TestUiSnapshotTests.NormalizeAndRewrite` rewrites any
application URL whose route has no *visual* catalogue entry to `#` (a
case-document download is `protocol`), and `VerifyOfflineBrowserRenderAsync`
then asserts every `<img>` has a non-zero `naturalWidth`. Receipt images
escape this because they are inlined as `data:` URLs; case-document images
are not.

Fix belongs in `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`
(UIIMP-005/UIIMP-013; UIIMP-014 owns per-section Case states): inline
case-document images the way receipt images already are, or exclude
`#`-rewritten images from the offline-image assertion. CASE-038 has not
touched that file (AGENTS.md rules 1 and 2, plan failure rules).
