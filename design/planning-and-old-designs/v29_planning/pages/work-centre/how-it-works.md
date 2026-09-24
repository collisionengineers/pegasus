# Work Centre — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

This covers the metric strip and how Triage items appear on the Work Centre
(`/`). Paths are under `src/` unless stated.

## What this page does not show

- No Triage count in the metric strip. The four metrics are Not ready,
  Review, Held and Unidentified (`WorkCentreMetrics`). Triage appears only as
  a kind in Needs attention.
- No With Engineer, Completed, Query or Awaiting instruction metric.
- No split by Case type. Every metric counts Cases of every type together.
- No Triage with a finding. The Triage kind reads only Open and Awaiting
  information records; a Triage in Finding recorded, Completed or Cancelled
  never appears.
- No Triage in New cases. That list is Cases created in the last 7 days.
- No due date on the Triage record itself. The due instant exists only here,
  computed from the Triage target.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-15](../../../../../docs/frd/frd-15-work-centre-queues-and-search.md) | Four counts (Not ready, Review, Held, Unidentified), each an exact link to its Cases tab; a failed read shows unavailable, never `0`; Needs attention kinds including Triage ("A Triage record without a finding", due "Triage target after it opened"); calendar-day rules; Office and Mine; kind chips; the Today pane's Open Triage and Assign to me; New cases. |
| [FRD-17](../../../../../docs/frd/frd-17-administration-workspace.md) | The workflow targets, including the Triage target, are settings. |
| [FRD-03](../../../../../docs/frd/frd-03-triage.md) | A Triage has an assignee but no due date and no chase schedule. |
| [CONTEXT.md](../../../../../CONTEXT.md) | Triage and Unidentified as separate pre-Case records; New cases today excludes Triage and Unidentified. |
| [Design authority](../../../../../docs/design/README.md) | `metric-strip--4`, `metric`: count buttons linking to `/Cases?tab=` (the Work Centre's four). |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pegasus.Web/Pages/Index.cshtml` | Renders `_WorkCentreBody`. |
| Web | `Pegasus.Web/Pages/_WorkCentreBody.cshtml` | The header (Create Case, freshness), the metric strip, Needs attention with its scope switch and kind chips, the Today pane, the Assign Engineer dialog, New cases, AI jobs. |
| Web | `Pegasus.Web/Pages/Index.cshtml.cs` | Reads the snapshot; `Metrics`, `KindCounts`, `Selected`, `CanTakeSelected`; `OnPostAssignTriageToMeAsync`; the Office default scope. |
| Web | `Pegasus.Web/Presentation/NeedsAttentionPresentation.cs` | Kind chip order and slugs; the Triage row's title, detail and Today facts; "Open Triage". |
| Web | `Pegasus.Web/Presentation/OperatorLabels.cs` (`WorkCentre`) | `KindChip`, `TriageTitle`, `NoEngineer`, due and received words. |
| Core | `Pegasus.Core/Operations/OperationsSnapshot.cs` | `WorkCentreMetrics`, `OperationsSnapshot`, `GetOperationsSnapshot` (inputs, composition, paging), `NeedsAttentionPolicy` (`CanTake`, `IsMine`). |
| Core | `Pegasus.Core/AiWork/AiDrafts.cs` | `WorkTargets.DueAt`. |
| Core | `Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | `TriageTargetDays` (default 1). |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` | `GetCaseStageCountsAsync`: Case counts grouped by workflow state. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs` | `ListQueueAsync`: open Unidentified items. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfTriageStore.cs` | The Triage list read per state. |

## Behaviours

### The metric strip

`_WorkCentreBody.cshtml` draws four `a.metric` links in a `metric-strip--4`:

| Metric | Links to | Counts | Source |
| --- | --- | --- | --- |
| Not ready | `/Cases?tab=not_ready` | Case workflows in `NotReady` | `CaseStageCounts.NotReady` from `EfDashboardQueries` |
| Review | `/Cases?tab=review` | Case workflows in `Review` | `CaseStageCounts.Review` |
| Held | `/Cases?tab=held` | Case workflows in `Held` | `CaseStageCounts.Held` |
| Unidentified | `/Cases?tab=unidentified` | Open Unidentified items | `inputs.Unidentified.Count` from `EfUnidentifiedStore.ListQueueAsync` (state Open) |

`GetOperationsSnapshot.ExecuteAsync` builds `Metrics` from those two reads.
The stage counts group every Case workflow by state, whatever its Case type.
A zero renders as 0. When the attention read
fails, the whole block (metrics, list and Today pane) is replaced by one
"unavailable" notice.

### How Triage items enter Needs attention

`FetchAttentionInputsAsync` reads every Triage in `Open` and every Triage in
`AwaitingInformation`, page by page, through `IListTriage`; the source comment
says "The Triage kind is work without a finding". Each becomes a
`NeedsAttentionKind.Triage` item:

- **Reference**: the Triage reference, or the registration when none.
- **Title**: the registration.
- **Reason**: the state name, which `WorkCentre.TriageTitle` turns into
  "Awaiting information" or "Finding required".
- **Owner**: the assignee's name; with none, `OwnerLabel` reads "No
  Engineer".
- **Due**: `WorkTargets.DueAt(record.CreatedAtUtc, TriageTargetDays)`, the
  start of the Europe/London day that is `TriageTargetDays + 1` days after
  the day it opened. The default target is 1, and Administration's Workflow
  configuration sets it ("Triage target").
- **Received**: when it opened.
- **Route**: `/Triage/{id}`.

The item joins the one list, grouped Overdue, Due today and Later, sorted by
due instant. The kind chip reads "Triage" (slug `triage`) and its count is
over the scope before the kind filter. In Mine, an item counts as mine when I
am its assignee, and an unassigned item counts when I may take its kind
(`NeedsAttentionPolicy.IsMine`).

### A Triage row and the Today pane

The row's bold line is "Finding required" or "Awaiting information"; its
second line is "Triage · {reference} · {registration}"; its side shows the
due words and "{owner} · Received …".

Selecting it fills the Today pane with six facts: Reference, Registration,
State, Owner, Due, Received. The action is **Open Triage** to `/Triage/{id}`.
**Assign to me** shows when the item has no owner and the actor is Staff
(`CanTakeSelected`, `NeedsAttentionPolicy.CanTake`). It posts
`AssignTriageToMe` with the Triage id; the handler reads the Triage for its
version and runs `IAssignTriageToMe`, with no edit scope.

### Triage counts elsewhere

The snapshot's `TriageCount` is Open plus Awaiting information. The Cases
rail's Triage count, read by another query, covers every state (see
[Cases list](../cases/cases-index/how-it-works.md)).

## Things the FRD does not settle

- Why the strip carries these four and not With Engineer, Query or Triage.
  FRD-15 lists the four without a reason.
- Whether a metric counts every Case type together. FRD-15 says each count
  is "an exact link to its Cases tab"; nothing says whether Audit or
  Inspection + Audit work should read apart.
- Whether a Triage in Finding recorded, which still needs completing, should
  be Needs attention work. FRD-15 says "without a finding" and the source
  follows it.
