# Plan — PR-045

## Approach

Add two focused SQL integration tests beside the existing queued allocation-recovery tests. Reuse the real `ProcessQueuedIntake`, `AssociateRetainedMailWithCase`, EF evidence query/write, disposable LocalDB, and retained-message helper. Test-only wrappers record provider→MAIL-09→allocation order, seed retained evidence immediately after live completion, and observe the refreshed current Case at downstream allocation.

## Governing docs

This proves the already-implemented FRD-08 behavior; it does not change behavior or the governing document.

## Steps

1. Add a delegating work-store test fake that seeds the retained row after live completion.
2. Add recording provider/evidence/allocation wrappers and a no-op first-pass allocator for replay setup.
3. Prove live and completed-replay paths, ordering, successful refresh, existing-case reuse, and no external writes.
4. Run focused and proportional verification; update blocker/TICK PIR and traceability.

No new production abstraction or policy.
