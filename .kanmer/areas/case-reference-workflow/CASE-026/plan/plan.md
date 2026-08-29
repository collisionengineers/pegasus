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
  the search already joins. No new query, no migration. **The ticket's Owns
  list was extended to name these two files on 2026-08-29** — see the
  round-2 dispositions.
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
3. View rewrite: header (freshness + Create Case → `/Upload`; `/Cases/Create`
   is receipt-bound and 404s without one — see the 2026-08-28 P1 disposition
   and the 2026-08-29 round-2 disposition R2, and [[PLAT-059]]),
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

> **Correction (2026-08-29, round 2):** the heading below calls those three
> "real test failures on sql-integration shard 2" without saying who wrote
> two of them. Corrected under R4 below — two of the three were this lane's
> own assertions from its own commit `9d739ab9`.

### P1 — `Index.cshtml:24` primary CTA returns NotFound

**Fixed.** `CreateModel.OnGetAsync` refuses an empty `receiptId`
(`Create.cshtml.cs:218`), so `/Cases/Create` with no receipt is a 404: the
page is the second half of one operator action whose first half is
`/Upload` (its own header links back there as "Upload another"). The
header action now targets `/Upload`; the contracted label stays.

Reported, not fixed (PLAT-029's files, identical dead link):
`Pages/Shared/_ShellDialogs.cshtml:64` (the Add dialog's Create Case card)
and `wwwroot/js/site.js:1364` (Ctrl N). A receipt-less `/Cases/Create`
entry point would settle all three in one place. **Now ticketed as
[[PLAT-059]] (2026-08-29).**

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
  verify modes. UIIMP-005's file; a one-line constant change. **Now
  ticketed as [[UIIMP-011]] (2026-08-29), together with line 28.**
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

## Review findings — dispositions (round 2)

Adversarial verifier re-run of the build, the focused filters and the
branch diff, 2026-08-29. Every finding has a disposition; nothing is
silenced. Two tickets were created rather than deferring in prose:
[[PLAT-059]] and [[UIIMP-011]].

### R1 — [major] Operator copy contorted to satisfy two test constants, shipping a stuttering double sentence — undisclosed

**Fixed, and the attribution corrected.**

*Fix.* `Index.cshtml` now renders one sentence:
`<p class="empty" aria-live="polite">No cases match these filters.</p>`.
The wording is the page's own — the image section three blocks above
already says "No vehicle images match these filters." — so the two empty
states on one page read as one voice, and the operator gets a label, not
two sentences of prose (`docs/design/README.md` §No explanatory copy and
page economy). `CasesIndexWebTests` was then made to assert what the page
should say (`No cases match these filters.`) and, additionally, that the
superseded sentence does **not** come back. That is a strengthening, not a
weakening: the empty state is still required character for character, and
one more thing is now pinned than before.
`AdministrationSearchAccountWebTests.cs:132` already asserted the surviving
sentence and is untouched.
`TestUiSnapshotTests.cs:28` (`["cases--empty"] = new("No matching cases.")`)
is UIIMP-005's file and is now [[UIIMP-011]], filed with line 29.

*Correction to the finding.* The verifier attributes the double sentence to
this lane's port commit `20843a7e` via `git log -L`. That is a false
attribution — `git log -L` follows the line's current position, not its
text. The stuttering sentence is on `origin/dev`:

```
$ git show origin/dev:src/Pegasus.Web/Pages/Search/Index.cshtml | sed -n '127p'
    <p class="empty-state" aria-live="polite">No matching cases. No cases match these filters.</p>
```

`git log -S "No matching cases. No cases match these filters." -- src/Pegasus.Web/Pages/`
returns `865b4c0c` ("feat(ui): workspace shell layouts… (PLAT-029)") and
`7206773a`, both before this lane existed. The port carried the pre-existing
line across unchanged, with its pre-existing comment. So the finding's
substance is right and is fixed here — the file is this lane's now, and the
lane reproduced the stutter and left it undisclosed — but the lane did not
author it, and no assertion was ever bent to accommodate it.

### R2 — [minor] The primary "Create Case" CTA sends the operator to `/Upload`

**Fixed in this lane as far as this lane reaches, and the rest ticketed as
[[PLAT-059]].**

The destination stays `/Upload`, and the reason is a domain fact, not a
preference: a case is made from received material, so `/Cases/Create` is
receipt-bound — `Create.cshtml.cs:215-219` returns `NotFound()` for an empty
`receiptId`, and both call sites that work today reach it *with* a receipt
(`Pages/Intake/Details.cshtml:451`, `Presentation/UploadOutcome.cs:322`).
The Search page holds no receipt. Retargeting the header at `/Cases/Create`
for label symmetry would ship a control that 404s on every click — a new
broken control on a page that had no CTA at all before the port (checked:
`git show origin/dev:src/Pegasus.Web/Pages/Search/Index.cshtml` has no
`Create Case`), which the epic forbids outright. The label is contracted by
`context.md` §1.7 and §1.2 and cannot change either.

What is genuinely unsettled is the *other* two call sites,
`Pages/Shared/_ShellDialogs.cshtml:64` and `wwwroot/js/site.js:1364`, which
are PLAT-029's files and both 404 today. [[PLAT-059]] carries the whole
mapping — all four call sites, both candidate resolutions (retarget the
shell pair at `/Upload`, or give `/Cases/Create` a receipt-less entry
point) — so the label→destination mapping ends up as one list in one place.
The in-file comment at `Index.cshtml:24` now names PLAT-059.

### R3 — [scope] `CaseQueries.cs` and `EfCaseQueryStore.cs` are outside the Owns list

**Justified, and the record fixed.** The edits stay; the ticket's Owns list
now names both files with the reason (updated 2026-08-29).

§1.7 draws a **Vehicle** results column carrying make/model and an
**Accident circumstances** line in the selected-Case preview. Neither fact
exists on the pre-port `CaseSearchItem`, so the contracted page cannot be
drawn without extending the projection; the alternative — an `IGetCase`
read per row — was rejected in the plan (P5) as 1+N reads for facts the
search already joins. The diff is three trailing optional constructor
parameters and their projection: additive, source- and binary-compatible
for every existing caller, no new query, no migration. `waves.md` gives
those two paths to no other EPIC-011 lane (wave 3's Core lane owns
`CaseTimeline.cs`), so there was no collision risk — the defect was the
stale Owns list, and that is what has been corrected.

### R4 — [minor/honesty] The two repaired "failures" were this lane's own red test

**Accepted, and the record corrected.** `git show 9d739ab9 --
tests/Pegasus.IntegrationTests/CasesIndexWebTests.cs` shows this lane added
both assertions in its own commit
`test(search): cover the ported page's selected-row preview and
closed-outcome chip (CASE-026)`. The port therefore pushed a red test, and
the review pass repaired its own breakage. Calling them "the three real test
failures on sql-integration shard 2" without saying so read as though the
shard had surfaced someone else's problem. A note to that effect is now
inline above the 2026-08-28 section, and the PR body is corrected. Only the
third failure (`QdosCustodialWebTests`, the dropped filter-form accessible
name) was a genuine regression the port inflicted on another lane's test.

### R5 — [minor] The failure notice's sentence was reworded without disclosure

**Accepted the change, disclosed now.** `Index.cshtml:115` reads
`The search could not be completed; try again.`; the pre-port line read
`The case query could not be completed; try again.` It is kept: on a page
titled *Search* the failed read is the search, and "case query" names the
internal `ISearchCases` object rather than anything the operator did
(`docs/design/README.md` §Voice). The whole notice was rewritten by the port
anyway — `status-card`/`<h2>` became `notice notice--danger`/`<strong>` —
so `docs/design/test-ui/pages/cases--unavailable.html` must be regenerated
for the markup regardless; the sentence adds no gate break that the markup
change had not already caused. Both snapshot constants are on [[UIIMP-011]],
and snapshot regeneration is the orchestrator's step, not a page lane's.

### R6 — [minor] The Steps section still named the superseded CTA target

**Fixed.** Steps item 3 above now reads `/Upload` and points at the two
dispositions and [[PLAT-059]], so the document no longer contradicts itself.

### Round-2 verification (2026-08-29, real numbers)

- `dotnet build ./Pegasus.slnx --configuration Release` — **Build
  succeeded. 0 Warning(s), 0 Error(s)** (19.67s).
- `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj
  --configuration Release --no-build --filter
  'FullyQualifiedName~CasesIndexWebTests'` — **Failed: 0, Passed: 5,
  Skipped: 0, Total: 5** (1m22s).
- Same project, `--filter 'FullyQualifiedName~AdministrationSearchAccountWebTests|
  FullyQualifiedName~ImageIntake|FullyQualifiedName~ShellAndStatusPageWebTests|
  FullyQualifiedName~QdosCustodialWebTests'` — **Failed: 0, Passed: 30,
  Skipped: 0, Total: 30** (2m12s). (The 2026-08-28 report quoted 18 for a
  narrower spelling of the same four names; 30 is what this filter string
  actually selects.)
- Not run here, and not this lane's to run: the full solution filter, the
  Browser category, `scripts/Update-TestUiSnapshots.ps1` and
  `scripts/Test-UiCatalogue.ps1`. The two `cases--*` snapshot states are
  known-failing until [[UIIMP-011]] lands.

### Simplification pass (2026-08-29, round-2 diff)

n/a beyond the change itself — the round-2 diff is one operator sentence,
one comment, and one test assertion pair. Reuse: the surviving sentence is
the page's existing wording rather than a new string. Simplification: one
sentence replaced two. Efficiency and altitude: unchanged.
