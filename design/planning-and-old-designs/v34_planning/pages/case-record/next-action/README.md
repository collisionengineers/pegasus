# Case record: Next action after the send, and the ribbon gates

- **Mockup route:** [`pegasus_case_next_action_blocker_v34.html`](../../../current/pegasus_case_next_action_blocker_v34.html) with `?state=next` or `?state=archive`. Both are captures of the as-built page (`current/captured/blocker-next-action.html`, `current/captured/blocker-archive.html`).
- **Live source:** `src/Pegasus.Web/Pages/Cases/Shared/_CaseAside.cshtml` (`data-next-action`, `data-open-tasks`), `src/Pegasus.Web/Pages/Cases/Shared/_CaseRibbon.cshtml` (Mark completed, Archive), `src/Pegasus.Web/Pages/Cases/Details.Frame.cs` (`OpenTasks`, `OpenTasksCondition`, `NextAction`), `src/Pegasus.Web/Presentation/CaseNextAction.cs`, `src/Pegasus.Core/Tasks/CaseTaskUseCases.cs` (`CaseTaskRules.OpenTasksBlockTerminal`), `src/Pegasus.Infrastructure/Persistence/ArchivedCaseGuard.cs` (`CaseTerminalReadinessGuard`), `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` (`GetPageFrameAsync`).
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Five per state: whole page at 1580, the aside and ribbon surface at 1580, then 1580×1000, 1440×900 and 760×1000.

- `next`: [31](../../../current/v34-shots/31-case_next_action_blocker-next-full.png) · [32](../../../current/v34-shots/32-case_next_action_blocker-next-surface.png) · [33](../../../current/v34-shots/33-case_next_action_blocker-next-1580.png) · [34](../../../current/v34-shots/34-case_next_action_blocker-next-1440.png) · [35](../../../current/v34-shots/35-case_next_action_blocker-next-760.png)
- `archive`: [36](../../../current/v34-shots/36-case_next_action_blocker-archive-full.png) · [37](../../../current/v34-shots/37-case_next_action_blocker-archive-surface.png) · [38](../../../current/v34-shots/38-case_next_action_blocker-archive-1580.png) · [39](../../../current/v34-shots/39-case_next_action_blocker-archive-1440.png) · [40](../../../current/v34-shots/40-case_next_action_blocker-archive-760.png)

## Notes

- The `archive` capture is a closed Case that still holds an open task. The store refuses to close a Case with an open task, so the capture comes from a Web test whose page frame is handed an open task (`PageFrameWithOpenTasks` in `current/captured/capture-test.patch`). How a live Case would reach that state was not traced for this folder.
- The tasks themselves are the [Tasks](../tasks/README.md) page.
