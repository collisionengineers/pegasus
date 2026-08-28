# Research — Work Centre port (UIIMP-008)

Machine recovery note: a previous agent died after pushing commit `78005070`
"feat(operations): add work-centre attention projection (UIIMP-008)" — Core
only (`OperationsSnapshot.cs` +187, `DashboardCounts.cs` +57); the Razor page
was never touched. This ticket audits and repairs that projection rather than
rewriting it, then ports the page.

## Verified by read-only check

- **Design contract.** EPIC-011 `context.md` §1.2, `docs/design/README.md`
  §Component map + §Work Centre contract, and FRD-12 §Work Centre agree:
  header (Work Centre / Office-wide work / freshness + Refresh + Create Case
  primary), five-metric strip (`work-centre-metrics`, `metric-strip--5`),
  two-pane `integrated-home integrated-home--expanded` (left Needs attention
  `work-item` rows; right Today/Selected work `work-detail`).
- **Prototype final render.** The prototype is monkey-patched; the effective
  layer is `renderHome` (l.985) as replaced by the patches at l.1602
  (five derived metrics) and l.1868 (drop the inspector aside, add
  `integrated-home--expanded`, right head becomes "Today"/"Selected work").
  `pane-layout--work` is NOT in `site.css` (0 hits) — dropped; the delivered
  vocabulary is `pane-layout integrated-home integrated-home--expanded`.
  Prototype defects not ported (§1.15): the Filter no-op, the fixture-driven
  Blocked metric, and the muted "Ordered by business urgency" line
  (how-it-works copy).
- **CSS already delivered by PLAT-029.** `site.css` carries every class this
  page needs: `page-header`, `eyebrow`, `page-actions`, `metric-section`,
  `work-centre-metrics` (incl. per-`data-value` icon wells for
  not_ready/review/held/unidentified/blocked), `metric-strip--5`, `pane`,
  `pane-head`, `pane-body pane-scroll`, `detail-canvas`, `work-item`,
  `work-item-head/foot`, `row-meta`, `work-detail`, `work-detail-lead`,
  `today-pane-title`, `fact-grid`/`fact`, `notice`, `panel`, `button-row`,
  `cluster`, `section-label`. No new CSS is required.
- **Metrics are real queries.** Not ready/Review/Held = `CaseStageCounts`
  (`IDashboardQueries.GetCaseStageCountsAsync`); Unidentified =
  `MailActivityCounts.Unidentified`; Blocked = `IntakeQueueCounts.BlockedIntake`
  — all already on `OperationsSnapshot`. Blocked links to
  `/Cases?tab=unidentified` (D14), like Unidentified.
- **Icons** exist in `_LucideSprite`: prototype metric map
  (not_ready→alert-triangle, review→eye, held→clock, unidentified→search,
  blocked→lock) maps to `icon-alert-triangle`, `icon-eye`, `icon-clock`,
  `icon-search`, `icon-lock`, plus `icon-plus` (Create Case),
  `icon-arrow-right` (action), `icon-copy` (Copy reference),
  `icon-external-link` (Open full record).
- **JS provided by the shell** (`site.js`): `[data-row-list]` ArrowUp/Down
  roving focus includes `.work-item`; `[data-copy-target]` copy buttons copy
  an element's text and self-reveal (rendered `hidden`). All control wiring is
  direct `querySelectorAll` — NOT delegation — so buttons cloned from
  `<template>` by the `[data-select-href]` preview mechanism would be inert.
  The Work Centre therefore selects server-side: rows are links to
  `/?selected=<id>` and the detail pane is rendered by the page model, so
  every control is live without new JS.
- **Core dependencies of the dead agent's projection all exist and are
  registered** (`Infrastructure/DependencyInjection.cs`):
  `ISearchCases` (l.291), `IUnidentifiedStore` (l.116),
  `GetRequestOperations` (l.241), `IStaffAccountQueries` (l.169), alongside
  the pre-existing five. `GetRequestOperations` demands the same
  `StaffAccessRight.PerformCasework` as `GetOperationsSnapshot`, and the
  Operations page carries the same role set as Index — no access regression.
- **Source shapes check out.** `CaseDueWork` (Reference,
  MissingMaterialReason — already operator text via
  `OperatorLabels.ChaseReason`, State, NextChaseAtUtc, DueBy,
  MostRecentChannel/Outcome), `CaseSearchItem` (Claimant, Principal,
  EngineerId, Origin, NextChaseAtUtc), `UnidentifiedQueueRow` (handle fields,
  ReasonCode, MediaKind), `TriageSummary` (Registration, State, AssigneeId),
  `RequestOperationProjection` (ExternalKind, AttemptCount, FailureCode/
  Reason, CanRetry) — the dead agent's mappings compile against all of them.
- **Labels.** `OperatorLabels` already has `ChaseState(CaseDueWorkState)`,
  `UnidentifiedReason(code)`, `CaseStage`, `Humanise`, `OfficeClock`, and the
  `CaseStage(string?)` string-parse overload precedent for labelling a Core
  enum name carried as a string.
- **Create Case** route is `/Cases/Create` (the shell Add dialog links it);
  record routes `/Cases/Details/{id}`, `/Triage/Details/{id}`,
  `/Unidentified/Details/{id}`, `/Operations` all exist.
- **Tests in this lane.** `DashboardCountersWebTests` asserted the old
  `metric__value` markup of a "Received today" tile that no longer exists in
  the ported design — rewritten against the new strip.
  `DashboardBoundaryTests` constructed `GetOperationsSnapshot` with the old
  5-argument signature — repaired for the four new dependencies.
  Corrected 2026-08-28 (review finding 4): the deleted tile's mailbox-only
  Received-today channel split has **no remaining pin** — coverage was
  deleted with the tile, and no test asserts the split. Wave 3 CASE-028
  owns `DashboardCounts.cs` and can pin it there.

## Assumptions (not independently verified)

- `GetDueAsync` returns due work in whatever chase state it holds; the
  projection labels the recorded state rather than assuming "Scheduled".
- The orchestrator regenerates snapshots/CI; this ticket builds only.
