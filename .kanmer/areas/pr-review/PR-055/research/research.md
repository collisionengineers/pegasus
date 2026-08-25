# Research — PR-055: atomic EVA export replay

## Question

Why can two simultaneous Export requests with the same operation key create duplicate action history, and what is the smallest pre-release fix that follows Pegasus's existing concurrency convention?

## Findings

- PR #539 at `cf28b8b0` builds the bundle and then calls `EvaHandoffStore.RecordExportAsync`. That method queries `ActionHistory` before it owns any transaction or aggregate lock, then inserts the history row (`src/Pegasus.Infrastructure/Persistence/EvaHandoffStore.cs`).
- The existing `ActionHistory` index on `AggregateType, CorrelationId` is not unique (`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`). Therefore the current catch-and-reread only resolves the once-per-case proxy primary-key race; when that proxy already exists, two same-key requests can both insert history.
- The repository already serializes same-aggregate idempotent actions by opening a serializable transaction and selecting the existing aggregate row with SQL Server `UPDLOCK, HOLDLOCK` before reading replay history (`src/Pegasus.Infrastructure/Persistence/EfApprovedOutlookCategoryStore.cs` and `EfCaseWorkflowStore.AcquireWorkflowMutationLockAsync`).
- Export requires an existing case in `Review`, so its existing case/workflow row is a stable lock target. The lock only needs to cover the short replay/proxy/history recording step; archive mapping and image loading can remain outside the transaction.
- No new table, general idempotency service, unique-index migration, retry framework, or compatibility path is needed. This is an unreleased product and the current aggregate-lock convention directly addresses the supported race.
- The current integration test proves sequential replay and different-key exports only (`tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs`). It does not overlap two database contexts, so it cannot prove the concurrency requirement.
- FRD-07 already requires replay-safe action history for every successful export. The fix implements that existing requirement and does not change export eligibility, bundle contents, the first-send proxy meaning, or the three planned routes (`docs/frd/frd-07-eva-and-external-engineering-handoff.md`).

## Implications

Use a serializable transaction in `RecordExportAsync`, acquire an update/hold lock for the case before the first history lookup, and commit after the proxy/history save or verified replay. Keep the lock helper local because the existing workflow helper is private and widening/refactoring unrelated workflow code would add scope for one small fix.

Add one SQL integration regression that starts two identical `ExecuteAsync` calls together using the same case, actor and operation key. Both must return the same successful bundle and the database must contain exactly one matching `eva_bundle_exported` row. Then reuse that key with different audited material (for example, a different actor) and assert the existing exact-replay conflict. Existing sequential different-key coverage stays.

## Open questions

None. The required behavior and the repository's existing locking convention determine the minimal implementation.
