# Proof

**Shipped:** PR #477, merge `e4d56d9e` · **Deployed:** ancestor of `4111ad29` → true (Release 16, active revision `…--4111ad291779`).

## The finding

> A duplicate POST arriving while the original provider call is still in flight can probe
> the unchanged source, mark the shared operation `failed`, and release the filtered
> active-claim index. A new-key retry may then be admitted while the original call later
> succeeds, allowing a duplicate external move despite [[PR-038]].

The subtlety: a `pending` operation whose source folder still looks unchanged is not
evidence of failure — it is evidence of a call that has not finished.

## Verified in the shipped code

**`pending` is treated as still active, not as inferable failure.** The filtered unique
index covers both non-terminal outcomes together —
`MailboxModelConfiguration.cs:118-120`, `HasFilter("[Outcome] IN ('pending', 'uncertain')")`
— so an in-flight `pending` operation holds the active slot exactly as an `uncertain` one
does. A different-key retry cannot be admitted while it stands, which is the guarantee
PR-038 established and this ticket stopped from being released early.

**The only transition out of `pending` on an interrupted request is to `uncertain`, never
to `failed`.** `EfRetainedMailFolderMoveStore.cs:172-174` and `:193-195` both route
`OperationCanceledException` into `MarkUncertainAfterCancellationAsync` (`:208`). A
concurrent probe therefore cannot demote a live operation to a terminal state and free the
slot; the worst it can do is leave it uncertain, which still holds the index.

Recovery from `uncertain` probes rather than moves ([[PR-039]]), so even the state a
concurrent request *can* reach cannot issue a second provider call.

## Not claimed

This is proved by the index definition, the exception paths and the shipped tests. Two
genuinely overlapping same-key POSTs have **not** been raced against live Graph in
production, and this proof does not claim they have.
