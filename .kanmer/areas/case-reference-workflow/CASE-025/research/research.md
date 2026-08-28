# Research — CASE-025 Cases queues (`/Cases`)

Branch `task/case-025-cases-queues` from origin/dev 5ca2572c (PLAT-029 merged).

## Verified by reading the code (not assumed)

- `Pages/Cases/Index.cshtml(.cs)`: five tabs (`not_ready|review|held|triage|unidentified`),
  legacy `.tabs` strip, one table per tab, `origin`/`principal` selects on Not ready,
  column sort links (`CaseSearchOrder`), Triage sub-tabs by state and a pager,
  Unidentified `kind` sub-tabs. `StateLabel(TriageState)` lives here and is
  referenced by `Triage/Details.cshtml.cs:241` (forwarder) and
  `Intake/Details.cshtml:196`.
- Core queries available: `ISearchCases` (`CaseSearchFilters.State` is a single
  state; `Principal` filter; `CaseSearchItem` has no completeness facts),
  `IDashboardQueries.GetCaseStageCountsAsync` → `CaseStageCounts(NotReady, Review,
  Held, WithEngineer)` — **no Complete count**; `IListTriage` (state `null` =
  every Triage row — the rail count convention in `RailCountsPageFilter`);
  `IUnidentifiedStore.ListQueueAsync(null)`; `IImageIntakeQueries.ListAsync(false)`
  + `ListImagesAsync(id)`; `IListIntake` with `Decision: BlockedIntake`
  (`IntakeReceiptSummary`: file name, sender/subject, received, failure reason);
  `IGetCase` → `CaseDetails.Data.Completeness.Values` (`CaseCompleteness`) and
  `Workflow.DueWork` (`CaseDueWork.DueBy/State/NextChaseAtUtc`) and
  `Workflow.AssignedEngineerId`; `IStaffAccountQueries.GetAsync` for the
  Engineer name.
- `Cases` table already stores `InstructionComplete`/`ImagesComplete`
  (`EfCaseQueryStore` joins `CaseEntity`; `SearchRow` does not project them).
- Design vocabulary present in `site.css`: `queue-layout`, `pane/pane-head/
  pane-body/pane-scroll`, `scope-list/scope-button/scope-visual-icon`,
  `queue-group-label`, `queue-exception[data-value=unidentified]`,
  `row-button/row-top/row-title/row-meta/row-excerpt/row-time`,
  `workflow-stepper(--compact)/workflow-step(-icon)/is-complete/is-current`,
  `blocker-list/blocker`, `definition-list/definition`, `filter-bar .field`,
  `eyebrow`, `status`. Sprite has `list, check-circle, user, check, clock,
  alert-triangle, alert-circle, image, mail, pause, file-text, folder-open`.
- `site.js`: `[data-row-list]` arrow navigation over `.row-button`;
  `[data-select-href]` preview needs a `<template>` and a
  `[data-preview-target]`; a row that is itself an `<a>` bypasses the JS
  click handler (`closest('a, button')`) and navigates, so a server-rendered
  `?selected=` pane is the no-script and the with-script path alike.
- `_StatusChip` tones "Not ready/Review/With Engineer/Complete/Held/
  Unidentified/Blocked intake"; `_FreshnessBanner` takes `RefreshFields`;
  `_PageHeader` reads `ViewData[Title/Eyebrow]`.
- Tests: `TriageQueuesWebTests` pins `Not ready\s*<span class="count">`,
  `name="origin"`, `<table` count, `subtabs` absence, `sort=received_asc`,
  `>REF</a>` ordering, `/Unidentified` 301, banned words on the Unidentified tab.
- `catalogue.json` `/Cases` entry: `queues--default` / `queues--empty`
  scenarios; `Test-UiCatalogue.ps1` fails on origin/dev already for
  `Administration/Principals/EvaSubmission.cshtml` (PLAT-029 report).

## Gaps and decisions

- **Complete count** does not exist: add `Complete` to `CaseStageCounts`
  (Core) and count `PostReportComplete` in `EfDashboardQueries` — a
  positional record parameter with default `0` keeps the one test constructor
  compiling. Outside Owns; reported.
- **With Engineer rows** = two state queries (`ReportPreparation`, `PostReport`)
  merged newest-first in the page (page 1 × 100, the Not ready convention), no
  Core filter change.
- **Missing filter** needs per-row completeness: add `InstructionComplete`/
  `ImagesComplete` (`bool?`, default `null`) to `CaseSearchItem` and project
  them in `EfCaseQueryStore.SearchRow`. Image-initiated rows are "Instructions
  missing" by definition. Outside Owns; reported.
- **Principal select** options: no principal directory port exists (Search
  uses a text input); the select lists the principals present in the loaded
  rows, the existing convention. Open question for the orchestrator.
- **Triage "provider"**: `TriageSummary`/`TriageRecord` carry no provider;
  the row shows registration · assignee only. Custody on image rows: no
  custody fact on `ImageIntakeSummary`; the row shows the image count.
- **Tab keys**: keep `not_ready` (Work Centre links, tests) and add
  `with_engineer`, `complete`; the README's hyphen spellings are accepted by
  normalising `-`→`_`.
- `StateLabel` move: `OperatorLabels.TriageState` becomes the owner;
  `IndexModel.StateLabel` is deleted and `Intake/Details.cshtml` retargeted.
  `Triage/Details.cshtml.cs:241` (INTK-046's file) is a one-line forwarder
  that must be retargeted — reported, not touched.
