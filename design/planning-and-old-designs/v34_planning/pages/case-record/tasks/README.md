# Case record: Tasks

- **Mockup route:** [`pegasus_case_tasks_v34.html`](../../../current/pegasus_case_tasks_v34.html) with `?state=edit` or `?state=read`. Both are captures of the as-built section (`current/captured/tasks-edit.html`; `read` is the same capture drawn without its actions).
- **Live source:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseTasks.cshtml`, `src/Pegasus.Web/Pages/Cases/Details.Tasks.cs`, `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs` (`Tasks`), `src/Pegasus.Core/Tasks/CaseTaskContracts.cs`, `src/Pegasus.Core/Tasks/CaseTaskUseCases.cs`, `src/Pegasus.Infrastructure/Persistence/EfCaseTaskStore.cs`, `src/Pegasus.Infrastructure/Persistence/ReportSentAfterSendTasks.cs`.
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Five per state: whole page at 1580, the section alone at 1580, then 1580×1000, 1440×900 and 760×1000.

- `edit`: [21](../../../current/v34-shots/21-case_tasks-edit-full.png) · [22](../../../current/v34-shots/22-case_tasks-edit-surface.png) · [23](../../../current/v34-shots/23-case_tasks-edit-1580.png) · [24](../../../current/v34-shots/24-case_tasks-edit-1440.png) · [25](../../../current/v34-shots/25-case_tasks-edit-760.png)
- `read`: [26](../../../current/v34-shots/26-case_tasks-read-full.png) · [27](../../../current/v34-shots/27-case_tasks-read-surface.png) · [28](../../../current/v34-shots/28-case_tasks-read-1580.png) · [29](../../../current/v34-shots/29-case_tasks-read-1440.png) · [30](../../../current/v34-shots/30-case_tasks-read-760.png)

## Notes

- The tasks Report sent creates and the ones staff add are drawn alike; nothing on the row says where a task came from.
- The aside's list of open tasks is the [Next action](../next-action/README.md) page; the Work Centre row is [Open tasks row](../../work-centre/open-tasks-row/README.md).
