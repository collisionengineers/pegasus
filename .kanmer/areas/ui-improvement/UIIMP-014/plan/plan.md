# Plan — UIIMP-014 (2026-09-02, gpt-5.6-terra high)

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
  as they stand, so Step 3's "small local helper" must not become a second
  copy of the seed: widen the three to `internal` (or move them into a
  shared `Browser/` helper file) under the conditional
  `OperatorJourneyTests.cs` ownership, and record that as the reuse in the
  post-implementation report.
- `LayoutIntegrityTests` (lines 17–78) is a single theory over
  `AccessibilityTests.AuthenticatedRouteList` × {1580, 1100, 760}; the
  seeded Case walk is a second `[Theory]` in the same class reusing the
  same `AllowedClipSelector`, geometry and inline-style checks.
- `docs/frd/frd-12-operator-experience.md` (lines 98–102, 152–174, 317,
  355–362, 534–536) states the eleven-section record, the
  `?section=estimate` redirect, the Awaiting instruction Pre-Case queue,
  the one-line partial-data notice and the 1580/1100/760 walk, as the plan's
  verified premises claim.

Wrapper corrections applied to the Codex text: Step 3's visit count was
"66 read-only and 66 edit"; it is 33 + 33 (11 sections × 3 widths each).
The `?tab=awaiting-instruction` value in Step 1 is CASE-042's to settle and
is verified there, not assumed.

## Objective

Regenerate the Test UI catalogue and snapshots for the merged single-scroll
Case record, its 22 section/mode states, the retired Assessment redirect,
Awaiting instruction, and the Operations partial-data notice; add the seeded
three-width Case-record browser walk.

Diff estimate: four test/catalogue files plus generated Test UI artefacts; no
production, policy, migration, label, CSS, or JavaScript changes.

## Starting state

Evidence baseline: detached `origin/dev` at
`897db9530a45063e8f684f2800685afbfdced006`; the supplied UIIMP-014 research
and files documents dated 2026-09-02 are the bounded ticket evidence.

UIIMP-014 starts only after CASE-038, ENG-034, ENG-035, CASE-039, CASE-041,
CASE-029, ENG-036, ENG-029, DOCS-018, ENG-031, CASE-042, PLAT-069,
DOCS-017, and PLAT-068 have merged to `origin/dev`. The shared
`docs/design/test-ui/**` lock has capacity one.

## Verified premises

- `git status --short; git log -1 --oneline` → this checkout was clean at
  `897db953`, before the wave-5 dependencies exist.
- `Get-Content docs/design/test-ui/catalogue.json -Raw | ConvertFrom-Json` →
  Case Details is visual with `default`, `unavailable`, and `conflict`;
  Assessment is presently visual; `/Cases` uses `queues--*.html`; Operations
  has `default` and `empty`.
- `Get-Content tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` →
  `StateMatches` supports semantic state selection, but ordinary Case,
  Assessment, queue, and Operations states currently rely partly on
  ordinal-first candidate selection.
- `Get-Content tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`
  and `AccessibilityTests.cs` → the existing 1580/1100/760 geometry checks
  cover authenticated unseeded routes only; no Case-record route is present.
- `rg -n -C 3 'SeedCustodyRecoveryCaseAsync|RepositoryEvaFixture|Edit Case'
  tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` → the
  existing accepted-Case, repository-fixture, edit-lease, and `?section=`
  journey is the closest seed pattern to reuse.
- `rg -n -C 3 'Case record|Assessment|Awaiting instruction|Operations'
  docs/frd/frd-12-operator-experience.md` → the FRD requires eleven ordered
  sections, one edit mode/lease, the Assessment redirect, the Pre-Case queue,
  Operations' single notice, and browser proof at all three widths.

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
| Modify | `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs` | Semantic state matchers and retired Assessment visual-state removal. |
| Modify | `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs` | Deterministic captures for states not reached by the browser seed. |
| Modify | `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` | Seeded Case-record walk at three widths. |
| Conditional modify | `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | Extract its Case seed only if a small local layout-test helper cannot reuse it. |
| Modify | `docs/design/test-ui/catalogue.json` | State inventory and Assessment redirect classification. |
| Generated | `docs/design/test-ui/index.html` | Generated catalogue index. |
| Generated | `docs/design/test-ui/pages/*.html` | Generated section, queue, and Operations snapshots; remove retired Assessment output. |
| Conditional modify | `scripts/Update-TestUiSnapshots.ps1` | Only if a demonstrated missing capture capability cannot be addressed by existing focused-render or browser tests. |

## Do not modify

Do not modify production Razor, partials, Core, Infrastructure, migrations,
`Presentation/OperatorLabels.cs`, CSS, JavaScript, governing docs,
`docs/operator-notes.md`, or `corpus/`.

Do not hand-edit generated pages or `index.html`. Do not overwrite CASE-038's
reserved `case-details--default.html` or `case-details--conflict.html`; a
whole-catalogue generator run may regenerate them.

## Constraints

- Preserve one list of labels: assertions consume rendered labels from
  `OperatorLabels`, especially Case workspace, lifecycle, queue, and Operations
  labels; they do not create a parallel test vocabulary.
- The merged Case page must expose these ordered IDs:
  `section-overview`, `section-engineer-notes`, `section-inspection`,
  `section-vehicle`, `section-damage`, `section-valuation`,
  `section-estimate`, `section-settlement`, `section-report`,
  `section-files`, and `section-notes`.
- Each captured state needs a unique, stable HTML discriminator for its
  section, active/jump state, and edit/read-only mode. “Edit Case” versus
  “Finish editing” is acceptable only when combined with the merged
  section-specific marker; generic route captures and capture order are not.
- Use `RepositoryEvaFixture` and documented-estate values. Do not copy mockup
  claimant contact values; D43 values require documented operator sign-off
  before any use.
- The Assessment route must be `redirect` with a non-empty reason and no
  visual state. An excluded capability is absent; only approved seams remain
  disabled.
- No migration is planned. A migration, policy, or production-page gap is a
  dependency on its owning ticket, not work for UIIMP-014.

## Ordered steps

### Step 1 — Verify merged wave-5 shape and capture discriminators

- Preconditions: every named wave-5 dependency is merged to `origin/dev`;
  UIIMP-014 holds the Test UI shared lock.
- Files: no modification.
- Symbols: `Details.cshtml` merged section frame, `OperatorLabels`,
  Assessment redirect, Cases queue, Operations notice, and existing snapshot
  capture scenarios.
- Change: use read-only `rg`, `Get-Content`, and route/source inspection to
  record the final section keys, `section-<key>` IDs, `?section=<key>` values,
  lazy-render/jump markers, edit/read-only markers, and exact rendered labels.
  Confirm the redirect returns 301 and targets
  `/Cases/{id}?section=estimate`; confirm Awaiting instruction is
  `?tab=awaiting-instruction`; confirm the partial-data notice has the merged
  Administration Service health link.
- Preserved behaviour: all discovered labels continue to come from
  `Presentation/OperatorLabels.cs`.
- Forbidden: changing any unowned production surface to create a test marker.
- Negative cases: stop if a required section/mode has no unique stable HTML
  discriminator, the IDs/order differ from D30, the redirect is not 301, or a
  dependency is absent; report the owning dependency ticket.
- Tests: none; this is the required read-only merge-shape gate.
- Commands: `git log origin/dev --oneline`, targeted `rg`, and targeted
  `Get-Content` commands against the merged paths and tests.
- Expected output: all eleven exact IDs and all required state discriminators
  are recorded before a state key or matcher is chosen.
- Done when: the implementation can name one deterministic matcher per new
  visual scenario without relying on candidate order.
- Deviation stop: do not begin generated artefacts or test changes while any
  discriminator or dependency remains unproved.

### Step 2 — Declare deterministic Test UI states and focused captures

- Preconditions: Step 1 identifies the final keys, labels, and markers.
- Files: `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`,
  `tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs`.
- Symbols: `StateMatches`, `Generate`, `IntakeWebApplicationFactory`, and the
  existing focused Razor rendering pattern.
- Change: add explicit semantic matchers for each of the 22 Case scenarios:
  read-only and edit for each D30 section, using the final section marker plus
  the final mode marker. Add matchers and focused captures for Awaiting
  instruction and Operations partial-data where the normal capture set cannot
  produce them deterministically. Remove the obsolete Assessment visual
  scenario and capture expectation.
- Preserved behaviour: keep existing unavailable/conflict scenarios and their
  matchers; retain generator selection, asset rewriting, and offline rendering.
- Forbidden: duplicate OperatorLabels strings in tests, fabricate domain data,
  weaken state assertions, or change generic capture middleware.
- Negative cases: each scenario must fail if its distinct section/mode or
  notice marker is absent; Assessment must not request a visual snapshot.
- Tests: `TestUiSnapshotTests` and `TestUiFocusedRenderTests`.
- Commands: the snapshot commands in Step 4 after all capture paths exist.
- Expected output: every catalogue visual state has exactly one deterministic
  candidate class, not an ordinal fallback.
- Done when: all required states have explicit, stable matchers and deterministic
  capture producers.
- Deviation stop: if existing capture support cannot capture a real required
  response, demonstrate the gap first; only then consider the narrowly scoped
  script change.

### Step 3 — Add the seeded three-width Case-record walk

- Preconditions: Steps 1 and 2 are complete; the seeded case can render all
  eleven sections and acquire the one Case edit lease.
- Files: `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`;
  conditionally `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs`.
- Symbols: `BrowserTestSupport.StartAsync`, `GoToAsync`,
  `RouteLaysOutWithoutOverflowClippingOrInlineStyle`,
  `SeedCustodyRecoveryCaseAsync`, `RepositoryEvaFixture`, and the existing
  geometry assertions.
- Change: add a dedicated seeded Case theory at 1580, 1100, and 760. Reuse the
  accepted Case seed and lease flow, preferably through a small local helper.
  At each width, visit every final `?section=` target, wait for its intended
  lazy section marker, prove the corresponding `section-<key>` is present,
  then apply the existing overflow, clipping, one-`main`, one-`h1`, and
  inline-style checks. Enter the single edit mode once and repeat the section
  walk with the merged edit marker and editable control evidence.
- Preserved behaviour: leave `AccessibilityTests.AuthenticatedRouteList`
  unseeded; preserve its existing whole-route walk.
- Forbidden: adding a second fixture estate, changing production handlers, or
  treating a disabled or absent control as a successful edit control.
- Negative cases: fail on a missing jump target, an unloaded intended section,
  an absent edit/read-only discriminator, overflow, clipping, duplicate
  landmarks, or inline styles.
- Tests: `LayoutIntegrityTests`; retain `OperatorJourneyTests` unless a shared
  extraction is genuinely necessary.
- Commands: run the Browser filter with `xUnit.MaxParallelThreads=2`.
- Expected output: all 33 read-only section/width visits and all 33 edit
  section/width visits (11 sections × 3 widths each, 66 in total) satisfy
  the existing layout invariants.
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
- Change: add the 22 Case section/mode visual states using final merged keys
  and flat `case-details--<section>-<mode>.html` names; retain existing Case
  default/unavailable/conflict states. Add `queues--awaiting-instruction.html`
  and `operations--partial-data.html`. Convert Assessment to `redirect` with a
  concise reason, remove its visual state, and let generation remove
  `case-assessment--default.html`. Generate the index and every page through
  the script.
- Preserved behaviour: retain all unrelated catalogue entries, flat filenames,
  branch claims, orphan detection, and offline asset validation.
- Forbidden: hand-edit generated HTML, create an alternate state taxonomy, or
  add explanatory operator copy through snapshot-only changes.
- Negative cases: the catalogue must reject missing branch claims, a redirect
  with visual states, duplicate states/files, or orphaned Assessment output.
- Tests: `TestUiSnapshotTests` and `Test-UiCatalogue.ps1`.
- Commands: `./scripts/Update-TestUiSnapshots.ps1`, then
  `./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture`, then
  `./scripts/Test-UiCatalogue.ps1`.
- Expected output: generated output exactly matches committed files; no
  orphans; every offline page renders.
- Done when: the catalogue documents every required state and the generated
  artefacts are fresh.
- Deviation stop: if generation changes unowned or reserved content beyond
  expected whole-catalogue artefacts, stop and reconcile with its owner.

### Step 5 — Run the required verification and simplification pass

- Preconditions: the complete intended diff is present.
- Files: no new files; update only the existing ticket plan/checklist with
  findings and command results.
- Symbols: existing solution, Browser, snapshot, and catalogue commands.
- Change: run the canonical non-Browser test rail, the Browser rail, and the
  Test UI generation/verification sequence. Review the branch diff through
  reuse, simplification, efficiency, and altitude lenses; record every
  finding and disposition.
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
  read-only and edit mode, each selected by a stable semantic matcher.
- The browser test proves the seeded Case record, section jumps/lazy readiness,
  and one edit lease across all eleven sections at 1580, 1100, and 760.
- `/Cases/{id}/Assessment` is catalogued only as a redirect with a reason; its
  obsolete generated visual page is absent.
- `/Cases?tab=awaiting-instruction` has its generated queue state, and
  Operations has its generated partial-data notice state.
- `Update-TestUiSnapshots.ps1 -Verify -SkipCapture` and
  `Test-UiCatalogue.ps1` pass after a fresh capture.
- This chore proves routed Web and Browser evidence only; it does not claim
  deployment, operator acceptance, migration, or policy-owner evidence.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category=Browser" -- xUnit.MaxParallelThreads=2
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

Run `./scripts/Test-MigrationGrants.ps1` only if a migration unexpectedly
enters scope; stop and report that ownership breach instead of accepting it.

## Failure and deviation rules

Stop and report rather than improvise if merged shapes lack stable discriminators,
a prerequisite ticket is missing, a state requires production-page or label
changes, mockup personal data is proposed without D43 operator sign-off, a
capture capability is absent, a test fails, or generated output exceeds the
expected catalogue artefacts.

## Simplification pass

Executor to complete before the PR: record reuse, simplification, efficiency,
and altitude findings against the branch diff, with each finding applied,
skipped with reason, or deferred to a named ticket.

## Stop condition

All verification commands pass, the simplification pass is recorded, the PR
targeting `dev` is open, and UIIMP-014 is moved to Review. Do not merge the PR,
write proof, or begin another ticket.
