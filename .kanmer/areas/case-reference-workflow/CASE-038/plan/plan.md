# Plan — CASE-038 (2026-09-02, gpt-5.6-terra xhigh)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in `.worktrees/research` at `origin/dev` = `897db953`
(three DELIV-041 docs-only commits past the research's `cad00be9`; among
the owned paths only `docs/design/README.md` changed); `git status
--porcelain` was empty afterwards. The wrapper did every board read and
write. Wrapper-verified inputs fed to the prompt and confirmed in the plan:

- DELIV-041 (#647) already added the five vocabulary rows (`case-sticky`,
  `section-nav`/`section-link`, `suggest-btn`, `outcome-option`, `derived`)
  at `docs/design/README.md` lines 808–816, so the README row in
  `files/files.md` is obsolete: CASE-038 makes no governing-doc edit.
- The eleven `?section=` keys are fixed by the governing docs (README line
  743, FRD-12): `overview`, `engineer-notes`, `inspection`, `vehicle`,
  `damage`, `valuation`, `estimate`, `settlement`, `report`, `files`,
  `notes`. AGENTS.md rule 6 means the legacy `valuations`,
  `inspection-address` and `case-files` keys are deleted, not aliased; the
  test callers are listed by file:line in Step 5.
- Only `Details.cshtml` and `_CaseWorkspaceNav.cshtml` compose
  `case-workspace`/`case-section-nav`/`case-context` (`rg -l`), so their CSS
  is retired in Step 3 (the README's "until no page composes them" clause).
- `PageModel.Partial` is available on `net10.0`; `LayoutIntegrityTests` is
  `[Trait("Category", "Browser")]` (and `SqlServer`).
- The ribbon labels are hard-coded strings today (`Details.cshtml` lines
  104–124: "Case/PO", "Registration", "Claimant", "Not recorded"); Step 2
  moves the frame vocabulary into `OperatorLabels.CaseWorkspace`.

Two wrapper corrections to the Codex text:

1. **CASE-041 circularity settled.** Codex made CASE-041's one-line
   inspection form-id change a prerequisite and told the executor to stop
   on the circular dependency. CASE-038 blocks CASE-041 (it merges first),
   so that prerequisite can never arrive. The wrapper's decision: the rename
   of `id="case-edit-form" data-edit-save` in
   `_CaseInspectionAddress.cshtml` (lines 71–76: id, attribute and the
   now-false "only one section renders an edit form at a time" comment) is
   a frame invariant, not inspect-at scope, and rides in CASE-038 under the
   `Pages/Cases/Shared/*` capacity-one lock this lane already holds for
   `_CaseWorkspaceNav.cshtml`. It is a declared exception in the same shape
   as the six test-consumer retargets; CASE-041 inherits the renamed form.
   The orchestrator may override this by reassigning the line to CASE-041
   and accepting a duplicate-id HTML defect on CASE-038's PR; the plan does
   not choose that.
2. **Browser test command** corrected to the runbook's exact form
   (`docs/runbook.md` line 325: the IntegrationTests project and
   `Category=Browser&Category!=Corpus`), not a solution-wide
   `Category=Browser` filter.

Wrapper re-entry (Claude, 2026-09-02, second pass): the plan and checklist
already post-dated the research, so Codex was not re-run; this pass finished
the ENG-034 reconciliation below and re-verified at `897db953` (clean): the
nine Assessment handler line numbers; lease claim/renew/heartbeat/release on
`DetailsModel` lines 247–313; `CanOpenAssessment` set from the existing
`IGetAssessmentAccess` load (`Details.cshtml.cs` 24/165/210, read at
`Details.cshtml` 274); no `_CaseDamage`/`_CaseEstimate`/`_CaseSettlement`/
`_CaseReport` file exists; `_CaseInspectionAddress.cshtml` 71–76 and
`_CaseWorkflow.cshtml` 161 both carry `id="case-edit-form" data-edit-save`.

## Contract reconciliation with ENG-034 (wrapper, 2026-09-02)

ENG-034's plan (`plan/plan.md`, written 2026-09-03 01:15Z, one minute
before this one) records a seven-item contract "CASE-038's own plan must
honour" and adopts its option A: CASE-038's `DetailsModel` hosts the moved
Assessment handler surface. Reconciliation, item by item:

| ENG-034 item | CASE-038 plan | Status |
| --- | --- | --- |
| 1 — five Engineer hosts in D30 order, all viewable, no Open Assessment / `CanOpenAssessment` | Steps 1–2 | Met |
| 2 — compose the four partials with `model="Model"`; expose Case id, assessment projection, estimates and selected editor state, actor role, operation keys, `LeaseToken`, `AssessmentIsReadOnly` from `AssessmentAccessPolicy.IsReadOnly` | Step 2 composes the four shells. Step 1 exposes Case id, actor role, operation keys, `LeaseToken` (already present) and `AssessmentIsReadOnly` — a one-line swap for `CanOpenAssessment` on the existing `IGetAssessmentAccess` load (`Details.cshtml.cs` 210). The assessment projection, estimates and selected-editor state have no reader until ENG-034's forms exist, so ENG-034 adds them with its handlers (decision below) | Met for the frame; projection/editor state deferred to ENG-034 |
| 3 — Case-page implementations of lease claim/heartbeat/release (already on `DetailsModel`, lines 247–313) and of `GenerateReportDraft`, `PreviewReportDraft` (GET), `SendToClaude`, `SaveEstimate`, `EditLine`, `DuplicateEstimate`, `DiscardEstimate`, `SetCurrentEstimate`, `ImportEstimate`, redirecting to `/Cases/{id}?section=estimate` | Lease handlers met. The nine Assessment handlers are `Assessment/Index.cshtml.cs` lines 529–1382 (about 800 lines plus helpers). Hosting them here before ENG-034 renders the forms that post to them ships registered-but-unreachable code (AGENTS.md rule 14) and a second copy of every handler until ENG-034 deletes the originals (rule 8) | **Not met by design — option B adopted below**: ENG-034 adds the nine handlers to `DetailsModel` in the PR that moves the forms |
| 4 — `OnGetPreviewReportDraftAsync` on `DetailsModel` | Moves with `_CaseReport` in ENG-034's PR (option B) | Deferred to ENG-034 |
| 5 — one lease across lazy fragments; unsaved estimate form preserved; dirty-form and heartbeat rebound | Step 3 | Met |
| 6 — `_CaseDamage`, `_CaseEstimate`, `_CaseSettlement`, `_CaseReport` exist as heading-only shells composed by `Details.cshtml` | Step 2 creates them (adopted: cheap, and a `<partial>` whose file is missing fails at render time) | Met |
| 7 — `?section=damage|estimate|settlement|report` renders server-side on first GET | Step 2 (addressed host) | Met |

**Decision on item 3 (wrapper-settled): option B.** CASE-038 supplies the
host shape only; ENG-034 adds the nine Assessment handlers (and
`OnGetPreviewReportDraftAsync`) to `DetailsModel` in the same PR that moves
the forms which post to them, after CASE-038 has merged. Reasons, each a
repository rule rather than a preference:

- AGENTS.md rule 14 (done means wired): under option A CASE-038 would ship
  about 800 handler lines that no rendered form posts to until ENG-034
  lands — registered-but-unreachable code, provable only by synthetic
  handler tests. Under B the forms and their handlers ride one diff and the
  reachability proof is the real page.
- Rules 1, 2 and 8 (scope is the brief; never absorb another ticket's
  scope; one list per concept): the Assessment handlers are ENG-034's
  brief, and copying them before ENG-034 deletes the originals leaves two
  implementations of every handler on `dev` for the whole gap between the
  two merges.
- `docs/engineering.md` § Plan sizing: the frame diff is CSS, JS, Razor
  and tests; an 800-line handler transplant would outweigh it.
- ENG-034's stated cost of B ("taking CASE-038's file after it merges,
  extending the capacity-one lease") does not hold: CASE-038 blocks
  ENG-034, so by the time ENG-034 starts `Details.cshtml.cs` is a merged
  file with no lease. `Details.cshtml.cs` is not on the shared-lock list
  in `waves.md`; ENG-034 takes it like any released whole file, serialized
  by the orchestrator against CASE-039/040/041, which also touch the
  Details pair after CASE-038.

Consequence for ENG-034 (orchestrator action, recorded in this ticket's
scratch and in ENG-034's): ENG-034's plan Step 1 ("no new host, lease, or
handler path is created") and Step 3 ("remove the source handler surface
only after CASE-038 owns it") need one amendment — ENG-034 moves the nine
handlers onto `DetailsModel` itself, redirecting to
`/Cases/{id}?section=estimate`, in the PR that moves the forms and
retires the route; its option table's row B becomes the adopted row.
Nothing else in ENG-034's plan changes. CASE-038's own deliverable is the
same under either option except for the handler transplant, so this
decision does not block CASE-038; if the orchestrator overrides to A, the
handler-transplant step kept in `scratch/notes.md` ("Option A
contingency") is the step to reinstate.

Diff estimate: 22 files changed: 11 owned frame, test, and snapshot files;
four new heading-only shell partials (ENG-034 contract item 6); six exact
mechanical test-consumer retargets and the one-line inspection form-id
rename as the explicitly declared exceptions below; `catalogue.json` only
if its Details branch wording changes. No Assessment handler moves (option
B above). No new project, package, Core port, adapter, registration,
schema, or migration.

## Objective

Replace the Case record's exclusive `?section=` panels with the D29/D30
single-scroll workspace: a sticky identity/action/jump-nav frame, eleven
ordered section hosts, lazy body loading, scroll-spy, and no-script addressed
section rendering.

Evidence is pinned to clean detached `897db953` (`git rev-parse HEAD; git
log -1 --oneline; git status --short`). `rg -n` confirms DELIV-041 already
added all five component-vocabulary rows, so this ticket makes no
`docs/design/README.md` change.

## Governing docs

- **FRD-12:** Meet the one-scroll frame, D30 section order, sticky jump-nav,
  lazy rendering, `?section=` jump, all-sections-viewable rule, and removal of
  Open Assessment. Verified with `Get-Content
  docs/frd/frd-12-operator-experience.md`.
- **FRD-01:** Meet one Case edit mode and one server-owned lease. Every
  mutation retains its lease token and version; no forced takeover, merge, or
  bypass is introduced. Verified with `rg -n -i -C 3
  'case edit authority|edit authority and recovery|one edit|lease'
  docs/frd/frd-01-case-identity-and-lifecycle.md`.
- **Design authority:** Reuse `case-sticky`, `section-nav`, and the eleven
  fixed keys already recorded in `docs/design/README.md`; do not alter its
  governing text or component vocabulary.

## Starting state and resolved decisions

- `DetailsModel.Section` currently accepts obsolete keys and drives exclusive
  body rendering. The replacement accepts only `overview`, `engineer-notes`,
  `inspection`, `vehicle`, `damage`, `valuation`, `estimate`, `settlement`,
  `report`, `files`, and `notes`; unknown and deleted old keys select
  Overview. There are no aliases.
- The first GET renders Overview, Engineer notes, and Inspection, plus an
  addressed later host. Thus `?section=estimate` has
  `#section-estimate` and works over plain HTTP; client script only scrolls.
- `OperatorLabels.CaseWorkspace` becomes the one ordered key/label source.
  `DetailsModel`, `_CaseWorkspaceNav`, Razor hosts, and script data attributes
  consume it; JavaScript keeps no duplicate section list.
- Existing partials are reused directly: `_CaseWorkflow` and `_CaseSummary`
  for Overview; `_CaseInspectionAddress` for Inspection; `_CaseVehicle`,
  `_CaseFiles`, and `_CaseHistory` as lazy bodies.
- CASE-038 renders heading-only hosts, with no empty-state prose or controls,
  for Engineer notes, Damage, Valuation, Estimate, Settlement, and Report.
  Their stable outer hosts are `section-<key>`. Damage, Estimate, Settlement
  and Report are composed from four new heading-only partials
  `_CaseDamage.cshtml`, `_CaseEstimate.cshtml`, `_CaseSettlement.cshtml`,
  `_CaseReport.cshtml` (`@model Pegasus.Web.Pages.Cases.DetailsModel`, one
  heading from `OperatorLabels`, nothing else) that ENG-034 owns and fills
  after CASE-038 merges (contract item 6). Engineer notes and Valuation keep
  a named inner slot; CASE-039 and CASE-029 create their partials and add
  the one composition line. A later lane adds its lazy body through
  `DetailsModel.OnGetSectionAsync` without restructuring the sticky frame,
  nav, or host identifiers.
- `OnGetSectionAsync` is the first Razor fragment endpoint. It calls the same
  authorized case-load and `RestoreLeaseState` path as `OnGetAsync`, loads
  only the requested section projection, and returns
  `PageModel.Partial(viewName, model)` for an implemented body. A heading-only
  current seam returns no body. `PageModel.Partial` is present in the installed
  `Microsoft.AspNetCore.App.Ref` net10.0 reference pack; the Web project
  targets `net10.0`.
- `CanOpenAssessment` and Open Assessment are removed from this page. Section
  visibility never uses that former gate; `DetailsModel` exposes
  `AssessmentIsReadOnly`, derived directly from
  `AssessmentAccessPolicy.IsReadOnly` (Complete only), for the Engineer
  bodies (contract item 2).
- The sticky Save remains bound to the Overview
  `_CaseWorkflow` form, the sole `id="case-edit-form"` and
  `data-edit-save` form. The fragment initializer must register every
  lease-carrying form with the dirty guard, while Ctrl+S submits the currently
  dirty Case form rather than the document's first matching form. This retains
  one edit mode and one lease without pretending independent posts are one
  HTML form.
- The absent Sign-off value reuses the ribbon's ordinary `"Not recorded"`
  value rendering; CASE-038 adds only the label and slot, not data or a
  default.

## Expected files

- `src/Pegasus.Web/Pages/Cases/Details.cshtml`
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` (create,
  heading-only shell; ENG-034 fills it — contract item 6)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseEstimate.cshtml` (create, as above)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml` (create, as above)
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml` (create, as above)
- `src/Pegasus.Web/wwwroot/css/site.css`
- `src/Pegasus.Web/wwwroot/js/site.js`
- `src/Pegasus.Web/Presentation/OperatorLabels.cs`
- `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs`
- `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`
- `docs/design/test-ui/pages/case-details--default.html`
- `docs/design/test-ui/pages/case-details--conflict.html`
- `docs/design/test-ui/catalogue.json`, only when the Details branch text
  still describes the replaced side-nav/context frame.
- Explicit mechanical-consumer exception:
  `CaseVehicleWebTests.cs`, `CaseTasksWebTests.cs`,
  `CaseCustodyWebTests.cs`, `ImageIntakeWebTests.cs`,
  `ImageViewingWebTests.cs`, and `Browser/OperatorJourneyTests.cs`.
- Declared `Pages/Cases/Shared/*` lock exception (wrapper-settled):
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml` — the
  form `id`/`data-edit-save` rename and its comment only.

Do not modify `docs/design/README.md`, `AccessibilityTests.cs`, any
`Pages/Cases/Assessment/**` file, any other line of
`_CaseInspectionAddress.cshtml`, Core, Infrastructure, migrations, or Test
UI files other than the three listed paths.

## Dependencies on other lanes

- **CASE-041 — inspection form identity (wrapper-settled, see the wrapper
  section above).** CASE-038 blocks CASE-041, so CASE-041 cannot deliver a
  prerequisite. CASE-038 therefore makes the one mechanical change itself
  under the `Pages/Cases/Shared/*` capacity-one lock it already holds:
  in `_CaseInspectionAddress.cshtml` rename the form to
  `id="case-inspection-address-form"`, remove `data-edit-save`, and replace
  the "only one section renders an edit form at a time" comment; retain its
  existing Details `Save` handler, antiforgery token, version, lease token
  and in-section submit button. No other line of that partial changes.
  Contract text for CASE-041: "The inspection form is
  `case-inspection-address-form`, posts to the Details `Save` handler, and
  is not the sticky-bar Save target; keep it so. Do not create another
  `case-edit-form`." Do not hide the duplicate id with JavaScript.

- **ENG-034 — Engineer composition and handler-host contract** (see
  Contract reconciliation above). "CASE-038 supplies the eleven stable
  Case-page section hosts, the fragment URL convention
  (`/Cases/{id}?handler=Section&section=<key>`), the four heading-only
  shells, and on `DetailsModel` the Case id, actor role, operation keys,
  `LeaseToken`, the lease claim/renew/heartbeat/release handlers and
  `AssessmentIsReadOnly`. ENG-034 owns the content of `_CaseDamage`,
  `_CaseEstimate`, `_CaseSettlement`, `_CaseReport`, Assessment source
  removal, and their tests, and adds to `DetailsModel` — in the PR that
  moves the forms — the assessment projection, estimates and
  selected-editor state, the nine Assessment handlers and
  `OnGetPreviewReportDraftAsync`, with mutating results redirecting to
  `/Cases/{id}?section=estimate`, preserving antiforgery, lease, version,
  operation keys and any `estimate`/dialog query state, and leaving no
  second Assessment handler path. CASE-038 reserves the host shape only;
  it does not move handlers from ENG-034-owned files."

- **CASE-039 — Engineer notes contract.**
  "Render into the existing `#section-engineer-notes` inner slot only; keep
  the frame, key, heading, navigation order, and Case lease. The section is
  separate from Notes history and has no placeholder controls before its
  partial exists."

- **CASE-029 — Valuation contract.**
  "Render the Valuation body only into `#section-valuation`; retain its fixed
  host and D30 position. CASE-038 ships its heading only and creates no
  valuation data, migration, or partial."

- **CASE-040 — Sign-off contract.**
  "CASE-038 supplies only the Sign-off Engineer ribbon label and ordinary
  absent-value slot. CASE-040 supplies Case data, eligible-account query,
  default rule, and EVA dialog behaviour; it must not duplicate the frame
  label or change CASE-038 action-bar scope."

- **UIIMP-014 — Test UI contract.**
  "CASE-038 owns regenerated default and conflict snapshots for the frame,
  with unavailable byte-identical. UIIMP-014 owns only new per-section
  edit/read-only states and their catalogue rows."

- **ENG-034 test contract.**
  `AssessmentEstimateImportWebTests.cs:48` must change when ENG-034 supplies
  the redirected Case handler: assert the `section=estimate` destination
  rather than asserting all section query text absent.

## Ordered steps

### Step 1 — Establish the canonical Case-section composition

- **Files:** `Details.cshtml.cs`, `OperatorLabels.cs`,
  `_CaseWorkspaceNav.cshtml`.
- **Reuse:** `DetailsModel.OnGetAsync`, `CaseMutationPageModel` lease helpers,
  `IGetCase`, `IGetAssessmentAccess` (already injected, `Details.cshtml.cs`
  24), and the existing accessible nav markup; `IGetAssessmentWorkspace` is
  not added — the projection arrives with ENG-034 (option B).
- **Change:** Replace `SectionFilter` alternatives with the eleven canonical
  keys from the single `OperatorLabels.CaseWorkspace` ordered definition.
  Delete the five old keys rather than mapping them. Replace
  `CanOpenAssessment` with `AssessmentIsReadOnly` (`= access.IsReadOnly`)
  on the existing `IGetAssessmentAccess` load and remove its visibility
  use. Add `OnGetSectionAsync`
  using the common authorized load, lease restoration, and section-specific
  projection loading; return only the requested partial body.
- **Preserve:** Unknown keys select Overview; missing/unauthorized cases keep
  the full GET's NotFound/Forbid behaviour; fragment requests never replace
  the record frame or another section.
- **Done when:** A canonical fragment request returns only its named body;
  an old key is not accepted; no new port, registration, schema, or migration
  exists.

### Step 2 — Render the stable single-scroll frame

- **Files:** `Details.cshtml`, `_CaseWorkspaceNav.cshtml`,
  `OperatorLabels.cs`, and the four new shells `_CaseDamage.cshtml`,
  `_CaseEstimate.cshtml`, `_CaseSettlement.cshtml`, `_CaseReport.cshtml`.
- **Reuse:** Existing record ribbon, action bar, edit bar, `StatusChip`,
  `_CaseWorkflow`, `_CaseSummary`, `_CaseInspectionAddress`, `_CaseVehicle`,
  `_CaseFiles`, and `_CaseHistory`; the `@model DetailsModel` partial
  convention of `_CaseVehicle.cshtml` for the four shells.
- **Change:** Replace the workspace grid with `case-sticky` and `section-nav`;
  render eleven ordered `section-<key>` hosts. Render the first three plus an
  addressed later body server-side; make later bodies named lazy placeholders.
  Keep heading-only host slots for unimplemented lane-owned bodies:
  compose `<partial name="Cases/Shared/_CaseDamage" model="Model" />` (and
  Estimate, Settlement, Report) from four new shells that render one
  `OperatorLabels` heading and nothing else; Engineer notes and Valuation
  are inner slots. Add Engineer and Sign-off ribbon items; use the current
  ribbon's absent-value rendering for Sign-off.
- **Preserve:** Existing action handlers and their state conditions; the
  existing EVA actions are unchanged. Remove Open Assessment entirely. The
  sticky Save targets the Overview form only; the inspection form is
  renamed `case-inspection-address-form` without `data-edit-save` (declared
  exception, `_CaseInspectionAddress.cshtml` lines 71–76).
- **Forbidden:** Tabs, a layout switch, explanatory text, inert controls,
  `CanOpenAssessment` gating, hard-coded labels, and copied fixture values.
- **Done when:** Plain HTTP for `?section=estimate` includes the first three
  sections and `#section-estimate`; all eleven nav links and hosts occur in
  D30 order.

### Step 3 — Implement sticky geometry, fragment mounting, and scroll-spy

- **Files:** `site.css`, `site.js`.
- **Reuse:** Existing `fetch`/`FormData` conventions, dirty-form guard,
  heartbeat handling, dialog bindings, reduced-motion rule, and breakpoint
  block.
- **Change:** Add the measured `--sticky-h` Case stack, horizontal
  `section-nav`, host `scroll-margin-top`, and responsive behaviour at the
  existing 1580/1100/760 proof widths. Add a Case-only idempotent initializer
  that fetches one named fragment as it approaches the viewport, replaces only
  its matching placeholder, restores enhancements and dirty-form binding, and
  performs query-target scrolling and scroll-spy `aria-current` updates.
- **Preserve:** Unsaved inputs, lease token, heartbeat, and dialogs in
  already-rendered sections. A failed fragment remains observable and
  retryable; no concurrent response is silently discarded.
- **Retire:** `case-workspace`, `case-section-nav`, and `case-context`,
  including their stale sticky offsets and breakpoint selectors. `rg` finds
  they are composed only by the current Case page.
- **Done when:** Scroll-spy follows all hosts, `?section=estimate` lands at
  its host, and mounting a lazy section does not replace an edited Overview
  form or its lease.

### Step 4 — Update focused server and browser proof

- **Files:** `CaseDetailsWebTests.cs`, `Browser/LayoutIntegrityTests.cs`.
- **Reuse:** Existing Case stores, antiforgery and lease helpers,
  `BrowserTestSupport`, `AllowedClipSelector`, three-width theory, and the
  seeded Case pattern in `OperatorJourneyTests`.
- **Change:** Replace exclusive-panel and Open Assessment assertions with
  canonical-key, eleven-host, addressed-server-render, fragment-boundary, and
  lease-survival assertions. Add a dedicated seeded Case scenario at
  1580/1100/760 that checks overflow/clipping, jump-nav/scroll-spy, lazy
  mounting, and preservation of a typed unsaved value and lease.
- **Do not change:** `AccessibilityTests.AuthenticatedRouteList`; it cannot
  supply a seeded record. The dedicated LayoutIntegrity scenario owns seeding.
- **Done when:** Tests prove old keys are not aliases, direct URLs work
  without JavaScript, and browser interaction preserves edits during a lazy
  mount.

### Step 5 — Retarget direct test consumers of deleted keys

- **Files:** `CaseVehicleWebTests.cs:223`,
  `CaseTasksWebTests.cs:124`,
  `CaseCustodyWebTests.cs:139,174`,
  `ImageIntakeWebTests.cs:143`,
  `ImageViewingWebTests.cs:152`, and
  `Browser/OperatorJourneyTests.cs:128,301`.
- **Reuse:** Existing test fixtures and URLs; no fake or product code.
- **Change:** Change only `case-files` to `files` and
  `inspection-address` to `inspection`. Retain already canonical
  `vehicle` and `notes` URLs.
- **Justification:** This is one narrow scope exception because these are
  direct callers of CASE-038's deleted query contract. Leaving them intact
  makes the full suite assert Overview after the old keys are deliberately
  removed.
- **Done when:** No listed test caller uses a deleted key, and no assertion or
  fixture behaviour is otherwise changed.

### Step 6 — Regenerate visual artifacts, simplify, and prepare review

- **Files:** The two owned Case Details snapshots and conditionally
  `catalogue.json`.
- **Reuse:** `Update-TestUiSnapshots.ps1`, its browser cap, and the existing
  visual catalogue entry.
- **Change:** Regenerate default and conflict. Confirm unavailable is
  byte-identical. Update only the existing default/conflict Details branch
  text if it still calls the replaced structure a side-nav/context layout.
- **Done when:** Snapshot verification and catalogue validation pass; the
  executor records simplification findings and dispositions, opens the PR,
  and moves CASE-038 to Review.

## Browser test

`LayoutIntegrityTests.cs` has `[Trait("Category", "Browser")]` and the three
viewports, verified with `rg -n "Category"
tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`. Run the
Browser category separately with the runbook's exact form
(`docs/runbook.md` line 325):
`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2`.
The snapshot script uses the same browser cap.

## Acceptance checks

- Exactly eleven ordered hosts and jump links exist; only the section in view
  has `aria-current`; the four shell partials render their heading and no
  control, prose or placeholder text.
- `?section=estimate` renders Overview, Engineer notes, Inspection, and
  Estimate on the initial HTTP response, then scrolls to `#section-estimate`.
- Lazy requests use the authorized Details path, retain the same valid lease,
  load only their requested projection, and replace only their placeholder.
- Overview and Inspection never produce duplicate `case-edit-form` IDs.
- The page has no Open Assessment action or `CanOpenAssessment` visibility
  condition; Complete is the Engineer-body read-only boundary.
- Labels are defined only in `Presentation/OperatorLabels.cs`; exact state
  labels, absent values, and existing disabled seams remain distinct.
- No explanatory copy, layout switch, Core/Infrastructure change, migration,
  or corpus-derived test data is introduced.
- Default and conflict snapshots change only through the regenerated tool
  output; unavailable remains byte-identical.

## Commands

```powershell
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./Pegasus.slnx --configuration Release --no-build --filter "Category!=Corpus&Category!=Browser"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "Category=Browser&Category!=Corpus" -- xUnit.MaxParallelThreads=2
./scripts/Update-TestUiSnapshots.ps1
./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture
./scripts/Test-UiCatalogue.ps1
```

Do not run `./scripts/Test-MigrationGrants.ps1`: this plan explicitly has no
migration. If one becomes necessary, stop and raise it as a dependency rather
than adding it.

## Failure and deviation rules

Stop and report if the inspection form rename needs more than the id,
attribute and comment lines, a fragment cannot use the authorized Details
path, a new port/schema/registration is needed, an unowned file is required
beyond the declared exceptions, or any verification command fails. Do not
add aliases, fallbacks, placeholder controls, or a parallel Assessment
handler path to bypass a failure.

## Simplification pass — dated by executor before PR

_To be completed over the CASE-038 branch diff before the PR opens, with
reuse, simplification, efficiency, and altitude findings plus dispositions._

## Stop condition

All acceptance checks and commands pass, snapshots are committed, the
post-implementation report is written, a PR targeting `dev` is open, and
CASE-038 is in Review. Do not merge the PR or begin another ticket.
