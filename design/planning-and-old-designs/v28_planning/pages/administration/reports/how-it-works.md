Read from the live source on 18 September 2026.

## What this page does not show

- MI02 and MI03 show only rows with actual activity in the filtered period
  (`GeneratedArtifacts > 0 || Sent > 0`, and the equivalent for turnaround);
  a Principal with nothing recorded simply is not a row.
- All three panels read "Unavailable" as their entire table body, not a
  zero, when the underlying query itself did not run
  (`Model.PrincipalActivity is null`) — per `docs/design/README.md`, a query
  failure must never look like a genuine zero.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Reports.cshtml` | All three panels (MI01, MI02, MI03), their filters and CSV links |
| `Pages/Administration/Reports.cshtml.cs` | `EngineerResult`, `PrincipalActivity`, `Csv`/`PrincipalCsv`/`TurnaroundCsv` handlers |
| `Presentation/OperatorLabels.cs` | `ReportKind()`, `ReportTurnaround()` |

## Behaviours

### MI01 — Engineer activity

Queries received are credited to the Case's assigned Engineer; reports sent
are credited to the staff member recorded as the sender — an explicit note
above the filter form, since the two counts on the same row can come from
different people's work. Filters: From/To (London time), Person. Table:
Person, Queries received, Reports sent, with a two-metric totals strip
above.

### MI02 — Reports by Principal

Reports produced, Reports sent, and a Report types cell listing each
generated kind and its count (`ReportKind()`: "Report" for
`AssessmentReport`, "Fee note" for `FeeNote`).

### MI03 — Turnaround

Currently held, Oldest held since, and three average durations (Time to
produce / to ready / to send) via `ReportTurnaround()`, which reads in whole
days once a duration reaches 24 hours and otherwise as a shorter duration
string.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
