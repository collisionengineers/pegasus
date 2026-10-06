# Work Centre Open tasks row: how it works

Read from the live source on 6 October 2026 (branch `task/report-dispatch-integration`, PR 1015 as built).

## What the row does not show

- The task's own words cannot be found with Find. Find matches the row's reference, title, detail and owner (`NeedsAttentionPolicy`). The Open tasks row's `Title` is the vehicle label. The task is its `Reason`, which is what the bold line prints.
- The row is not tied to Report sent. `ListOpenCaseTasksAsync` reads every open task on every Case, so a task staff add before the report is sent puts the Case on the Work Centre. The Case page lists open tasks only after Report sent.
- A task's assignee is not shown when it is not the first task's. Owner is the first task's assignee, else whoever created it.
- Nothing is due. The row always sits under Later, and Due reads its no-date text.
- Received is not when anything was received. It is when the row's first task was created.

## Governing documentation

| Document | What it settles for this row |
| --- | --- |
| [FRD-15 Work Centre](../../../../../../docs/frd/frd-15-work-centre-queues-and-search.md#work-centre) | The kind (one row per Case, its oldest open task and how many more, no due instant, always Later), the Tasks chip, the Owner rule, Open tasks opening the Case at Tasks, the row returning after a dismissal once a newer open task is created |
| [FRD-13 Due work and chasing](../../../../../../docs/frd/frd-13-case-lifecycle-and-workflow.md#due-work-and-chasing) | What a Case task is; "Chasing above is the only due work" |
| [FRD-16 Tasks](../../../../../../docs/frd/frd-16-case-record-workspace.md#tasks) | The section the row opens |

## Source by layer

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Presentation/NeedsAttentionPresentation.cs` | Chip order, slug `tasks`, action label, bold line, the open row's facts |
| Web | `Presentation/OperatorLabels.cs` | The kind "Open tasks", the chip "Tasks", `OpenTasksTitle`, `ReceivedAge` |
| Core | `Operations/DashboardCounts.cs` | `OpenCaseTask`; `NeedsAttentionKind.OpenTasks` ("named by its oldest open task; no due instant, so always Normal") |
| Core | `Operations/OperationsSnapshot.cs` | One row per Case, its owner, received and qualified instants; `CanTake`, `IsMine`, `OwnerText` |
| Infrastructure | `Persistence/EfDashboardQueries.cs` (`ListOpenCaseTasksAsync`) | Every open task with its Case, vehicle, claimant, Principal, assignee, and its creation instant and creator from `case_task_created` |

## The behaviours

### One row per Case

`ComposeNeedsAttentionAsync` groups the open tasks by Case and orders each group by creation, then task id. The first task names the row. `MoreCount` is the rest. The bold line is `OpenTasksTitle`: "{first task} (+{n} more)", or the task alone. Origin: "A Case with open tasks (operator, 6 October 2026)" (`OperatorLabels.WorkCentre.OpenTasks`).

### Labels

The kind beneath the bold line is "Open tasks". The chip is "Tasks", placed after Triage and before AI draft (`ChipOrder`). The action button is "Open tasks". These are plain labels chosen for building.

### Owner and Mine

`TaskOwner` is the first task's assignee, else the staff member its `case_task_created` entry records. A creator who was not staff counts as no one. With no one the Owner reads "Unassigned", since the kind has a person slot (`HasPersonSlot`). Mine lists the row for its owner only. An unowned Open tasks row is on no one's Mine, because `CanTake` is false for this kind.

### Due, group and order

The row carries no due instant and priority `Normal`, so it is under "Later (n)". The list orders by due instant with undated last, then received, then reference.

### Received

`Received` is the first task's creation instant. The ledger prints its age ("Today", "3 d ago").

### Dismiss

The row's `QualifiedAtUtc` is the newest open task's creation. A dismissal hides the row until a task newer than the dismissal is created on that Case.

### The open row and the action

The six facts are Reference, Vehicle (the row's title), Principal (its detail), Owner, Due and Received. **Open tasks** goes to `/Cases/{id}?section=tasks` (`StaffNotificationPolicy.CaseRoute`).

### Where the facts come from

`ListOpenCaseTasksAsync` joins each open task to its `case_task_created` history entry by id. That gives the instant and the actor, because a task row records neither. The Case's vehicle and claimant come from the instruction draft, else the confirmed Case data, as the Case search reads them.

## Things the FRD does not settle

- Which open task is "first" when the operator thinks of the oldest as something else (creation order today).
- What the Received column means for this kind (the first task's creation today).
- Where the chip sits in the toolbar (before AI draft today).
- Whether Find should match the task's words.
- Whether a Case appears here before its report is sent.
- The kind label "Open tasks", the chip "Tasks" and the action "Open tasks", which are working labels.
