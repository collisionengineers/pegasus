# Next action after the send, and the ribbon gates: how it works

Read from the live source on 6 October 2026 (branch `task/report-dispatch-integration`, PR 1015 as built).

## What the page does not show

- Before Report sent is recorded, the page knows nothing of open tasks. The page frame reads them only when `ReportSentEvidence` is set (`EfCaseQueryStore`), so no list appears and nothing is greyed, whatever tasks the Case holds.
- Only Mark completed and Archive are greyed. The store refuses more than those two while a task is open. `CaseTerminalReadinessGuard.RequireNoOpenTasksAsync` also guards Close case (every outcome, `EfCaseWorkflowStore.CloseAsync`), Correct principal (`EfLinkedCaseReplacementStore`) and the cancellation when the source e-mail is unlinked (`EfIntakeMutationStore.CancelOnSourceUnlinkAsync`). Those stay live in the menu and refuse with the sentence after the click.
- The Cases list's quick detail does not carry the gate. `Cases/Index.cshtml.cs` calls `CaseNextAction.Of` without the gate, so "Current work" reads "Mark completed" as a plain text pair.
- The reason is hover text only (`title` on the wrapper). A greyed button gives no other cue to keyboard or touch users.
- Each task row repeats a button labelled "Tasks". All of them go to the same section, not to the task.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-16 Case workspace](../../../../../../docs/frd/frd-16-case-record-workspace.md#case-workspace) | Once Report sent is recorded, the Next action lists the open tasks, oldest first, each linking to Tasks; Mark completed greyed there with the reason |
| [FRD-16 Actions menu](../../../../../../docs/frd/frd-16-case-record-workspace.md#actions-menu) | Mark completed and Archive greyed with "Complete or cancel every open task first." on hover |
| [FRD-13 Due work and chasing](../../../../../../docs/frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) | A Case with an open task cannot be completed or archived; the reason's words |
| [Case-workspace guardrails](../../../../../../.agents/skills/pegasus-ui-guardrails/references/case-workspace.md) | The open tasks in the same blocker style as the report blockers; the aside scrolls on its own |
| [Design authority](../../../../../../docs/design/README.md) | "Complete or cancel every open task first." is approved copy |

## Source by layer

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pages/Cases/Shared/_CaseAside.cshtml` | The Next action step, its greyed Mark completed, and the Tasks list |
| Web | `Pages/Cases/Shared/_CaseRibbon.cshtml` | The Actions menu's Mark completed and Archive, greyed while a task is open |
| Web | `Pages/Cases/Details.Frame.cs` | `OpenTasks`, `OpenTasksCondition`, `NextAction` |
| Web | `Presentation/CaseNextAction.cs` | `CaseNextActionStep.Gate`; `AfterTheSend` |
| Web | `Pages/Cases/Index.cshtml.cs` | The Cases list's Current work, from the same `CaseNextAction.Of` |
| Core | `Tasks/CaseTaskUseCases.cs` | `CaseTaskRules.OpenTasksBlockTerminal` |
| Infrastructure | `Persistence/EfCaseQueryStore.cs` (`GetPageFrameAsync`) | Reads the open tasks only once Report sent is recorded |
| Infrastructure | `Persistence/EfCaseTaskStore.cs` (`ReadOpenAsync`) | The open tasks, oldest first by their `case_task_created` history |
| Infrastructure | `Persistence/ArchivedCaseGuard.cs` (`CaseTerminalReadinessGuard`) | The store refusal while a task is open |

## The behaviours

### The step after the send

Once the report is sent, `CaseNextAction.AfterTheSend` returns Create audit on an Inspection + Audit Case that has no Audit yet. On any other Case it returns Mark completed, carrying `Gate`. `Details.Frame.cs` passes `OpenTasksCondition` as that gate: `CaseTaskRules.OpenTasksBlockTerminal` while the Case has an open task, else null. With a gate, the aside draws the step's label as a disabled button inside `<span class="menu-gated" title="{reason}">`, because a disabled button takes no pointer events. Without one the row is the label and a link named for the step's section (`overview`). Origin: "Mark completed is greyed with its reason, as the menu item is, while the Case has an open task (operator, 6 October 2026)" (`_CaseAside.cshtml`).

### The Tasks list

When `OpenTasks` is not empty the aside draws a sub-panel headed "Tasks" in the report-blocker style. It has one row per open task with the description in bold and a **Tasks** button that jumps to the Tasks section in the current view. `EfCaseTaskStore.ReadOpenAsync` orders them oldest first by each task's earliest `case_task_created` history entry, then by id. It is read in the page frame's own context. Origin: "Once Report sent is recorded the Case's open tasks are what stands before Mark completed (operator, 6 October 2026)" (`_CaseAside.cshtml`).

### Both views

The open tasks are the Case's, read once for the frame, so the list is drawn in the Audit view and the Inspection view alike. In the Inspection view the step itself is the Inspection report's (`CaseNextAction.OfPastWork`), which has no Mark completed.

### The Actions menu gates

Mark completed is offered in Report sent (`PostReport`) and Archive on a closed, unarchived Case, both only while no colleague holds the lease. While `OpenTasksCondition` is set, each is drawn greyed in a `menu-gated` wrapper whose title is the reason, "as Create audit is" (`_CaseRibbon.cshtml`). Otherwise each opens its dialog.

### The store's refusal

The page's greying is a cue. The rule is in the store. `CaseTerminalReadinessGuard.RequireNoOpenTasksAsync` refuses with the same sentence in `CloseAsync` (Mark completed and both adverse closures), `ArchiveAsync`, the Correct principal replacement and the source-unlink cancellation.

## Things the FRD does not settle

- How the gate shows in the Cases list's quick detail.
- Whether each listed task has its own button, or the list has one link to Tasks.
- Whether the open tasks are listed in the Inspection view as well as the Audit view.
- Whether Close case and Correct principal are greyed like Mark completed and Archive, since the store refuses them too.
- Whether the page says anything about open tasks before Report sent is recorded.
