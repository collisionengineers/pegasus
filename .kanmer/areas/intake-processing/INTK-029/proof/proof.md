# Proof

**Shipped:** PR #505, commits `1a86f5db` (projection) and the cancel half ·
**Deployed:** Release 17 (`71911734`), carried to Release 22 (`191ddf33`), the
serving revision. Dialog cover added in PR #516.

## Behaviour — end to end against real SQL

`CaseAcceptanceReplayTests` drives the real ports:

```
UnlinkCancelsCase                 true before the unlink, false after
CaseWorkflows.State               SourceEmailUnlinked
CurrentCaseId                     null
AcceptedCaseId                    retained — both origins stay on the record
CaseIntakeLinks                   exactly 1 row
replay of the same request        idempotent (version, links unchanged)
replay with a changed reason      IntakeOperationConflictException
relink of a cancelled case        refused until deliberately reopened
```

`TerminalCaseStateTests` covers the taxonomy: `SourceEmailUnlinked` is terminal,
and the generic `CloseCase` refuses it — that outcome is reached by the unlink
action, never chosen from a Close dialog.

## The rendering — the half an operator reads

`TheUnlinkDialogWarnsOnlyWhenUnlinkingCancelsTheCase` (PR #516) seeds a retained
message, accepts its receipt through the **real** acceptance command so the case
is genuinely that receipt's own, and drives the page over HTTP:

- the prepared unlink dialog contains
  `Unlinking this email cancels case {reference}.`, with the reference
  acceptance actually allocated;
- after the unlink the case reaches `SourceEmailUnlinked`;
- the dialog rendered afterwards contains no `cancels case` text — the receipt
  is no longer any case's source, so the warning correctly disappears.

Both branches of the ternary at `Mail/Message.cshtml:415`, asserted through the
page rather than the model.

```
dotnet build --configuration Release        0 errors
~MailWorkspaceWebTests (Release)            40 passed, 0 failed
CI on PR #516                               10 checks green
```

This cover exists because [[CASE-017]] shipped a defect of exactly that shape on
the same day — a value written where nothing read it, invisible to CI and to
source review. An earlier attempt at this test failed and was abandoned; the
cause turned out to be that the association block sits behind
`ActiveSection == "case"` (`Message.cshtml:343`) and the request never selected
that section. A URL parameter, not a missing fixture — found by reading the
Razor gate instead of guessing at the data.

## Root cause, for the record

The mail projection resolved the case as
`linkedCase?.CaseId ?? allocationState?.CaseId`. The automatic allocation
attempt still names the case it created, so once the association was reversed
the fallback put the link the operator had just removed straight back on screen.
The unlink always worked; it never looked like it had. Fixed by
`IntakeAssociations.AllocationMayStandIn`.

A second defect was diagnosed here and then **disproved** — `EfCaseAcceptanceStore`
writes an active manual association alongside the accepted `CaseIntakeLink`, so
the spawning receipt was always unlinkable. A pre-existing test caught that
error before it became a change.

## Deliberate capability removal

The origin can no longer be unlinked and freely relinked. An unlink cannot both
cancel the case and leave it relinkable. Recovery is a deliberate reopen with a
reason, and `SourceEmailUnlinked` is not on the reopen bar.

## Evidence tier

**End-to-end against real SQL** for the behaviour, and **through the rendered
page** for the operator-facing warning.

Not exercised on a live case, and deliberately not: verifying it in production
means cancelling a real audit. QDOS26009 and QDOS26010 are both real. The next
case, once the operator has finished with it, is where that would happen — but
nothing about the change is unproven without it.
