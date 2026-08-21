# Why unlink did very little

## Correction

An earlier version of this document claimed a second defect: that the spawning
email could not be unlinked at all, because it reaches its case through an accepted
`CaseIntakeLink` and `AutoLinkAsync` refuses to give an accepted receipt a manual
association, so `ReverseLinkAsync` would find nothing to reverse and throw.

**That was wrong, and it was checked rather than argued.** A pre-existing integration
test, `AcceptedOriginCanBeUnlinkedAndRelinkedWithoutDeletingLineage`, asserted the
opposite. Running it showed why: `EfCaseAcceptanceStore.cs:332` writes an **active
manual association alongside** the `CaseIntakeLink` when a case is accepted. The
spawning receipt therefore has both rows, and the existing manual-association branch
already handles its unlink.

The reasoning that produced the wrong conclusion was sound about `AutoLinkAsync` and
about `CurrentCaseId`; it simply never checked what acceptance itself writes. The test
was the check that caught it, before the dead branch it had motivated could ship.

## The one real defect

The mail projection resolved the case as `linkedCase?.CaseId ?? allocationState?.CaseId`.
The automatic allocation attempt still names the case it created, so once the
association was reversed the fallback put the very link the operator had just removed
straight back on the screen. Unlink worked; it just never looked like it had.

Fixed in `1a86f5db`: `IntakeAssociations.AllocationMayStandIn(receiptId)` refuses the
allocation fallback for a receipt whose association has been reversed
(`CurrentIntakeAssociations.cs`).

## What the operator asked for on top

Unlinking the email that created a case should cancel that case, with a warning first.
That is new behaviour, not a defect fix, and it is where the terminal outcome goes.

The rule is decided once, on the receipt:
`UnlinkCancelsCase => AcceptedCaseId is not null && AcceptedCaseId == CurrentCaseId`.
It is true only while the receipt's current link is the case its own acceptance
created. A receipt since relinked elsewhere is not that case's source, so unlinking it
leaves that case alone.

**The accepted origin row is never deleted.** Release validation requires intake to
preserve "reversible source associations and both origins"; what clears the link is
the reversed manual association, through the precedence rule that already exists in
`IntakeReceipt.CurrentCaseId`.

### A capability this removes, stated plainly

`AcceptedOriginCanBeUnlinkedAndRelinkedWithoutDeletingLineage` proved you could unlink
the origin and relink it freely. Once unlinking cancels the case, that is no longer
possible: the case is terminal and the relink is refused. Recovery is a deliberate
reopen with a reason, then a relink.

That is the unavoidable consequence of the operator's decision — an unlink cannot both
cancel the case and leave it available for relinking — and the stronger guard is
arguably the point: an accidental unlink now costs a reopen instead of passing
unnoticed. `SourceEmailUnlinked` is deliberately **not** added to the reopen bar, so
that recovery path exists. The approved dialog sentence says the case is cancelled and
does not claim it cannot be reopened, which is consistent.

## The dead end resolves itself

`Mail/Message.cshtml:444-459` already renders a case search-and-link form whenever no
case is associated. The operator saw a dead end only because the link never visibly
cleared. No new "next action" UI is needed.

## Terminal states were written in three places

Adding a terminal state was only safe once this was fixed, because a state missing
from one copy is silently non-terminal for whatever that copy guards:

- `CaseLifecycleRules.IsTerminal` — `Lifecycle/CaseLifecycle.cs` (the owner)
- `EvaHandoffStore.IsTerminalWorkflow`
- `EfVehicleWorkflowStore.EnqueueDueAsync` (a `string[]` for a LINQ query)

Both copies now read Core, and `TerminalStateNames()` is *derived from* `IsTerminal`
rather than restating it, so drift is impossible by construction.

## Checked, not assumed

- `State` and `ClosureOutcome` are `string?` columns, `HasMaxLength(40)`
  (`CaseWorkflowModelConfiguration.cs:25,27`) — **no EF migration is needed**. The
  earlier plan's "lifecycle state plus a migration" was wrong on this too.
- QDOS26008 itself could not be re-queried: the Azure test data was cleared earlier in
  this session, so the diagnosis is from the code path and from running the tests.

## Operator decisions taken

- New terminal outcome, not `CreatedInError` (which requires the atomic
  corrected-principal replacement and is refused by the generic close).
- State label `Cancelled — email unlinked`; dialog sentence
  `Unlinking this email cancels case <reference>.` — a fourth entry on the closed
  necessary-copy list, recorded in `docs/design/README.md` in both places it appears.
