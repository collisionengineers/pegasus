# Research — UIIMP-014 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

## Wrapper check (Claude, 2026-09-02)

Codex ran read-only in the shared detached checkout `.worktrees/research` at
`origin/dev` = `897db953` (DELIV-041 merged); `git status --porcelain` was
empty afterwards, so no cleanup was needed. The wrapper did every board read
(ticket body, EPIC-012 context D29–D43, EPIC-011 context and waves, the
sibling EPIC-012 lane list) and quoted them into the prompt; Codex never
touched the board. Spot-checked in the main checkout with my own commands,
all confirmed:

- `docs/design/test-ui/catalogue.json`: `/Cases` (`Pages/Cases/Index.cshtml`)
  is `visual` with `default` and `empty` only, stored as
  `pages/queues--default.html` / `pages/queues--empty.html`; the
  `cases--default/--empty/--unavailable.html` files belong to
  `Pages/Search/Index.cshtml`. The wrapper's prompt wrongly said "cases
  default/empty/unavailable" for the Cases queue page — Codex's correction
  below stands.
- `TestUiSnapshotTests.Generate` (lines 116–172): a state's candidate is the
  capture whose HTML matches its `StateMatches` entry, or — when it has none —
  a capture that matches none of the sibling states' matchers; ties resolve by
  `.Order(StringComparer.Ordinal).FirstOrDefault()`. `case-details--default`,
  `case-assessment--default`, `queues--default` and `operations--default` have
  no explicit matcher today (lines 18–53).
- `Browser/LayoutIntegrityTests.cs`: theory over
  `AccessibilityTests.AuthenticatedRouteList` × {1580, 1100, 760}; asserts
  200, no horizontal overflow, no clipped container outside
  `AllowedClipSelector`, one `main`, one `h1`, no inline `style` (lines
  17–78). The route list has `/Cases`, `/Cases?tab=triage`,
  `/Cases?tab=unidentified`, `/Operations` … but no `/Cases/{id}`.
- `Browser/OperatorJourneyTests.cs`: `RepositoryEvaFixture.Load()` (line 30)
  and `SeedCustodyRecoveryCaseAsync` (line 315) seed an accepted Case through
  Core ports, then `GoToAsync($"/Cases/{id}?section=case-files")` and click
  "Edit Case" (line 441) — the seeded-case browser pattern to reuse.
- `AssessmentAccessPolicy` is a static class in
  `src/Pegasus.Core/Assessment/AssessmentWorkspace.cs` (line 43) with
  `CanOpen` / `IsReadOnly`; `IGetAssessmentAccess` is at line 72.
- `dotnet --list-sdks` → 10.0.204 and 10.0.303.

Board facts the wrapper adds: profile `chore` gates are leave-preparing →
`plan`, enter-done → `proof` (`data/board.yml`); backlog → preparing is
ungated. UIIMP-014 `blocks` DELIV-030. The main checkout `dev` (1e6ac077) is
behind `origin/dev` (897db953) by the DELIV-041 merge only; none of the
claims below depend on that delta.

## Scope and verification method

This is a read-only audit of detached `origin/dev` at `897db953`. No files,
tests, builds, snapshots, or board calls were made.

- **VERIFIED** — checkout and commit: `git status --short; git log -1
  --oneline`.
- **VERIFIED** — installed SDKs are 10.0.204 and 10.0.303: `dotnet
  --list-sdks`.
- **ASSUMED** — the quoted ticket, D29–D43, sibling ownership, and acceptance
  criteria are authoritative operator/board inputs. The board was deliberately
  not contacted (the wrapper read them from the board — see above).

## Current repository behaviour

### Case, Core, Infrastructure, and migrations

The current Case Details page is `/Cases/{id:guid}`. It renders a Case header,
identity ribbon, edit-lease presence, actions, and existing partial-led content.
It has query-failed, normal, and conflict shapes. The current source still has
a separate routed Assessment page at `/Cases/{id:guid}/Assessment`.

- **VERIFIED** — current routes and Details page branches:
  `rg -n -C 3 'partial name|_Case|section|Section|CanOpenAssessment|Assessment'
  src/Pegasus.Web/Pages/Cases/Details.cshtml` and `rg -n '^@page|Assessment|Save|ReadOnly|OnGet'
  src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml
  src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`.
- **VERIFIED** — Details currently uses existing `_CaseSummary`,
  `_CaseVehicle`, `_CaseInspectionAddress`, `_CaseFiles`, `_CaseHistory`,
  `_CaseWorkflow`, and `_CaseWorkspaceNav` partials:
  `Get-ChildItem src/Pegasus.Web/Pages/Cases/Shared -File`.
- **VERIFIED** — the current Assessment domain has `IGetCaseAssessment`,
  `ISaveAssessment`, `IGetAssessmentAccess`, `IGetAssessmentWorkspace`, and
  `ICaseAssessmentStore`; `AssessmentAccessPolicy` makes Complete read-only:
  `rg -n -C 2 'interface IGetCase|interface ISaveCase|ICaseDataQueries|class
  .*Case.*Store|IGetAssessment|Assessment' src/Pegasus.Core/Cases
  src/Pegasus.Core/Assessment src/Pegasus.Infrastructure` and
  `Get-Content src/Pegasus.Core/Assessment/AssessmentWorkspace.cs`.
- **VERIFIED** — `EfCaseAssessmentStore` persists assessment fields and estimate
  lines under version, lease, archive, and writable-state guards; the existing
  `EfAssessmentWorkspaceSource` is a bounded Assessment-specific projection:
  `Get-Content src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs
  | Select-Object -First 120` and `Get-Content
  src/Pegasus.Infrastructure/Persistence/EfAssessmentWorkspaceSource.cs |
  Select-Object -First 120`.
- **VERIFIED** — the current migration directory contains Case/Assessment
  migrations, including `20260829095336_CaseValuations`; UIIMP-014 itself has
  no data-model requirement:
  `Get-ChildItem src/Pegasus.Infrastructure/Persistence/Migrations -File`.
- **ASSUMED** — after ENG-034/035 and the other lanes land, the Case page will
  own all eleven sections and the Assessment endpoint will redirect. Those
  changes do not exist at this revision.

`OperatorLabels.cs` already owns lifecycle, inspection-mode, Case workspace,
and Operations vocabulary. It does not yet contain the D29 section list or the
new lane labels.

- **VERIFIED** — current label ownership and available Case labels:
  `rg -n -C 2 'CaseStage|Case.*Label|Assessment|Operations|Queue|Triage|Engineer|Inspection|Vehicle|Damage|Valuation|Estimate|Settlement|Report|Files|Notes'
  src/Pegasus.Web/Presentation/OperatorLabels.cs`.
- **ASSUMED** — UIIMP-014 must not edit `OperatorLabels.cs`; the shared-lock
  file belongs to CASE-038 and new labels belong to the implementing lanes.

### Current Test UI machinery

`catalogue.json` is the inventory. Each routed Razor `@page` source must appear
once. A visual entry needs states, branch text, and a flat
`pages/<route>--<state>.html` file. `redirect`, `download`, and `protocol`
entries require a non-empty reason and no generated visual state.

- **VERIFIED** — classifications, route/source inventory checks, reason rule,
  state/file rules, orphan checks, and broken-link validation:
  `Get-Content scripts/Test-UiCatalogue.ps1`.
- **VERIFIED** — the catalogue presently contains:

  | Route | Current classification and states |
  | --- | --- |
  | `/Cases/{id:guid}` | visual: `default`, `unavailable`, `conflict` |
  | `/Cases/{id:guid}/Assessment` | visual: `default` |
  | `/Cases` | visual: `default`, `empty`, using `queues--*.html` |
  | `/Operations` | visual: `default`, `empty` |

  Command: `Get-Content docs/design/test-ui/catalogue.json -Raw |
  ConvertFrom-Json`.

The prompt wording said "cases default/empty/unavailable," but current
`origin/dev` has only Cases `default` and `empty`; it has no Cases
`unavailable` state. Its filenames are `queues--default.html` and
`queues--empty.html` (the `cases--*.html` files are the Search page's).

- **VERIFIED** — actual current entries and filenames:
  `Get-Content docs/design/test-ui/catalogue.json -Raw | ConvertFrom-Json`.
- **ASSUMED** — a future Cases unavailable state may be introduced by CASE-042
  or another sibling. It is not a present UIIMP-014 state unless its landed
  Razor implementation yields a distinct rendered response.

`Update-TestUiSnapshots.ps1` runs a browser capture at two threads, then a
non-browser capture, then invokes `TestUiSnapshotTests` in update or verify
mode. The capture middleware activates only when
`PEGASUS_TEST_UI_CAPTURE_DIR` is set and stores HTML responses plus receipt
images. Snapshot verification regenerates output, compares it byte-for-byte
after newline normalization, rejects orphan pages, and renders every generated
page offline in Chromium.

- **VERIFIED** — capture/verify sequence:
  `Get-Content scripts/Update-TestUiSnapshots.ps1`.
- **VERIFIED** — capture middleware activation and response/image capture:
  `rg -n -C 4 'TestUiResponseCapture|PEGASUS_TEST_UI_CAPTURE|IStartupFilter|Capture'
  tests/Pegasus.IntegrationTests`.
- **VERIFIED** — generation, URL rewriting, offline render, and orphan checks:
  `Get-Content tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.

A state is declared in two places: a catalogue state record and, when
disambiguation is necessary, `TestUiSnapshotTests.StateMatches`. That dictionary
currently has explicit matches for Case unavailable/conflict and queue/Operations
empty, but not for Case default, Assessment default, Cases default, or
Operations default. Unmatched states are selected by excluding the explicit
matchers for sibling states and then taking the ordinal-first candidate.

- **VERIFIED** — current explicit state matches and fallback selection:
  `rg -n -C 3 'StateMatches|Generate\('
  tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.
- **Risk** — every new section/edit/read-only state needs a unique, stable
  rendered discriminator. Otherwise several captures for the same Case route
  can select the wrong candidate.

`index.html` is generated from the manifest, not hand-maintained. It lists
visual routes and states, and lists nonvisual routes with classification and
reason.

- **VERIFIED** — index generation:
  `rg -n -C 3 'BuildIndex|Classification'
  tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.
- **VERIFIED** — a hand-edited index or snapshot will fail fresh verification:
  `Get-Content tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.

CI always runs `Test-UiCatalogue.ps1` in the documentation job. Changes under
`docs/design/test-ui/`, tests, source, the snapshot scripts, or CI set the
build flag, which also enables the Test UI capture-and-verify job.

- **VERIFIED** — catalogue CI step:
  `rg -n -C 2 'Test UI catalogue' .github/workflows/ci.yml`.
- **VERIFIED** — snapshot CI job and command:
  `rg -n -C 3 'test-ui|Update-TestUiSnapshots' .github/workflows/ci.yml`.
- **VERIFIED** — build-relevant path classification:
  `Get-Content scripts/Get-CiChangeFlags.ps1`.

### Existing browser walk and seed conventions

`LayoutIntegrityTests` currently iterates every route in
`AccessibilityTests.AuthenticatedRouteList` at widths 1580, 1100, and 760. It
checks HTTP 200, horizontal overflow, clipping, one `main`, one `h1`, and no
author-controlled inline styles. The route list has `/Cases`, Triage, and
Unidentified tabs, but no seeded Case Details route and no Assessment route.

- **VERIFIED** — three-width test and assertions:
  `Get-Content tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs`.
- **VERIFIED** — current authenticated route inventory:
  `Get-Content tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs |
  Select-Object -First 160`.
- **VERIFIED** — browser support creates an isolated authenticated factory,
  loopback host, headless Chromium, and configured viewport:
  `Get-Content tests/Pegasus.IntegrationTests/Browser/BrowserTestSupport.cs |
  Select-Object -First 150`.

`OperatorJourneyTests` already provides the closest reuse pattern for a seeded
Case: it creates a controlled accepted case through Core ports, obtains the
edit lease, and navigates to a section URL. It should be extracted or reused
by a UIIMP-014-specific browser walk without changing product code.

- **VERIFIED** — seeded-case, lease, and `?section=` navigation:
  `rg -n -C 5 'Seed.*Case|Accept.*Case|/Cases/\{|Details|Edit Case'
  tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs`.
- **ASSUMED** — the final Case sections will expose stable `section-<key>` IDs
  and editable controls as specified by D29. The mockup has them; current
  Razor does not yet prove them.

The existing Case snapshot candidate is not attributable to one deterministic
named seed merely from the current state declarations. `case-details--default`
has no dedicated matcher, and the generator selects an ordinal-first captured
candidate after exclusion.

- **VERIFIED** — no Case-default matcher and ordinal-first candidate selection:
  `rg -n -C 3 'case-details|StateMatches|Order\(StringComparer.Ordinal\)'
  tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs`.
- **VERIFIED** — focused Test UI rendering only explicitly adds Case
  unavailable/error coverage; it does not seed a normal Case:
  `Get-Content tests/Pegasus.IntegrationTests/TestUiFocusedRenderTests.cs`.
- **Gap** — add an explicit, stable seeded Case capture for every future
  Case-section state; do not rely on capture ordering.

### Mockup findings

The supplied mockup's `CASE_SECTIONS` order is Overview, Engineer notes,
Inspection, Vehicle, Damage, Valuation, Estimate, Settlement, Report, Files,
Notes. Its Case renderer uses `section-<key>`, edit/read-only paths,
`data-action` controls, and lazy section rendering. `cdp.js` connects to
headless Chrome, walks sections, toggles edit mode, captures screenshots, and
records console/exceptions.

- **VERIFIED** — section labels/order:
  `Get-Content C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/03-labels.js |
  Select-Object -Skip 100 -First 40`.
- **VERIFIED** — renderer attributes and lazy/edit behaviour:
  `rg -n -C 4 'section-|CASE_SECTIONS|render.*Section|lazy|data-action|edit'
  C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/20-case.js
  C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/21-case-sections.js
  C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/22-case-engineer.js`.
- **VERIFIED** — DevTools walk pattern:
  `Get-Content C:/Users/PC/Downloads/Pegasus_UI_v2_src/cdp.js |
  Select-Object -First 240`.
- **VERIFIED** — mock fixtures say they are corpus-derived and include real
  claimant contact data:
  `rg -n 'Fixtures|corpus-derived|phone|email'
  C:/Users/PC/Downloads/Pegasus_UI_v2_src/src/04-fixtures.js`.

D43 permits use of those values, but the current repository does not establish
that they are needed. The existing browser fixture is repository-controlled,
not copied from the mockup.

- **VERIFIED** — repository-controlled Case fixture pattern:
  `rg -n -C 3 'RepositoryEvaFixture|CaseDataProjection|CaseField'
  tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs`.
- **ASSUMED** — additional D43 values will only be necessary if the landed
  section implementations cannot be rendered from existing fixture data. The
  operator must approve the documented real-data use before copying any value.

## Required future snapshot coverage

The following are requirements inferred from the supplied acceptance criteria,
not current repository behaviour:

| Required state | Dependency that must land first |
| --- | --- |
| Eleven Case sections, each read-only | CASE-038 frame; section owner for each section |
| Eleven Case sections, each edit mode | CASE-038 one-lease edit mode; section owner for each section |
| Assessment route as `redirect` with reason | ENG-034 |
| Awaiting instruction queue state | CASE-042 |
| Operations partial-data notice | PLAT-069 |
| 1580/1100/760 Case record walk, each section and edit mode | CASE-038 plus all section lanes |

- **ASSUMED** — this is the minimum implied state set: 22 section visual
  states, one redirect inventory entry, one Awaiting instruction visual state,
  one Operations notice visual state, and a seeded three-width walk.

## Gap list

1. Current Case Details has no proven eleven-section snapshot matrix.
2. The current snapshot matcher cannot distinguish future Case section and mode
   states without new stable markers.
3. Current browser layout coverage walks unseeded routes only; it cannot prove
   a Case record, section jump navigation, lazy content, or edit mode.
4. Assessment remains a visual Razor page and its snapshot remains in the
   catalogue; after ENG-034 it must become `redirect` with a reason, and the
   obsolete generated snapshot must be removed.
5. Current Cases has no Awaiting instruction state.
6. Current Operations renders a service-health table when composed; it has no
   dedicated partial-data-notice snapshot.
7. Current test UI capture makes default-state selection partly dependent on
   capture candidate order.
8. Snapshot code currently has no Case-section-aware semantic state declarations.

## Existing helpers and conventions to reuse

- `Update-TestUiSnapshots.ps1` — one capture/regen/verify workflow.
- `TestUiSnapshotTests.StateMatches` — semantic scenario discriminator.
- `TestUiResponseCaptureMiddleware` — generic capture; no change is needed.
- `Test-UiCatalogue.ps1` — inventory, classification, reason, link, and
  prototype validation.
- `OperatorJourneyTests` seeded accepted-Case and lease pattern.
- `BrowserTestSupport.StartAsync` and `GoToAsync`.
- `LayoutIntegrityTests` overflow/clipping/main/h1/inline-style assertions.
- `AccessibilityTests.AuthenticatedRouteList` — only for unseeded routes; do
  not add the seeded Case route there.
- `OperatorLabels.CaseWorkspace` and `OperatorLabels.CaseStage` — consume
  labels from the single presentation owner; do not duplicate them in tests.
- Catalogue flat name convention: `pages/<route>--<state>.html`.

## Risks

- The wave is serial. Starting before every section owner has merged creates
  snapshots of incomplete shapes and makes the final walk meaningless.
- `docs/design/test-ui/**` is shared-lock capacity one. CASE-038's explicitly
  reserved regeneration of `case-details--default` and
  `case-details--conflict` must not be overwritten.
- New state keys need unique rendered match markers; generic route captures are
  insufficient.
- `?section=` plus lazy rendering requires the browser walk to wait for the
  intended section before checking geometry or edit controls.
- The retired Assessment page must leave no routed `@page` source behind; the
  catalogue validator rejects both unclassified routes and inventory sources
  that are no longer routed.
- Mockup values include real personal data. D43 authorizes them only with the
  stated operator sign-off; current repository fixtures should be preferred.
- Do not change Core, Infrastructure, migrations, labels, production pages,
  CSS, or JavaScript in this chore.

## Open questions

None. The supplied D29–D43 decisions specify the intended snapshot states.
The exact state keys and fixture values should be selected from the merged
implementations, using existing repository fixture values unless D43-approved
values are demonstrably required.
