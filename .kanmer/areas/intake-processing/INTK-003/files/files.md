# Files — INTK-003

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Extend the existing work-store recovery contract and staged-artifact reconciler with the explicit stale-dispatch cutoff owned by Core. Risk: changing the interface affects every fake/decorator; recovery must remain bounded and must not overwrite an active processing claim. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` | Select stale unleased `dispatched` candidates using `DueAtUtc` and conditionally move them to `pending`. Risk: candidate ordering, batch sharing, and compare-before-update concurrency determine whether recovery is fair and race-safe. |
| `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Add stale-versus-fresh dispatched recovery and redispatch/process-once coverage using the existing adjustable clock and real EF store. Risk: a store-only assertion could miss dispatcher/processor interaction. |
| `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs` | Update the local `IIntakeWorkStore` fake for any contract signature/result change; keep the function on the existing reconciler and timer. |
| `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Update its intake-work-store fake mechanically if the shared interface changes; mailbox behavior itself is not in scope. |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Update its delegating store mechanically if the recovery contract changes; retain its fault-injection behavior. |
| `tests/Pegasus.ArchitectureTests/DependencyDirectionTests.cs` | Update its compile-time fake mechanically if required; preserve Core/Infrastructure dependency direction. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Change only if [[INTK-041]] has not already supplied the normative lost-publication recovery sentence. Avoid duplicating the recovery contract. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `src/Pegasus.Worker/IntakeFunctions.cs` | `StagedArtifactReconciliationFunction` is the existing host caller. This ticket must not add or combine Functions; it should remain a Core/persistence recovery behavior reached by this timer. |
| `src/Pegasus.Worker/host.json` | Queue delivery uses a five-minute visibility timeout and five dequeue attempts. These are host mechanics, not the stale-publication policy or a reason to wait for queue-message expiry. |
| `src/Pegasus.Worker/AzureQueueIntakeWorkQueue.cs` | Publication carries only the canonical staged-receipt GUID and sets no explicit TTL. Recovery should republish through the existing enqueuer/dispatcher unchanged. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | `IntakeWorkItems` already has `(State, DueAtUtc)`; no migration or new recovery timestamp is evidenced. |
| `docs/frd/frd-02-intake-and-source-identity.md` | Worker-exclusive processing, durable dispatch, identifier-only payloads, idempotent duplicate delivery, and bounded staff-visible states are binding behavior. |
| `docs/operations.md` | Records the deployed timer and queue-poll settings as runtime facts; it must not be edited by this source-only fix unless deployment is separately approved and performed. |
| `src/Pegasus.Core/Custody/CustodyContracts.cs` and `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs` | External-work dispatch has a parallel state vocabulary, but INTK-003 is specifically intake recovery. Do not generalize into custody recovery without a separate demonstrated gap. |
| Kanmer [[SIMPLI-009]] research/proof | Establishes why Worker owns processing, the valid enqueue-before-mark race, duplicate-delivery safety, and the historic zero-row production observation. |
| Kanmer [[INTK-041]] | Owns the near-real-time recovery timing contract and blocks this implementation. |
| Kanmer [[INTK-042]] | Downstream immediate-publication work that relies on this safety net; it must not be pulled into INTK-003. |

## Ripple effects

- Every `IIntakeWorkStore` implementation, fake, and decorator must compile against the settled recovery signature.
- Reconciliation telemetry currently reports one `RecoveredLeases` count. If stale publications need a distinct operational signal under [[INTK-041]], change the result/log once at the Core/Worker boundary; otherwise retain the existing aggregate rather than invent parallel counters.
- Focused SQL integration tests are required because concurrency-safe conditional updates and indexed candidate selection are EF/SQL behavior.
- The existing dispatch and processing tests must remain green to prove the recovery does not break immediate queue delivery or at-least-once idempotency.
- No deployment proof follows from local tests; live state counts and timer execution belong to the later delivery ticket and require separately approved read/write boundaries.

## Out of scope

- Do not implement immediate publication ([[INTK-042]]), Graph mailbox wake-up, mailbox polling changes, manual-upload status changes, source-reading performance work, or Application Insights controls.
- Do not add a new queue, timer Function, background service, configuration framework, schema column, migration, or compatibility path.
- Do not recover `completed` or `failed` work, reset processing attempt counts, or retry terminal failures.
- Do not broaden this intake fix to `ExternalWorkItems` without evidence that the same unleased-dispatched gap exists and a ticket that owns it.
- Do not perform production SQL repair, deployment, or any Azure/cloud write.
