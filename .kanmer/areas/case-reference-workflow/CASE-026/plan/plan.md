# Plan — CASE-026 Search page port

## Decisions

- P1 Reuse before build: shell partials (`_FreshnessBanner`, `_StatusChip`),
  wave-1 classes (`advanced-search-grid`, `case-search-layout`,
  `tr[data-select-href]`, `fact-grid`, `detail-canvas`, `blocker-list`,
  `pagination`, `row-button`, `.empty`, `notice--danger`), the wave-1
  row-selection script, `OperatorLabels` for every code→words mapping, and
  CASE-025's selected-row/pager/name-resolution patterns. No new CSS or JS.
- P2 The named Core extension: `CaseSearchItem` gains `VehicleMake`,
  `VehicleModel`, `AccidentCircumstances` (trailing optional constructor
  parameters), projected by `EfCaseQueryStore` from the instruction draft
  the search already joins. No new query, no migration.
- P3 Grid control types follow the ticket's "1:1 to the existing UI-07
  inputs": `query` is the "Case/PO or image reference" field (the only
  parameter that feeds both the Case reference match and the image-intake
  lookups), State stays the enum select, Principal/Engineer/Origin stay
  text inputs. The prototype's option lists were fixture data.
- P4 `case`, `receivedDate`, `instructionDate`, `kind` stay bound, applied
  and pager-preserved but are not drawn: §1.7 draws ten fields, and old
  `/Cases` bookmarks must keep working with values intact.
- P5 The preview pane is row-projection-built (P2 fields + one batched
  engineer-name resolve), not `IGetCase`-built: the wave-1 selection script
  needs a `<template>` per row regardless, and this keeps the page at two
  queries total. `Next action` = first outstanding requirement's resolve
  text, else "Not recorded" (CASE-025's precedence, minus the due-work
  fallback the row does not carry).
- P6 Image-initiated results keep the existing lookup and render as
  `row-button` rows in a "Vehicle images" section (the legacy `queue-list`
  class dies in wave 5); chip = `ImageIntakeLifecycleState` per FRD-12's
  named states. The old `ImageIntakeOutcomeLabel` helper is deleted with
  its caller.
- P7 Copy Case/PO uses the established `[data-copy-target]` pattern
  (button `hidden`, revealed by script) and sits OUTSIDE the
  `[data-preview-target]` region: site.js binds copy handlers once at
  load, so a button travelling inside the script-swapped preview would be
  permanently hidden and unbound. Open Case travels with the preview (an
  anchor stays live after the swap).

## Steps

1. Core + Infrastructure extension (P2). Build.
2. Page model rewrite: keep every existing bind + the ISearchCases call and
   error paths; add `SelectedId`, `ResultRows`, engineer-name resolve,
   `LoadedAtUtc`, `SelectHref`/`PageUrl` helpers, `RefreshFields`.
3. View rewrite: header (freshness + Create Case → `/Cases/Create`),
   advanced-search-grid GET form, "Vehicle images" section, two panes with
   `data-row-list`/`data-preview-target`, per-row
   `<template>`+`_CasePreview`, pager. Empty/unavailable states render
   inside the results pane with the settled sentences.
4. `_CasePreview.cshtml` partial (eyebrow type, h2 ref · reg, muted
   claimant · principal, chip, Accident circumstances, fact grid Provider
   ref/Engineer/Due/Next action, Outstanding (n), Open Case dark; Copy
   Case/PO rendered by the page beside the swapped region).
5. Tests: keep the three contracts; extend the recording-fake test with
   selected-row/preview/Closed-outcome assertions. Build only — the
   orchestrator runs the wave test loop.
6. Simplification pass over the branch diff; record below.
7. Commit in slices `feat(search): ... (CASE-026)`; PR to dev
   "CASE-026: Port the Search page (/Search) with the advanced filter grid
   and selected-Case pane"; stop at the open PR.

## Verification

- `dotnet restore ./Pegasus.slnx --locked-mode` then
  `dotnet build ./Pegasus.slnx --configuration Release --no-restore` —
  green on the task branch (recorded 2026-08-28: Build succeeded,
  0 warnings, 0 errors; subagents do not run tests/snapshots).
- Existing web-test contracts stay assertable in source: filters reach
  `ISearchCases` intact; pager preserves them; `/Cases/{id}` row link;
  301 keeps values; empty vs unavailable sentences distinct.
- Ticket checklist tracks the §1.7 element list.

## Simplification pass (2026-08-28)

Lenses: reuse, simplification, efficiency, altitude. Findings and
dispositions:

1. `Join` ("first · second") now exists here and privately in
   `Cases.IndexModel`. Not extracted to a shared helper: a third
   Presentation static for a three-line join buys nothing —
   `_CaseSummary` already joins vehicle parts inline the same way.
   Accepted duplication, noted here.
2. `ResultRow` composes all display strings model-side rather than the
   view computing them — kept, matching `Cases.IndexModel.QueueRow`
   (one composition place, same shape as the precedent lane).
3. Preview sources considered: `IGetCase` per selected row vs the row
   projection. Projection chosen (P5): every §1.7 fact is derivable, and
   the per-row templates the selection script requires would have needed
   1+N `IGetCase` reads otherwise.
4. Copy Case/PO placement (P7): inside the swapped preview the button
   would never render (site.js reveals it at load, then the row-selection
   script replaces the pane with an unrevealed template clone). Placed in
   the stable pane footer bound to the server-selected row. Known gap,
   not fixable in this lane: after a script swap the copyable reference
   lags the previewed facts until the page re-requests. Follow-up
   suggestion (site.js is PLAT-029's file): bind `[data-copy-target]` by
   delegation so controls inside swapped regions work; out of scope here.
5. Deleted with the port: `ImageIntakeOutcomeLabel` (superseded by the
   FRD-12 lifecycle chip), the public `PageUrl`/`RouteValues` pair
   (collapsed into one `Href` builder + `RefreshFields`), and the old
   disclosure/`hasAdvancedFilter` logic (the grid replaces the filterbar).
6. Fixed during the pass: freshness now stamps in images-only mode too;
   the "No matching cases" line no longer renders when invalid input kept
   the query from running (`ViewData.ModelState.IsValid` guard).
7. Efficiency: the staff-name resolve runs only when the page's rows name
   an Engineer; no per-row queries anywhere.

## Review findings — dispositions (2026-08-28)

Codex review of PR #606, plus the three real test failures on
sql-integration shard 2. Every finding has a disposition; nothing is
silenced.

### P1 — `Index.cshtml:24` primary CTA returns NotFound

**Fixed.** `CreateModel.OnGetAsync` refuses an empty `receiptId`
(`Create.cshtml.cs:218`), so `/Cases/Create` with no receipt is a 404: the
page is the second half of one operator action whose first half is
`/Upload` (its own header links back there as "Upload another"). The
header action now targets `/Upload`; the contracted label stays.

Reported, not fixed (PLAT-029's files, identical dead link):
`Pages/Shared/_ShellDialogs.cshtml:64` (the Add dialog's Create Case card)
and `wwwroot/js/site.js:1364` (Ctrl N). A receipt-less `/Cases/Create`
entry point would settle all three in one place.

### P1 — `Index.cshtml:249` Copy Case/PO copies the previous selection

**Fixed.** Each result row now carries `data-copy-reference`, and a
page-scoped `@section Scripts` moves the copy source — and the refresh
form's hidden `selected` field — onto the row the shell selects. Both
controls have to live outside `[data-preview-target]` (site.js resolves
`[data-copy-target]` once at load, then the row-selection module replaces
the pane's children), so the page keeps the two values it owns in step
rather than duplicating the clipboard or the selection, which stay the
shell's. The durable fix is still site.js binding `[data-copy-target]` by
delegation so any control inside a swapped region works; that file is
PLAT-029's and is reported, not touched.

### P1 — `Index.cshtml.cs` drops the image record's lifecycle state

**Fixed.** `LoadImageIntakeResultsAsync`'s `GetByReferenceAsync` branch now
passes `byReference.State` and `byReference.ClosureReason`. Omitting them
took `ImageIntakeSummary`'s `AwaitingInstruction` default, so an exact
reference hit on a merged or closed record showed the wrong chip while the
registration search beside it showed the right one. Covered by
`ExactImageReferenceResultCarriesItsRecordedLifecycleState`.

### P2 — refresh's hidden `selected` goes stale

**Fixed** by the same selection-sync script; refresh reruns the row the
operator is reading.

### P2 — an image-query failure renders an empty result

**Fixed.** Both reads run inside the one guarded load, so the failure
notice is now rendered once, above both sections, and neither the
"Vehicle images" empty line nor the two panes (with their "0 results"
count) render on a failed load. Covered by
`UnavailableImageQueryRendersTheFailureNoticeNotAnEmptyList`.

### P2 — `ResolveStaffNamesAsync` loops per account

**Rejected for this lane, and the claim corrected.**
`Core/Actors/ActorDisplayNames.cs` and `IStaffAccountQueries` are outside
CASE-026's files, and a batched read is a new Core port, not a page
change. The page does make one shared resolve for every Engineer on the
page rather than one per row, which is what the PR body now says; the
round-trip count is the shared helper's and belongs to a Core ticket.

### The three shard-2 failures

1. and 2. `CasesIndexWebTests` asserted `"Closed · Created in error"` and
   `"QDOS3100043 · AB12CDE"` — text that never appears in the response.
   The framework's HTML encoder writes non-ASCII as numeric references, so
   the bytes are `Closed &#xB7; Created in error`. Both assertions now
   pin the rendered bytes (the convention MAIL-025 settled at
   `MailWorkspaceWebTests.cs:897`). No assertion weakened: the chip and
   the heading are still required, character for character.
3. `QdosCustodialWebTests` asserts the `/Search` filter form still has its
   accessible name; the port dropped the pre-port `<h2 class="vh">Filter
   cases</h2>`. Restored as `aria-label="Filter cases"` on the form —
   the naming MAIL-025 uses for its filter form, and no visible copy.

### Out of scope, found while working (do not fix here)

- `tests/Pegasus.IntegrationTests/TestUiSnapshotTests.cs:29` —
  `StateMatches["cases--unavailable"]` requires
  `<h2>Cases are unavailable</h2>`, which is the pre-port `status-card`
  markup. The ported notice renders `<strong>`, so snapshot generation
  reports `cases--unavailable (/Search)` as missing in both update and
  verify modes. UIIMP-005's file; a one-line constant change.
- One `dotnet test` run over `Pegasus.slnx` aborted with "Test host
  process crashed" before any test reported; the identical command
  re-run passed 5/5. Not reproduced, recorded rather than ignored.

## Simplification pass (2026-08-28, review-fix diff)

Lenses over this branch's own additional diff: reuse, simplification,
efficiency, altitude.

1. Reuse: the failure notice, `[data-copy-target]`, `[data-refresh-form]`
   and the row-selection contract are all the shell's existing pieces; the
   only new thing is a page script that moves two values, and it calls no
   clipboard and no selection code of its own.
2. Simplification: hoisting the one failure notice removed a nested
   branch and made the "0 results" count unreachable on a failed load;
   the results pane's state chain is one condition shorter.
3. Efficiency: no new queries, no new per-row work; the image branch
   gained two constructor arguments.
4. Altitude: the copy/refresh sync sits on the page that owns those two
   controls. The general fix (delegated binding) belongs to the shell and
   is reported above rather than reimplemented here.
