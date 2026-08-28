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
