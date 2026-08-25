# Files — INTK-042

## Where the change lands

| Path | Why |
|---|---|
| `src/Pegasus.Core/Intake/DurableIntake.cs` | Extend the one intake dispatch protocol so a just-committed receipt can request immediate publication without bypassing claim/mark/release state or moving queued processing into Web. High overlap: INTK-040 currently edits this file, so implementation waits for that work to merge. |
| `src/Pegasus.Core/Custody/CustodyContracts.cs` and `ExternalWorkProcessing.cs` | Reuse/generalise the existing external-work dispatcher for immediate post-commit publication and slower recovery, keeping at-least-once and failure release in one owner. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` | Support the selected exact-or-due dispatch claim while preserving serializable state transitions, due times, replay, and the [[INTK-003]] recovery contract. |
| `src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs` | Support immediate custody publication using the same atomic claim and recovery state as timer dispatch. |
| `src/Pegasus.Infrastructure/Messaging/*` (new or existing closest folder) | Host the shared Azure Storage queue adapters/configuration needed by both composition roots; this replaces, rather than duplicates, the Worker-only adapter implementations. |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Add the Azure Queue Storage dependency only if required after moving the concrete adapters into the shared external-boundary project. |
| `src/Pegasus.Worker/AzureQueueIntakeWorkQueue.cs`, `AzureQueueExternalWorkQueue.cs`, and the queue-specific portion of `WorkerAzureClientFactory.cs` | Remove or reduce the superseded Worker-only implementations once the shared Infrastructure owner exists; retain Worker-only Blob/Functions composition that still has a real caller. |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Compose the shared adapters and immediate publisher for mailbox/queued workflows. High overlap: INTK-040 currently edits this file. |
| `src/Pegasus.Web/Program.cs` | Compose the same queue senders/publisher for manual upload and Web-originated custody work under existing runtime-profile validation. |
| `src/Pegasus.Web/Pages/Upload.cshtml.cs` and/or the final shared intake submission boundary selected in planning | Invoke immediate post-commit intake publication without executing processing and without turning a queue-send failure into a false “receipt failed” result. Prefer the shared submission boundary if it covers both single/grouped upload and mailbox without changing unrelated callers. |
| Relevant Case/Image Intake application boundaries and stores identified during planning | Trigger immediate external/custody publication after their committed work is visible. Do not add queue calls inside an open EF transaction or duplicate calls in every Razor Page. |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Retain the timer only as slow recovery and update naming/logging if needed so it no longer presents ordinary dispatch polling as the primary path. |
| `infra/modules/platform.bicep` | Give Web only the queue-send role and queue service settings its new caller requires; change pending dispatch/reconciliation cadence to the approved one-minute recovery safety net. No deployment is part of this ticket. |
| `src/Pegasus.Worker/local.settings.example.json` and relevant Web development configuration/examples | Keep local composition explicit and aligned with production queue endpoint/connection-string rules. |
| `tests/Pegasus.Core.Tests/Custody/ExternalWorkDispatchTests.cs` and focused intake dispatch tests | Prove immediate claim→enqueue→mark ordering, failure release, exact id behavior if selected, and duplicate/no-op semantics. |
| `tests/Pegasus.IntegrationTests/RecoveryTests.cs` and `CustodyOutboxIntegrationTests.cs` | Prove committed work publishes without waiting for the timer, a missing/duplicate delivery remains safe, and the one-minute recovery path redrives it after [[INTK-003]]. |
| `tests/Pegasus.IntegrationTests/UploadConfirmationWebTests.cs`, `MailboxIntakeIntegrationTests.cs`, and grouped-upload coverage | Prove both intake routes publish after durable receipt while status remains truthful during queue failure and processing stays Worker-owned. |
| `tests/Pegasus.ArchitectureTests/WorkerAzureClientCompositionTests.cs`, `WorkerCompositionTests.cs`, and `DependencyDirectionTests.cs` | Update adapter ownership/composition assertions and prevent queue business policy from drifting into Web/Worker. |
| `docs/current-architecture.md` | Replace the timer-first as-built path with immediate post-commit publication plus slow recovery after implementation is real. |
| `docs/operations.md` | Record the changed schedules, identity/role shape, and implemented-but-not-yet-deployed evidence accurately. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `docs/frd/frd-02-intake-and-source-identity.md` | Governs durable acknowledgement, source/work identity, truthful status, and Worker-only processing; consume the [[INTK-041]] revision before coding. |
| `docs/frd/frd-05-documents-extraction-and-custody.md` | Keeps Box custody asynchronous after Case/Image identity and forbids automatic business retry after terminal custody failure; faster queue publication must not change that policy. |
| `docs/adr/0002-dotnet-modular-monolith-on-azure.md` | Establishes the SQL outbox, at-least-once queue delivery, shared Core/Infrastructure ownership, and currently accepted Worker-hosted dispatch statement that [[INTK-041]] will partially supersede. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | Formal Case acceptance already commits and returns its custody work id in the outcome. |
| `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs` | Replacement Case creation also returns a custody work id and is a Web-originated committed-work path. |
| `src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs` | Image registration/merge create custody work internally and do not consistently expose a new work id, warning against forcing transport ids through every business contract. |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | Shares `ExternalWorkItems` for non-custody vehicle lookups; a targeted custody path must not classify work merely by table. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | Owns idempotent external/custody processing after queue delivery; Web must never call it. |
| `src/Pegasus.Core/Intake/GroupedIntake.cs` | One manual submission can commit several child receipts sequentially; immediate publishing must handle the group without inventing a second grouped path. INTK-040 also edits it. |
| `src/Pegasus.Core/Intake/MailboxIntake.cs` | Worker mailbox intake uses the same durable receipt owner; mailbox wake-up in [[MAIL-013]] should end here, not gain a separate publication policy. |
| `src/Pegasus.Worker/host.json` | Queue-trigger retry/visibility behavior remains the transport authority and must be checked against recovery thresholds. |
| `infra/modules/platform.bicep` role assignments | Current least privilege grants queue contribution only to Worker; Web needs sender, not contributor/processor authority. |
| INTK-040 ticket research/files/plan and its active worktree diff | Shows current ownership and exact overlap in DurableIntake, GroupedIntake, Worker DI, FRD-02, and tests; do not touch or rebase that agent’s work. |
| INTK-003 ticket documents | Own the dispatched-message-loss recovery prerequisite; INTK-042 must reuse its final state instead of adding a second recovery rule. |
| INTK-041 governing-document changes | Own the accepted near-real-time behavior and architecture; this ticket implements rather than redefines it. |

## Ripple effects

Web gains an external queue-send dependency and deployed managed-identity permission, while Worker remains the only queue-trigger processor. Constructor and DI tests/fakes for intake submission, Case acceptance, replacement, Image Intake, and combined dispatch may change depending on the chosen shared post-commit boundary. Queue adapter types moving assemblies affects architecture assertions and test visibility. Bicep role/config snapshots and current-state docs must match the real composition. No database migration should be needed unless [[INTK-003]] introduces state metadata; the existing durable work rows already contain stable ids, state, due time, and leases.

The change affects latency telemetry: spans/metrics must distinguish SQL commit, immediate publish attempt, recovery publish, queue dequeue, and processing so [[INTK-043]] can measure the remaining source-reading budget. It also changes Functions invocation volume by slowing the recovery timer; cost proof belongs to the release ticket, not this implementation claim.

## Out of scope

Graph webhook/subscription handling ([[MAIL-013]]), sender/status presentation fixes ([[INTK-001]]), source-reader optimisation ([[INTK-043]]), the Application Insights cap ([[PLAT-036]]), mailbox classification/extraction/case policy, Box business retry policy, queue replacement, Service Bus, a new worker/runtime, always-ready capacity, cloud deployment, production role assignment, or live cost/latency proof. INTK-040’s mailbox-image behavior and uncommitted work are explicitly out of scope and must land before implementation begins.
