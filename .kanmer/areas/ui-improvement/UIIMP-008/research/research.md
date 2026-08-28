# Research — UIIMP-008 Work Centre

Branch `task/uiimp-008-work-centre` from origin/dev 5ca2572c (PLAT-029 merged).

## Contract (EPIC-011 context §1.2, FRD-12 § Work Centre, design README § Work Centre)

Header Work Centre / eyebrow "Office-wide work" / freshness + Refresh + Create Case (primary). Five metrics → `/Cases?tab=not_ready|review|held|unidentified|unidentified` (D14: Blocked → Unidentified tab; Blocked figure = `IntakeQueueCounts.BlockedIntake`). Two panes `integrated-home--expanded`: left "Needs attention" work-item buttons (kind · ref, title, priority chip, detail, owner, due); right pane head "Today" / "Selected work" + "Open full record"; detail eyebrow, h2, lead, chip, notice "Why this needs attention" (label + Core value), fact grid (Source, Owner, Last recorded outcome, Due), panel "Next permitted action" (Open Case/Triage/Operations/Review source + Copy reference). Exactly five kinds. No Filter button. No Today counters are drawn; the old Case work due table and Today/This week strips go.

## Verified by read-only check

- `Pages/Index.cshtml` still renders legacy `.page-heading`, `.metric__value`, three `metric-strip--3` sections, a "Today and this week" strip and a "Case work due" list. `IndexModel` reads `IGetOperationsSnapshot` only.
- `GetOperationsSnapshot` (Core) takes `IIntakeReceiptQueries`, `IListTriage`, `ICaseDueWorkQueries` (bound 20), `IDashboardQueries`, `TimeProvider`; returns `OperationsSnapshot(AsOfUtc, Intake, TriageCount, DueWork, CaseStages, CaseActivity, MailActivity)`. Only caller of the Core class directly: `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` (constructs it with stubs). `ReadinessEndpointTests` resolves it through DI.
- Sources for the five kinds, all existing and registered in DI:
  - Case → `ICaseDueWorkQueries.GetDueAsync(asOf, max)` → `CaseDueWork` (Reference, MissingMaterialReason, DueBy, State, NextChaseAtUtc, MostRecentChannel/Outcome).
  - Held decision → `ISearchCases` with `CaseSearchFilters(State: Held)` (the Cases page's Held tab query) → `CaseSearchItem` (Reference, Principal, Claimant, EngineerId, Origin, ReceivedAtUtc, NextChaseAtUtc). PageSize ≤ 100.
  - Mail → `IUnidentifiedStore.ListQueueAsync(null)` → `UnidentifiedQueueRow` (Reference, MediaKind, FileName/EmailSubject/EmailSender, ReceivedAtUtc, ReasonCode).
  - Triage → `IListTriage` (PageSize ≤ 100) → `TriageSummary` (State, AssigneeId, NormalizedVehicleRegistration, CreatedAtUtc); "no finding" = state Open or AwaitingInformation.
  - External work → `GetRequestOperations.ExecuteAsync(actor)` (bound 100) filtered `Kind == ExternalWork && CanRetry` — the same filter `Operations/Index.cshtml` line 11 uses (CaseReference, PrincipalCode, ExternalKind, AttemptCount, FailureReason, LastActivityAtUtc).
  - Owner names → `ActorDisplayNames.ResolveStaffNamesAsync(IStaffAccountQueries, ids)` (Core pattern used by `GetTriage`).
- Shell hooks exist in site.js: `[data-copy-target]` (reveals the button, copies element text), `[data-row-list]` roving focus over `.work-item`, `[data-select-href]` + `<template>` → `[data-preview-target]` with `?selected=` replaceState. No page uses `data-select-href` yet.
- CSS vocabulary present: `.work-centre-metrics .metric-strip--5 .metric[data-value=…]`, `.integrated-home--expanded`, `.pane/.pane-head/.pane-body/.pane-scroll`, `.work-item/-head/-foot`, `.priority--overdue/high/today`, `.work-detail`, `.work-detail-lead`, `.today-pane-title`, `.notice`, `.fact-grid .fact`, `.panel/.panel-head/.panel-body`, `.status--*`, `.eyebrow`.
- `_MetricCard` takes ViewData MetricLabel/Value/Url/Icon/Key; `_PageHeader` takes Title/Eyebrow/PrimaryAction*; `_FreshnessBanner` takes the model time and belongs in `.page-actions` — but `_PageHeader` only renders `.page-actions` around its primary action, so the page renders its own `header.page-header` markup with freshness + Create Case (`/Cases/Create`).
- Labels: `OperatorLabels.ChaseReason`, `ChaseState`, `UnidentifiedReason`, `UnidentifiedMediaKind`, `EmailHandle`, `CaseStage`, `Humanise`, `OfficeDate/OfficeTime`; Triage state label is `Pages.Cases.IndexModel.StateLabel`.
- Tests pinning the old page: `DashboardCountersWebTests` ("Received today … metric__value"), `OperatorJourneyTests` (h1 "Dashboard", section order "active cases/e-mail activity/today and this week", `.metric .metric__value`), `AccessibilityTests` (h1 "Dashboard", dual selector).
- Catalogue entry for `/`: `pages/dashboard--default.html`, branch "Current loaded dashboard with ordinary metrics."; scenario id `dashboard--default` must stay (snapshot script).
- Banned words (design README § Voice) checked against the new copy: none used.

## Assumed

- Priority: Overdue = due instant before now; Today = due within the office day; High = retryable external failure; everything else Normal. This is the only place the vocabulary lives (Core enum).
- A Held Case's reason is its recorded state (no hold-reason read model exists on `CaseSearchItem`); the Held item shows the state as the Core value.
