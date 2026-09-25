# Cases list — how it works today

Read from the live source on 23 September 2026 (origin/dev 446c3ce2f).

`/Cases` is one page of queues: a rail of tabs, the open tab's table and a
quick-detail pane. This covers the rail, its counts, the Triage tab and how
Case type shows. Paths are under `src/` unless stated.

## What this page does not show

- No Case type column or filter. An Audit Case carries an "Audit" chip
  beside its reference; an Inspection + Audit Case carries nothing.
- No Principal filter on the Triage tab. The Principal select is drawn only
  for Case queues (`IndexModel.ListsCases`).
- No state filter on the Triage tab. It lists Triage in every state,
  including Completed and Cancelled, newest first.
- No Case/PO on a Triage row. Its reference is the `T-` reference.
- No Completed or Awaiting instruction share in the shell rail's Cases count.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-15](../../../../../../docs/frd/frd-15-work-centre-queues-and-search.md) | One page; the rail groups Workflow (Not ready, Review, With Engineer, Completed, Query), Pre-Case work (Triage, Awaiting instruction) and Exceptions (Held, Unidentified), each with its own count; `?tab=`; Principal on every queue and Missing on Not ready; each queue's row shape (a Triage row: reference, registration, provider and assignee); quick detail. |
| [FRD-12](../../../../../../docs/frd/frd-12-operator-experience.md) | The shell rail's Cases count is Not ready + Review + With Engineer + Query + Held + Triage + Unidentified; an absent count renders nothing. `/Triage` and `/Unidentified` redirect here. |
| [FRD-13](../../../../../../docs/frd/frd-13-case-lifecycle-and-workflow.md) | The state labels, With Engineer being two internal states. |
| [FRD-03](../../../../../../docs/frd/frd-03-triage.md) | Triage states and the `T-` reference. |
| [Design authority](../../../../../../docs/design/README.md) | Triage and Unidentified are pre-Case records reached through the Cases rail, never Case states; the rail count sum and `RailCountsPageFilter` as its one supplier. |

## Source

| Layer | File | What it owns |
| --- | --- | --- |
| Web | `Pegasus.Web/Pages/Cases/Index.cshtml` | The rail form, the filter bar, the table, the empty states, the quick-detail pane. |
| Web | `Pegasus.Web/Pages/Cases/Index.cshtml.cs` | `Tabs` (keys, labels, groups, icons), `Queue`, `Count(tab)`, `Columns`, `LoadAsync`, `LoadTriageAsync`, `TriageRow`, `CaseRow`, `RecordDetail`, the search-parameter redirect. |
| Web | `Pegasus.Web/Presentation/RailCountsPageFilter.cs` | The shell rail's Cases count; `SetCaseCounts` lets this page hand over what it already read. |
| Core | `Pegasus.Core/Triage/TriageQueryUseCases.cs` | `ListTriage.ExecuteAsync` and `CountAsync` (a null state means every state). |
| Core | `Pegasus.Core/Operations/DashboardCounts.cs` | `CaseStageCounts`. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs` | Case counts by state and the Awaiting instruction count. |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfTriageStore.cs` | `CountAsync`, the list projection (`Provider` is the instruction draft's suggested principal code). |
| Infrastructure | `Pegasus.Infrastructure/Persistence/EfUnidentifiedStore.cs` | `CountOpenAsync`. |

## Behaviours

### The rail and its groups

The rail pane is headed "Workflow". Under it, one GET form to `/Cases` holds
a submit button per tab, grouped by `Tab.Group` with a divider and a group
label:

| Group | Tabs (key) |
| --- | --- |
| Workflow | Not ready (`not_ready`), Review (`review`), With Engineer (`with_engineer`), Completed (`complete`), Query (`query`) |
| Pre-Case work | Triage (`triage`), Awaiting instruction (`awaiting`) |
| Exceptions | Held (`held`), Unidentified (`unidentified`) |

State labels come from `OperatorLabels.CaseStage`. Triage uses the icon
`icon-file-text`. The default tab is Not ready. `?queue=` is accepted as an
alias of `?tab=`, and hyphens normalise to underscores. An unknown tab
answers 404. A request carrying a search-only parameter is redirected
permanently to `/Search`.

### The counts

`LoadAsync` reads three counts together, whatever tab is open:

| Tab | Count | Source |
| --- | --- | --- |
| Not ready, Review, Held | Case workflows in that state | `EfDashboardQueries.GetCaseStageCountsAsync` |
| With Engineer | `ReportPreparation` plus `PostReport` | same |
| Completed, Query | `PostReportComplete`; `Query` | same |
| Awaiting instruction | Vehicle-images records awaiting an instruction with no current Case association | same |
| Triage | Every Triage, in every state | `IListTriage.CountAsync(actor, state: null)` → `EfTriageStore.CountAsync` |
| Unidentified | Open Unidentified items | `EfUnidentifiedStore.CountOpenAsync` |

The page passes these to `RailCountsPageFilter.SetCaseCounts`, so the shell
does not read them again. On other pages the filter runs the same three
queries. The shell rail's Cases figure is Not ready + Review + With Engineer
+ Query + Held + Triage + Unidentified (`CaseRailCounts.Total`). The
filter's remarks call the Triage figure "the open-Triage total"; the call
passes `state: null`, which counts every state.

When the reads fail, the page shows "Cases are unavailable. Refresh to run
the live queues again." instead of the rail and table.

### The Triage tab

`LoadTriageAsync` reads one page of 25 through `IListTriage` with no state
filter, resolves assignee names, and builds each row with `TriageRow`:

| Column | Value |
| --- | --- |
| Reference | The Triage reference, or the registration when none; links to `/Triage/{id}` |
| Registration | The normalised registration |
| Principal | `TriageSummary.Provider`: the instruction draft's suggested principal code |
| Received | When the Triage opened (office date) |
| Assignee | The assignee's name |
| State | The Triage state chip (`OperatorLabels.TriageState`) |

Rows sort newest first. The empty state reads "No open Triage". Selecting a
row fills the quick detail: eyebrow "Triage", the reference and registration
as heading, facts Reference, Registration, Principal ("Not known" when
none), Assigned to ("Unassigned"), Opened, a State chip, and **Open Triage**.

The list's Principal column reads the draft's suggested code; the Triage
record's Principal reads the recorded `PrincipalId`. After **Set principal**
on the record, the two can differ.

### Case type in the Case queues

Case rows (every Workflow and Exceptions tab except Unidentified) have the
columns Case/PO, Registration, Claimant, Principal, Received, Due, then State
(or Missing on Not ready, Engineer on With Engineer), then Editing. There is
no Case type column. `CaseRow` sets an "Audit" chip beside the reference when
the Case type is Audit, and nothing otherwise. The quick detail for a Case
lists a Type fact from `OperatorLabels.CaseTypeName` ("Inspection", "Audit",
"Inspection and audit").

## Things the FRD does not settle

- Whether the Triage tab and its count should include Completed and
  Cancelled Triage. FRD-15 and FRD-12 name a Triage count without a state;
  the empty state says "No open Triage" while the list and count cover every
  state.
- The Triage row's columns. FRD-15 names reference, registration, provider
  and assignee; the source adds Received and State and labels provider
  "Principal".
- Which Principal a Triage row shows: the draft's suggestion or the recorded
  Principal.
- How Case type should read in the queues. Nothing in FRD-15 asks for it;
  the source marks Audit Cases only.
