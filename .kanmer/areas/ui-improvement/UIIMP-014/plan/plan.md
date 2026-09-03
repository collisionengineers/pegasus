# Plan — UIIMP-014 (2026-09-02, gpt-5.6-terra high; revised 2026-09-03 after the gpt-5.6-sol xhigh plan review)

## Wrapper check (Claude, 2026-09-02)

Codex (gpt-5.6-terra, high) ran read-only in the detached
`.worktrees/research` checkout at `origin/dev` = `897db953`, prompt piped on
stdin; `git status --porcelain` was empty afterwards. The wrapper did every
board read and write; Codex never touched the board. Spot-checks in the same
checkout, all confirmed:

- `TestUiSnapshotTests.StateMatches` (lines 18–53) is keyed by catalogue
  `scenario`, so every new state record in `catalogue.json` needs a
  `scenario` value and a matching entry there; `Generate` (lines 116–172)
  falls back to sibling-exclusion then ordinal-first only for scenarios with
  no matcher — the plan's "one deterministic matcher per new scenario" rule
  is the correct fix.
- Existing `redirect` entries carry only `source`, `route`, `classification`
  and `reason` (for example `/Triage`, `/Unidentified`); the Assessment entry
  takes that shape and drops `states`.
- `SeedCustodyRecoveryCaseAsync` (line 315), `BrowserAcceptedCase` (line
  517) and `RepositoryEvaFixture` (line 616) are all `private` to
  `OperatorJourneyTests`. A walk in `LayoutIntegrityTests` cannot call them
  as they stand, so Step 3's helper must not become a second copy of the
  seed; see the revised Step 3 for the exact extraction.
- `LayoutIntegrityTests` (lines 17–78) is a single theory over
  `AccessibilityTests.AuthenticatedRouteList` × {1580, 1100, 760}; the
  seeded Case walk is a second `[Theory]` in the same class reusing the
  same `AllowedClipSelector`, geometry and inline-style checks.
- `docs/frd/frd-12-operator-experience.md` (lines 98–102, 152–174, 317,
  355–362, 528–536) states the eleven-section record, the
  `?section=estimate` redirect, the Awaiting instruction Pre-Case queue,
  the one-line partial-data notice and the 1580/1100/760 walk, as the plan's
  verified premises claim. Its acceptance-evidence paragraph (528–536) also
  requires **axe accessibility and focus behaviour** on that walk.

Wrapper corrections applied to the Codex text: Step 3's visit count was
"66 read-only and 66 edit"; it is 33 + 33 (11 sections × 3 widths each).
The `?tab=awaiting-instruction` value in Step 1 is CASE-042's to settle and
is verified there, not assumed.

## Objective

Regenerate the Test UI catalogue and snapshots for the merged single-scroll
Case record — 22 section/mode states in total, the retired Assessment route,
Awaiting instruction, and the Operations partial-data notice — and add the
seeded three-width Case-record browser walk.

Diff estimate: four test/catalogue files plus generated Test UI artefacts; no
production, policy, migration, label, CSS, or JavaScript changes.

## Starting state

Evidence baseline: detached `origin/dev` at
`897db9530a45063e8f684f2800685afbfdced006`; the supplied UIIMP-014 research
and files documents dated 2026-09-02 are the bounded ticket evidence.

UIIMP-014 is the only wave-5 lane. Every other ticket named here is an
**earlier-wave prerequisite that must already be merged to `origin/dev`**:
PLAT-070, CASE-038, ENG-034, ENG-035, CASE-039, CASE-041, CASE-029, ENG-036,
ENG-029, DOCS-018, ENG-031, CASE-042, PLAT-069, DOCS-017 and PLAT-068.
PLAT-070 is a prerequisite because D44 gives it removal of the staff
image-review flag and the Workflow configuration review panel; a
whole-catalogue capture taken before it merges would bless UI that D44
forbids. Because no other lane is running, `docs/design/test-ui/**` is
uncontended and regenerating any merged lane's generated pages — including
CASE-038's `case-details--*` output — is expected, not a boundary breach.

## Verified premises

- `git status --short; git log -1 --oneline` → this checkout was clean at
  `897db953`, before the wave-5 dependencies exist.
- `Get-Content docs/design/test-ui/catalogue.json -Raw | ConvertFrom-Json` →
  Case Details is visual with `default`, `unavailable`, and `conflict`;
  Assessment is presently visual; `/Cases` uses `queues--*.html`; Operations
  has `default` and `empty`.
- `Get-Content tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` →
  `StateMatches` supports semantic state selection through
  `StateMatch(Required, Excluded)` — **one** required substring and at most
  one exclusion — and ordinary Case, Assessment, queue, and Operations
  states currently rely partly on ordinal-first candidate selection.
- `Get-Content scripts/Test-UiCatalogue.ps1` → the validator enforces routed
  sources, a reason on non-visual entries, a branch claim, the flat filename
  and prototype existence/orphan rules. It does **not** reject states on a
  non-visual entry and does not validate `scenario` at all.
- `Get-Content tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`
  and `AccessibilityTests.cs` → the existing 1580/1100/760 geometry checks
  cover authenticated unseeded routes only; no Case-record route is present,
  and the assertions live inline in the test method, not in a helper.
- `rg -n -C 3 'SeedCustodyRecoveryCaseAsync|RepositoryEvaFixture|Edit Case'
  tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` → the
  existing accepted-Case, repository-fixture, edit-lease, and `?section=`
  journey is the closest seed pattern to reuse. It seeds
  `CaseLifecycleState.Review` (line 563) and its image seeding is a separate
  `SeedEligibleImageAsync` (line 394).
- `rg -n -C 3 'Case record|Assessment|Awaiting instruction|Operations'
  docs/frd/frd-12-operator-experience.md` → the FRD requires eleven ordered
  sections, one edit mode/lease, the Assessment redirect, the read-only rule
  once Complete, the Pre-Case queue, Operations' single notice, and browser
  proof including axe and focus at all three widths.
- `.github/workflows/ci.yml` (job `test-ui`) → CI runs
  `./scripts/Update-TestUiSnapshots.ps1 -Verify` (a **fresh** capture) with a
  75-minute timeout, and the capture alone already takes ~40 minutes on the
  hosted runner.

## Governing docs

- `docs/frd/frd-12-operator-experience.md` — meets the specified Case,
  redirect, queue, Operations, and browser evidence requirements without
  changing product behaviour.
- `docs/engineering.md` — meets evidence-tier and simplicity requirements by
  reusing the capture, snapshot, fixture, and layout-test mechanisms. The
  browser walk is Web/browser evidence, not a claim of deployment or operator
  acceptance.
- `docs/design/README.md` — records snapshots of the merged UI only: no
  explanatory copy, no duplicate label vocabulary, and absent versus disabled
  remains owned by the production lanes.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | Multi-marker state matchers, manifest self-checks, retired Assessment visual state. |
| Modify | `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Deterministic captures for states not reached by the browser seed. |
| Modify | `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` | Extracted layout helper plus the seeded Case-record walk at three widths. |
| Modify | `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | Expose one `internal` seeded-browser entry point wrapping its private collaborators. |
| Modify | `docs/design/test-ui/catalogue.json` | State inventory and Assessment reclassification. |
| Generated | `docs/design/test-ui/index.html` | Generated catalogue index. |
| Generated | `docs/design/test-ui/pages/*.html` | Generated section, queue, and Operations snapshots; remove retired Assessment output. |
| Conditional modify | `scripts/Update-TestUiSnapshots.ps1` | Only if a demonstrated missing capture capability cannot be addressed by existing focused-render or browser tests. |

## Do not modify

Do not modify production Razor, partials, Core, Infrastructure, migrations,
`Presentation/OperatorLabels.cs`, CSS, JavaScript, governing docs (the FRDs
included), `docs/operator-notes.md`, or `corpus/`.

Do not hand-edit generated pages or `index.html`. Regenerating any merged
lane's `docs/design/test-ui/pages/*.html` through the script is expected;
UIIMP-014 holds that tree alone.

## Constraints

- Preserve one list of labels: assertions consume rendered labels from
  `OperatorLabels` (public, already referenced by
  `AutomationActorLabelTests`) where a label is the discriminator; they do
  not create a parallel test vocabulary.
- Preserve one list of sections. D30's order is Overview, Engineer notes,
  Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report,
  Files, Notes. The **final** `?section=` values and `section-<key>` element
  ids are read from the merged jump-nav at run time (today's shipped
  convention is `?section=case-files`, not `files`), and the browser walk
  enumerates the rendered jump-nav links rather than hard-coding a second
  copy of the eleven keys. Only the catalogue's 22 state records name the
  keys literally, because the manifest is the inventory.
- Each captured state needs a unique, stable HTML discriminator combining its
  section's active/jump marker with its edit/read-only mode marker. A
  structural `section-<key>` id alone cannot discriminate: in a single-scroll
  record every section element is present in every response.
- Use `RepositoryEvaFixture` and documented-estate values: it is the existing
  repository-controlled estate and reusing it needs no new fixture. D43
  already carries operator sign-off for the mockup's corpus-derived values,
  so no further sign-off is required if a merged section genuinely cannot be
  rendered from the existing fixture.
- The Assessment route carries no visual state after ENG-034.
- No migration is planned. A migration, policy, or production-page gap is a
  dependency on its owning ticket, not work for UIIMP-014.

## Ordered steps

### Step 1 — Verify merged shape, discriminators, and the D44/D45/D46 gates

- Preconditions: every named prerequisite, PLAT-070 included, is merged to
  `origin/dev`.
- Files: no modification.
- Symbols: `Details.cshtml` merged section frame, the merged jump-nav,
  `OperatorLabels`, Assessment redirect, Cases queue, Operations notice, and
  existing snapshot capture scenarios.
- Change: use read-only `rg`, `Get-Content`, and route/source inspection to
  record the final section keys, `section-<key>` ids, `?section=<key>`
  values, lazy-render/jump markers, edit/read-only markers, and exact
  rendered labels. Confirm the redirect returns 301 and targets
  `/Cases/{id}?section=estimate`, and record whether ENG-034 kept a routed
  `@page` returning 301 or removed the page altogether — Step 4 branches on
  that. Confirm Awaiting instruction is `?tab=awaiting-instruction`; confirm
  the partial-data notice has the merged Administration Service health link.
  Run three explicit negative gates:
  - **D44** — `rg -n 'RequireStaffImageReviewBeforeEngineerAssignment|ImagesReviewedByStaff|instructionReviewed|imagesReviewed'`
    over `src/` and `tests/` (migration history excluded) returns nothing,
    and `Administration/Configuration.cshtml` carries no review checkbox or
    panel. If anything remains, stop: it is PLAT-070's.
  - **D45** — the merged Damage section markup, the label map and the report
    projection expose zone, severity and note only; no damage-type control,
    option list or report column. If one exists, stop: it is ENG-036's. Do
    not edit the FRD text that still says "severity, type and note" —
    governing docs are not UIIMP-014's to change; report the discrepancy.
  - **D46** — the Files image viewer and the Report image cards expose the
    crop control while the record is read-only (no Edit Case pressed). If
    they do not, stop: it is ENG-031's.
- Preserved behaviour: all discovered labels continue to come from
  `Presentation/OperatorLabels.cs`.
- Forbidden: changing any unowned production surface to create a test marker.
- Negative cases: stop if a required section/mode has no unique stable HTML
  discriminator, the ids/order differ from D30, the redirect is not 301, a
  prerequisite is absent, or any of the three gates above fails; report the
  owning ticket.
- Tests: none; this is the required read-only merge-shape gate.
- Commands: `git log origin/dev --oneline`, targeted `rg`, and targeted
  `Get-Content` commands against the merged paths and tests.
- Expected output: the eleven final keys, every state discriminator, and
  three passing negative gates are recorded before a state key or matcher is
  chosen.
- Done when: the implementation can name one deterministic matcher per new
  visual scenario without relying on candidate order.
- Deviation stop: do not begin generated artefacts or test changes while any
  discriminator, prerequisite or gate remains unproved.

### Step 2 — Declare deterministic Test UI states and focused captures

- Preconditions: Step 1 identifies the final keys, labels, and markers.
- Files: `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`,
  `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs`.
- Symbols: `StateMatches`, `StateMatch`, `Generate`,
  `IntakeWebApplicationFactory`, and the existing focused Razor rendering
  pattern.
- Change:
  - Extend the existing `StateMatch` record to carry a collection of
    **all-required** markers alongside its existing single exclusion, so one
    scenario can require the section's active marker *and* the mode marker.
    This is an extension of the one existing mechanism, not a second one.
  - Declare exactly 22 Case section/mode scenarios: read-only and edit for
    each D30 section. **`case-details--default` becomes one of the 22**
    (Overview read-only) rather than a twenty-third state: the existing
    `default` branch already reads "Case Overview in Review with the edit
    lease held", so keeping it beside an Overview-edit state would duplicate
    one concept and, having no matcher, would be left with no candidate once
    every sibling has one. Retain `unavailable` and `conflict` unchanged.
  - Add matchers and focused captures for Awaiting instruction and Operations
    partial-data where the normal capture set cannot produce them
    deterministically. Remove the obsolete Assessment visual scenario.
  - Add manifest self-checks in this owned test file, because
    `Test-UiCatalogue.ps1` does not enforce them: a non-visual entry carries
    no `states`; every visual state has a non-empty `scenario`; scenarios are
    unique; and every scenario **that UIIMP-014 adds or changes** has an
    explicit matcher. Do not extend that last requirement to the 58 existing
    catalogue-wide states — that is not this ticket's scope.
- Preserved behaviour: keep existing unavailable/conflict scenarios and their
  matchers; retain generator selection, asset rewriting, and offline
  rendering.
- Forbidden: duplicate OperatorLabels strings in tests, fabricate domain data,
  weaken state assertions, or change generic capture middleware.
- Negative cases: each scenario must fail if its distinct section/mode or
  notice marker is absent; Assessment must not request a visual snapshot.
- Tests: `TestUiSnapshotTests` and `TestUiFocusedRenderTests`.
- Commands: the snapshot commands in Step 4 after all capture paths exist.
- Expected output: every new or changed catalogue visual state has exactly one
  deterministic candidate class, not an ordinal fallback.
- Done when: all required states have explicit, stable matchers and
  deterministic capture producers.
- Deviation stop: if existing capture support cannot capture a real required
  response, demonstrate the gap first; only then consider the narrowly scoped
  script change.

### Step 3 — Add the seeded three-width Case-record walk

- Preconditions: Steps 1 and 2 are complete.
- Files: `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`,
  `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs`.
- Symbols: `BrowserTestSupport.StartAsync`, `GoToAsync`,
  `RouteLaysOutWithoutOverflowClippingOrInlineStyle` (its body),
  `SeedCustodyRecoveryCaseAsync`, `SeedEligibleImageAsync`,
  `BrowserAcceptedCase`, `BrowserCaseDataState`,
  `BrowserAcceptedCaseDataQueries`, `BrowserVehicleEvidenceQueries`,
  `ConfirmedVehicle`, `RepositoryEvaFixture`, and the existing geometry
  assertions.
- Change — two named extractions, then one theory:
  1. In `OperatorJourneyTests.cs`, expose a single `internal static` seeded
     browser entry point (for example
     `SeedCaseRecordBrowserAsync(CaseLifecycleState state, bool withImage)`)
     that encapsulates its private collaborators. Widening only the three
     symbols named in the wrapper check is not enough — the seed also needs
     `BrowserCaseDataState`, `BrowserAcceptedCaseDataQueries`,
     `BrowserVehicleEvidenceQueries` and `ConfirmedVehicle`, which stay
     private behind the one new entry point. Never copy the seed.
  2. In `LayoutIntegrityTests.cs`, extract the existing test body to
     `AssertLayoutIntegrityAsync(IPage page, string label, int width)` and
     have the existing route theory call it, so the seeded walk reuses the
     same overflow, clipping, one-`main`, one-`h1` and inline-style checks
     instead of restating them.
  3. Add **three** theory cases — one per width, 1580/1100/760 — each
     starting one browser and walking the record inside that session. Three
     browser sessions, not 66: at each width the walk makes 11 read-only and
     11 edit section visits (33 + 33 = 66 visits in total across the three
     widths).
- Read-only and edit are two lifecycle shapes, not one page with the lease
  released. D30 makes Engineer sections read-only **once Complete**, and the
  existing seed hardcodes `CaseLifecycleState.Review`:
  - the read-only walk seeds a **Complete** case, asserts every section is
    still viewable, and asserts the Engineer sections expose no editable
    control;
  - the edit walk seeds a **Review** case, acquires the one lease, and
    asserts an enabled editable control per section (a disabled or absent
    control is not evidence).
- At each section: enumerate the rendered jump-nav links (do not hard-code
  the eleven keys), activate the jump control rather than only loading the
  `?section=` URL, wait for the intended lazy section marker, assert the
  scroll-spy's current item is that section, assert the section elements
  appear in D30 order, assert the sticky identity ribbon/action bar/jump-nav
  remain in view, then call `AssertLayoutIntegrityAsync`.
- D46 crop entry points are proved once per width on the read-only record:
  seed a real repository image through the new entry point, assert the crop
  control is reachable from the Files image viewer and from a Report image
  card **without** pressing Edit Case, and assert that saving one crop
  acquires the edit lease and leaves one curation record. The cropper's own
  interaction behaviour (drag, resize, rotate, aspect lock, reset, preview)
  stays with ENG-031; this walk proves reachability and the lease effect
  only.
- The FRD's acceptance evidence requires axe and focus behaviour on this
  walk, so run the existing axe and focus checks once per width against the
  seeded read-only record. Leave `AccessibilityTests.AuthenticatedRouteList`
  unseeded and preserve its existing whole-route walk — the seeded route is
  never added to that inventory.
- Forbidden: adding a second fixture estate, changing production handlers, or
  treating a disabled or absent control as a successful edit control.
- Negative cases: fail on a missing jump target, an unloaded intended section,
  a wrong scroll-spy item, sections out of D30 order, an absent
  edit/read-only discriminator, an editable Engineer control on a Complete
  case, a crop control that requires Edit Case, an axe or focus violation,
  overflow, clipping, duplicate landmarks, or inline styles.
- Tests: `LayoutIntegrityTests`; `OperatorJourneyTests` keeps every existing
  test unchanged.
- Commands: run the Browser filter with `xUnit.MaxParallelThreads=2`.
- Expected output: all 66 section/width visits, both lifecycle shapes, the
  crop entry points, and the axe/focus checks satisfy the invariants.
- Done when: the browser proof covers the actual seeded Case caller rather
  than an unseeded route inventory entry.
- Deviation stop: if the browser must change a production surface or fixture
  estate to reach a required state, stop and name its owner.

### Step 4 — Update catalogue and generated snapshots

- Preconditions: deterministic responses exist for every declared visual state.
- Files: `docs/design/test-ui/catalogue.json`, `docs/design/test-ui/index.html`,
  `docs/design/test-ui/pages/*.html`.
- Symbols: the existing manifest schema, `TestUiSnapshotTests.Generate`,
  `BuildIndex`, and `Update-TestUiSnapshots.ps1`.
- Change: record the 22 Case section/mode visual states using the final merged
  keys and flat `case-details--<section>-<mode>.html` names, with the former
  `default` folded into them per Step 2; retain `unavailable` and `conflict`.
  Add `queues--awaiting-instruction.html` and
  `operations--partial-data.html`. Handle Assessment per Step 1's finding:
  - ENG-034 kept a routed `@page` that returns 301 → convert the entry to
    `redirect` with a concise reason and no states;
  - ENG-034 removed the page → delete the inventory entry entirely, because
    `Test-UiCatalogue.ps1` rejects an inventory source that is not a current
    routed Razor page.

  Either way `case-assessment--default.html` is removed. Generate the index
  and every page through the script.
- Preserved behaviour: retain all unrelated catalogue entries, flat filenames,
  branch claims, orphan detection, and offline asset validation.
- Forbidden: hand-edit generated HTML, create an alternate state taxonomy, or
  add explanatory operator copy through snapshot-only changes.
- Negative cases: the catalogue must reject missing branch claims, duplicate
  states/files, or orphaned Assessment output; the new manifest self-checks in
  `TestUiSnapshotTests` reject a non-visual entry that still carries states.
- Tests: `TestUiSnapshotTests` and `Test-UiCatalogue.ps1`.
- Commands: `./scripts/Update-TestUiSnapshots.ps1`, then
  `./scripts/Update-TestUiSnapshots.ps1 -Verify` (a fresh capture, exactly
  what CI runs), and `./scripts/Test-UiCatalogue.ps1`.
  `-Verify -SkipCapture` is a fast local loop only; it reuses the capture the
  update run made and is not the proof.
- Expected output: generated output exactly matches committed files; no
  orphans; every offline page renders.
- Done when: the catalogue documents every required state and the generated
  artefacts are fresh.
- Deviation stop: if generation changes content beyond the expected
  whole-catalogue artefacts, stop and reconcile with its owner.

### Step 5 — Run the required verification and simplification pass

- Preconditions: the complete intended diff is present.
- Files: no new files; update only the existing ticket plan/checklist with
  findings and command results.
- Symbols: existing solution, Browser, snapshot, and catalogue commands.
- Change: run the canonical non-Browser test rail, the Browser rail, and the
  Test UI generation/verification sequence. Record the wall-clock duration of
  the fresh `-Verify` run: CI's `test-ui` job has a 75-minute timeout whose
  capture already costs ~40 minutes, and this ticket adds 22 committed pages,
  their offline renders, and three seeded browser walks. If the local fresh
  verify shows the job is at risk, report it against UIIMP-013 (which owns
  making that job cheaper) rather than trimming states or raising the
  timeout here. Review the branch diff through reuse, simplification,
  efficiency, and altitude lenses; record every finding and disposition.
- Preserved behaviour: no tests are weakened, skipped, or reclassified merely
  to pass.
- Forbidden: run `Test-MigrationGrants.ps1` unless a migration appears; if one
  appears, stop because migrations are outside UIIMP-014 scope.
- Tests: full required command set below.
- Commands: listed in the Commands section.
- Expected output: every command exits zero; the simplification pass identifies
  no unaddressed in-scope defect.
- Done when: verification evidence and simplification dispositions are ready
  for independent review.
- Deviation stop: any failing command, unexpected generated output, dependency
  addition, migration, or scope expansion stops the ticket for report.

## Acceptance checks

- `/Cases/{id}` has generated visual snapshots for every ordered section in
  read-only and edit mode — 22 states plus `unavailable` and `conflict`, with
  no separate `default` — each selected by a stable multi-marker matcher.
- The browser test proves the seeded Case record, jump-nav activation,
  scroll-spy, lazy readiness, D30 section order, sticky chrome, the Complete
  read-only rule and the single Review edit lease across all eleven sections
  at 1580, 1100, and 760, with axe and focus checks passing.
- The D46 crop control is reachable from Files and Report without Edit Case,
  and saving a crop starts the lease.
- `/Cases/{id}/Assessment` carries no visual state; its obsolete generated
  page is absent.
- `/Cases?tab=awaiting-instruction` has its generated queue state, and
  Operations has its generated partial-data notice state.
- No staff review flag, checkbox, dialog or panel (D44) and no damage type
  (D45) survives into any captured snapshot.
- `./scripts/Update-TestUiSnapshots.ps1 -Verify` (fresh capture) and
  `./scripts/Test-UiCatalogue.ps1` pass.
- This chore proves routed Web and Browser evidence only; it does not claim
  deployment, operator acceptance, migration, or policy-owner evidence.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Browser" -- xUnit.MaxParallelThreads=2
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify
./scripts/Test-UiCatalogue.ps1
```

`./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture` is available as a
fast local loop while iterating, but the committed evidence is the fresh
`-Verify` above, which is what CI runs.

Run `./scripts/Test-MigrationGrants.ps1` only if a migration unexpectedly
enters scope; stop and report that ownership breach instead of accepting it.

## Failure and deviation rules

Stop and report rather than improvise if merged shapes lack stable
discriminators, a prerequisite ticket is missing, a D44/D45/D46 gate fails, a
state requires production-page or label changes, a capture capability is
absent, a test fails, or generated output exceeds the expected catalogue
artefacts.

## Accepted risk

22 committed Case snapshots roughly double the `docs/design/test-ui` tree and
add 22 offline Chromium renders plus three seeded browser walks to a CI job
already sized at 75 minutes. D29/D30 and the ticket require per-section,
per-mode evidence, so the coverage is not trimmable here; the cost is
recorded and belongs to UIIMP-013.

## Simplification pass

Executor to complete before the PR: record reuse, simplification, efficiency,
and altitude findings against the branch diff, with each finding applied,
skipped with reason, or deferred to a named ticket.

## Stop condition

All verification commands pass, the simplification pass is recorded, the PR
targeting `dev` is open, and UIIMP-014 is moved to Review. Do not merge the PR,
write proof, or begin another ticket.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

gpt-5.6-sol read the plan independently at `origin/dev` `897db953` in the
detached research checkout (clean afterwards) and returned REQUEST CHANGES
with ten findings. Claude Opus verified each against the repository and
dispositioned them; four further findings came from the disposition pass and
are numbered 11–14.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | Starting state, Step 1 | PLAT-070 absent from the prerequisites though D44 assigns it removal of the review flag; the baseline still carries `RequireStaffImageReviewBeforeEngineerAssignment` and the Workflow configuration panel, so a capture could bless forbidden UI. | Fixed. PLAT-070 added to the prerequisites; Step 1 carries an explicit D44 negative gate. |
| 2 | blocker | Steps 2–3 | "Read-only" was untied to lifecycle. The reusable seed hardcodes `CaseLifecycleState.Review` (`OperatorJourneyTests.cs:563`), so a lease-free visit proves only "not currently editing"; D30 and FRD-12 require read-only once Complete. | Fixed. The read-only walk seeds Complete and asserts no editable Engineer control; the edit walk seeds Review with the lease held. |
| 3 | blocker | Step 3 | D46 uncovered: the plan entered edit mode via "Edit Case" only, and the seed adds no image (`SeedEligibleImageAsync` is separate). | Fixed, narrowed. The walk seeds an image and proves crop reachability from Files and Report without Edit Case plus the lease effect and one curation record. Cropper interaction behaviour stays ENG-031's — absorbing it would take another lane's scope. |
| 4 | blocker | Steps 2, 4 | Keeping `case-details--default` beside 22 section/mode states duplicates Overview-edit and, being matcher-less, can be left with no candidate once every sibling has a matcher. | Fixed. `default` becomes Overview read-only inside the 22; the route carries 22 states plus `unavailable` and `conflict`. |
| 5 | should-fix | Steps 2, 4 | The plan claimed `Test-UiCatalogue.ps1` rejects a redirect carrying states; it only requires a reason on non-visual entries and never validates `scenario`. | Fixed. The false claim is removed and the checks move into the owned `TestUiSnapshotTests`, scoped to non-visual states, scenario presence/uniqueness, and matchers for this ticket's own scenarios only. |
| 6 | should-fix | Step 3 | The named reuse was not executable: the seed also needs private `BrowserCaseDataState`, `BrowserAcceptedCaseDataQueries`, `BrowserVehicleEvidenceQueries`, `ConfirmedVehicle`, and the layout assertions are a test body, not a helper. | Fixed. Two named extractions: one `internal` seeded-browser entry point in `OperatorJourneyTests.cs`, and `AssertLayoutIntegrityAsync` in `LayoutIntegrityTests.cs`. |
| 7 | should-fix | Step 3 | Loading `?section=` and finding an id proves neither the jump/scroll-spy behaviour nor FRD-12's required axe and focus evidence (verified at `frd-12` lines 528–536). | Fixed, narrowed. Jump activation, scroll-spy, D30 order and sticky chrome are asserted, and axe/focus run once per width on the seeded record. The seeded route is still not added to `AuthenticatedRouteList`. |
| 8 | should-fix | Step 1 | D45 was not an explicit negative gate, and the FRD still reads "severity, type and note". | Fixed, partly rejected. A D45 negative gate over the merged UI, labels and report projection is added. Editing the FRD is rejected: governing docs are not UIIMP-014's to change; the discrepancy is reported to its owner instead. |
| 9 | should-fix | Step 2 | `StateMatch` supports one required substring plus one exclusion, so "section marker and mode marker" is not expressible; and "every catalogue visual state" would drag in 58 states against 24 matchers. | Fixed. `StateMatch` is extended with an all-required marker collection, and the deterministic-matcher rule is scoped to this ticket's new or changed scenarios. |
| 10 | nit | Constraints, ownership | CASE-038's pages described as "reserved" and prerequisites called concurrent wave-5 lanes; D43 treated as awaiting sign-off it already records. | Fixed. Prerequisites are described as merged earlier-wave tickets, the reservation warning is gone, and `RepositoryEvaFixture` is justified as the existing estate rather than by an unresolved D43. |
| 11 | should-fix | Step 4, Commands | The plan proved with `-Verify -SkipCapture`, which reuses the capture the update run just made; CI runs a fresh `-Verify`, and CLAUDE.md names that as the proof. | Fixed. Fresh `-Verify` is the committed evidence; `-SkipCapture` is labelled a local loop. |
| 12 | should-fix | Step 4 | The plan assumed the Assessment entry converts to `redirect`; if ENG-034 removes the routed page, `Test-UiCatalogue.ps1` (line 40) rejects the inventory source and the entry must be deleted. | Fixed. Step 1 records which shape landed and Step 4 branches on it. |
| 13 | should-fix | Constraints, Step 3 | The plan hard-coded eleven `section-<key>` ids as a constraint while Step 1 was meant to discover them, and the shipped convention is `?section=case-files`, not `files` — a second copy of the section list. | Fixed. The walk enumerates the rendered jump-nav; only the catalogue names keys literally. |
| 14 | should-fix | Step 5 | The plan costed nothing, though CI's `test-ui` job is already ~40 minutes of capture inside a 75-minute timeout and this ticket adds 22 pages, their renders and three walks. | Fixed as an accepted risk: the fresh verify is timed and any shortfall is reported against UIIMP-013 rather than trimming coverage or raising the timeout here. |

No finding required an operator decision; the ticket records no open
questions.
