# PLAT-049 post-implementation report

Branch `task/plat-049-operations-features`, based on `origin/dev` = `b92cb9a7`
(merged in first, clean, no conflicts).

## What shipped

**AI Job List panel** on `/operations`, first in the stack per EPIC-011 §1.11.
Meta "n jobs"; table `Job | Record | Started by | Created | State | Action`.
Membership is FRD-11's: every non-terminal job, plus the jobs that reached a
terminal state today, newest first. Non-terminal membership comes from the
unbounded `IAiJobQueries.ListOpenAsync`, so no live job can fall outside the
200-row recent window that bounds only the terminal tail.

Every drawn action reaches a real destination:

| Row | Action rendered | Goes to |
| --- | --- | --- |
| `Draft ready` Estimate | Review estimate, Cancel | `/Cases/{id}/Assessment`, `ICancelAiJob` |
| `Draft ready` Unidentified resolution | Review, Cancel | `/Unidentified/{id}`, `ICancelAiJob` |
| `Draft ready` Query response | Open query, Complete job, Cancel | `/Cases/{id}`, `IConfirmAiJob`, `ICancelAiJob` |
| `Draft ready` Unidentified-queue pass | Complete job, Cancel | `IConfirmAiJob`, `ICancelAiJob` |
| Any other non-terminal | Cancel | `ICancelAiJob` |
| Terminal (today's) | `—` | nothing |

`ICancelAiJob` and `IConfirmAiJob` were registered but had **no production
caller** before this change; they have one now.

**Send Unidentified to AI** — a dark control in the panel head that creates an
`UnidentifiedResolution` job for a chosen open U reference through the
existing `ICreateAiJob` seam (which itself consults `ISendToAiControl`). No
second dispatch path. Drawn only when an open Unidentified item exists.

**Service health** — the table was already real against `ServiceHealth.cs` from
PLAT-023; its Retry command is unchanged, and the action cell now renders `—`
where Core names no retry target instead of an empty cell.

**Labels** — one new nested static class `OperatorLabels.AiJobs`. Nothing
existing in that file was reordered or edited; the diff is 72 insertions and 0
deletions.

## Read this: I did not follow the lane brief on one point

The lane brief says the Send button "creates an Unidentified-queue pass job".
**It creates an `UnidentifiedResolution` job instead**, because:

- **FRD-11 § AI Job List** — a governing `refs` document of this ticket —
  says verbatim: "`Send Unidentified to AI` creates an
  Unidentified-resolution job for a chosen U reference", and its kinds table
  gives that kind the "Started from" value `Operations "Send Unidentified to
  AI" for one U reference`.
- **The PLAT-049 ticket body** says the same: "creating an
  UnidentifiedResolution job".
- **EPIC-011 D5** says scheduled queue passes "are created by external crons
  through the Automation Actor" — a Pegasus button creating one contradicts it.
- **`AiJobPolicy.RequireCreator`** encodes the same split in Core: the
  Automation Actor may create *only* `UnidentifiedQueuePass`.

If the orchestrator wants the queue pass instead, it is a one-line change to
the handler plus its test — but it needs an operator decision against FRD-11,
not a quiet edit.

## Verification — real numbers, run in this worktree

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | **Build succeeded. 0 Warning(s), 0 Error(s).** |
| `dotnet test ./tests/Pegasus.IntegrationTests --no-build --filter "FullyQualifiedName~OperationsWebTests"` | **Failed: 0, Passed: 19, Skipped: 0, Total: 19** (9 pre-existing + 10 new) |
| `dotnet test ./tests/Pegasus.ArchitectureTests --no-build` | **Failed: 0, Passed: 100, Skipped: 0, Total: 100** |

A baseline `dotnet build` was run before any edit and was also green, so the
0/0 above is this branch's own result and not an inherited one.

**Not run, and therefore not claimed:** the full suite, the `Browser` category
(`Browser/AccessibilityTests` and `Browser/OperatorJourneyTests` both visit
`/Operations` and will re-render it), and the snapshot capture script. The
lane brief forbids all three. `docs/design/test-ui/catalogue.json` carries two
`/Operations` states (`operations--default`, `operations--empty`) whose stored
markup is now stale; regeneration is the merging branch's job once per merge,
per the 2026-08-29 orchestration decisions.

## Assertions

No existing assertion was weakened, skipped, deleted or inverted. The two
existing negative assertions about this panel —
`DoesNotContain("AI operations")` and the "Requesting an AI job … are planned"
placeholder sentence — both remain true, because the panel is titled "AI Job
List" and carries no placeholder copy. They were left exactly as written.

## Defects outside this lane, all reported, none absorbed

Recorded with dispositions under a dated heading in the ticket's `plan`.

1. **EVA handoffs panel is not shipped.** §1.11 and the ticket body ask for it
   (Case, Route, Engineer, State, Result). No Core port lists EVA handoffs:
   `IEvaSubmissionQueries` offers only `GetLatestAsync(caseId)`,
   `GetRecentFailuresAsync` and `GetActivityAsync`, and
   `EvaSubmissionFailure` carries `CaseId` with no case reference, engineer or
   route. Building it needs a new `Core/Eva` read model and port plus an EF
   adapter and registration — outside this lane's boundary and large enough to
   need its own plan. **Nothing was rendered in its place**: an uncomposed
   capability is absent, never an empty panel.
2. **Service health `View` is not rendered.** §1.11 says "Retry/View";
   `ServiceHealthRow` carries `RetryTarget` and no view target. A guessed
   area→route map would be a second list of routes owned by nobody, and its
   plausible destinations (`/Administration/Mailboxes`,
   `/Administration/Automation`) are Administrator-only while `/Operations` is
   open to Engineer and User, so the link would 403 for most readers. `—` is
   rendered instead.
3. **Attention required has no `Item` column** (§1.11 lists Case, Work, Item,
   Attempts, Failure, Retry). `RequestOperationProjection` has no field naming
   the thing the work is about. PLAT-023's panel, now this lane's file, but the
   fix is in `Core/Operations` and the EF projection.
4. **Active upload links has no `Recipient` column** (§1.11 lists it). Same
   projection, same reason.
5. **`Open query` opens the Case, not the message.** A `QueryResponse` job's
   `SubjectKind` is `Case` and it carries no message identity, so FRD-11's
   "opens the message" is not reachable. Accepted risk: the link opens the
   record the job actually names rather than rendering an unresolvable control.

## Simplification pass

Ran over this branch's own diff before the PR; findings and dispositions are
recorded under "Simplification pass — 2026-08-29" in the `plan` document. Five
findings, four fixed, one rejected with a reason.
