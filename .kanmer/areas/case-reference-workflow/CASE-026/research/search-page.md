# Research — CASE-026 Search page port

## Contract sources (read, verified)

- EPIC-011 `context.md` §1.7 (binding render), D3 (display-state mapping),
  wave 2 lane D ownership (`waves.md`).
- `docs/frd/frd-12-operator-experience.md` §Search: the UI-07 filter set is
  exactly — Case/PO or image reference, Registration, Claimant,
  Claim/provider reference, Principal, State, Engineer, Received from/to,
  Origin — with Search and Clear. Results table: Case/PO + provider
  reference, vehicle, claimant, principal, type, state, due. Selected-Case
  preview: type, state, accident circumstances, provider reference,
  Engineer, due, next action, outstanding requirements, Open Case, copy
  Case/PO. Image-initiated Cases stay searchable and use their named states.
- Prototype final render (`renderCases` + `caseSearchBar`): header
  "Search"/freshness/Create Case; `advanced-search-grid` inside
  `panel > panel-body`; two panes `pane-layout pane-layout--2
  case-search-layout` ("Case results" + "Selected Case"); preview via
  `detail-canvas` (eyebrow type, h2 ref · reg, muted claimant · principal,
  chip, summary paragraph, `fact-grid`, Outstanding, Open Case dark +
  Copy Case/PO). Prototype fixture copy ("Comparison table", empty-state
  prose paragraphs) is not ported per the epic rules.

## Premises verified by read-only checks

1. Wave-1 CSS already ships `advanced-search-grid`, `case-search-layout`,
   `tr[data-select-href]` hover/focus/aria-selected styling, `table-row-link`,
   `.secondary`, `fact-grid`, `detail-canvas`, `blocker-list`, `pagination`,
   `row-button`, `.empty` — `site.css` lines 154/161-162 (prototype parity)
   and 261/268/327-328/343/359/407/510/567/659-670. No new CSS needed.
2. `site.js` row-selection (lines 1423-1481): rows carrying
   `data-select-href` (+ optional `data-select-id`) with an inner
   `<template>` swap into `[data-preview-target]`; click/Enter/focus select;
   the `selected` URL parameter is written from `data-select-id`;
   `aria-selected="true"` marks the initial row. Roving ArrowUp/Down works
   through `[data-row-list]` (line 1384 includes `tr[data-select-href]`).
3. Copy mechanism: `[data-copy-target]` (site.js lines 51-68) copies an
   element's text, button rendered `hidden` and revealed by script;
   precedent `Pages/Error.cshtml` line 27.
4. CASE-025 queues port (merged, `Pages/Cases/Index.*`) is the in-repo
   precedent for: page-header + `_FreshnessBanner` with
   `ViewData["RefreshFields"]`, `?selected=` binding with first-row default
   and NotFound when the id is absent from the loaded rows, batched
   engineer-name resolution (`ActorDisplayNames.ResolveStaffNamesAsync`),
   `pagination` nav, `Href(...)` route-value builder.
5. CASE-012 workspace (merged) supplies the settled fact vocabulary:
   "Provider reference" (`_CaseSummary` line 28, confirmed claim number
   falling back to summary), "Accident circumstances" (line 56),
   "Unassigned"/"Not recorded" absent-value words.
6. `OperatorLabels.CaseStage` already implements D3 including
   "Closed · <outcome>" for ProviderCancelled /
   CollisionEngineersRejected / CreatedInError / SourceEmailUnlinked, so
   terminal cases are searchable here with the right chip for free.
7. `SearchCases`/`CaseSearchFilters` (`Core/Cases/CaseQueries.cs`) already
   carries every §1.7 field except none — the grid maps onto existing
   parameters: query (Case/PO or image reference — it is the one parameter
   that feeds both the reference contains-match and the image-intake
   by-reference/by-registration lookups, `LoadImageIntakeResultsAsync`),
   registration, claimant, claimNumber (label becomes "Claim/provider
   reference"), principal, state, engineerId, fromDate/toDate (Received
   from/to), origin.
8. The one genuine extension: the results table needs vehicle make/model
   and the preview needs accident circumstances per row.
   `InstructionDraftEntity` already persists `VehicleMake`, `VehicleModel`,
   `AccidentCircumstances` (PegasusDbContext.cs lines 1408-1426) and the
   search projection joins that draft (`EfCaseQueryStore.SearchRows`), so
   extending `CaseSearchItem` + `SearchRow` + `MapSearchItem` needs no new
   query.
9. Existing tests to keep green (owned files):
   `CasesIndexWebTests.SearchUsesAuthorizedCoreQueryAndPreservesEveryFilterInPagingUrl`
   (all filters reach `ISearchCases`; pager preserves them; a real
   `/Cases/{id}` link renders), `EmptyAndUnavailableQueriesRender...`
   ("No matching cases" vs 503 "Cases are unavailable"),
   `AdministrationSearchAccountWebTests.OldCasesSearchLinksRedirect...`
   (301 + "No cases match these filters."). `RecordingSearchCases`
   constructs `CaseSearchItem` positionally — trailing optional params keep
   it compiling.
10. `docs/design/test-ui/catalogue.json` /Search entry keeps states
    default/empty/unavailable — the ported page keeps those three branches,
    so no structural catalogue edit is required (catalogue edits were
    PLAT-029's lane).
11. Branch base `origin/dev` (4d696225) carries PLAT-029 (shell/CSS/JS),
    CASE-025 (queues) and CASE-012 (workspace); no `Migrations/*` overlap
    with this lane.

## Assumptions (not machine-verified)

- Grid control types follow the ticket's "mapping 1:1 to the existing
  UI-07 inputs": State stays the enum select; Principal/Engineer/Origin
  stay text inputs (the prototype's selects were fixture-driven; a select's
  honest option source would be either fixture-shaped or circular on a
  search page). Recorded as decision P3 in the plan.
- `Received on` (exact), `Instructed on` (exact) and the record-kind filter
  are not §1.7 grid fields; their parameters stay bound, applied and
  pager-preserved so old `/Cases?...` bookmarks 301'd by PLAT-029 keep
  working with values intact, they are just not drawn.
- The preview pane is built from the row projection (not `IGetCase`):
  every fact §1.7 names is derivable from `CaseSearchItem` + the three
  extended fields + one batched staff-name resolve, and per-row
  `<template>` previews are required by the wave-1 selection script
  anyway.
