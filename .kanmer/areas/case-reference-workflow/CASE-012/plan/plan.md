# CASE-012 plan — Case workspace frame + Overview

Scope: EPIC-011 §1.8 frame + Overview only. Non-Overview sections are
placeholders wired to what exists on dev; their views belong to E2 (CASE-027)
and wave 4. Reuse: every action posts to an existing handler
(`Cases/Details`, `Cases/Workflow`, `Cases/Closure`, `Cases/Tasks`,
`Cases/Custody`, `Cases/Eva/Send`, `Cases/Documents/Export`); lease logic,
dialog JS, labels (`OperatorLabels`), chips (`_StatusChip`) are reused, never
reimplemented.

## Steps

1. **Frame (`Details.cshtml/.cs`)** — replace `tab` with `section`
   (`overview|vehicle|valuations|inspection-address|case-files|notes`,
   default `overview`). Header: h1 ref, eyebrow "Case workspace · reg", Back
   to Cases link + Refresh (GET form carrying `section`). Identity ribbon
   (`.record-ribbon`): Case/PO, Registration, Claimant, Principal, State
   chip. Presence strip when an edit authority is active
   (`EditModeDisplay` wording). Action bar per FRD-12 (see contracts below).
   Sticky `.edit-bar` while editing: "Editing · ref", version, Discard
   (release form `data-edit-toggle-off`), Save (`form=` attribute into the
   edit form). No time is shown for a held lease (FRD-01 vs §1.8 "until T" —
   FRD wins; heartbeat makes any printed expiry false).
2. **Workspace grid** — `.case-workspace`: `_CaseWorkspaceNav` (six links,
   `aria-current`), `.case-main` (section content), `.case-context`:
   "Current position" card (State chip, Version, Due, Engineer name, Edit
   authority) + "Next action" card (first outstanding requirement, labels
   only).
3. **Overview** — stepper (Not ready → Review → With Engineer → Complete,
   `is-complete`/`is-current` from state; `workflow-exception` Held badge);
   Outstanding requirements (blockers derived from
   `data.Completeness.Values` + `DueWork.MissingMaterialReason`: title,
   Source, Why, Resolve); edit form when editing (six drawn fields + Reason
   visible; the other twelve editable fields post hidden at current values —
   save is a full overwrite); Confirm completeness (edit mode — the Resolve
   action); "Case overview" panel via `_CaseSummary` (Work facts incl.
   Report sent mailbox+time when linked; Parties; accident card). Proposed-
   values conflict panel (stale version) unchanged behaviour, new classes.
4. **Action-bar contracts** (state-gated; lease-gated where Core demands):
   - closed: Reopen Case (dialog: destination select + readiness carried
     from current completeness as hidden inputs + reason → Closure/Reopen).
     Absent for Created in error (Core refuses it permanently).
   - Complete: Return to Engineer (dialog: reason + destination
     ReportPreparation → Closure/Reopen).
   - editing: Finish editing (dark) + Renew editing (hidden with script).
   - other holder: `EditModeDisplay.HeldBy` note. Recover editing kept.
   - else: Edit Case (ClaimLease).
   - editing + not closed: Place on Hold / Release Hold (reason dialog →
     Workflow/Hold|ReleaseHold); Create upload link (form → Custody/
     CreateRequestUploadLink; one-time secret notice kept); Close Case
     (danger, not Complete; dialog below).
   - Close Case dialog (review round 1): the named-outcome form (Post-report
     complete / Provider cancelled / Collision Engineers rejected) plus the
     atomic wrong-principal path — a second form posting to the existing
     `Workflow/CreateLinkedReplacement` handler with the replacement
     principal code and a reason, carrying the necessary-copy consequence
     sentence "Created in error cannot be reopened. Create and link the
     replacement case." (FRD-01: ValidateClose refuses a bare CreatedInError
     outcome, so the linked replacement is the only route and this dialog is
     its only surface).
   - Review: Send to EVA / Download EVA package (label by export marker) —
     dialog: Engineer select (named accounts, Engineer role, enabled) +
     hidden readiness + reason → Workflow/AssignEngineer (edit mode only);
     Export ZIP → Documents/Export Bundle; Send via API → Eva/Send Submit
     (rendered only when the principal allows manual submission and the
     case has not reached EVA).
   - With Engineer (ReportPreparation) with detected Sent evidence, editing:
     Mark report sent (primary; dialog lists detected evidence mailbox+time,
     confirm → Tasks/LinkReportEvidence). No detected evidence → no control
     (D10: evidence-driven, never asserted).
   - right: Open Assessment (dark, `IGetAssessmentAccess`, disabled with
     condition until the export proxy exists), Close Case (danger).
   - Edit-cluster (edit mode only, undrawn-but-required so no Core path
     loses its only surface; renders only when one applies): Start report
     preparation (Review), Return to Review (With Engineer), Unlink report
     evidence (linked, non-terminal), Archive (terminal).
5. **Sections E2/wave-4** — vehicle: panel + link to `/Cases/{id}/Vehicle`
   (existing page, CASE-027 ports it); valuations, inspection-address:
   panel head only (absent capability, no inert controls); case-files:
   existing `_CaseDocuments` + instruction-photo and vehicle-image
   galleries (moved from the old Evidence tab, `?section=case-files`);
   notes: `_CaseHistory` ported (entries with date/time/actor, newest
   first, Add note, Record chase when editing and a chase is scheduled).
6. **Core/Infrastructure const** — promote `eva_bundle_exported` to
   `EvaBundleSchema.BundleExportedHistoryEventKind`; store uses it; Details
   checks history for it (labels the EVA control "Download EVA package").
7. **Tests** — retargeted `CaseDetailsWebTests` (record-bar class, Open
   Assessment casing, notes section param, hold dialog strings, Review-state
   store for the EVA control, chase display assertions dropped with the
   chase-history panel), `CaseReportApprovalWebTests` (typed-SHA form
   removed; handler contract pinned for its future caller), the two
   `?tab=evidence` → `?section=case-files` params in the image web tests,
   and — review round 1 — both red `OperatorJourneyTests` journeys
   (custody recovery via Operations Attention required + export from the
   Send page; reason-dialog journey on the case-files section).
   Release build green; no test runs (orchestrator owns the wave loop).
8. **Simplification pass** — recorded below.

## Deviations from §1.8 (each cited)

- "Editing held by X until T." — no time printed: FRD-01 (never a time) +
  CASE-024 heartbeat makes T false while read.
- "Download EVA package (With Engineer or Complete, exported)" — control
  offered in Review only, label switches once exported: Core refuses export
  outside Review (FRD-07, `CaseNotInReviewException`) and the prototype's
  final render gates the control on Review. Reported to orchestrator.
- Unsaved chip on the edit bar — needs new site.js behaviour (dirty→chip
  toggle); site.js is PLAT-029's file. Deferred with a finding; the
  edit-finish confirm (which the chip would advertise) already exists.
- Save-in-Review dialog — same constraint; a one-sentence consequence notice
  renders in the edit form instead ("Saving returns the case to Not
  ready.").
- Record chase dialog fields and upload-request dialog fields are CASE-029
  (wave 4); existing forms render inside the sections meanwhile.
- Parties "Image source" row — no backing Core field distinguishes an image
  supplier; the row is not rendered (Origin already carries the source
  channel label).

## Acceptance

- Build green (Release) with tests retargeted to compile.
- Every action control maps to an existing named handler; no new JS, no new
  CSS, no inline styles.
- No raw identifiers, hashes, typed-SHA inputs or GUID selects on the page.
- No explanatory copy beyond labels, values and the two cited consequence
  sentences.

## Simplification pass — 2026-08-28

Applied (behaviour-preserving unless named):

1. Removed dead `DetailsModel.ReportApprovalId` and `DetailsModel.EvidenceCount`
   — no caller after the redesign (the approval form is gone and the drawn
   side nav carries no counts).
2. The readiness envelope existed in four copies; the two hidden-input form
   copies now share `Cases/Shared/_ReadinessHiddenFields.cshtml`. The two
   `_ReasonDialog` ViewData sites keep five literal dictionary entries each —
   the shared partial cannot express them and a merge helper would cost more
   than it saves; accepted with a note.
3. `EditModeDisplay.HolderName` added so the context column's holder value
   reuses the one naming rule (AI / unnamed / named) instead of a second copy
   in markup.
4. Carried-over gallery headings switched from bare `panel-head` (outside a
   panel) to the `blockhead` convention `_CaseDocuments` itself uses.
5. Defect found in pass: the "Lifecycle actions" panel rendered with no
   controls for Not ready and Held cases — restructured with explicit offer
   flags and now renders only when a control applies.
6. Defect found in pass: Reopen Case rendered for Created in error, which
   Core always refuses — the control is now absent for that outcome, and
   (review round 1) the necessary-copy sentence "Created in error cannot be
   reopened. Create and link the replacement case." renders in the Close
   Case dialog's linked-replacement form, which is the outcome's only route.
7. Review round 1: the wrong-principal replacement form was missing
   entirely (it was the deleted `_CaseWorkflow` markup's last copy) —
   restored inside the Close Case dialog as above.

Considered and rejected: caching `OutstandingRequirements` (a four-flag
computation read twice per render); extracting reason-dialog ViewData blocks
(dictionary initializers cannot spread a partial without more machinery than
they save). Efficiency: the Engineer list and EVA submission state are
queried only in Review; the Engineer name only when one is assigned.

## Continuation — round 3, the rest of lane E1 (2026-08-28)

Branch `task/case-012-eva-send-salvage`, worktree
`../pegasus-worktrees/case-012-eva-send-salvage`, from `origin/dev` 9868cf58.

The plan above scoped round 1 to "frame + Overview only". `EPIC-011/waves.md`
gives lane E1 four more files, and PR #599 left all four at base == dev:
`Create.*`, `Eva/Send.*`, `Workflow.*`, `Closure.*`. This continuation closes
them, so the ticket returns to `implementing` until it does.

### Steps

1. **`docs/design/test-ui/catalogue.json`** — rewrite both `case-details`
   branch texts. They still described the pre-redesign page, so UIIMP-005's
   gate would have compared the redesigned workspace against the wrong claim.
   Reuse: the parallel branch's identical two-line fix.
2. **`Cases/Create.cshtml`** — port to the shipped design system. Reuse:
   `page-header`/`page-title`/`page-actions`, `panel`/`panel-head`/
   `panel-body`, `definition-list`/`definition`, `field`/`field-error`/`req`,
   `notice--warning`/`notice--danger`, `cluster`, `stack`, `provenance`, and
   `grid grid-2` with `label.choice` — the idiom the merged `_CaseWorkflow`
   already uses in this lane. Reuse the existing `Shared/_ErrorSummary`
   partial (an orphan until now) instead of the ad-hoc validation summary, and
   keep `_InstructionDraftFields` unchanged. `form-grid`, `form-column`,
   `status-card`, `detail-list`, `section-label` and `prov` are all
   legacy-block classes and are dropped; `page-heading` has no CSS at all.
   Drop the explanatory copy the design authority bans; keep the state
   statements, including the one `CaseCreateWebTests` pins.
3. **`Cases/Eva/Send.cshtml(.cs)`** — port the same way. Reuse
   `OperatorLabels.OfficeTime` for both times (the page kept its own copy of
   the office format) and `Shared/_StatusChip` for the recorded outcome.
   Correct the class summary, which still claimed the case bar opens this
   page. Keep the page rather than reduce it to a handler: with script off the
   bar's `data-dialog-open` control is inert, so this route is the only way to
   the handoff, and `OperatorJourneyTests` depends on it.
4. **`Cases/Workflow.cshtml`, `Cases/Closure.cshtml`** — verified, nothing to
   port. Both are two-line `@page`/`@model` files with no markup, already
   classified `redirect` in the catalogue, and their handlers are the live POST
   targets of the lifecycle dialogs round 1 shipped. Not subsumed, not
   deletable. Reported; the deletion question is UIIMP-009's, not this
   ticket's.
5. **Tests** — salvage the parallel branch's three pins by retargeting them
   onto dev's model rather than copying its files: the handoff is a dialog not
   a link, the report-sent control is gated on the edit authority too and is
   labelled "Mark report sent", and dev has no `?tab=` alias. Add
   `AvailableReportSentEvidence` to the fixture store and one
   `CurrentSectionLabel` helper scoped to the section nav.

### Deliberate drops, each with its reason

- The parallel branch's Engineer-assignment form on the Send page: the merged
  handoff dialog already carries that selector and posts the same
  `Workflow/AssignEngineer` handler. A second copy is a duplicate
  implementation of one control.
- Its widened Send state gate: the shipped bar and the salvaged
  `SendToEvaRendersOnlyInReview` pin both say Review only, and Core refuses the
  export outside Review.
- Its `?tab=` section aliases: no link in the product writes one, and
  AGENTS.md forbids a compatibility path with no caller. The retargeted pin
  records `?tab=` landing on Case Overview instead.
- `_CaseVehicle.cshtml` and `_CaseFiles.cshtml`: lane E2 (CASE-027). Noted
  there as prior art rather than absorbed.

### Acceptance

- Release build green; `CaseDetailsWebTests` and `CaseCreateWebTests` green.
- Every control maps to an existing named handler; no new CSS, script, package
  or abstraction.
- No class used that wave 5's `site.css` deletion would unstyle, except where
  this lane's own merged code already uses it (recorded as a finding).
- No explanatory copy beyond labels, values and state statements.

### Simplification pass — 2026-08-28 (round 3)

Applied: `Create.cshtml`'s six near-identical read rows plus its separate
`Prov` local function became one `Row` local function; its ad-hoc validation
summary became `Shared/_ErrorSummary`; `Eva/Send.cshtml`'s own copy of the
office time format became `OperatorLabels.OfficeTime` and its bare outcome
text became `_StatusChip`.

Considered and rejected, with reasons: dropping `data-word` from the
provenance glyph — it renders no tooltip on the new `.provenance` class, but
the merged `Mail/Message.cshtml` writes it and the existing convention wins;
the two-convention split is reported instead. Folding `SendModel`'s
`CanSubmitToApi` into `DetailsModel.CanSubmitToEva` — both ask the same Core
policy for their own render, which is composition, not a second rule.

## Review findings — dispositions (round 2, 2026-08-29)

An independent adversarial verifier re-ran the build, the three focused test
filters, and the branch's diff against PR #615. Verdict: **clean** — no
blockers, no majors, six minors. Each is disposed below; the fixes are on
`task/case-012-eva-send-salvage`, rebuilt and re-tested (see Verification).

1. **FIX — provenance glyph lost its tooltip.** `Create.cshtml`'s glyph now
   carries `class="prov"` again (was `class="provenance"`), which is the
   class site.css actually renders a hover/focus `::after` tooltip for; the
   markup and attribute order now match `Shared/_Provenance.cshtml`, the
   partial `docs/design/README.md`'s shared-partials table lists (alongside
   `_ErrorSummary`) as "Retained, restyled to the vocabulary". This
   **supersedes** the round 3 "Considered and rejected" note above: that note
   kept `.provenance` because the merged `Mail/Message.cshtml` also uses it,
   reasoning it was "the existing convention" — but neither page's
   `.provenance` class has ever rendered a tooltip, so that was two pages
   sharing one defect, not a convention. `.prov` is the class the CSS and the
   design README's own partial actually work for. `Mail/Message.cshtml`'s
   copy of the same defect is out of lane (wave-2 B / MAIL-025) and is
   reported, not fixed, below.

2. **REJECT — `Create.cshtml` as the only `_ErrorSummary` caller.**
   `docs/design/README.md`'s shared-partials table designates `_ErrorSummary`
   a retained, restyled component of the vocabulary, the same table entry as
   `_Provenance` and `_StatusChip` — not a second, ad-hoc convention this
   lane invented. Round 3's plan already reasoned this exact point ("Reuse
   the existing `Shared/_ErrorSummary` partial (an orphan until now) instead
   of the ad-hoc validation summary"). The 19 other pages using the bare
   `asp-validation-summary` tag helper predate a caller for the documented
   partial; that is their gap against the design system, not a defect this
   lane introduced. No change made.

3. **FIX — EVA outcome chip forced amber for every failure.** `Eva/Send.cshtml`
   now computes tone per outcome (`OutcomeTone`) instead of a hardcoded
   `"amber"` override: `Rejected` and the unreachable-transport fallback are
   `red` (refusal/failure, per `_StatusChip`'s "red is blocked/failed/denied"),
   `Partial` stays `amber` (the case did reach EVA; something it should have
   returned did not — incomplete, not failed). Accepted risk, not fixed: this
   branch still has no unit/integration/browser test of its own (none existed
   before this round either) — building the fakes for
   `ICaseDataQueries`/`IEvaSubmissionModeStore`/`IEvaSubmissionQueries` that a
   new Send-page web test would need is a bigger lift than the one-line tone
   fix it would cover, and the Send page is already named as an unverified-
   in-lane risk gated on the orchestrator's Browser run (see round 3's
   Verification note and finding 6 below, unchanged).

4. **FIX — inconsistent required marking.** `InspectionAddress`'s label in the
   `required`-textarea branch (the "nothing in this file said where the
   vehicle is" branch) now carries `class="req"`, matching `Reason`. The
   other `InspectionAddress` occurrence (the "use this address instead"
   branch) has no `required` attribute on its textarea, so it correctly still
   carries no `.req`.

5. **FIX — catalogue's `redirect` classification of `Workflow.cshtml` and
   `Closure.cshtml` was untrue.** Both files have only `OnPost*` handlers and
   no `OnGet`, so a GET renders no content — it does not redirect anywhere.
   Reclassified both to `protocol` (an allowed classification already listed
   in `Test-UiCatalogue.ps1`'s `$allowedClassifications`, previously unused)
   with a reason stating the actual behaviour. Same defect, not fixed because
   out of lane (E2/CASE-027 owns the files): `Custody.cshtml`,
   `Tasks.cshtml`, and `Vehicle.cshtml` are catalogued `redirect` and have the
   identical no-`OnGet` shape — reported to the orchestrator for CASE-027 or
   UIIMP-005 to correct. `Account/SignOut.cshtml`, `Triage/Index.cshtml`, and
   `Unidentified/Index.cshtml` were checked and are genuine redirects (each
   has an `OnGet` returning `RedirectPermanent`/`RedirectToPage`) — left
   alone.

6. **NO ACTION — Browser-gate risk, confirmed real by the verifier's own
   static read.** Unchanged from round 3: `Eva/Send.cshtml` is exercised only
   by `OperatorJourneyTests` (Browser category), which this lane does not
   run. Not a new finding; the orchestrator's Browser run remains the gate.

**Reported, not fixed (outside this lane's files):**
`docs/design/test-ui/catalogue.json` is allocated to PLAT-029 in
`EPIC-011/waves.md`, but lane E1 (this ticket) needed two of its
`case-details` branch strings corrected in round 3 and now two more
(`classification`/`reason`) in round 2 — both edits are inside this lane's
own entries and (round 3's) byte-identical to the parallel branch, so the
exposure is a merge-collision risk with UIIMP-005 (#588) over file ownership,
not wrong content. Handing the ownership question to the orchestrator rather
than resolving it here.

### Verification (round 2)

- `dotnet build ./Pegasus.slnx --configuration Release` — succeeded, 0
  warnings, 0 errors.
- `dotnet test … --filter "FullyQualifiedName~CaseDetailsWebTests"` — 42
  passed, 0 failed (unchanged from round 3's claim, re-verified).
- `dotnet test … --filter "FullyQualifiedName~CaseCreateWebTests"` — 17
  passed, 0 failed (unchanged, re-verified).
- `dotnet test … --filter` the three pinned theories (`SendToEvaRendersOnlyInReview`,
  `ReportSentRendersOnlyWithDetectedEvidenceWhileWithEngineer`,
  `SectionQuerySelectsOneSectionAndUnknownValuesFallBackToOverview`) — 15
  passed, 0 failed (unchanged, re-verified).
- `docs/design/test-ui/catalogue.json` parses as valid JSON after the edit
  (`Test-UiCatalogue.ps1` itself is not run in-lane; the orchestrator's wave
  loop owns it).
