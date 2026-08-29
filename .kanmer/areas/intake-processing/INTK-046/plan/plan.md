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

## Review findings — dispositions (round 2) — 2026-08-29

Adversarial verifier re-ran the build, the focused filters and the diff.
It refuted no mechanical claim; it refuted two interpretive ones. Every
finding below is disposed. Nothing is silenced.

### Correction to this plan's own round-2 text

The round-2 correction above says **"D7 forbids an inert control — one
wired to no handler."** That is a misquote and it is withdrawn. It is
the Rules-section bullet's second sentence ("Every drawn control maps to
a named handler or an approved disabled seam (D7). Never render an inert
control"), attributed to D7's table row, which actually reads:

> Uncomposed integrations (Experian, Glass's, Audatex, Cazana) render
> disabled as drawn; **a disabled control is permitted only for a named,
> ticketed integration seam.**

The re-argument against the real wording is in the finding below. The
round-2 text is left standing rather than rewritten, so the error and
its correction are both on the record.

### [major] Shipped heading contradicts EPIC-011 §1.5, which names the panel "Notes"

**Disposition: deferred to [[UIIMP-012]]** (created, area `ui-improvement`,
group EPIC-011), plus the record corrected here and in the
post-implementation report. The finding is accepted in full: §1.5 does
name the panel, the softened wording understated it, and the binding
document is now stale against shipped code.

The lane brief's instruction was "restore the contract's name". That was
tried and **measured, not argued** — and it cannot be done inside this
lane:

- `origin/dev:src/Pegasus.Web/Pages/Triage/Details.cshtml:348` already
  read `<h2 id="history-title" class="section-label">Permanent
  history</h2>` **before this port**. The name is pre-existing `dev`, not
  a lane rename. This lane's regression was renaming it *away* to
  "Notes"; round 2 put `dev`'s name back.
- `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs:477`
  asserts it. `git diff origin/dev HEAD -- tests/` is empty — that file
  is byte-identical to `dev` and is not in this ticket's "Owns" list.
- Applying the rename, building green and running the filter:

  ```
  dotnet test ./Pegasus.slnx -c Release --no-build \
    --filter "FullyQualifiedName~QdosTriageIntegrationTests"
  Failed: 1, Passed: 8, Skipped: 0, Total: 9
    at QdosTriageIntegrationTests.cs:line 477 — Not found: "Permanent history"
  ```

  Reverted.

Restoring §1.5's name therefore means either shipping a red pre-existing
assertion into `dev`, or editing an assertion this lane does not own in
order to pass a test. Both are banned outright by AGENTS.md rule 19 and
by the lane brief. The rename and the assertion have to move in one diff,
owned by whoever owns both — that is UIIMP-012, which carries the
evidence and both possible resolutions.

Code change made: the Razor comment at `Details.cshtml:392` no longer
says the panel "keeps §1.5's entry shape under the name the domain
actually gives it". It now states that §1.5 calls it the Notes panel,
that the name diverges, and that UIIMP-012 owns the divergence.

### [minor] D7 misquoted, and the misquote justified the contested Complete control

**Disposition: fixed (the record) + deferred to [[UIIMP-012]] (the rule).**
Finding accepted. Re-argued against the actual wording:

- The Complete control **satisfies** the Rules bullet — it posts the same
  `complete` action to `OnPostActionAsync` in both states, so it is not
  inert.
- It **does not satisfy D7's second clause read literally** — it is a
  state gate, not a named ticketed integration seam. Conceded, not
  argued away.
- But D7's second clause is already contradicted by merged work and by
  `dev`'s own test, so this lane cannot be the place it is enforced:
  `Pages/Cases/Details.cshtml:269` (state gate) and
  `Pages/Cases/Assessment/Index.cshtml:765` (role gate) both ship the
  same `.gated` shape; `site.css:1893-1911` defines `.gated` as a general
  design-system rule with a forced-colours case at 1961, not an
  integration rule; and `QdosTriageIntegrationTests.cs:216-221` pins
  `"Available once a finding is recorded"` under the comment
  *"Completion keeps its place with its condition named, rather than
  disappearing until it happens to work."*
- Removing the disabled Complete to satisfy D7's literal clause would
  fail that pre-existing assertion — the same banned move as above.

Interpreting a binding contract clause is the epic owner's, so UIIMP-012
carries it with both resolutions written out. Corrected in three places:
this plan (above), the post-implementation report, and the Razor comment
at `Details.cshtml:191`, which now quotes D7's clause and says plainly
that it reads against the merged convention.

### [minor] Label file: not appended inside a nested static class

**Disposition: half fixed, half rejected with reason.**

*Fixed* — the mid-file placement. `UnidentifiedResolutionTarget` moved
from `OperatorLabels.cs:54` to the end of the class. The diff against
`origin/dev` is now a single hunk, `@@ -881,6 +881,30 @@`, `+24/-0`. This
matters more than the verifier knew: `origin/task/uiimp-008-work-centre`
inserts at `@@ -47,6 +47,17 @@` and `@@ -69,6 +80,19 @@` — directly
around the old position, extending the same `Unidentified*` group. The
other two sharers sit at `@@ -307` (ENG-028) and `@@ -806` (TICK-058), so
the end of the class is the one anchor no sibling lane touches.

*Rejected* — wrapping it in a nested static class. Reason a reviewer can
check: **no lane did**, and the file's convention is against it. All four
in-flight branches touching `OperatorLabels.cs` this wave add flat
top-level methods —

```
origin/task/uiimp-008-work-centre  +UnidentifiedReason, +UnidentifiedMediaKind,
                                   +ChaseState, +NeedsAttentionKind, …  (flat)
origin/task/eng-028-estimate-editor                                     (flat)
origin/task/tick-058-provider-submission-api                            (flat)
```

— as did merged PLAT-023 (`a0c28af8`, five flat methods). The file holds
~55 flat methods against 3 nested classes (`Nav`, `Admin`, `Freshness`),
each of which groups *route/area* labels, not a lane's output. A
lane-named nested class would be a new convention with no second caller,
and would split one concept's labels across two scopes. CLAUDE.md:
"The existing convention wins... needs a reason recorded in the ticket
plan" — recorded here.

*Confirmed clean* — the "never reorder existing members" half was
honoured throughout: the change was and remains a pure insertion, `+24`
with `0` deletions of existing members.

### [minor] Ticket's own verification item left unticked

**Disposition: fixed — audited and ticked.** "Every button posts an
existing handler; no inert control" is now checked on the ticket body.
The audit, run over all four owned pages:

- Every `data-dialog-open` target resolves to a declared dialog:
  `triage-assign/unassign/await/complete/cancel/unlink-case/reopen` via
  `_ReasonDialog` `["DialogId"]` (lines 421-525),
  `triage-link-case-dialog` via the inline backdrop,
  `unidentified-resolve-dialog` via the inline backdrop,
  `image-intake-close-dialog` via `_ReasonDialog` (line 211). No orphan
  targets.
- Every form posts a handler that exists: `OnPostActionAsync` (Triage),
  `OnPostResolveAsync` (Unidentified), `OnPostCloseAsync` via
  `Url.Page(..., "Close", ...)` (image record), and the ten named Intake
  handlers (`Block`, `ClaimCaseLease`, `CorrectDraft`,
  `DismissSuggestion`, `LinkCase`, `OpenTriage`, `Reevaluate`,
  `RegisterImageIntake`, `RetryAllocation`, `ReverseCaseLink`).
- The one disabled control (Complete) posts the same `complete` action,
  so it is not inert. Whether D7's *disabled-control* clause permits it
  is a different question, and it is UIIMP-012's — the checkbox as
  written is satisfied.

The other unticked item ("No clipped text/overflow at 1580/1100/760")
stays unticked: it is the orchestrator's browser walk (UIIMP-010) per
EPIC-011, not this lane's to claim.

### [minor] `.gated::after` renders an empty tooltip pill in the enabled state

**Disposition: deferred to [[PLAT-061]]** (created, area
`platform-operations`, group EPIC-011, linked to [[PLAT-029]]).
Confirmed independently: `grep -n "gated"
src/Pegasus.Web/wwwroot/css/site.css` returns only 1893, 1895, 1911,
1961 — no `[data-condition]` guard exists, so `content:
attr(data-condition)` resolves empty while the pseudo-element keeps its
padding and `--band` background. Not edited: `site.css` is PLAT-029's
file and EPIC-011 says report, do not fix. PLAT-061 also carries the
secondary `:focus-within` note (a `<button disabled>` is not focusable,
so the condition is hover-only) since both are one selector's problem.

### Verifier claims checked and confirmed, not disputed

Scope breach: none. Honesty: no mechanical overclaim. Both re-checked
here — `git diff origin/dev HEAD -- tests/` empty, `git rev-list --count
HEAD..origin/dev` = 0, working tree clean before this round.

### Round-3 verification (real numbers)

- `dotnet build ./Pegasus.slnx --configuration Release` — exit 0,
  **0 Warning(s), 0 Error(s)**.
- `--filter "FullyQualifiedName~QdosTriageIntegrationTests"` —
  **Failed: 0, Passed: 9, Skipped: 0, Total: 9** (1 m 21 s).
- `--filter` over the six other owned classes — **Failed: 0, Passed: 15,
  Skipped: 6, Total: 21** (50 s).
- No assertion was weakened, skipped, deleted or inverted in this round.
  `tests/` remains byte-identical to `origin/dev`.
