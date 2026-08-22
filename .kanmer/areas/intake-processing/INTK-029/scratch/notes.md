## 2026-08-22 — what is left, exactly

Deployed in release 17 (`71911734`), carried to release 20 (`05fe7a7f`).

Both halves shipped: the projection fix (`IntakeAssociations.AllocationMayStandIn`,
so a reversed association stops reporting the old case) and the cancel-on-unlink
half, with `SourceEmailUnlinked` added to both the lifecycle state and the
closure outcome, refused by the generic close, and written in the same
transaction as the reversal. The terminal taxonomy was consolidated to one owner
first, so the new state is terminal everywhere rather than in one of three
copies.

**Single gate:** verifying it means unlinking the spawning email of a real case,
which mutates an Outlook association and cancels a live case. That is an operator
action on operator data, not something to try on QDOS26009 or QDOS26010 — both
are real audits.

The cleanest verification is on the **next** case, after the operator has
finished with it: unlink its origin, confirm the dialog names the reference,
the case closes as `Cancelled — email unlinked`, the inbox stops showing the
link, and the search-and-link surface returns.

## 2026-08-22 — coverage audited; one piece remains

Attempted to close this the way [[CASE-013]], [[CASE-014]] and [[DOCS-007]] were
closed — end-to-end evidence rather than deployed code alone. Result:

**Already covered end to end** by `CaseAcceptanceReplayTests` against real SQL,
through the real ports:

- `UnlinkCancelsCase` is **true** before the unlink and **false** after;
- unlinking the accepted origin closes the case as `SourceEmailUnlinked`;
- `CurrentCaseId` is cleared while `AcceptedCaseId` is retained — both origins
  stay on the record, and `CaseIntakeLinks` keeps exactly one row;
- the unlink is idempotent under replay, and a replay with a changed reason
  raises `IntakeOperationConflictException`;
- a cancelled case refuses the relink until it is deliberately reopened.

`TerminalCaseStateTests` covers the taxonomy half: `SourceEmailUnlinked` is
terminal, and the generic close refuses it.

**Not covered:** the rendered consequence sentence. `Mail/Message.cshtml:415`
passes `UnlinkCancelsCase ? $"Unlinking this email cancels case {reference}." :
null` into `_ReasonDialog`'s `DialogConsequence` slot, and nothing asserts that
rendering.

I tried to cover it through `MailWorkspaceWebTests`. The harness seeds a
retained message and its receipt, and accepting that receipt for real does
create the case — but the Inbox detail page then renders no association block at
all (`LINKED=False`, no Unlink control, only the `CorrectClassification`
handler), so `PrepareUnlinkCase` never appears. The page's association
projection needs more fixture than the mail seeder establishes. Three attempts,
no progress, so it was abandoned rather than half-landed — the branch was
reverted to keep only the custody proof.

**Why this matters more than it looks:** [[CASE-017]] shipped a one-line wiring
defect of exactly this kind — a value written where nothing read it — that
source review and CI both missed. The same class of risk sits on this ternary.

**What closes it:** unlinking the spawning email of a real case, on the next case
after the operator has finished with it. Not QDOS26009 or QDOS26010 — both are
real audits. Alternatively, a web test once the mail association fixture is
understood well enough to render the unlink control; that is worth its own
ticket rather than more attempts here.
