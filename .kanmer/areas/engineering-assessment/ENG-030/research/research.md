# Research — ENG-030: Remove the excluded Glass's and Audatex direct-service controls

## Question

Where does the permanently-inert Glass's/Audatex direct estimating-service
launch control render on the Assessment record bar, what seam backs it, what
must be removed versus preserved (file import, manual valuations, Experian/
Cazana seams), and do the governing docs already cover the capability-map
update this removal implies?

## Findings

All evidence read read-only from `origin/dev` at `fbf8ee40` (2026-09-02,
after ENG-025 PR #616 and ENG-028 PR #630 both merged; ENG-028's Kanmer
status still shows `verifying` but its commits are already on `dev`).

- The two controls are drawn at
  `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml:219-224`:
  ```cshtml
  <span class="gated" data-condition="@Model.EstimatingServiceCondition">
      <button type="button" class="btn" disabled aria-disabled="true">Glass's</button>
  </span>
  <span class="gated" data-condition="@Model.EstimatingServiceCondition">
      <button type="button" class="btn" disabled aria-disabled="true">Audatex</button>
  </span>
  ```
  Neither carries a form, `href`, or `data-dialog-open` — no reachable
  enabled state exists (confirmed independently by the GPT-5.6 audit
  recorded in ENG-025's `proof/proof.md`, "Reversed out of Done" section).
- The condition they share is a hard-coded page-model property,
  `Index.cshtml.cs:322-329`:
  ```csharp
  /// The single condition the D7 estimating-service seams (Glass's,
  /// Audatex, EXT-09) state. ...
  public string EstimatingServiceCondition =>
      "Available once the estimating-service link is agreed";
  ```
  This is the ticket's "hard-coded direct-service condition property."
- Two comments elsewhere in the same two files name the seam and must be
  reworded once the controls are gone (not deleted wholesale — the
  surrounding prose still needs to describe the record bar correctly):
  `Index.cshtml:155` (razor comment "Glass's and Audatex are the D7 disabled
  seams (EXT-09)") and `Index.cshtml.cs:34-40` (class `<summary>`, "the
  record bar (estimate import, the Glass's and Audatex disabled seams, Send
  to Claude ...)").
- No other production file references `EstimatingServiceCondition`, and no
  `site.css`/`site.js` selector or hook is scoped to these two buttons
  (`git grep` for `Glass|Audatex|EstimatingService` under
  `src/Pegasus.Web/wwwroot/**` returns nothing) — there is no JS/CSS seam to
  clean up beyond the two files above.
- `Suggestions.cshtml` (same `assessment_shared` lock group) has no
  Glass's/Audatex reference.
- **What stays, and where it actually lives** (none of these are touched by
  this ticket):
  - File import: ENG-028's "Import estimate" control
    (`Index.cshtml:210-218`, `ImportCondition`/`OnPostImportEstimateAsync`)
    and the Audatex-PDF parser (EXT-12/ENG-002) are a different code path —
    the Import estimate button posts through a form/dialog, is gated on
    role and read-only state, not on `EstimatingServiceCondition`.
  - `OperatorLabels.cs:381-382` — `RepairSpecificationSourceRoute.Glasses`
    → "imported from Glass's", `.AudatexPdf` → "imported from Audatex" —
    labels for imported estimates, unrelated to the launch buttons.
  - Manual valuation records: `ValuationSource.Glasses` (Case → Valuations
    tab, `Valuations` dialog) is a distinct capability, proven by
    `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` and unaffected.
  - Experian (`Vehicle checks → Run Experian check`, ENG-001) and Cazana
    (Valuation source, ENG-008/ENG-009) remain approved D7 disabled seams —
    context.md D7 narrows to "uncomposed integrations" generally and D21
    names only the Glass's/Audatex *service-launch* controls for removal.
    `docs/design/README.md:699-703`'s seam table still lists all three rows
    on merged `dev`; PR #643 (below) removes only the Glass's/Audatex row.
- **TICK-085** (Glass's file import from a representative export,
  `backlog`) and **ENG-032** (Audatex full-report variant reconciliation)
  own the file-import capability this ticket must not disturb; both are
  linked from ENG-030.
- **ENG-025's proof was reversed out of Done** (2026-08-29) specifically
  because these two buttons render permanently inert in a file ENG-025
  owns, under the operator's strict rule-14/D21 reading ("a disabled
  control or a closed feature gate is never a delivered capability"). The
  GPT-5.6 audit named Audatex as "no ticket supplied this at the time of
  the audit — raised as [[ENG-030]]". **ENG-030 is the ticket that
  discharges that finding** — its own Verification item 4 says so
  explicitly ("[[ENG-025]] has no permanently inert estimating-service
  control left to discharge").
- **EPIC-011 context.md** governs the removal:
  - §1.9: "There is no Import estimate dialog and no Glass's or Audatex
    launch control (D16, D21, ENG-030)."
  - §1.14 Removed: "the Glass's and Audatex launch controls (D21, ENG-030)."
  - D7 (narrowed 2026-09-01, UIIMP-012): governs *uncomposed integrations*
    only, and explicitly carries "until removed by ENG-030, the drawn
    Glass's/Audatex controls" as the transitional wording.
  - D21: "An excluded capability is absent from the interface, never drawn
    as a disabled control: the direct Glass's and Audatex service-launch
    controls are removed (ENG-030); ... Experian and Cazana remain disabled
    seams under D7; manual Glass's, Cazana and Engineer valuation records
    stay active."
  - `decisions/2026-09-01-work-pack.md` "Committed prototype scope" lists
    "direct Glass's or Audatex service-launch controls" as one of four
    retained exclusions, and separately: "Glass's and Audatex file imports
    remain in scope."
  - `decisions-and-constraints.md` (work-pack root) records the same
    exclusion and additionally: "whether a future direct Glass's or Audatex
    service integration is dropped permanently or returns as a separately
    authorized capability (ENG-030)" is **explicitly not decided by this
    pack** — i.e. removal now is settled; a future reintroduction is an
    open product question outside this ticket's scope, not something this
    research needs to resolve.
- **ENG-025's `scratch/notes.md` and `proof/proof.md`** (read for "what does
  ENG-025's acceptance hold expect") show ENG-025 has nothing else pending
  on ENG-030 beyond this discharge: its checklist is 21/22, its remaining
  open item is the 1580/1100/760 layout walk (owned by UIIMP-010, unrelated
  to this ticket), and it is otherwise sitting in `verifying` waiting only
  on the Kanmer `blocked_by: [ENG-030]` edge to clear (per
  `ticket-ledger.yml:2085`, "Blocked by ENG-030 (Kanmer edge), so Done waits
  for wave A").
- **DELIV-040's docs PR #643** (`gh pr view 643`: OPEN, base `dev`, `dev
  fbf8ee40`, `mergeStateStatus: CLEAN`) already carries the capability/
  boundary-map correction this removal implies, written as "allocated to
  ENG-030, not delivered":
  - `docs/boundaries.md`: adds a new row — "direct Glass's and Audatex
    service launch | ... | the launch controls themselves — they are
    **absent from the interface, never drawn as disabled controls** (D21,
    2026-09-01, ENG-030) | an accepted vendor service contract, credential
    custody, failure/recovery contract and real caller".
  - `docs/capabilities.md`: **EXT-13** row gets "Decided 2026-09-01 (EPIC-011
    D21, allocated to `ENG-030`, not delivered): the direct Glass's and
    Audatex service-launch controls are **absent** from the interface
    rather than drawn as disabled controls, while manual Glass's, Cazana and
    Engineer valuation records stay active and Experian and Cazana remain
    named disabled seams under D7." **ENG-01** row gets the matching
    sentence. **EXT-12**'s note is also rewritten for D16 (whole-page
    import), not for this ticket.
  - `docs/design/README.md`: removes the "Glass's, Audatex | Assessment
    record bar sources | EXT-09" row from the §Absent versus disabled seam
    table and rewords §1.9's Assessment description and §1.14 Removed list
    to state the controls are absent.
  - This PR is **docs-only** (its own description: "No code, no test, no
    generated artifact ... no runtime behaviour changes") — it does not
    touch `Index.cshtml`/`Index.cshtml.cs`, so it does not conflict with
    this ticket's file scope, and this ticket does not need to duplicate
    the docs edits. Whichever of ENG-030 / PR #643 merges first, the other
    is unaffected: PR #643 doesn't reference code state, and ENG-030's own
    Approach explicitly forbids touching docs beyond "Record the settled
    exclusion in the EPIC-011 decision/context record" (already done, in
    `context.md`/`decisions/2026-09-01-work-pack.md`, both read above).
- **Test UI catalogue snapshot is currently stale in the removal direction**:
  `docs/design/test-ui/pages/case-assessment--default.html:242,245` still
  renders both disabled buttons (`docs/design/test-ui/catalogue.json:292`
  maps that page to `Index.cshtml` as its `source`). Regeneration is owned
  by the `test_ui_catalogue` lock ("capture once per PR-ready state; commit
  with the page change; programme-wide regeneration is UIIMP-011") and by
  the orchestrator's per-merge snapshot step — ENG-025's proof already
  established that a lane never regenerates snapshots itself and the CI
  gate is not yet wired on `dev` (`.github/workflows/` holds only `ci.yml`).
  This ticket's implementer therefore removes the buttons in the page but
  does **not** run the snapshot script (M6 forbids it); the snapshot goes
  stale until the next regeneration pass, same as it did for ENG-025.
- No `Pegasus.ArchitectureTests` file scans for inert controls or button
  markup (`git grep` for `type=\"button\"|inert|gated` under
  `tests/Pegasus.ArchitectureTests/**` returns nothing), and no
  `Assessment*WebTests`/`AssessmentReadinessSummaryBrowserTests.cs`
  assertion names the Glass's/Audatex buttons or counts the record bar's
  total controls — the one browser test's control-count assertions are
  scoped to `.record-bar-end .gated` (report-draft) and `.estimate-tabs`/
  `.estimate-empty` (ENG-028's editor), neither of which the removal
  touches. Removing the two buttons should not need any test edit.

## Implications

- The change is a small, precisely bounded deletion inside exactly the two
  files the ticket's own Approach names: `Index.cshtml:155,219-224` and
  `Index.cshtml.cs:34-40,322-329` (comment rewording plus control/property
  deletion). No other production or test file needs to change.
- Both files sit inside the `assessment_shared` lock
  (`Pages/Cases/Assessment/Index.cshtml`, `Index.cshtml.cs`,
  `Suggestions.cshtml`, plus three Core Assessment files this ticket does
  not touch) — the implementer branches from current `origin/dev` (which
  already carries ENG-025 and ENG-028), not from any stale base.
- The docs/capabilities/boundaries/design-README update this removal implies
  is already written and pending in PR #643 (DELIV-040); this ticket does
  not need to (and per its own Approach, should not) touch those three
  files — doing so would create a duplicate/conflicting edit against an
  open PR outside this ticket's scope.
- The Test UI snapshot for this page will be stale in the same direction
  ENG-025 left it (still showing removed controls) until the next
  programme-wide regeneration; this is a disclosed, pre-existing convention
  (owned by UIIMP-011/the orchestrator's per-merge step), not a defect this
  ticket must fix.
- This ticket is the literal, named discharge of the one finding that
  reversed ENG-025 out of Done — its own Verification item 4
  ("[[ENG-025]] has no permanently inert estimating-service control left to
  discharge") is satisfied exactly by deleting the two spans and the
  property; ENG-025 needs no other action from this ticket.

## Open questions

None found during research beyond what `open-questions` already carries as
an explicit, non-blocking parked item (see that document).
