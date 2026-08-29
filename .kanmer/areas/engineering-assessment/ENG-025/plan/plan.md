# Plan — ENG-025

Scope: wave-2 lane F port of `Pages/Cases/Assessment/**` to context.md §1.9,
plus the D11 Core access-policy change. Build-only verification (no tests,
snapshots, browser runs — orchestrator owns the wave loop).

## Step 0 — degrade point (decided, recorded)

ENG-026 (#595) is not on this branch's base and merging `origin/dev` is a
no-op (research §1), so the estimate surfaces render on the seams that DO
exist: the repair specification (accepted/draft), the estimate import
(Audatex PDF), and the Engineer acceptance. The contract's estimate tabs
(tablist), New estimate, Delete/Duplicate/Use estimate/Save estimate,
per-estimate fields/lines/totals editor, and the import dialog's name/source
fields have no Core use case on this base and would be inert controls —
forbidden by context.md — so they wait for ENG-026/ENG-028 and are named in
the PR. RecordEngineerFinding stays UI-less (contract does not draw it).
The four old inline scripts need no site.js block: their surfaces are either
removed (readiness hover, PAV slider, embers) or already carried by site.js
(`data-dialog-open` replaces the hand-rolled send-confirm focus trap).

## Step 1 — Core D11 (commit 1)

`AssessmentWorkspace.cs`:

- `AssessmentAccessPolicy.CanOpen`: state ∈ {ReportPreparation, PostReport,
  PostReportComplete} AND `LatestExportVersion >= LatestReviewVersion`
  (unchanged export clause; never Review/NotReady/Held/other terminals).
- `AssessmentAccessState.IsReadOnly` => State == PostReportComplete (D11
  read-once-complete; FRD-11). Core owns both rules.

`AssessmentPolicyTests`: rewrite the access theory rows for the new set
(Review rows now false; PostReport true with current export;
PostReportComplete true + read-only; Held/NotReady false; stale export
false). Reused: existing theory shape.

## Step 2 — page model (commit 2, `Index.cshtml.cs`)

Reused unchanged: `CaseMutationPageModel`, lease trio
(Claim/Heartbeat/Release + `RestoreEditModeAsync`), `CanAccessAsync`
(now backed by the D11 policy), ImportEstimate + AcceptSpecification
handlers, `ReportDraftPreparation`, operation-key discipline.

Changes:

- OnGet: access first → if !CanOpen render the unavailable surface
  (h1 "Assessment unavailable", warning notice naming the export condition,
  Back to Case; ref from `IGetCase` — reused). Workspace 404 only when the
  case itself is unknown. Load instruction docs (`CaseFiles.Live` on
  `details.Documents`, role Instruction) + evidence images
  (`ICaseEvidenceImageQueries` — same queries Details uses) for the rail.
- Ribbon data (7 items): ref/reg/claimant/principal-code/state-chip from the
  workspace header + `details.Summary.Claimant`; Mileage via the existing
  saved→confirmed→DVSA cascade (`MileagePrefill` logic, reused); Vehicle
  make/model via the existing `VehiclePrefill` evidence cascade.
- `OnPostSendToClaudeAsync`: `ICreateAiJob` with `AiJobKind.Estimate`,
  direction textarea → Instruction (default sentence when empty), target
  percent (int 1..100), surfacing Core's refusal messages (engineer value /
  state / kill switch) through TempData. Replaces the old Send/Reconcile
  handlers and the `ISendCaseToAi` panel-state machine (AUTO-011 superseded
  it; the ledger's Review estimate action lives in Operations, PLAT-023).
- `OnGetPreviewReportDraftAsync`: same `GenerateCaseAssessmentReportDraft`
  seam, inline PDF disposition (browser viewer is the preview; no site.js
  iframe-on-open pattern exists — recorded).
- Remove: `OnPostSaveDamageAsync` (its only caller, the report-section
  diagram, is not drawn by §1.9; impact location stays writable through
  `ISaveAssessment`), section routing, PAV slider data, panel-state
  machinery, unused prefill helpers.

## Step 3 — page view (commit 2, `Index.cshtml`)

Per §1.9 final layer, reusing Details.cshtml's established patterns:

- `page-header`: h1 "Assessment", eyebrow "REF · reg", actions Back to Case
  + Refresh (`data-refresh-form`).
- Notices: TempData status/error via the shell notice classes.
- 7-item `record-ribbon` (+ `assessment-identity-ribbon` 7-col variant is
  CSS-implicit at 7 items; class only if site.css names it — it does not, so
  plain `record-ribbon` with 7 items).
- Presence strip (viewer editing / holder) as Details renders it.
- `record-bar`: edit-lease controls (Edit assessment / Finish editing /
  Recover / held-by — real handlers the import/accept need) → Import
  estimate (opens dialog; disabled with condition when read-only,
  non-Engineer, or a draft already exists) → Glass's, Audatex (D7 disabled
  seams, EXT-09, `aria-disabled`, no handler) → Send to Claude (primary,
  opens dialog; disabled with condition read-only / no confirmed
  Engineer's Value) → `record-bar-end`: Generate report draft (POST,
  download) + Preview report draft (GET inline) or one disabled control
  naming the outstanding count when not ready.
- `assessment-v3` grid: `aside.pane.assessment-v3-evidence` (pane-head h2
  Evidence + `[data-rail-toggle]`; `data-evidence-set` list of
  `[data-evidence-item]` row anchors — Instruction docs then images, both
  through the authorised download-inline routes) | `main.pane
  .assessment-v3-main` (pane-head h2 Estimates; pane-scroll detail-canvas):
  accepted/draft specification panel with lines table + basis (reusing the
  existing `RenderSpecificationLines` renderer and accept form) or the
  empty "No estimates recorded." (page economy: no empty panels).
- Dialogs (`dialog-backdrop[data-dialog]` frame): Import estimate (dropzone
  + reason, the real multipart handler; name/source fields wait for
  ENG-026), Send to Claude (direction textarea; `Target Estimate %` range
  with `data-range-output`/`data-range-base`/`data-range-amount-output`;
  Case Valuation + Target amount definitions; Engineer's-Value warning
  notice; disabled confirm without it). `_EvidenceViewer` + 
  `_EditFinishConfirm` partials.

## Step 4 — tests (commit 3)

As per files/map.md. `AssessmentWorkspaceTestData`: fake open state becomes
ReportPreparation (D11). Web tests retargeted: import/accept/lease journeys
assert the new markup anchors; damage/section journeys deleted with their
handlers; prefill cascade asserts ribbon values; browser readiness test
proves the not-ready condition on the report-draft control + a11y clean
(replacing the removed disclosure assertions). No new fixtures beyond the
documented estate.

## Step 5 — build + simplification pass

`dotnet restore --locked-mode` → `dotnet build -c Release --no-restore`
(worktree root). No test runs. Simplification pass over the branch diff
(reuse/simplification/efficiency/altitude) recorded below under a dated
heading.

## Step 6 — PR

Push `task/eng-025-assessment-shell`, PR to `dev`, title "ENG-025: Port the
Assessment workspace shell (assessment-v3, evidence rail, D11 access)".
STOP at the open PR (no merge; review + merge are the orchestrator's).

## Acceptance

- Page renders §1.9 shell from real data; no inert control; access matches
  FRD-11 (D11) in Core with tests; CSP holds (no inline script/style); no
  new CSS/JS file; clipboard-clean build; PR open.

## 2026-08-28 — Scope correction (before the pass)

Commit `5611f316`, subject-labelled "(ENG-026)", had added the full
multi-estimate editor — estimate tabs, the per-estimate editor
(Delete/Duplicate/Use estimate/Save), the "New estimate" control and the
import name/source fields — to `Pages/Cases/Assessment/Index.cshtml(.cs)`.
That contradicts step 0 of this plan, the ticket body ("The multi-estimate
editor is wave 4") and waves.md, which puts "Assessment estimate editor +
Send to Claude" in wave 4 on ENG-028; ENG-026 owns Core estimates only and
no page.

Nothing of the shell needs it: with the editor removed the Estimates pane
still renders the accepted/draft repair specification, its lines, basis and
the Engineer acceptance, plus the "No estimates recorded" empty state, and
the whole solution builds and its Assessment tests pass. So the whole
commit was reverted, not partially kept.

- Reverted on this branch by `bc16d8fa` (a new commit — `5611f316` stays
  reachable, no history rewrite).
- Salvaged to `task/eng-028-estimate-editor` (branch created from
  `origin/dev` @ `9868cf58`, worktree
  `../pegasus-worktrees/eng-028-estimate-editor`, commit `6b4d11db`,
  pushed). No PR: ENG-028 is wave 4 and blocked. Its state — the two page
  files are taken whole from `5611f316`, so its diff against `dev` also
  contains this shell — is recorded on ENG-028's scratch
  (`scratch/salvaged-editor.md`).
- The page comments that named "ENG-026/ENG-028" as the editor's home now
  name ENG-028 (`c9e90360`).

## 2026-08-28 — Simplification pass

Lenses: reuse, simplification, efficiency, altitude, over the branch's own
diff against `origin/dev` @ `9868cf58`.

### Defects found and fixed (correctness, not simplification)

1. **Two `main` landmarks.** The Estimates pane was a second `<main>`
   nested in the shell's; axe reported `landmark-no-duplicate-main` and
   `landmark-main-is-top-level` and the retargeted browser test failed on
   it. It is a `<section>` now, as CASE-012 did for the Case workspace pane
   (`8603f945`). Fixed in `d5dd2c3f`.
2. **The report-draft controls could never enable.** `OnGetAsync` called
   `AssessmentReportProjection.Prepare(Assessment, costs: null)` with no
   estimate, so ENG-026's `RepairCostRequirement` fired for every case and
   Generate/Preview report draft stayed disabled even where generating
   would have succeeded. It now passes `AcceptedSpecification` as the
   Current estimate — the same inputs `EfAssessmentReportProjectionSource`
   hands `Project`. Fixed in `5d3b658c`.
3. **Four test assertions were wrong against merged `dev`** (written before
   this branch merged it, never run under the plan's build-only rule).
   Fixed in `22dd1870`; none weakened — see the post-implementation report.

### Simplification findings

- **Reuse (no finding).** The port adds no new helper: it reuses the lease
  trio and `CaseMutationPageModel`, `CaseFiles.Live` +
  `ICaseEvidenceImageQueries` for the rail, `ICreateAiJob` (AUTO-011) for
  Send to Claude, `GenerateCaseAssessmentReportDraft` for the draft, the
  `_StatusChip` / `_EvidenceViewer` / `_EditHeartbeat` partials and the
  existing `site.js` dialog, tablist, range, rail-toggle and dropzone
  modules. No new CSS or JS file; no inline `<script>`/`<style>`.
- **The `gated` span repeats five times in the record bar.** Not extracted:
  `Details.cshtml` renders the same shape inline, so a partial would be a
  second way to do something the codebase already does, with no caller
  outside this file. Convention wins.
- **`IndexModel.Assessment` is public but read only by the page model.**
  Left as-is: page models expose their state, and narrowing it is churn
  with no reader affected.
- **Dead machinery is gone, verified.** No reference remains anywhere under
  `Pages/Cases/Assessment/` to `SaveDamage`, the section routing, the PAV
  slider, the readiness disclosure (`readiness-summary`) or `assessment-v2`.
- **Efficiency (no finding).** One access query, one workspace query, one
  case query and one evidence-image batch per render; the view holds no
  per-row query.
- **Altitude (no finding).** Access is decided once in Core
  (`AssessmentAccessPolicy`) and every gate — page, workspace source,
  access source, the Case workspace's "Open Assessment", the report-draft
  seam — reads it. The record-bar conditions are computed once per render
  so a control and its gating span cannot disagree.

`Pages/Cases/Assessment/Suggestions.cshtml` is unchanged and still carries
no `@page` directive, so no route activates — the deferred-surface state
`docs/design/README.md` describes, not an inert control on a live page.

## 2026-08-29 — Correction to step 2 (the SaveDamage justification was false)

Step 2's removal bullet reads:

> Remove: `OnPostSaveDamageAsync` (its only caller, the report-section
> diagram, is not drawn by §1.9; impact location stays writable through
> `ISaveAssessment`)

**The first clause is true; the second is false and is struck.** Impact
location does *not* stay writable through `ISaveAssessment` from any operator
surface. Verified on this branch:

```
git grep -n ISaveAssessment -- 'src/**'
  src/Pegasus.Core/Assessment/AssessmentContracts.cs:296   (interface)
  src/Pegasus.Core/Assessment/AssessmentOperations.cs:24   (implementation)
  src/Pegasus.Infrastructure/DependencyInjection.cs:332    (registration)
  src/Pegasus.Web/Mcp/AssessmentMcpTools.cs:154            (MCP tool)

git grep -n ISaveAssessment origin/dev -- 'src/**'
  … the same four, plus
  src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs:51
```

The Razor Pages caller is gone. The bullet should have read: *`ISaveAssessment`
keeps one production caller, `AssessmentMcpTools` — the seam Claude writes the
assessment back through — but no operator surface writes it any more, and
impact location loses the only UI that could set it.*

The corrected statement is now carried by the post-implementation report and
by [[ENG-029]], which owns restoring the editor.
