# Plan — UIIMP-008

Diff estimate: ~600 lines (Core +200, page ~180 rewritten, page model ~90, labels +25, tests ~150, catalogue 2).

1. **Core projection** (`DashboardCounts.cs`): `NeedsAttentionKind {Case, HeldDecision, Mail, Triage, ExternalWork}`, `NeedsAttentionPriority {Overdue, High, Today, Normal}` (declaration order is the sort order), `NeedsAttentionItem(Kind, Id, Reference, Title, Detail, Reason, Priority, Owner, Due, LastOutcome, Source)`. Reason/Source/Title are recorded facts or Core enum names; Web labels them. Reuses: nothing new persisted.
2. **Composition** (`OperationsSnapshot.cs`): `GetOperationsSnapshot` gains `ISearchCases`, `IUnidentifiedStore`, `GetRequestOperations`, `IStaffAccountQueries`; builds the five kinds from the existing queries (research lists each), resolves owner names via `ActorDisplayNames`, orders by Priority then Due (absent last) then Reference, `Take(MaximumNeedsAttention = 50)`. Snapshot record gains `NeedsAttention`. Route mapping stays in Web (Core does not know page routes).
3. **Page**: `Index.cshtml` per §1.2 using `_MetricCard` ×5 in `.metric-strip.metric-strip--5` inside `.work-centre-metrics`, `_FreshnessBanner`, `.pane-layout.pane-layout--2.integrated-home.integrated-home--expanded`, `.work-item` as `<a data-select-href data-select-id>` with a `<template>` per item, right pane `[data-preview-target]` server-rendered from `?selected=`. `IndexModel`: `Selected` = matching item or first; `Route(item)` switch on kind; label helpers through `OperatorLabels`.
4. **Labels**: `OperatorLabels.NeedsAttentionKind`, `.NeedsAttentionPriority`; reason via existing `ChaseReason`, `UnidentifiedReason`, `Cases.IndexModel.StateLabel`, `CaseStage`.
5. **Tests**: retarget the three pinned files at equal strength; Core stub update + an ordering/bound unit test.
6. **Catalogue**: branch text for `/`; run `pwsh ./scripts/Test-UiCatalogue.ps1`.
7. Build Release; merge origin/dev; simplification pass; report; PR to dev.

Out of scope (report): deleting `CaseActivityCounts` / `MailActivityCounts.ReceivedToday` and their EF query now that no page renders them (Infrastructure file not owned); the Cases page three-pane layout (C1); `StateLabel` move to `OperatorLabels` (C2).
