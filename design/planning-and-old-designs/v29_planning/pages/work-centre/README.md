# Work Centre

- **Parent:** [v29 pages](../README.md)
- **Mockup route:** [pegasus_work_centre_v29.html](../../current/pegasus_work_centre_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Index.cshtml`, `Index.cshtml.cs`, `_WorkCentreBody.cshtml`
  - `src/Pegasus.Web/Presentation/NeedsAttentionPresentation.cs`, `OperatorLabels.cs` (`WorkCentre`)
  - `src/Pegasus.Core/Operations/OperationsSnapshot.cs` (`WorkCentreMetrics`, `GetOperationsSnapshot`, `NeedsAttentionPolicy`)
  - `src/Pegasus.Core/AiWork/AiDrafts.cs` (`WorkTargets.DueAt`)
  - `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`, `EfUnidentifiedStore.cs`, `EfRecentCaseQueries.cs`

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Baseline: [1580](../../current/v29-shots/s05-work-centre-1580.png) · [1440](../../current/v29-shots/s05-work-centre-1440.png) · [760](../../current/v29-shots/s05-work-centre-760.png)
- Triages metric (P8, P9): [1580](../../current/v29-shots/p14-work-centre-1580.png) · [1440](../../current/v29-shots/p14-work-centre-1440.png) · [760](../../current/v29-shots/p14-work-centre-760.png)
- Triages after Held (P8 variant): [1580](../../current/v29-shots/p15-work-centre-metric-after-held-1580.png) · [1440](../../current/v29-shots/p15-work-centre-metric-after-held-1440.png) · [760](../../current/v29-shots/p15-work-centre-metric-after-held-760.png)

## Notes

- Scope for v29: the metric strip and how Triage items appear in the Needs
  attention list and Today pane. The rest of the page is in v28's
  [Work Centre how-it-works](../../../v28_planning/pages/work-centre/how-it-works.md)
  as of 18 September 2026.
