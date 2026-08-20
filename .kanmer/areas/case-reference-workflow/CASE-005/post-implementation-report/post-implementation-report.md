# Post-implementation report — CASE-005

Branch task/case-005-allocation-deadlock. Two changes, both at the point the ticket's analysis named:

1. **Structural**: `EfIntakeAllocationStore.BeginAsync` takes an exclusive transaction-scoped `sp_getapplock` on `intake-allocation:{receiptId}` immediately after opening its Serializable transaction. The deadlock was the textbook check-then-insert: both transactions took key-range S locks on the two attempt reads (unique OperationKey, latest-by-receipt) and then each needed to insert past the other's ranges. With the lock, same-receipt Begins queue and the loser resolves through the existing replay/suppression branches. Different receipts are unaffected; the lock times out at 15 s and fails loudly (`THROW 51205`).
2. **Convergence window**: `AllocateIntake.AwaitRecordedOutcomeAsync` waited 40×25 ms = 1 s for the owning attempt to publish its outcome — under load the concurrent caller got `Pending` back (the ticket's PR-#423 sighting reproduced exactly during this fix). Now 100×100 ms = 10 s, still bounded, still honestly `Pending` afterwards.

**Verification against the ticket's bar**:
- Deadlock graph: not captured at runtime (ephemeral CI; LocalDB XEvents unavailable in the harness) — dispositioned in the plan: the two conflicting lock paths are named from the code's own transaction semantics and match the observed victim stack (`BeginAsync` → `SaveChangesAsync`).
- Repeated convergence: the parallel-retry test now loops **5 contention rounds** asserting both callers reach `Succeeded` on one case aggregate with exact table counts; run **4× locally = 20 rounds, all green** (before the window fix, the suppressed-Pending divergence reproduced on round 1 — evidence the loop bites).
- Full `QdosAllocationRecoveryTests` suite 15/15; Release build 0/0.

Simplification pass: two surgical edits; no retry-on-deadlock layer added (the ticket's own warning) — the applock removes the deadlock rather than masking it; no new list/abstraction. Deviation: subagents barred — self-reviewed.

## Verification hand-off
CI: the shard containing QdosAllocationRecoveryTests stops deadlocking across the remaining PRs of this round.
