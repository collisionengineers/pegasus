# Plan — CASE-025

1. **Core/Infra facts** (reuse `CaseStageCounts`, `CaseSearchItem`,
   `EfDashboardQueries`, `EfCaseQueryStore`): add `Complete` count and the two
   completeness booleans on search items. No new port.
2. **Labels** (reuse `OperatorLabels`): move `TriageState` from `IndexModel`;
   add `CaseRequirement(instructionsMissing|imagesMissing)` → (requirement,
   resolve) pairs used by the Missing filter and the quick-detail blockers.
3. **PageModel**: tabs `not_ready|review|with_engineer|complete|triage|held|
   unidentified` (+`queue` alias, hyphen accepted); counts via
   `Task.WhenAll(stage counts, IListTriage page-size-1 total, ListQueueAsync)`
   — the `RailCountsPageFilter` recipe; rows per tab (cases: `ISearchCases`
   with `Principal`; With Engineer = two states merged; Not ready = cases +
   awaiting-instruction images, Missing filter applied in-page; Triage:
   `IListTriage`; Unidentified: queue rows + `IListIntake(BlockedIntake)`
   rows uncounted); newest-first with `sort=received_asc` toggle kept;
   `selected` defaults to the first row; selected case loads `IGetCase` and
   the Engineer name via `IStaffAccountQueries`; selected image loads
   `ListImagesAsync`. Unknown `tab`/`missing`/`sort` → 404 as today.
4. **View**: `_PageHeader` + `_FreshnessBanner`; `form.filter-bar
   data-auto-submit` (Principal select, Missing select on Not ready, Clear
   link, noscript Apply); `section.pane-layout queue-layout` with the rail
   (`queue-group-label` × 3, `scope-button`s as links with `aria-pressed`,
   icon well, count), the row pane (`[data-row-list]`, `a.row-button
   [data-select-href] [aria-selected]`, `pagination`), and the quick-detail
   pane (`[data-preview-target]`; case → eyebrow origin, h2, compact
   stepper, `blocker-list`, `definition-list` Due/Engineer/Next action, Open
   full Case; others → `definition-list` + open button). No inline styles,
   no prose, no banned words.
5. **Tests**: retarget `TriageQueuesWebTests` (count regex on the
   scope-button count span; `row-button` count instead of `<table`;
   `name="missing"` instead of `origin`; row order by `data-select-id`);
   add Blocked-uncounted, Complete/With Engineer group counts, selected pane
   tests.
6. **Catalogue**: branch text for `/Cases` states; run
   `pwsh ./scripts/Test-UiCatalogue.ps1` (pre-existing EvaSubmission failure
   expected).
7. Build, merge origin/dev, simplification pass, report, PR.

## Simplification pass — 2026-08-28

(filled in after implementation)
