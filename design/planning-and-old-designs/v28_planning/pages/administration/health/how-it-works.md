Read from the live source on 18 September 2026.

## What this page does not show

- No manual "recheck" or refresh action — this page only reads the last
  recorded evidence; the whole panel is replaced by a single warning notice
  ("Service health is unavailable. Refresh to try again.") when
  `Model.Snapshot` is null, rather than an empty table.
- No incident history or trend — each row is the single latest recorded
  state per service, not a log.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Health.cshtml` | The one table, and the unavailable notice |
| `Pages/Administration/Health.cshtml.cs` | `Snapshot` |
| `Presentation/OperatorLabels.cs` | `ServiceHealthServiceName()`, `ServiceHealthAreaName()`, `ServiceHealthStateName()`, `ServiceHealthDependencyName()` |

## Behaviours

### Columns

Service (the service's own name, with its area as a small line underneath),
State (chip), Last recorded, Dependency. Renamed internal names: the Core
"Intake dispatch" service reads "Receiving dispatch" and "Automation
ingress" reads "Automation clients" — both banned-word renames, done "here
and only here" per the source comment, since every other service name is
already the operator's own word.

### States

`ServiceHealthStateName` collapses several Core states into three operator
words: "Working" (Current or Running), "Needs attention" (Partial, Failed or
Review required), "No recorded activity" (Configured but nothing has run
yet); anything else reads "Unknown".

### Areas and dependencies

Six areas (Mail, Receiving, Box, EVA, AI, Automation) and the external thing
each service's evidence depends on (Microsoft Graph, Worker, Box, EVA API,
AI, Automation client).

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
