# Cases list

- **Parent:** [v29 pages](../../README.md)
- **Mockup route:** [pegasus_work_centre_v29.html](../../../current/pegasus_work_centre_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Cases/Index.cshtml`, `Index.cshtml.cs`
  - `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs`
  - `src/Pegasus.Core/Triage/TriageQueryUseCases.cs` (`ListTriage`)
  - `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`, `EfTriageStore.cs`, `EfUnidentifiedStore.cs`
- **Child pages:** [Case record](../case-record/README.md) · [Create case](../create/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Baseline, Triage tab: [1580](../../../current/v29-shots/s07-cases-triage-1580.png) · [1440](../../../current/v29-shots/s07-cases-triage-1440.png) · [760](../../../current/v29-shots/s07-cases-triage-760.png)
- Triage in the Workflow group (P9, P10): [1580](../../../current/v29-shots/p16-cases-triage-1580.png) · [1440](../../../current/v29-shots/p16-cases-triage-1440.png) · [760](../../../current/v29-shots/p16-cases-triage-760.png)

## Notes

- Scope for v29: the rail's groups and counts, the Triage tab, and how Case
  type shows in the Case queues. v28's
  [Cases how-it-works](../../../../v28_planning/pages/cases/index/how-it-works.md)
  covers the rest as of 18 September 2026.
