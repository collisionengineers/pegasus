# Research — SIMPLI-009: queued-intake execution ownership

## Question

Which components currently receive, dispatch, process, recover, and report queued intake, and where does Web duplicate Worker ownership? This research describes the repository as it exists at the ticket branch head; it does not prescribe an implementation or claim deployed/live behavior.

## Scope and authority

- The ticket asks to make Worker the only queued-intake processor, stage work as pending, remove Web inline processing and its request-local completion polling, repair stranded dispatched work, and classify unexpected failures explicitly.
- `docs/temp-plans/simplify/simplify.md:1233-1241` repeats that intended scope. It is a transient planning artifact and evidence for the ticket, not canonical product or architecture authority.
- `docs/frd/frd-02-intake-and-source-identity.md` is the functional authority for intake/source behavior. `docs/current-architecture.md` and `docs/operations.md` describe as-built and deployed/runtime state respectively. `docs/design.md` owns the operator-facing Upload behavior. Accepted ADRs are historical, append-only decisions and are not rewritten to describe a later implementation.
- “Polling” in this ticket means the short SQL completion loops inside the Web HTTP request. It does not mean the Worker dispatch timer, mailbox polling, Sent-evidence polling, the Azure queue trigger, or the staged-artifact reconciliation timer.

## Current state model and contracts

### `src/Pegasus.Core/Intake/DurableIntake.cs`

- Lines 10-18 define the persisted work states: `Pending`, `Dispatched`, `Processing`, `RetryScheduled`, `Completed`, `Failed`, and `Dispatching`.
- Lines 82-100 define the submission result and `IIntakeSubmission`. A result contains a receipt identifier, duplicate flag, and disposition.
- Lines 303-383 define `IIntakeWorkStore`. The same store contract exposes both:
  - `ReceiveAsync`, the ordinary durable receive path;
  - `ReceiveForProcessingAsync`, the inline fast path;
  - dispatch claim/mark/release operations;
  - processing claim/complete/fail operations;
  - expired-lease recovery.
- Lines 386-389 define `IIntakeWorkEnqueuer`. Its payload is only the staged receipt identifier.

### `ProcessIntakeSubmission`: Web-owned processing and Web-local polling

- `src/Pegasus.Core/Intake/DurableIntake.cs:103-300` defines `ProcessIntakeSubmission`.
- It is not merely a receipt adapter. At lines 136-144 it:
  1. calls `ReceiveIntake.ExecuteInlineAsync`;
  2. calls its own `TryProcessAsync`, which invokes `ProcessQueuedIntake.ExecuteAsync`.
- After invoking the processor, it reads the completed evaluation and allocation projection. The class defines 100 attempts with a 100 ms interval at lines 123-125.
- `AwaitAllocationOutcomeAsync` and `AwaitCompletedEvaluationAsync` loop against persisted state. Their maximum wait is approximately ten seconds.
- These loops are request-local completion polling. They exist so an Upload POST can wait for processing performed in that same Web process or by a concurrent request, then redirect immediately to a case, create screen, or receipt.
- If no evaluation exists, lines 179-189 read the work item. A non-completed/non-failed item is returned as `Queued`; otherwise the class throws because inline processing did not persist an evaluation.
- `TryProcessAsync` suppresses exceptions classified as recoverable and returns false. It does not hand the work to a separate queue at that point.

### `ReceiveIntake`: two receive modes

- `src/Pegasus.Core/Intake/DurableIntake.cs:391-542` defines `ReceiveIntake`.
- `ExecuteAsync` calls the common receiver with `processInline: false`.
- `ExecuteInlineAsync` calls it with `processInline: true`.
- Both modes validate source identity, file name, media type, actor, operation key, non-empty content, channel-specific size limit, and source hash. Both stage bytes through `IIntakeArtifactStore`.
- For an existing source, lines 461-479 verify that the same source identity has the same hash. The inline branch calls `ReceiveForProcessingAsync`; the ordinary branch calls `ReceiveAsync`.
- For a new source, lines 482-518 stage the artifact and construct `IntakeStagedReceipt`. The same inline/ordinary store branch is then applied.
- The explicit `IIntakeSubmission.ExecuteAsync` implementation at lines 521-531 uses ordinary `ExecuteAsync` and always returns the `Queued` disposition.

## Persistence behavior and the stranded-state gap

### `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs`

- `ReceiveAsync` at lines 101-105 persists through `ReceiveWithRetryAsync` with initial state `Pending`.
- `ReceiveForProcessingAsync` at lines 107-112 uses initial state `Dispatched`.
- The duplicate receive path around lines 140-180 can promote an existing `Pending` or `RetryScheduled` item to `Dispatched` for inline processing. That transition has no dispatch lease and does not publish a queue message.
- `ClaimDispatchAsync` begins at line 215. Its selection query admits due `Pending` and `RetryScheduled` work, changes the winner to `Dispatching`, and attaches a dispatch lease.
- `MarkDispatchedAsync` begins at line 247. It accepts the dispatch lease and records successful publication.
- `ClaimProcessingAsync` begins at line 284. It accepts delivery for work in the dispatch/processing states and attaches a processing lease. This supports at-least-once delivery, including the valid race where a queue message is delivered before the dispatcher finishes `MarkDispatchedAsync`.
- Processing failure at lines 400-407 persists either `RetryScheduled` with a due time or terminal `Failed`, stores a failure code, and clears the lease.
- `RecoverExpiredLeasesAsync` begins at line 434. It selects expired leased `Dispatching` and `Processing` records. Expired dispatch claims return to `Pending`; expired processing claims become `RetryScheduled` or terminal `Failed`.
- That recovery query does not select an unleased `Dispatched` record.
- Consequently, the current inline path has a state-machine hole: a work item created or promoted by `ReceiveForProcessingAsync` can be stranded if Web stops after the `Dispatched` write but before `ProcessQueuedIntake` successfully claims it. This repository is not operating against real production records; persisted rows are test data. The ticket therefore needs to remove the state transition and prove the replacement behavior in fresh test databases, not migrate or repair a live historical dataset.

## The existing Worker processing path

### Core dispatcher and processor

- `DispatchPendingIntakeWork` is at `src/Pegasus.Core/Intake/DurableIntake.cs:544-587`.
- It claims one due work item with a one-minute dispatch lease, publishes the staged receipt identifier through `IIntakeWorkEnqueuer`, then marks the item dispatched.
- If enqueueing or marking fails, it releases the dispatch claim and sets a 30-second due time before rethrowing.
- `ProcessQueuedIntake` begins at line 589.
- It claims a five-minute processing lease for the staged receipt. A missing claim is treated as duplicate/concurrent/completed delivery: it either returns or re-drives idempotent association/allocation/triage work for a completed evaluation.
- For a new processing claim, it reads staged content, verifies the content hash, stores durable content, calls `ProcessIntake.ExecuteRetainedAsync`, persists the completed evaluation, and performs case association/allocation, triage, and image-intake follow-up.
- The retry schedule is fixed in the class: 30 seconds, 2 minutes, 10 minutes, 30 minutes, and 2 hours.
- Artifact integrity and invalid-data failures are caught as terminal failures. Exceptions accepted by `IntakeExceptionPolicy.IsRecoverable` are persisted as retryable until the retry schedule is exhausted, then terminal.
- Some post-processing follow-up is intentionally non-blocking and suppresses exceptions accepted by the same policy. A plan must distinguish that behavior from failure of the primary intake evaluation.

### Worker composition root and triggers

- `src/Pegasus.Worker/WorkerDependencyInjection.cs:85-102` registers:
  - `EfIntakeWorkStore` behind the intake store/authority contracts;
  - `AzureQueueIntakeWorkQueue` as `IIntakeWorkEnqueuer`;
  - `ReceiveIntake`;
  - `DispatchPendingIntakeWork`;
  - `ProcessQueuedIntake`;
  - poison and staged-artifact reconcilers.
- `src/Pegasus.Worker/IntakeFunctions.cs:8-28` defines `PendingWorkDispatchFunction`. A timer invokes the combined pending-work dispatcher with a maximum batch of 50.
- Lines 30-45 define `IntakeWorkFunction`. Its `intake-work` queue trigger validates a canonical GUID message and calls `ProcessQueuedIntake.ExecuteAsync`.
- Lines 47-65 define `IntakePoisonFunction` for `intake-work-poison`.
- Lines 67-97 define `StagedArtifactReconciliationFunction`, which invokes staged-artifact and lease reconciliation.
- `src/Pegasus.Worker/AzureQueueIntakeWorkQueue.cs` publishes the staged receipt GUID in canonical string form.
- `src/Pegasus.Worker/host.json` configures the Storage Queue extension with a five-minute visibility timeout and maximum dequeue count of five. These are existing host settings, not evidence that a specific deployment is active.

## Web composition and user-visible behavior

### `src/Pegasus.Web/Program.cs`

- Lines 560-578 register `EfIntakeWorkStore`, `ReceiveIntake`, `ProcessQueuedIntake`, and `ProcessIntakeSubmission` in Web.
- The comments state the deliberate current split:
  - the manual Upload page uses inline processing to produce an immediate destination;
  - automation ingress resolves `IIntakeSubmission` to queue-only `ReceiveIntake`.
- This means Web can resolve and invoke the same Core processor as Worker. The ownership duplication is present in composition, not only in page code.

### `src/Pegasus.Web/Pages/Upload.cshtml.cs`

- `UploadModel` directly injects `ProcessIntakeSubmission` at lines 35-39.
- `OnPostAsync` validates the external receipt token, uploaded file, actor, file size, and content, then calls the inline submission at lines 114-125.
- A `Queued` result redirects back to Upload with “received and is being processed” copy.
- A processed result is read from `IIntakeReceiptQueries` and routed to:
  - Case Details when a case exists;
  - Case Create for readable material needing a case;
  - Intake Details otherwise.
- The page catches source-identity conflicts, staging failures, and exceptions classified as recoverable. Current comments explicitly say the operator waits while the file is read.
- The change in this ticket therefore affects the meaning and destination of a successful Upload POST. SIMPLI-008 is the adjacent ticket for staff-visible queued processing status; this research does not invent that UI contract.

### Other Web ingress

- `src/Pegasus.Web/Mcp/IntakeMcpTools.cs` depends on `IIntakeSubmission`, not `ProcessIntakeSubmission`.
- Web maps `IIntakeSubmission` to `ReceiveIntake`, so that ingress already stages and returns queued without invoking `ProcessQueuedIntake`.
- This is useful evidence that the queue-only Core submission contract already exists and has a current Web caller.

## Failure classification

### `src/Pegasus.Core/Intake/IntakeContracts.cs`

- `IntakeExceptionPolicy.IsRecoverable` at lines 499-505 returns true for every exception except `OperationCanceledException`, `OutOfMemoryException`, and `AccessViolationException`.
- In practical terms, ordinary programming or invariant exceptions such as `InvalidOperationException` are currently classified as recoverable.
- `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs` contains an assertion reflecting that current behavior.
- `ProcessQueuedIntake`, Upload error handling, artifact staging/reconciliation, and some non-blocking follow-up all use this shared predicate.
- Therefore “classify unexpected failures explicitly” is not isolated to one catch block. Tightening the shared predicate changes multiple paths; adding processor-specific classification would be narrower but would leave other callers on the current broad policy.
- Queue-trigger exception behavior matters: if the Worker function returns successfully after an unexpected exception, Azure Functions will not redeliver that message. If the exception escapes, Functions retry/poison behavior remains active. Durable SQL classification and trigger failure signaling must be designed together.

## Infrastructure and permission boundaries

### `infra/modules/platform.bicep`

- Lines 133-140 declare the `intake-work` and `intake-work-poison` Storage queues.
- Lines 317-326 give the Worker identity queue contributor and transient-blob data-owner roles.
- Lines 335-339 give Web transient-blob access, which is consistent with Web staging source bytes.
- Lines 353-357 give Web the Storage Queue Message Sender role scoped to `intake-work`.
- Worker settings at lines 504-515 provide intake storage/queue service URIs, a one-minute pending-work dispatch schedule, a staged-artifact reconciliation schedule, and activation-controlled settings for dispatcher, work, poison, and reconciliation functions.
- The source currently shown for the Web container does not provide an `IntakeQueue__ServiceUri` setting, while the Worker does. The Web queue-sender role therefore exists in infrastructure even though the current Web Core publisher registration is not `AzureQueueIntakeWorkQueue`.
- This is a source-level permission observation only. Removing or retaining a cloud role is an implementation/release decision and any actual cloud write requires separate explicit approval.

### SQL runtime roles

- Runtime grants are created by append-only migrations, including `src/Pegasus.Infrastructure/Persistence/Migrations/20260729199000_RuntimeRoleReconciliation.cs`.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` asserts the exact Web and Worker table permissions.
- The current contract gives Web staging-related access and gives Worker the processing writes needed for receipts/evaluations/work.
- Historical migrations must not be edited. If the intended caller boundary requires a grant correction, it needs a new migration and corresponding permission tests.

## Existing tests and what they actually prove

### Recovery and queue semantics

- `tests/Pegasus.IntegrationTests/RecoveryTests.cs` covers:
  - duplicate receipt identity;
  - expired dispatch-lease recovery;
  - the enqueue-before-`MarkDispatchedAsync` immediate-delivery race;
  - exactly-once processing under duplicate delivery;
  - poison reconciliation/replay.
- These are valuable Core/persistence proofs, but an in-process enqueuer is not proof that Web and Worker are separate runtime callers.

### Web and allocation tests

- `tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs` currently asserts that manual upload processes “on the spot” while automation submission remains queue-only.
- `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` contains helpers that explicitly run dispatch and processing after a queued Web result. Those helpers simulate Worker completion inside the test process.
- `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` has direct `ProcessIntakeSubmission` callers as well as separate dispatcher/`ProcessQueuedIntake` scenarios.
- These tests must be read by intent: a helper that submits and immediately drains work can prove an end-to-end outcome but cannot prove that the Web request itself lacks processor ownership.

### Composition, activation, permissions, and performance

- `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs` proves positive Worker registrations for dispatch, processing, and functions.
- `tests/Pegasus.ArchitectureTests/WorkerActivationReleaseContractTests.cs` protects the expected intake-function settings/census.
- `tests/Pegasus.ArchitectureTests/WorkerAzureClientCompositionTests.cs` proves Worker queue client construction and queue names.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` proves declared database-role permissions.
- `tests/Pegasus.PerformanceTests/FailureInjectionTests.cs` and `CapacitySoakTests.cs` use a test-side `ProcessQueuedAsync` helper after uploads. Their persistence and concurrency assertions remain useful, but that helper is not evidence of the deployed execution boundary.

## Documentation state

- `docs/frd/frd-02-intake-and-source-identity.md:49-58` already requires durable dispatch and identifier-only queue payloads. It does not currently state that Worker is the exclusive processor.
- `docs/current-architecture.md` currently shows staff intake flowing directly toward Core processing. As an as-built document, it must continue describing current code until implementation changes.
- `docs/operations.md:17-19` and line 172 explicitly name `ProcessIntakeSubmission` as the current authenticated `/Upload` caller. That is accurate for this source tree. It must not be changed to a Worker-only claim until the source/release state supports it.
- `docs/runbook.md:275-278` already assigns queued-intake execution to Worker operationally. That statement coexists with the current Web inline path because Web bypasses the queue dispatcher for manual upload.
- `docs/design.md:482` describes intake receipt/upload through `ReceiveIntake`. Any final queued acknowledgement/status navigation must be reconciled there after SIMPLI-008’s UI ownership is respected.
- Accepted ADR-0005 contains historical Web-to-`ProcessIntake` wording. It is not rewritten. A new ADR is required only if planning identifies a genuinely new durable architectural decision rather than implementing the already accepted queue architecture.

## Implications for planning

- The smallest coherent caller-boundary change is not just removing one Web registration. The inline receive method, store contract, persistence transition, Upload result model, and request-local polling exist as one connected path.
- Existing Worker dispatch/processing infrastructure can carry the desired ownership. No new queue, project, runtime, or deployment unit is evidenced as necessary.
- No historical-data repair or compatibility migration is required: there are no real production records, only disposable test data. Remove the legacy creator/transition and recreate test databases.
- The new path must still preserve the valid enqueue-before-mark race and prove crash-after-stage recovery: after staging, the item remains `Pending` and is eligible for ordinary Worker dispatch.
- Web submission evidence and Worker completion evidence must be separate in tests. The Web boundary should be asserted before any test helper drains queued work.
- The operator response cannot keep promising immediate case navigation once Web stops processing. The plan must use the receipt/status behavior owned with SIMPLI-008 rather than design a competing surface.
- Failure classification needs explicit categories and expected state/trigger behavior before code changes: terminal input/integrity failure, transient retry, exhausted retry/poison, unexpected implementation failure, and failure after evaluation completion are not the same event.
- Documentation changes must follow their authority:
  - FRD for required behavior;
  - design for Upload/status interaction;
  - current architecture for merged as-built composition;
  - operations for deployed/live state;
  - runbook only for changed operational procedures;
  - ADR only for a new durable technical decision.

## Open questions

- What exact receipt-keyed destination and operator wording will SIMPLI-008 provide for a newly staged manual upload?
- Which exception types are explicitly transient for intake processing, and which unexpected exceptions must both persist a classification and escape to preserve queue redelivery?
- How should failures after evaluation completion but before allocation/triage completion be represented without changing a completed intake evaluation to failed?
- Is the Web queue-sender Azure role deliberately reserved for a future direct-publish ingress, or is it unused privilege that this ticket should remove?
- Does Worker-only ownership require a new ADR, or is it fully covered by the accepted Storage Queue architecture plus the updated FRD behavior?
