# Plan — DOCS-018 (2026-09-02, gpt-5.6-terra high)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in the shared detached checkout `.worktrees/research` at
`origin/dev` = `897db953`; `git status --porcelain` was empty afterwards. The
Kanmer MCP tools returned only project metadata again this session, so the
board reads were taken from the board worktree files and every write was
confirmed on disk. Spot-checked with my own commands against `origin/dev`,
all confirmed:

- `OnGetPreviewReportDraftAsync` (`Assessment/Index.cshtml.cs:579-594`) maps
  `NotFound` → 404, `NotReady` → `RedirectToPage`, else
  `File(result.Draft!.Assessment.Pdf, "application/pdf")`.
- `AssessmentReportDraft(Assessment, FeeNote)` and
  `RenderedReportArtifact(SuggestedFileName, Pdf, …)` at
  `AssessmentReportRendering.cs:283-291`.
- `AssessmentReportDraftWebTests.FakeRenderer` already returns a `fee-note`
  artifact (lines 286-296) and the suite registers
  `ThrowingDocumentContentStore` (line 298).
- The Preview anchor markup is inline text today (`Index.cshtml:252-256`);
  [[ENG-034]] moves it to `_CaseReport.cshtml` and adds the
  `OperatorLabels.CaseWorkspace.EngineerSections` group, so the label key is
  a one-line addition to that group.
- FRD-11 D42 paragraph (lines 130-133) and the design README sections
  "Voice, labels and necessary copy" (628), "No explanatory copy and page
  economy" (654), "Absent versus disabled" (705).

Two wrapper corrections, both folded into the sections below:

1. **Snapshot commands do apply.** Codex reasoned that a partial is not a
   routed page. But `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`
   (lines 78-95) byte-compares freshly generated captures against the
   committed `docs/design/test-ui/pages/*.html`, and the catalogue's
   `case-details--default` state captures `/Cases/{id}` (Case in Review, edit
   lease held). After [[CASE-038]] → [[ENG-034]] → [[ENG-029]] that capture
   composes `_CaseReport.cshtml`; if the fixture Case is report-ready, the new
   anchor changes the capture and CI's verify lane fails. The implementer
   therefore runs the three snapshot commands and, if any
   `case-details--*.html` capture changes, commits it in the same PR under the
   capacity-one `docs/design/test-ui/**` lease — coordinating with
   [[UIIMP-014]], which owns that path for the wave. No catalogue entry
   changes (no routed page or state is added).
2. **Owned set refined.** The orchestrator's approximate owned paths were
   `_CaseFeeNote.cshtml` and the Infrastructure fee-note template. Research
   verified the template and renderer need no change, and the decision record
   below rejects a one-anchor partial. The `files/files.md` document was
   revised to the four files in "Expected files" (plus the conditional
   snapshot captures); every other path is a named dependency or must-not-touch.

Hand-off settled as option (a) from the research: DOCS-018 is sequenced after
[[ENG-029]] merges and then owns the whole D42 feature itself. [[ENG-029]]'s
plan need not carry anything for the fee note; a scratch note on ENG-029
records this.

## Starting state

VERIFIED (`git rev-parse HEAD`, `git status --short`, and `git log -1
--oneline`): this detached, clean checkout is
`897db9530a45063e8f684f2800685afbfdced006`.

VERIFIED (`git show HEAD:src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`):
`OnGetPreviewReportDraftAsync` calls `GenerateCaseAssessmentReportDraft`,
returns `NotFound` or the existing `NotReady` redirect, and streams
`Draft.Assessment.Pdf` inline.

VERIFIED (`git show HEAD:src/Pegasus.Core/Reports/AssessmentReportRendering.cs`):
`AssessmentReportDraft` already contains `FeeNote`, whose
`RenderedReportArtifact` supplies `SuggestedFileName` and `Pdf`; Core owns the
20% VAT calculation and 89-day fee terms.

VERIFIED (`git show
HEAD:tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs`):
the existing report-web test suite substitutes `IAssessmentReportRenderer` with
`FakeRenderer`, which already returns both assessment and fee-note artifacts,
and registers `ThrowingDocumentContentStore`.

ASSUMED (the board snapshot at planning time; recheck with Kanmer before
taking the ticket): the required order is [[CASE-038]] → [[ENG-034]] →
[[ENG-029]] → [[DOCS-018]]. [[ENG-034]] and [[ENG-029]] must first merge their
Case-page handler surface, Report-section body, and label group to `dev`.

## Governing docs

VERIFIED (`git show
HEAD:docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md`):
FRD-11 requires the Report-section fee-note preview from agreed fee and
description lines; sending remains `MAIL-17`.

VERIFIED (`git show HEAD:docs/design/README.md`): the design authority requires
no explanatory copy and distinguishes unavailable capabilities from record-state
controls.

The implementation changes no governing document. It uses the existing
renderer, contract terms, generic description fallback, and browser PDF viewer;
it does not adopt the mockup's differing payment wording or description.

## Decision record

1. Sequence DOCS-018 after [[ENG-029]] is integrated. DOCS-018 then takes the
   sequential shared-path leases for the exact files below, so it owns the
   complete, production-wired D42 feature rather than a disconnected partial.
   Reconfirm the predecessors, leases, and file hand-off after refreshing from
   `origin/dev`.

2. Add one `Preview fee note` anchor directly beside `Preview report draft` in
   `_CaseReport.cshtml`; do not create `_CaseFeeNote.cshtml`. A partial
   containing one anchor has neither a second caller nor a boundary, whereas
   the existing Report action cluster is the established convention.

3. Mirror the existing report-preview visibility condition. Do not introduce an
   `AssessmentIsReadOnly` exception: the fee-note preview is a read-only GET
   and remains available whenever the existing report-draft preview is
   available, including after Complete if that preview remains available.

## Expected files

| Path | Change | Existing component reused |
| --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Add the fee-note preview GET handler. | `OnGetPreviewReportDraftAsync`; injected `GenerateCaseAssessmentReportDraft`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` | Add one Report action anchor. | Existing report-preview anchor and action cluster. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Add the one `PreviewFeeNote` Report-action label. | [[ENG-034]]'s `CaseWorkspace.EngineerSections` group. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | Add direct fee-note preview coverage. | `Compose`, `FakeRenderer`, and `ThrowingDocumentContentStore`. |
| `docs/design/test-ui/pages/case-details--*.html` | Only if the regenerated capture differs: commit the regenerated file(s). | `Update-TestUiSnapshots.ps1` (UIIMP-005 tooling); capacity-one lease shared with [[UIIMP-014]]. |

## Do not modify

Do not create `src/Pegasus.Web/Pages/Cases/Shared/_CaseFeeNote.cshtml`; it is
intentionally absent. Do not modify Core, Infrastructure, the Scriban fee-note
template, migrations, routed-page markup, shared shell paths, or any
`docs/design/test-ui/**` file other than a `case-details--*.html` capture the
verify run reports as stale (`catalogue.json` is untouched: no routed page or
state is added). No migration grant command applies because no migration is
introduced.

## Steps

### Step 1 — Refresh dependencies and take the shared-file hand-off

- Reconfirm that [[CASE-038]], [[ENG-034]], and [[ENG-029]] have merged, then
  refresh the DOCS-018 worktree from `origin/dev`
  (`git merge --no-edit origin/dev`).
- Take the sequential capacity-one leases for
  `Details.cshtml.cs`, `_CaseReport.cshtml`, `OperatorLabels.cs`, and
  `AssessmentReportDraftWebTests.cs`; confirm [[UIIMP-014]] is not mid-lease
  on `docs/design/test-ui/**` before step 5 commits a capture.
- Reuse the post-[[ENG-034]] `DetailsModel` report-preview host and the
  post-[[ENG-029]] Report action cluster; do not recreate an Assessment-page
  handler or retain a compatibility endpoint.

### Step 2 — Expose the existing fee-note artifact inline

- In `DetailsModel`, add `OnGetPreviewFeeNoteAsync(Guid id,
  CancellationToken cancellationToken)` beside
  `OnGetPreviewReportDraftAsync`.
- Reuse `TryGetActor`, `GenerateCaseAssessmentReportDraft.ExecuteAsync`, and
  the existing `NotFound` and `NotReady` result mapping exactly.
- On a generated draft, return `Draft.FeeNote.Pdf` as `application/pdf`,
  inline. Do not store, queue, send, introduce a port, or add error
  suppression.
- The handler remains read-only and relies on the existing Core readiness and
  authorisation path.

### Step 3 — Wire the Report action through the existing presentation model

- Add `PreviewFeeNote` once to [[ENG-034]]'s
  `OperatorLabels.CaseWorkspace.EngineerSections` vocabulary; do not hard-code
  a second label list in Razor.
- Add one `target="_blank"`/`rel="noopener"` anchor beside the existing
  `Preview report draft` control in `_CaseReport.cshtml`, targeting
  `/Cases/{id}?handler=PreviewFeeNote`.
- Reuse the existing button, icon, URL-generation, and visibility-condition
  markup. Render no dialog, helper copy, disabled placeholder, or extra
  partial.

### Step 4 — Prove the new endpoint is reachable and side-effect free

- Extend `AssessmentReportDraftWebTests` with a complete-case GET to
  `?handler=PreviewFeeNote`, using its existing `Compose`, ready projection,
  `FakeRenderer`, and `ThrowingDocumentContentStore`.
- Assert an OK `application/pdf` response and the fake fee-note PDF bytes.
  The throwing document store proves this preview path does not store an
  artifact; the handler's reused render-only use case proves it does not send.
- Preserve existing incomplete-case failure-closed coverage. Do not duplicate
  renderer VAT tests: existing renderer integration coverage remains the owner
  of fee-note PDF content, including the 20% VAT and total.

### Step 5 — Regenerate and verify the Test UI captures

- Run `./scripts/Update-TestUiSnapshots.ps1`, then
  `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` and
  `./scripts/Test-UiCatalogue.ps1`.
- If `git status` shows a changed `docs/design/test-ui/pages/case-details--*.html`,
  commit it in the same PR (wrapper correction 1). Any other changed file
  under `docs/design/test-ui/**` is not this ticket's: revert it and report.

## Commands

Run from the DOCS-018 task worktree after implementation:

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

Do not run `./scripts/Test-MigrationGrants.ps1`: no migration is introduced.

## Acceptance conditions

- A ready Case's Report section exposes `Preview fee note` beside `Preview
  report draft`, with the same availability condition and new-window browser
  PDF behaviour.
- The fee-note endpoint authorises through the existing path, returns 404 for
  an unavailable Case, preserves the existing not-ready redirect, and returns
  the existing `Draft.FeeNote.Pdf` with `application/pdf` when ready.
- The response is the renderer's fee-note artifact, which already contains the
  agreed fee, description, net, fixed 20% VAT, total, and report-contract fee
  terms.
- The direct Web test proves the fee-note response bytes and that no document
  content store is invoked. No sending, persistence, new renderer, migration,
  template, route, or dependency is added.
- `Update-TestUiSnapshots.ps1 -Verify -SkipCapture` and `Test-UiCatalogue.ps1`
  exit 0 on the branch as pushed.

## Design rules that bind

- No explanatory copy, modal, or alternate HTML preview surface.
- Labels exist only in `src/Pegasus.Web/Presentation/OperatorLabels.cs`;
  exact Case state labels continue to come from `OperatorLabels.CaseStage`.
- The control maps to the named `PreviewFeeNote` handler. A feature unavailable
  through composition is absent; a record-state condition follows the existing
  preview-control convention rather than inventing a disabled seam.
- Existing convention wins: one anchor in the Report action cluster and the
  browser's native PDF viewer. No abstraction is added for one caller.

## Stop condition

Stop when the implementation is committed on the DOCS-018 task branch, the
listed verification commands pass, the post-implementation report is written,
the PR targeting `dev` is open, and the ticket is in Review. Do not merge.

## Simplification pass

(Written by the implementer after the diff exists: dated heading, the four
lenses, findings and dispositions.)
