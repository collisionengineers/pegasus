# Tasks: how it works

Read from the live source on 6 October 2026 (branch `task/report-dispatch-integration`, PR 1015 as built).

## What the section does not show

- Where a task came from. A task Report sent created and one staff added are the same row. The difference is only in history (`CaseTaskReasons.ReportSent` against "Added from the Tasks section").
- When a task was made, or by whom. The table has no date and no creator column, although the Next action and the Work Centre both order by the creation time.
- There is no unassign. The Assign select has a blank first option, selected when the task is unassigned, but the select is `required`, so a blank cannot be posted. Core would accept a null assignee (`AssignCaseTaskRequest.AssigneeId` is nullable; the store's refusal names "assigned, reassigned or unassigned").
- A completed or cancelled task cannot be reopened or changed: `CaseTaskRules.RequireOpen`.
- Nothing can change while the Case is closed: "Case tasks cannot be changed while the case is closed. Reopen the case first." (`RequireNonTerminal`).
- The list stops at 500 rows (`EfCaseTaskStore.ListAsync`, `Take(500)`), with no word saying so.
- A task has no due date.

## Governing documentation

| Document | What it settles for this section |
| --- | --- |
| [FRD-16 Tasks](../../../../../../docs/frd/frd-16-case-record-workspace.md#tasks) | The dense table, open first, shared by both views, loaded like Files and Notes; Complete, Cancel task, Assign and Add task in the edit session with fixed reasons; no due date; tasks from Report sent; the open-task gate |
| [FRD-13 Due work and chasing](../../../../../../docs/frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) | General Case tasks (`CASE-20`): description, optional assignee, three states; the gate on completing and archiving; After-send tasks created when Report sent is recorded, in the sender's name, replay-safe, as system work |
| [FRD-14 Case edit lease](../../../../../../docs/frd/frd-14-record-edit-leases.md#case-edit-lease) | System work keeps the session; every staff change ends the lease it was made under |
| [Case-workspace guardrails](../../../../../../.agents/skills/pegasus-ui-guardrails/references/case-workspace.md) | Tasks owns the Case's tasks; no task actions in Report |

## Source by layer

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pages/Cases/Shared/_CaseTasks.cshtml` | The section: New task, the table, the open row's three forms; `mayChange` |
| Web | `Pages/Cases/Details.Tasks.cs` | The four handlers, each with its fixed reason, `keepEditing: true`, and the return to the Tasks section; `TaskAssigneeChoices` |
| Web | `Pages/Cases/Details.cshtml.cs` | Reads the section body and, only while it can change, the assignee choices (`caseEngineerChoices`) |
| Web | `Presentation/CaseWorkspaceLabels.cs` (`Tasks`) | Every word, the state chip's text and tone, the four reasons and the four notices |
| Core | `Tasks/CaseTaskContracts.cs` | `CaseTaskState`, `CaseTaskRecord`, `CaseOpenTask`, the requests, `CaseTaskReasons.ReportSent` |
| Core | `Tasks/CaseTaskUseCases.cs` | `CreateCaseTask`, `AssignCaseTask`, `CompleteCaseTask`, `CancelCaseTask`; `CaseTaskRules` |
| Infrastructure | `Persistence/EfCaseTaskStore.cs` | The list order; the four mutations under the lease, with version checks, replay and history; `AddConsequenceTask`; `ReadOpenAsync` |
| Infrastructure | `Persistence/ReportSentAfterSendTasks.cs` | The tasks Report sent creates |

## The behaviours

### The list

The table has the columns Task, Assignee and State, and in the edit session an actions column with a screen-reader label "Actions". The assignee is the staff member's name, "Unassigned" when there is none, or the former-staff name when the account is gone (`ActorDisplayNames.FormerStaff`). The state is a plain chip: Open amber, Completed green, Cancelled neutral (`StateTone`). The order is open, then completed, then cancelled, and within each alphabetical by description, then by id (`EfCaseTaskStore.ListAsync`). The section belongs to the Case, so both views show one list. It is fetched like Files and Notes.

### When the actions are offered

`mayChange` is `CanEditCaseData` and the Case is not closed. `CanEditCaseData` needs a lease token for this render, a Case that is neither Completed nor Query, and one that is not archived. Without it the list stands alone.

### Add task

**New task** is one required text box. A description has no length limit "(operator, 6 October 2026)" (`CaseTaskRules.ValidateCreate`). A new task is open and unassigned. The notice is "The task was added."

### Assign

An open task offers a select of the choices `caseEngineerChoices` gives (the enabled staff), and **Assign**. The select starts on the task's assignee, else a blank first option. Core requires the chosen account to exist and be enabled (`RequireEligibleAssignee`). The notice is "The task was assigned." With no choices the Assign form is not drawn.

### Complete and Cancel task

**Complete** and **Cancel task** (danger style) end an open task. The notices are "The task was completed." and "The task was cancelled."

### One fixed reason, and the session carries on

Each action posts with its fixed reason ("Added from the Tasks section", "Assigned from the Tasks section", "Completed from the Tasks section", "Cancelled from the Tasks section"), so there is no reason box. The store checks the Case version under the lease (`RequireVersionUnderLease`), the task's version, the lease, an archived Case and a closed Case. It advances the version, ends the lease the action was made under, and writes the history. The handler then claims the lease again (`keepEditing: true`, save as you go, 29 September 2026) and returns to the Tasks section. Origin: `Details.Tasks.cs`.

### Tasks Report sent creates

When Report sent is recorded and the linked Sent item is a Pegasus report send with an after-send list (`StaffMailSendOperations.ReportDispatchJson`), `ReportSentAfterSendTasks.AddAsync` adds one open, unassigned task per description, inside the transaction that records the link. Each task carries the reason `CaseTaskReasons.ReportSent` and is in the name of the staff member who sent the report, or whoever recorded the link when the send does not record exactly one staff role. The task id comes from the Sent evidence and the position in the list, so a replay adds nothing. It is system work: `CaseMutationGuard.Advance` moves the Case's version on and "an editor keeps their session" (operator, 6 October 2026).

### The gate the tasks set

While a task is open the Case cannot be completed, closed or archived. `CaseTerminalReadinessGuard.RequireNoOpenTasksAsync` refuses with `CaseTaskRules.OpenTasksBlockTerminal`, "Complete or cancel every open task first." (operator, 6 October 2026). How the page shows the gate is the [Next action](../next-action/how-it-works.md) page.

## Things the FRD does not settle

- Whether staff can unassign a task.
- The order within the open tasks (alphabetical today, while the Next action and the Work Centre use creation order).
- Whether a row says where a task came from (added by staff, or from the Principal's report sending rules), or when.
- Whether a completed or cancelled task can be reopened.
- Whether the list says it is capped.
