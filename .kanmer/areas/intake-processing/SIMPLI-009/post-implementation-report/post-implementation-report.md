# Post-implementation report — SIMPLI-009 (with SIMPLI-008)

Branch `task/simpli-009`; combined delivery for [[SIMPLI-009]] (Worker sole processor) and [[SIMPLI-008]] (staff status page). Commits: `195154f9` implementation; `e9f27fe7` merge of `origin/dev`; `caad05e8` remove repo temp-plan; `8bf0a3e6` review blockers + simplification pass. **Net PR diff vs `origin/dev` (e6422250): 31 files, +873/−817.** PR #385.

## What changed, file by file

### Core (`src/Pegasus.Core`)

| File | Change | Why |
| --- | --- | --- |
| `Intake/DurableIntake.cs` | Deleted `ProcessIntakeSubmission`, `IntakeSubmissionResult`, `IntakeSubmissionDisposition`, `ReceiveIntake.ExecuteInlineAsync` and the `processInline` branch (the `ReceiveCoreAsync` forwarder is inlined into `ExecuteAsync`), and `IIntakeWorkStore.ReceiveForProcessingAsync`. `IIntakeSubmission.ExecuteAsync` returns `ReceivedIntake`; `ReceiveIntake` implements it directly. Added `QueuedIntakeStatusKind`, `QueuedIntakeStatus`, `IQueuedIntakeStatusQueries`, and `QueuedIntakeStatusKinds.FromWorkState(IntakeWorkState)` (explicit, fail-closed collapse of every waiting state to Received). `ProcessQueuedIntake.ExecuteAsync` returns `QueuedIntakeProcessingOutcome` (NoOp / Completed / RetryScheduled / Failed). Catch chain: `TerminalInputFailureCode(exception) is { } code` (integrity → `staged_artifact_integrity_failure`, invalid data → `invalid_intake_data`, source-identity conflict → `source_identity_conflict`) → terminal Failed; `IsTransientProcessingFailure` (retention, operation/version conflict, `IntakeDependencyUnavailableException`, `IOException`, `TimeoutException`, `DbException`, **looking through `InnerException`**) → bounded retry with `TransientFailureCode`; `IntakeExceptionPolicy.IsRecoverable` guard → persist terminal `unexpected_intake_processing_failure` then `throw;`. `FailProcessingAsync(workItem, terminal, failureCode, ct)`. | Plan steps 1–4 as amended in the plan's "Simplification pass": one taxonomy, persist-then-rethrow. |
| `Intake/IntakeContracts.cs` | Added `IntakeDependencyUnavailableException`. | The named transient fault adapters translate to. |

### Infrastructure (`src/Pegasus.Infrastructure`)

| File | Change | Why |
| --- | --- | --- |
| `Intake/AzureBlobIntakeArtifactStore.cs` | Read and upload paths translate non-404/non-conflict `RequestFailedException` via one `DependencyUnavailable(...)` factory; upload flattened into `UploadOrVerifyAsync` (409/412 → `VerifyBlobAsync`, still wrapped by the outer catch). | Azure faults become the named transient fault; no nested try. |
| `Persistence/EfIntakeWorkStore.cs` | Removed `ReceiveForProcessingAsync` and receive-time promotion to unleased `Dispatched`; every receive persists `Pending`; missing-work-item guard is a plain `if`; `ParseState` is `internal` for reuse. | Plan step 2; single state-string table. |
| `Persistence/EfQueuedIntakeStatusQueries.cs` (new) | One query: staged receipt + work state + processed receipt + failure code + case id via correlated subquery on `CaseIntakeLinks` (PK is the receipt id); state → `IntakeWorkState` → Core kind. | Plan step 6. |

### Web (`src/Pegasus.Web`)

| File | Change | Why |
| --- | --- | --- |
| `Program.cs` | Removed `ProcessQueuedIntake` and `ProcessIntakeSubmission` registrations; registered `IQueuedIntakeStatusQueries`; `IIntakeSubmission → ReceiveIntake` retained for MCP ingress. | Plan step 5. |
| `Pages/Upload.cshtml(.cs)` | Depends on `IIntakeSubmission` only; success (incl. duplicate) redirects to `/Upload/Status/{id}` with `?duplicate=true` when a replay (route value, the `/Received/{id}` convention); outcome card, `Describe`, `UploadOutcome`, `OutcomeMessage` removed. | Plan step 8. |
| `Pages/UploadStatus.cshtml(.cs)` (new) | Authorised staff page on the design system (`page-heading`, `panel`, `detail-list`, `button-row`, `primary-action`/`secondary-action`; no nested `<main>`); 404 unknown; `Heading` + `StateMessage` per state, duplicate prefix from the route flag; `data-auto-refresh="2000"` only while Received/Processing; tag-helper Refresh preserving the flag; "Open case" / "Open receipt". | Plan step 7 (SIMPLI-008); review B2. |
| `Pages/Cases/Create.cshtml(.cs)`, `Pages/Intake/Details.cshtml` | Removed the dead `TempData["UploadOutcomeMessage"]` readers (no writer remains). | Review N5. |
| `Presentation/OperatorLabels.cs` | Labels for `invalid_intake_data`, `source_identity_conflict`, `processing_lease_expired`, `queue_poisoned`, `unexpected_intake_processing_failure`. | Bounded operator wording for every code the processor now persists. |
| `wwwroot/js/site.js` | `[data-auto-refresh]` reload, placed after `'use strict'`. | CSP-safe refresh. |
| `Mcp/IntakeMcpTools.cs` | Reads `StagedReceiptId`; reports literal `"Queued"`. | Follows the collapsed contract; behaviour-neutral (always was queue-only). |

### Worker

`src/Pegasus.Worker/IntakeFunctions.cs` — **unchanged from `origin/dev`** (the earlier outcome-logging variant was removed by the simplification pass; unexpected faults reach the host as thrown exceptions).

### Infrastructure-as-code

| File | Change | Why |
| --- | --- | --- |
| `infra/modules/platform.bicep` | Removed the Web identity's Storage Queue Data Message Sender assignment on the intake queue and its role variable. Web blob-staging access retained. Source only; no deployment. | Least privilege — Web never enqueues. |

### Tests

| File | Change |
| --- | --- |
| `IntegrationTests/RecoveryTests.cs` | `TransientProcessingFailureSchedulesARetry` theory (`io`, `dependency`, `wrapped-database` — a `DbUpdateException` wrapping a `DbException`); `UnexpectedProcessingFailureIsPersistedThenRethrown` (throws → row Failed with the unexpected code → redelivery `NoOp` → status page Failed without leaking the code); `QueuedStatusProjectsAnActiveProcessingLease`; `TransientProcessingFailureExhaustsTheBoundedRetrySchedule`; shared `StageAndDispatchAsync`; uses `IntakeWebDriver.CreateProcessor` and the shared enqueuer. |
| `IntegrationTests/IntakeWebTestSupport.cs` | `CreateProcessor(services)`, `DrainStagedAsync(services, id)`, one `internal ImmediateIntakeWorkEnqueuer`; `ProcessQueuedAsync` drains via the helper; `Landing` reads `/Upload/Status/{id}` + `duplicate`; `UploadLanding(StagedReceiptId, IsDuplicate)`; dead create-screen/case branches, `CreateScreenReceiptId`, `CaseId(UploadResult)`, legacy query keys removed. |
| `IntegrationTests/QdosIntakeWebTests.cs` | `ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage` (Pending, no evaluation, Web DI cannot resolve `ProcessQueuedIntake`, `<h1>Received</h1>` + auto-refresh, then `<h1>Complete</h1>` without auto-refresh and "Open receipt"), `UploadStatusIsStaffOnlyAndUnknownReceiptsReturnNotFound`, `CompletedAllocatedUploadStatusLinksOnlyToItsCase`. |
| `IntegrationTests/QdosAllocationRecoveryTests.cs` | `AllocationTestData.SubmitAndProcessAsync` returns `Guid` via `DrainStagedAsync`; local enqueuer and `ProcessedSubmission` removed; explicit stage → dispatch → process steps. |
| `IntegrationTests/IntakeWebNegativeTests.cs`, `MailboxIntakeIntegrationTests.cs`, `InstructionDraftWebTests.cs` (replay asserts "was already received"), `StagedArtifactReconciliationFunctionTests.cs`, `AzureBlobIntakeArtifactStoreTests.cs`, `Core.Tests/Intake/PollApprovedInboxTests.cs` | Mechanical follow-through; processor via `CreateProcessor`; removed fake method; new blob dependency-translation test. |
| `PerformanceTests/CapacitySoakTests.cs` | Write p95 budget back to 3 s (request only stages). |

### Documentation

| File | Change |
| --- | --- |
| `docs/frd/frd-02-intake-and-source-identity.md` | Normative paragraph: Web stages pending and never processes; Worker sole owner; duplicate delivery idempotent; four staff-visible states; bounded failure wording. |
| `docs/current-architecture.md` | Intake diagram stage → Pending → dispatcher → queue → Worker; status query; `/Upload/Status/{id}` in the route paragraph; implementation-map row for `Upload`/`UploadStatus`/`EfQueuedIntakeStatusQueries`. |
| `docs/design/README.md` | Intake receipt and upload row: status redirect, four states, refresh, links; page inventory. |
| `docs/operations.md` | `/Upload` POST is the staging caller through `ReceiveIntake`; Worker owns processing; no deployment/live claim. |
| `docs/temp-plans/simpli-009.md` | Added in `195154f9`, removed in `caad05e8` (duplicated the ticket plan; CI rejects new Markdown outside `docs/{prd,frd,adr}`). |

## Deviations from the plan (recorded as plan amendments)

- **Step 3** — the fault taxonomy is one Core policy per decision (`TerminalInputFailureCode`, `IsTransientProcessingFailure` with inner-exception unwrap, catch-all guarded by the shared `IntakeExceptionPolicy.IsRecoverable`) rather than three type lists; "HTTP" is reached through the blob adapter's `IntakeDependencyUnavailableException` translation, not by matching `HttpRequestException` in Core (no `HttpClient` sits in the processor's try).
- **Step 4** — unexpected faults are persisted terminal and rethrown instead of returned as an outcome and logged by the Worker; `IntakeWorkFunction` is unchanged.
- The negative "Web cannot resolve the processor" assertion lives in `QdosIntakeWebTests` (real host) rather than `ArchitectureTests`; a `DependencyDirectionTests` fact is a recorded follow-up.
- No new ADR (implementation of ADR-0002's Worker-owned queue processing + FRD-02); no schema, host.json, or applied-migration change; no deployment.
- Impact-listed `AzureSqlRuntimeRoleMigrationTests`, `WorkerCompositionTests`, `Core.Tests/ProcessIntakeTests`, `PerformanceTests/FailureInjectionTests` unchanged — judged acceptable in review (role matrix already denies Web `IntakeReceipts:INSERT`; positive Worker assertion stands; FailureInjection already drives the drain).
- Ticket line "repair stranded dispatched work" — routed to [[SIMPLI-010]] with the reviewer's evidence (read-only production check + stale-`dispatched` re-dispatch); see the review scratch.

## Verification on `8bf0a3e6`

- `dotnet build Pegasus.slnx --configuration Release`: 0 warnings, 0 errors.
- `Pegasus.Core.Tests`: 572 passed. `Pegasus.ArchitectureTests`: 94 passed.
- `Pegasus.IntegrationTests` focused + driver consumers (Recovery, QdosIntakeWeb, QdosAllocationRecovery, IntakeWebNegative, MailboxIntake, InstructionDraftWeb, AzureBlobIntakeArtifactStore, StagedArtifactReconciliation, ImageIntake*, IntakeStablePersistence, LocalIntakeAccess): 86 passed, 6 skipped (corpus-gated), 0 failed.
- CI on PR #385: see the PR checks (full sql-integration shards, browser, unit, documentation incl. markdown placement, infrastructure).
- Pre-merge (`195154f9`): full IntegrationTests 529 passed / 16 skipped / 0 failed.

## Not claimed

No deployment, no live Worker execution, no cloud write. The bicep role removal is source only.
