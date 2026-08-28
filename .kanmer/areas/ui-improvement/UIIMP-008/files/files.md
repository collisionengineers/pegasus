# Files — UIIMP-008

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Operations/DashboardCounts.cs` | Add `NeedsAttentionKind`, `NeedsAttentionPriority`, `NeedsAttentionItem` records. |
| `src/Pegasus.Core/Operations/OperationsSnapshot.cs` | `OperationsSnapshot` gains `NeedsAttention`; `GetOperationsSnapshot` composes it from `ICaseDueWorkQueries`, `ISearchCases`, `IUnidentifiedStore`, `IListTriage`, `GetRequestOperations`, `IStaffAccountQueries`; bound 50. |
| `src/Pegasus.Web/Pages/Index.cshtml` | Rewritten to §1.2. |
| `src/Pegasus.Web/Pages/Index.cshtml.cs` | `?selected`, item routes, label mapping. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | (outside Owns — labels must live here per EPIC-011 rules) `NeedsAttentionKind`, `NeedsAttentionPriority` labels. |
| `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` | Stub constructor update + ordering/bound test. |
| `tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs` | Retargeted: five metrics, Blocked target, Mail kind item and detail. |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | h1 "Work Centre", pane order, `.metric-value`. |
| `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` | h1 "Work Centre", `.metric .metric-value`. |
| `docs/design/test-ui/catalogue.json` | `/` state branch text. |
