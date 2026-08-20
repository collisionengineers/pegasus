# Files — CASE-005

| File | Change |
| --- | --- |
| src/Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs | `BeginAsync` serializes same-receipt attempts with `sp_getapplock` (transaction-scoped, exclusive, resource `intake-allocation:{receiptId}`) before its reads, so the check-then-insert race converges instead of deadlocking |
| tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs | Repeated-contention assertion (the parallel-retry test looped; convergence every round) |
