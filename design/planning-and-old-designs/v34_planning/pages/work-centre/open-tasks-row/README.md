# Work Centre: Open tasks row

- **Mockup route:** [`pegasus_work_centre_tasks_v34.html`](../../../current/pegasus_work_centre_tasks_v34.html) with `?state=row`, a capture of the as-built Work Centre (`current/captured/work-centre-tasks-row.html`).
- **Live source:** `src/Pegasus.Core/Operations/DashboardCounts.cs` (`OpenCaseTask`, `NeedsAttentionKind.OpenTasks`), `src/Pegasus.Core/Operations/OperationsSnapshot.cs` (the Open tasks rows, `TaskOwner`, `NeedsAttentionPolicy`), `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` (`ListOpenCaseTasksAsync`), `src/Pegasus.Web/Presentation/NeedsAttentionPresentation.cs`, `src/Pegasus.Web/Presentation/OperatorLabels.cs` (`WorkCentre.OpenTasks`, `KindChip`, `OpenTasksTitle`).
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Whole page at 1580, the Needs attention surface at 1580, then 1580×1000, 1440×900 and 760×1000.

- `row`: [41](../../../current/v34-shots/41-work_centre_tasks-row-full.png) · [42](../../../current/v34-shots/42-work_centre_tasks-row-surface.png) · [43](../../../current/v34-shots/43-work_centre_tasks-row-1580.png) · [44](../../../current/v34-shots/44-work_centre_tasks-row-1440.png) · [45](../../../current/v34-shots/45-work_centre_tasks-row-760.png)

## Notes

- Only the new kind is in scope. The rest of the Work Centre is as v32 left it.
- The row opens the Case at its [Tasks](../../case-record/tasks/README.md) section.
