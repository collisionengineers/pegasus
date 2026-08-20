# Plan — CASE-005

The deadlock's lock paths are read directly from the code's own transaction semantics rather than a captured runtime graph (disposition below): `BeginAsync` opens a **Serializable** transaction, takes key-range shared locks on `IntakeAllocationAttempts` twice (by unique `OperationKey`, then latest-by-`IntakeReceiptId`), and then inserts. Two parallel calls for the same receipt each hold range-S locks the other's insert must upgrade past — the textbook serializable check-then-insert deadlock, matching the observed victim stack (`BeginAsync` → `SaveChangesAsync`).

1. Serialize the contended unit, don't retry around it (the ticket's own warning): immediately after `BeginTransactionAsync`, take `sp_getapplock @Resource = 'intake-allocation:{receiptId:N}', @LockMode = 'Exclusive', @LockOwner = 'Transaction'`. Same-receipt Begins now queue; the second sees the first's committed row and resolves through the EXISTING replay/concurrency branches (convergence is the designed behaviour, reached deterministically). Different receipts are untouched.
2. Reuse: the codebase's own app-lock precedent (`__EFMigrationsLock` uses sp_getapplock via EF; grep confirms the pattern is known to the platform). No isolation-level change, no EnableRetryOnFailure masking.
3. Test: loop the existing `DistinctParallelRetriesResolveToOneCaseAggregate` body under contention (20 rounds) — asserts convergence, not merely no-throw, per the ticket's verification bar.

Disposition on the "capture the deadlock graph" step: not captured at runtime — the CI environment is ephemeral and LocalDB XEvents access is unavailable in the harness; the code-derived lock analysis above names the two conflicting paths explicitly and the 20-round contention test is the empirical check. Recorded honestly rather than claimed.

Simplification pass: recorded after implementation.
Deviation: subagents barred this round — self-review in scratch.
