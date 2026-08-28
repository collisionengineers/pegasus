# Plan — MAIL-022

## Situation

The required outcome was already delivered. The stale-threshold row in
`docs/open-decisions.md` was corrected in commit `cb2ab070`, which shipped
inside the release-35 documentation PR [#578](https://github.com/collisionengineers/pegasus/pull/578)
([[DELIV-029]]) — that PR was already editing `docs/open-decisions.md` for the
App Insights cap decision, and the one-row correction rode with it rather than
opening a second PR against the same table.

Docs-only, one table cell. No behaviour change, no code change.

## The change (already merged)

Before:

```
| Ship the provisional 15 minutes (fifteen missed one-minute ticks), recorded in `GetRetainedMailFreshness.StaleAfter`. |
```

After:

```
| Ship the provisional 15 minutes (three missed `ApprovedInboxPollSchedule` recovery ticks at `0 */5 * * * *`), recorded in `GetRetainedMailFreshness.StaleAfter`. |
```

This matches the model [[MAIL-021]] wrote into the `StaleAfter` remark in
`src/Pegasus.Core/Intake/RetainedMail.cs` (Graph change notifications primary;
`InboxRecoveryFunction` on `ApprovedInboxPollSchedule` = `0 */5 * * * *`), which
cites this open decision. No other row and no other file changed.

## Steps

1. Confirm the corrected row is on merged `main` at `68adedaf`. — done
2. Record proof and close. No branch, no worktree, no PR of this ticket's own.

## Simplification pass

n/a — docs-only.
