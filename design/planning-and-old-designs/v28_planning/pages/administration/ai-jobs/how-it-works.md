Read from the live source on 18 September 2026.

## What this page does not show

- No filters — this table has none in the live page, only paging.
- Stop is replaced with an em dash for any job in a terminal state
  (`AiJobStates.IsTerminal`), never shown disabled.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/AiJobs.cshtml` | Table, meta line, Stop form, pagination |
| `Pages/Administration/AiJobs.cshtml.cs` | `Result` (counts, jobs, paging), `Stop` handler |
| `Presentation/OperatorLabels.cs` (`AiJobs` class) | `Kind()`, `State()`, `StateToneOverride()`, `Count()` |

## Behaviours

### Columns and meta

Job (kind, plus the job's own instruction text in a small line when
present), Subject (the case/record reference, or "Unidentified queue" for a
`Queue`-subject job — its Core subject reference is an internal token no
operator reads), Created, State (chip), Action. The panel meta line reads
"N active · N failed · Active/Stopped/Unavailable" — the third word is the
Automation kill switch's own state (`SendToAiSwitchEnabled`), or
"Unavailable" when the transport itself is not composed.

### State chips

`AiJobs.State()` covers Queued, Taken, Draft ready, Completed, Failed,
Cancelled, Expired. `StateToneOverride` forces amber for Queued/Draft ready
and navy for Taken; every other state falls through to `_StatusChip`'s own
tone map (Completed → green, Failed → red, Cancelled/Expired → neutral).

### Stop

A non-terminal job's row carries a Stop form (danger button); a terminal
job's Action cell is a plain em dash, never a disabled button — consistent
with `docs/design/README.md`'s absent-not-disabled rule, since a completed
job's Stop action does not exist rather than existing-but-refused.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
