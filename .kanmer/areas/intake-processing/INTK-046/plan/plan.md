# Plan — INTK-046 (lane C2)

Reuse (named): `_PageHeader` pattern from `Cases/Details` (page-header +
Back to Cases + `data-refresh-form`), `_StatusChip`, `_ReasonDialog`
(`DialogHiddenFields` + `data-dialog-open`), `_ImageGallery` +
`_EvidenceViewer`, `_InstructionDraftFields`, `OperatorLabels` maps,
`DetailsModel.OnPostActionAsync` / `OnPostResolveAsync` /
`OnPostCloseAsync` handlers as-is. No new CSS/JS; one `<main>` (the
shell's) — inner regions are `<section>`.

## P1 Triage `/Triage/{id}` (§1.5)

- `page-header`: eyebrow "Triage", h1 = registration; actions Back to
  Cases (`/Cases?tab=triage`) + Refresh GET form.
- `article.record`: `record-head` (registration; identity: source
  channel, Opened date; chip `OperatorLabels.TriageState`), accent,
  `record-bar` (eyebrow "Triage" + muted Assigned/Unassigned;
  bar-end: Assign to me / Unassign reason dialogs when mutable),
  `record-body`.
- Determinations panel: one conditional form — Roadworthiness + Repair
  outcome selects, Reason, primary button posting `record_finding`
  (no active finding) / `supersede_finding` (the single active finding,
  preselected; from Completed labelled "Record correction" per FRD-03).
  Multiple active findings → alert notice. Transition buttons below,
  each an existing handler: Await information (Open/FindingRecorded),
  Complete Triage (FindingRecorded), Cancel Triage (danger, mutable),
  Reopen (terminal) — all reason dialogs.
- Source panel: definition-list Material (channel), Received, Case
  (linked case link or None); held-case notice when the model reports
  the case unavailable; Link case dialog (case id + reason) / Unlink
  case reason dialog when mutable; "View retained source" →
  `/Received/{receiptId}`.
- Response evidence panel renders when linked evidence or candidates
  exist; link/unlink forms unchanged.
- Notes panel: `notes-list` over `Triage.History` newest first —
  `note-meta` Date/Time/ID (actor display name) + event label and
  reason.
- Vehicle images section retained (`_ImageGallery`) — INTK-034's
  capability and owned test.

## P2 Unidentified `/Unidentified/{id}` (§1.6)

- `page-header`: eyebrow "Unidentified", h1 = U-reference; Back to Cases
  (`/Cases?tab=unidentified`) + Refresh.
- `article.record`: record head (kind, received, handle; chip state),
  warning `notice--warning` (canonical reason), Retained source panel
  (definition-list: Permanent reference, Kind, Operator handle,
  Received, Source, Canonical reason; one-image gallery for image
  material; "View retained source" → `/Received/{receiptId}` (receipt
  origin only) and "Resolve destination" dark, Open items only),
  History panel (`timeline`, newest first, resolution target labelled).
- Resolve dialog (Open only): Destination select (labelled kinds),
  Destination identifier, Destination reference, Reason → existing
  `OnPostResolveAsync` bindings (`ExpectedVersion`, `OperationKey`
  hidden). Model-error summary stays.

## P3 Label map (one list)

`OperatorLabels.UnidentifiedResolutionTarget`: InstructionCase
"Add to existing Case", ImageIntake "Register Image-initiated Case",
Triage "Link to Triage", BlockedIntake "Blocked intake",
ExternalReference "Close with reason". Divergence recorded: the
prototype's fourth wording "Create Case from accepted instruction" has
no resolution kind (Core resolves to existing destinations; the
create-case action lives on the Received page and the item then
auto-resolves) — rendering it would be an inert control, so it is not
drawn.

## P4 Received `/Received/{id}` — restyle, handlers unchanged

- `page-header` (eyebrow "Received review", h1 = decision outcome),
  notices for TempData/duplicate/allocation states, every existing
  panel restyled onto `panel`/`panel-head`/`panel-body`,
  `definition-list`/`blocker-list`, `btn` family; forms post the same
  handlers with the same field names. Empty-only sections (Missing
  fields, Scanned PDF pages, evidence) stop rendering when empty.
  Image thumbnail renders through the one-image gallery.

## P5 Image record `/VehicleImages/{id}` — restyle

- `page-header` (eyebrow "Image-initiated Case", h1 = image reference;
  Back to Cases `/Cases?tab=not_ready` + Refresh), `article.record`
  (identity: registration, registered, image count; chip lifecycle
  state), `record-bar`: "Open the origin receipt" link + Close with
  reason (danger dialog → `OnPostCloseAsync`, AwaitingInstruction
  only), Record panel (definition-list incl. "None — awaiting
  definitive instruction" continuation label), Preserved origin panel,
  Images gallery + `_EvidenceViewer` retained, History timeline,
  candidates and recognition panels kept (populated only).
  Validation summary added so the close handler's model errors surface.

## Steps

1. P3 label map → build. ✔
2. P1 Triage → build; slice commit. ✔
3. P2 Unidentified → build; slice commit. ✔
4. P4 Received → build; slice commit. ✔
5. P5 Image record → build; slice commit. ✔
6. Merged `origin/dev` (mail lane #597) — no Migrations conflicts; full
   locked restore + Release build green. ✔
7. Simplification pass over the branch diff; recorded below. ✔
8. Push, PR to `dev`, stop (no merge, no proof).

## Verification

- `dotnet restore ./Pegasus.slnx --locked-mode`;
  `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
  green, 0 warnings. Tests/snapshots are the orchestrator's (EPIC-011
  rule).
- Every drawn control posts/navigates an existing handler or route;
  no inert control; no new CSS/JS file; labels via `OperatorLabels`.
- One `<main>` (shell); no `aria-pressed`/`aria-current` introduced
  (no nav links or toggles on these pages); buttons are `<button>`;
  inner regions `<section>`/`<div>`; no legacy classes remain in the
  four pages.

## Simplification pass

### 2026-08-28

Lenses: reuse, simplification, efficiency, altitude over the branch diff.

- **Applied — unify the determination forms (reuse/duplication):** the
  record, supersede and post-send correction forms were three copies
  of the same selects/reason/button; one conditional form now covers
  all three postings (`record_finding` / `supersede_finding`, label
  "Save determinations"/"Record correction").
- **Applied — response-evidence either/or (bug-adjacent
  behaviour-preserving fix):** the candidates panel and the
  linked-evidence panel were exclusive branches; when both existed the
  linked evidence and its unlink form silently dropped. Restructured
  into one panel rendering both.
- **Applied — hoist `hiddenBase`:** the reopen dialog rebuilt the same
  hidden-field dictionary; both dialog branches now share the one
  declared in the page header block.
- **Applied — drop the unused `unavailableCaseId` pattern variable**
  (the reason alone decides).
- **Rejected — extracting the determination form into a
  `Pages/Shared` partial:** the shared partials folder is PLAT-029's
  wave-1 lane and a Triage-folder partial is outside this ticket's
  owned paths; the in-page conditional form is the proportionate
  shape.
- **Rejected — a case-picker seam for the Unidentified resolve
  dialog:** no existing case-search port backs a picker on this page;
  the destination-identifier input is the existing binding and stays.
- **Rejected — dropping the "Back to Operations" link on Received:**
  changing the back target is a product decision, not a restyle.
- **Note:** `aria-current`/`aria-pressed` intentionally absent — these
  pages carry no links-acting-as-nav or toggle buttons.

Status: pass complete; findings above applied or disposed with reasons.

## Review dispositions — 2026-08-28 (round 1, independent reviewer)

- APPROVE. One recorded drop: the old page's "Recorded findings" list
  (including superseded findings with their supersession chains) is not in
  the §1.5 contract and is not ported; Core retains the full record. A
  superseded finding's recorded values are currently visible nowhere in the
  UI — if operators need the supersession audit trail visible, that is a
  follow-up ticket, not this lane.
- The ticket's "no clipped text/overflow at 1580/1100/760" verification item
  is owned by the orchestrator's browser walk (UIIMP-010), per EPIC-011.
- The disabled-but-visible Complete affordance is replaced by the per-state
  control convention used across the workspace pages — accepted.

## Correction — 2026-08-28 (round 2, regression fix)

Round 1's last disposition — "the disabled-but-visible Complete
affordance is replaced by the per-state control convention used across
the workspace pages — accepted" — rested on a false premise, and CI
caught it. The workspace pages use the opposite convention:

- `Pages/Cases/Details.cshtml:269` —
  `data-condition="@(Model.CanOpenAssessment ? null : "Available after
  the current Review export")"` around a state-gated `is-disabled`
  Open Assessment control.
- `Pages/Cases/Assessment/Index.cshtml:765` — the same `.gated` shape
  around a permission-gated "Import estimate" button.
- `wwwroot/css/site.css:1893-1911` carries `.gated`/`data-condition`
  as a design-system rule, with a forced-colours case.

So "disabled with the condition named" *is* the workspace convention
for a control whose handler exists but whose state does not yet permit
it; per-state show/hide is the convention only for controls Core
forbids outright (Await, Reopen). D7 forbids an **inert** control — one
wired to no handler. Complete posts the same `complete` action either
way, so it is a state gate, not an inert seam, and EPIC-011 §1.5 keeps
the server-side transitions "available through the determinations flow".

Three port regressions followed from that premise, each pinned by
`QdosTriageIntegrationTests` and each fixed in the markup — no
assertion was weakened:

1. Complete vanished when unavailable → restored to the `.gated`
   shape (`Details.cshtml:186`).
2. The post-send correction lost its name when the three determination
   forms were unified → the panel is named "Post-send correction" on a
   Completed record, keeping the single §1.5 panel
   (`Details.cshtml:104`).
3. The permanent-history panel was renamed "Notes" → every entry is a
   retained business event and Triage has no note entity, so the
   generic name misstated a term FRD-03 owns; §1.5's entry shape is
   kept under the domain's own name (`Details.cshtml:392`).

Finding 3 is the one worth carrying forward: the unification in
simplification-pass item 1 was sound, but it silently dropped two
operator-facing names along with the duplicated markup. A dedup that
also renames is two changes, not one.
