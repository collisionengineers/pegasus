# CASE-012 post-implementation report

Branch `task/case-012-case-workspace`, PR to `dev` (#599).
`dotnet build ./Pegasus.slnx --configuration Release` green after both the
original round and review round 1. No tests, snapshot scripts or browser runs
were executed by this agent (orchestrator owns the wave loop).

## Delivered

- Case workspace frame per EPIC-011 §1.8: page header (ref / "Case
  workspace · reg" / Back to Cases + Refresh), five-item identity ribbon,
  presence strip (edit-authority holder, no time), state-gated action bar,
  sticky edit bar (Discard/Save; Save wired to the edit form through the
  native `form` attribute so Ctrl+S works), six-section side nav, context
  column (Current position + Next action).
- Overview: workflow stepper with Held exception badge, Outstanding
  requirements derived from the case's completeness flags and due-work
  reason with the Confirm completeness action, the six-field edit form
  (remaining editable values post hidden at current values because the save
  overwrites every field), and the Case overview panel (Work / Parties /
  accident card; Report sent shown as mailbox + time only).
- Dialogs on the shell dialog system: hold, release, close (outcome), reopen
  (destination), Return to Engineer, EVA handoff (named Engineer selector +
  Export ZIP + Send via API), Mark report sent (confirms detected evidence,
  D10), and — review round 1 — the linked-replacement form inside the Close
  Case dialog (see below). All post to existing Core handlers; no lease or
  lifecycle logic was reimplemented.
- Inherited scope: Engineer GUID input replaced by a named selector
  (`IStaffAccountQueries` + `ActorDisplayNames` convention); Engineer display
  is the account name; typed-SHA approval form and hash/ID evidence panels
  removed; no raw identifiers, handles or hashes render.

## Review round 1 changes

- **Blocking 1 fixed.** The wrong-principal replacement path is restored:
  the Close Case dialog now carries a second form posting to the existing
  `Workflow/CreateLinkedReplacement` handler (id/version/operationKey/lease
  token + replacement principal code + reason), carrying the necessary-copy
  consequence sentence "Created in error cannot be reopened. Create and
  link the replacement case." — which therefore renders to the operator, not
  only as a comment. Core's `ValidateClose` refusal of a bare CreatedInError
  outcome makes this dialog the outcome's only surface (FRD-01).
- **Blocking 2 fixed.** Both red browser journeys retargeted (summary in the
  journey section below). The other three journeys were already green and
  are untouched.
- Record-only items (a)–(f) folded into this report.

## Browser journey retarget summary (round 1)

- `CustodyRecoveryAndExportAreKeyboardUsableWithoutInternalIdentifiersOrExternalClaims`
  (script off): the case page still pins the EVA-named-exactly-once rule and
  operator-safe text, now against the workspace (the handoff dialog's own
  wording is hidden markup, not rendered text). The custody failure and its
  keyboard retry moved to Operations "Attention required" (the retryable
  external-work row names the case and the failure reason; "Retry this work"
  is driven by keyboard with no reason field — requeueing operational work
  is not a case edit). Recovery is asserted twice on Operations (the row
  leaves the retryable list when queued and stays gone after the queued
  custody runs) and once on the case's Case Files section, where the
  confirmed custody shows as "Open Box case folder" and not "preparing".
  The export act itself is unchanged (Download export by keyboard, POST
  response asserted, download name and digest asserted, twice, no
  identifiers).
- `PageRenderedReasonDialogStaysReachableWhileOpen` (script on): same
  fixture; the remove-document trigger is driven at
  `?section=case-files` instead of the old `?tab=evidence`, and every
  reachability assertion (focus inside, no inert ancestor, submit enabled,
  close by pointer, hidden after, zero inert) is unchanged.
- Shared helper: `EnterEditModeByKeyboardAsync` now targets "Edit Case"
  (the workspace's casing); `ExportByKeyboardAsync` asserts it is on the
  Send page and drives the download act.

## Explicitly reported (reviewer to decide — not silently weakened)

- **Scriptless reachability of the EVA handoff.** With script off, the case
  page's dialog control cannot open and nothing on the case links to
  `/Cases/{id}/Eva/Send`, so the custody journey navigates to the Send page
  by URL before exporting. The export act itself remains fully
  keyboard-driven on that page. Options: accept for this wave, or have the
  dialog-system owner (PLAT-029's site.js) add an anchor-intercept so a
  dialog trigger can be a real link (the div-backdrop binding currently
  never `preventDefault`s). Inventing that pattern in this lane would touch
  PLAT-029's files, so it was not done here.

## Deferred to other lanes (how wired)

- Vehicle view — side-nav section renders a panel linking to the existing
  `/Cases/{id}/Vehicle` page; CASE-027 ports the section content.
- Valuations, Inspection address — section panel head only (wave 4
  CASE-029/ENG-027 and CASE-027); no inert controls (absent, not disabled).
- Case Files — renders the existing `_CaseDocuments` partial plus the
  instruction-photo and vehicle-image galleries under `?section=case-files`
  (old Evidence tab); CASE-027 restyles.
- Notes — history entries, Add note and Record chase (existing forms);
  CASE-028/029 own the merged timeline and fuller dialogs.
- Custody retry surface, chase-history/chaser-draft panels — removed from
  the Case page; retry renders on Operations (PLAT-023 lane H) once ported,
  chase facts return with the wave-4 Notes timeline.
- Report approval — the typed-SHA form is gone (inherited scope); the
  handler `Closure/RecordReportApproval` is now without a UI caller until
  the Assessment report-draft lane wires it.

## Out-of-scope findings

1. ~~`Browser/OperatorJourneyTests.cs` pins the old case page~~ — resolved
   in round 1 (both red journeys retargeted, above).
2. The Unsaved chip and save-in-Review dialog need new site.js behaviour;
   site.js is PLAT-029's file. The edit-finish confirm (same loss) already
   exists; a one-sentence consequence notice covers save-in-Review. The
   scriptless EVA-handoff reachability gap is reported above.
3. `Closure/RecordReportApproval` handler is orphaned (see above).
4. Task CRUD lost its only UI because the approved prototype draws none —
   recorded as a design-contract fact; no follow-up ticket unless the
   operator asks.
5. `EditModeDisplay.cs` and `Core/Eva/EvaBundleSchema.cs` are not named in
   any wave-2 lane; both changes are additive (one value-naming method, one
   promoted const) and flagged here for the reviewer.
6. (a) `Workflow/RecordEngineerFinding` is now UI-less; ENG-025 (Assessment
   lane) owns the surface that will call it.
7. (e) The Overview stepper's state→ordinal switch duplicates the D3
   display grouping `OperatorLabels.CaseStage` owns (ReportPreparation and
   PostReport both map to "With Engineer"). Recorded as a follow-up for the
   simplification wave; not refactored across lanes in this PR.
8. (f) "Not recorded" permanent placeholders for Repairer/holder and
   Intermediary in the Parties facts — accepted as contract-drawn rows
   (reviewer ruling, round 1); no change.

## CI round 2 (head b7c4d8d2) — one shared root cause, one focused render

- **Root cause fixed (three browser failures).** The workspace's main column
  was a second `<main class="case-main">`, so `Locator("main")` resolved to
  two elements and every strict-mode read of main failed
  (`QdosAllocationRecoveryBrowserTests:137`,
  `OperatorJourneyTests.CustodyRecovery…:56`,
  `PageRenderedReasonDialog…`). The shell owns the one main landmark:
  `case-main` is now a plain `<section class="case-main">` (unnamed, so it
  is not a region landmark either; the per-section label the prototype put
  on `main` had no second home and is dropped). The side nav is likewise no
  longer a second `<nav>` inside the workspace column — it is a
  `<div class="case-section-nav">` with the same anchors and styling.
  Landmark audit of the rest of the new markup: the page header keeps
  `<header class="page-header">` (PLAT-029's own `_PageHeader` convention,
  scoped inside main, not a banner); the context column keeps its
  `<aside class="case-context">` (a labelled complementary landmark is legal
  and prototype-drawn); no `<footer>` exists; remaining sections are
  aria-labelledby panels. `CaseDetailsWebTests.RecordBar` bounded its
  extraction on the next `<nav>` and is re-bounded on `</article>`.
- **Focused render fixed.** `TestUiFocusedRenderTests…:59` drives a throwing
  `IGetCase` and asserts "Case unavailable" on the 503 page; the redesign
  had kept the sentence but dropped the heading (leaving the failure state
  with no h1 at all, which also breaks the one-h1 integrity rule). The
  state is not superseded — restored at the root: the failure state renders
  `<h1>Case unavailable</h1>` plus the unchanged one-sentence notice.
