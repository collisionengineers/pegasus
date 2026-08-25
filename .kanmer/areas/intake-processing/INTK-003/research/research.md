# Research — INTK-003: lost queue-message recovery

## Question

How can Pegasus recover an intake work item whose queue publication was recorded but whose queue message never reaches processing, without duplicating evaluation, Case/reference allocation, or downstream effects?

## Findings

- `src/Pegasus.Core/Intake/DurableIntake.cs` defines one durable sequence: `ReceiveIntake` persists pending work; `DispatchPendingIntakeWork` claims it with a one-minute lease, enqueues only the staged-receipt GUID, then calls `MarkDispatchedAsync`. Enqueue failure releases the claim to `pending` with a 30-second due time.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` confirms the state hole. `MarkDispatchedAsync` writes `dispatched`, records the publication time in `DueAtUtc`, and clears `LeaseToken`/`LeaseExpiresAtUtc`. `ClaimDispatchAsync` admits only due `pending|retry_scheduled`; `RecoverExpiredLeasesAsync` admits only leased `dispatching|processing`. No current query can move an unleased `dispatched` row back to dispatchable work.
- The same store already has the concurrency guard needed for recovery. Lease recovery reads candidates then conditionally updates the exact id/state/attempt/lease facts it observed. The stale-dispatched transition should retain that compare-before-update shape so a concurrent processing claim wins rather than being overwritten.
- `ClaimProcessingAsync` accepts `dispatching|dispatched|processing`, which deliberately supports delivery before the publisher finishes `MarkDispatchedAsync`. `ProcessQueuedIntake` returns a no-op for a competing delivery and uses the completed evaluation for safe replay after completion. Existing recovery tests prove exactly-once evaluation under duplicate delivery. Re-enqueueing a stale item may create duplicate queue deliveries but must not create a duplicate evaluation, Case, reference, or side effect.
- `src/Pegasus.Worker/host.json` sets a five-minute queue visibility timeout, five dequeue attempts, identifier-only unencoded messages, and a two-second maximum idle poll. Visibility begins after dequeue; it is not evidence that a successfully published but never delivered message is lost after five minutes.
- `src/Pegasus.Worker/AzureQueueIntakeWorkQueue.cs` calls the default `QueueClient.SendMessageAsync` overload and sets no explicit expiry. The repository therefore does not own a message-TTL value that can safely be copied into business policy.
- `src/Pegasus.Core/Intake/DurableIntake.cs` runs lease recovery inside `ReconcileStagedArtifacts`; `src/Pegasus.Worker/IntakeFunctions.cs` invokes it from the existing staged-artifact reconciliation timer. No new function, queue, worker, or scheduling abstraction is required for this fix.
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` already defines an `(State, DueAtUtc)` index on `IntakeWorkItems`. A stale-`dispatched` candidate query can use the existing state/publication-time facts without a schema migration or another timestamp.
- `tests/Pegasus.IntegrationTests/RecoveryTests.cs` already owns durable queue/recovery proofs, including expired dispatch lease recovery, enqueue-before-mark delivery, duplicate processing, poison handling, and bounded processing retry. It is the proportional home for stale/fresh dispatched recovery and an end-to-end redispatch/process-once assertion.
- FRD-02 currently requires the Worker to dispatch pending work, claim deliveries idempotently, recover expired leases, and record completion/failure. It does not yet state recovery of a recorded publication whose delivery disappears. Blocking [[INTK-041]] owns the new recovery timing/behavior contract and any FRD-02 change; this fix should implement that settled contract rather than define a competing threshold.
- [[SIMPLI-009]] and [[SIMPLI-010]] recorded a read-only 2026-08-17 production count of zero unleased `dispatched` rows. That is historic evidence that no data repair was then required, not proof that the failure mode cannot occur. This ticket is forward resilience and does not authorize a production mutation.
- [[INTK-042]] is blocked by this ticket because immediate post-commit publication makes the publish-record/delivery boundary more prominent. Recovery must land first while retaining Worker as sole processing owner.

## Implications

- Extend the existing intake-work recovery contract and `ReconcileStagedArtifacts`; do not introduce another timer, queue, hosted service, or business-policy owner.
- Put the stale-age decision in Core/reconciliation policy and pass an explicit cutoff to persistence. EF should select and conditionally transition facts, not decide operational timing.
- Re-arm only unleased `dispatched` rows whose recorded publication time is at or before the settled cutoff. Transition them to `pending`, set `DueAtUtc` to the recovery time, clear failure/lease fields, and leave `AttemptCount` unchanged because processing never began.
- Preserve one bounded maximum across expired-lease and stale-dispatch recovery. Planning must state ordering or combined oldest-first behavior so a large class cannot starve the other.
- Verify both halves: a stale dispatched row is re-dispatched and processed once; a fresh dispatched row is untouched. Also retain the existing delivery-before-mark race and duplicate/no-op tests.
- No migration is indicated: the necessary state, publication timestamp, conditional-update pattern, and supporting index already exist.

## Open questions

- The exact stale-dispatch age is intentionally not selected here. [[INTK-041]] must settle it against the near-real-time recovery contract and queue backlog/cold-start tolerance before this ticket is planned.
