# Post-implementation report

**Branch:** `task/qdos26008-regressions` · **PR:** #505 · **Commits:** `1a86f5db`, `db1055a3`, `1183f9fd`

## What was built

As planned, with one correction and one addition.

| Planned | Built |
| --- | --- |
| Consolidate the terminal taxonomy first | ✅ `TerminalStateNames()` derived from `IsTerminal`; both Infrastructure copies now read Core |
| Add `SourceEmailUnlinked` to both enums | ✅ |
| Refuse it from the generic close | ✅ in Core `ValidateClose` **and** `EfCaseWorkflowStore.CloseAsync`, matching the existing `CreatedInError` pair |
| Cancel in the unlink's own transaction | ✅ `CancelOnSourceUnlinkAsync` |
| Decide the rule once, on the receipt | ✅ `IntakeReceipt.UnlinkCancelsCase` |
| `_ReasonDialog` consequence slot | ✅ optional `DialogConsequence`, reusing the existing `.notice` class — no new CSS |
| Label and approved copy | ✅ `OperatorLabels`, and `docs/design/README.md` in both places the closed list appears |
| Change `Message.cshtml.cs` | ❌ **not needed** — the view already had the receipt |

## The correction

Research claimed a second defect: that the spawning email could not be unlinked at all.
The pre-existing test `AcceptedOriginCanBeUnlinkedAndRelinkedWithoutDeletingLineage`
contradicted it, and running it showed why — `EfCaseAcceptanceStore.cs:332` writes an
active manual association alongside the accepted link, so the spawning receipt always
had both.

The first implementation put the cancel in a branch that could therefore never run, and
the old test passed for the wrong reason. Moving the cancel into the branch that does run
made that test fail on a terminal-case relink, which is what proved the code was finally
wired. The dead helper was deleted; research records the correction.

## The addition

The simplification pass found "stop the chase schedule" written identically in four
stores — this change had just made it the fourth. Extracted to `CaseChaseState.Stop`
(`1183f9fd`) rather than deferred, because every terminal route has to stop the chase and
a stale copy is a live defect, not a tidiness issue.

## Behaviour deliberately removed

The accepted origin can no longer be unlinked and freely relinked. An unlink cannot both
cancel the case and leave it relinkable. `AcceptedOriginCanBeUnlinkedAndRelinked…` is
replaced by `UnlinkingTheAcceptedOriginCancelsTheCaseAndKeepsItsLineage`. Recovery is a
deliberate reopen with a reason — `SourceEmailUnlinked` is not on the reopen bar, and the
approved dialog sentence does not claim it cannot be reopened.

Called out in the PR body for the reviewer.

## Evidence

- `dotnet build Pegasus.slnx --configuration Release` — 0 warnings, 0 errors
- `Pegasus.Core.Tests` — 908 passed (5 new in `TerminalCaseStateTests`, 4 in `UnlinkCancelsCaseTests`)
- `Pegasus.ArchitectureTests` — 99 passed
- `UnlinkingTheAcceptedOriginCancelsTheCaseAndKeepsItsLineage` — passed
- Full integration suite: recorded before merge

## Not done

No EF migration — string columns. No new "next action" UI — the search-and-link form
already renders once no case is associated.
