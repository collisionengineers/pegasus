# Post-implementation report — SIMPLI-009 (with SIMPLI-008)

Branch `task/simpli-009`; combined delivery for [[SIMPLI-009]] (Worker sole processor) and [[SIMPLI-008]] (staff status page). Implementation commit `195154f9`; `origin/dev` merged in on 2026-08-17 (`e9f27fe7`, clean); repo temp-plan removed (`caad05e8`). Net PR diff vs `origin/dev`: 29 files, +742/−653.

## What changed, file by file

### Core (`src/Pegasus.Core`)

| File | Change | Why |
| --- | --- | --- |
| `Intake/DurableIntake.cs` | Deleted `ProcessIntakeSubmission`, `IntakeSubmissionResult`, `IntakeSubmissionDisposition`, `ReceiveIntake.ExecuteInlineAsync`, the `processInline` branch of `ReceiveCoreAsync`, and the explicit `IIntakeSubmission` implementation that wrapped `ReceiveIntake`. `IIntakeSubmission.ExecuteAsync` now returns `ReceivedIntake` and `ReceiveIntake` satisfies it directly. Removed `IIntakeWorkStore.ReceiveForProcessingAsync`. Added `QueuedIntakeStatusKind`, `QueuedIntakeStatus`, `IQueuedIntakeStatusQueries` (SIMPLI-008 read model). `ProcessQueuedIntake.ExecuteAsync` now returns `QueuedIntakeProcessingOutcome` (NoOp / Completed / RetryScheduled / Failed / UnexpectedFailed); the catch chain is: integrity + invalid data → terminal Failed; `IsTransientProcessingFailure` (retention, operation/version conflict, `IntakeDependencyUnavailableException`, `IOException`, `TimeoutException`, `DbException`) → scheduled retry until the bounded schedule is exhausted; anything else except cancellation → terminal `unexpected_intake_processing_failure`. `FailProcessingAsync` takes an optional explicit failure code. | Plan steps 1–4: one durable path, no inline processing or request-local polling, explicit fault taxonomy instead of the broad `IntakeExceptionPolicy.IsRecoverable`. |
| `Intake/IntakeContracts.cs` | Added `IntakeDependencyUnavailableException`. | Named transient dependency fault that adapters translate into, so Core's classifier does not depend on SDK exception types. |

### Infrastructure (`src/Pegasus.Infrastructure`)

| File | Change | Why |
| --- | --- | --- |
| `Intake/AzureBlobIntakeArtifactStore.cs` | Read path and upload path translate non-conflict `RequestFailedException` into `IntakeDependencyUnavailableException`; 409/412 still fall through to `VerifyBlobAsync`. | Azure storage faults become an explicit retryable classification rather than "unexpected". |
| `Persistence/EfIntakeWorkStore.cs` | Removed `ReceiveForProcessingAsync` and the receive-time promotion of pending/retry work to unleased `Dispatched`; every receive persists `Pending`. | Plan step 2 — the stranded unleased-Dispatched defect. |
| `Persistence/EfQueuedIntakeStatusQueries.cs` (new) | Projects staged receipt id, file name, received time, work state → public status (pending/dispatching/dispatched/retry_scheduled → Received; processing → Processing; completed → Complete; failed → Failed), processed receipt id, case id via `CaseIntakeLinks` (PK is the receipt id, so single), failure code. | Plan step 6. |

### Web (`src/Pegasus.Web`)

| File | Change | Why |
| --- | --- | --- |
| `Program.cs` | Removed `ProcessQueuedIntake` and `ProcessIntakeSubmission` registrations and the two-submissions comment; registered `IQueuedIntakeStatusQueries → EfQueuedIntakeStatusQueries`; `IIntakeSubmission` still maps to `ReceiveIntake`. | Plan step 5 — Web cannot resolve the processor. |
| `Pages/Upload.cshtml.cs` | Depends on `IIntakeSubmission` only; dropped `IIntakeReceiptQueries`, `OutcomeMessage`, `Describe`, `UploadOutcome`, and the case/create/receipt routing. Success (including duplicate) redirects to `/UploadStatus/{stagedReceiptId}`; duplicates set one-time `TempData["DuplicateUpload"]`. Validation, antiforgery, identity-conflict and staging-error handling unchanged. | Plan step 8. |
| `Pages/Upload.cshtml` | Removed the outcome status card. | Outcome now lives on the status page. |
| `Pages/UploadStatus.cshtml(.cs)` (new) | Authorised (Administrator/Engineer/User) `/Upload/Status/{id:guid}`; 404 for unknown ids; heading/message per state; `data-auto-refresh="2000"` only while Received/Processing; manual Refresh link; "Open case" when a case is linked, otherwise "Open receipt" when Complete; failure wording via `OperatorLabels.IntakeFailure`. | Plan step 7 (SIMPLI-008). |
| `wwwroot/js/site.js` | Reads `[data-auto-refresh]` and reloads after the given delay. | CSP forbids inline scripts; refresh must live in the external bundle. |
| `Mcp/IntakeMcpTools.cs` | Reads `StagedReceiptId` and reports the literal `"Queued"` disposition. | Follows the collapsed `IIntakeSubmission` contract; automation ingress was already queue-only. |

### Worker (`src/Pegasus.Worker`)

| File | Change | Why |
| --- | --- | --- |
| `IntakeFunctions.cs` | `IntakeWorkFunction` awaits the outcome and logs a sanitized `LoggerMessage` (event 1510) for `UnexpectedFailed`; message and trigger unchanged. | Plan step 4; queue redelivery semantics preserved because unexpected faults are persisted terminally rather than rethrown. |

### Infrastructure-as-code

| File | Change | Why |
| --- | --- | --- |
| `infra/modules/platform.bicep` | Removed the Web identity's Storage Queue Data Message Sender assignment on the intake queue and its role variable. Web blob-staging access retained. | Least privilege — Web never enqueues; the Worker dispatcher publishes. No deployment performed. |

### Tests

| File | Change |
| --- | --- |
| `tests/Pegasus.IntegrationTests/RecoveryTests.cs` | Added `ProcessorDistinguishesTransientAndUnexpectedFailures`, `QueuedStatusProjectsAnActiveProcessingLease`, `TransientProcessingFailureExhaustsTheBoundedRetrySchedule`; enqueuer helper adjusted for the new outcome-returning processor. |
| `tests/Pegasus.IntegrationTests/QdosIntakeWebTests.cs` | Replaced `ReadableManualUploadIsProcessedOnTheSpotAndOpensTheCreateScreen` with `ReadableManualUploadStagesPendingWorkAndOpensItsStatusPage` (asserts Pending work, no evaluation, Web DI **cannot** resolve `ProcessQueuedIntake`, Received page with auto-refresh, then Complete without auto-refresh and an "Open receipt" link); added `UploadStatusIsStaffOnlyAndUnknownReceiptsReturnNotFound` and `CompletedAllocatedUploadStatusLinksOnlyToItsCase`. |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Direct `ProcessIntakeSubmission` calls replaced with explicit stage → dispatch → Worker-process steps. |
| `tests/Pegasus.IntegrationTests/IntakeWebNegativeTests.cs` | Uploads now assert a 302 to `/Upload/Status/` and drain the Worker explicitly before checking decisions; the staging-retry test asserts no receipt exists after a failed stage and two attempts. |
| `tests/Pegasus.IntegrationTests/IntakeWebTestSupport.cs` | `ProcessQueuedAsync` constructs `ProcessQueuedIntake` via `ActivatorUtilities` (Web no longer registers it) and always dispatches; `Landing` recognises `/Upload/Status/`. |
| `tests/Pegasus.IntegrationTests/MailboxIntakeIntegrationTests.cs`, `InstructionDraftWebTests.cs`, `StagedArtifactReconciliationFunctionTests.cs`, `AzureBlobIntakeArtifactStoreTests.cs`, `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` | Mechanical: processor construction, removed `ReceiveForProcessingAsync` fake, replay assertion follows the status page, new blob dependency-translation test. |
| `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` | Write p95 budget restored to 3 s because the request now only stages; drain is measured separately. |

### Documentation

| File | Change |
| --- | --- |
| `docs/frd/frd-02-intake-and-source-identity.md` | New normative paragraph: Web stages pending and never processes; Worker is sole owner; duplicate delivery must not duplicate side effects; staff can inspect Received/Processing/Complete/Failed; failure wording bounded. |
| `docs/current-architecture.md` | Intake diagram now shows stage → Pending → dispatcher → queue → Worker; status query listed. |
| `docs/design/README.md` | Intake receipt and upload row describes the status redirect, four states, refresh, and links; lists `Upload`/`UploadStatus` pages. |
| `docs/operations.md` | `/Upload` POST is the staging caller through `ReceiveIntake`; Worker owns processing; no deployment/live claim. |
| `docs/temp-plans/simpli-009.md` | Added in `195154f9`, **removed** in `caad05e8`: the content duplicates this ticket's plan document and `scripts/Test-MarkdownPlacement.ps1` (CI) rejects new Markdown outside `docs/{prd,frd,adr}`. |

## Deviations from the plan / files survey

- `IntakeExceptionPolicy.IsRecoverable` was left untouched; the narrower taxonomy lives in `ProcessQueuedIntake.IsTransientProcessingFailure` (the files survey allowed either).
- Plan step 3 named "HTTP" as transient; the implementation covers Azure dependency faults through `IntakeDependencyUnavailableException` translation in the blob adapter rather than catching `HttpRequestException` in Core.
- The negative "Web cannot resolve the processor" assertion is in `QdosIntakeWebTests` (against the real Web host) rather than `tests/Pegasus.ArchitectureTests/WorkerCompositionTests.cs`; the positive Worker assertion there is unchanged.
- No new ADR (the files survey conditioned one on planning concluding this was a new architectural decision; it is implementation of accepted queue architecture + FRD-02). No schema change, no host.json change, no applied-migration edit, no deployment.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` and `tests/Pegasus.PerformanceTests/FailureInjectionTests.cs` were listed as definite test impact but are unchanged in the diff.

## Verification on the merged branch (2026-08-17)

- `dotnet restore Pegasus.slnx`; `dotnet build Pegasus.slnx --configuration Release --no-restore`: 0 warnings, 0 errors.
- `Pegasus.Core.Tests`: 572 passed.
- `Pegasus.ArchitectureTests`: 94 passed, 0 failed (the previously noted local `Test-AzureDeploymentPlan.ps1` failure no longer reproduces on current dev).
- Focused `Pegasus.IntegrationTests` (Recovery, QdosIntakeWeb, QdosAllocationRecovery, IntakeWebNegative, MailboxIntake, InstructionDraftWeb, AzureBlobIntakeArtifactStore, StagedArtifactReconciliation, AzureSqlRuntimeRole): see the review scratch for the result appended after the run.
- Pre-merge evidence (2026-08-13, commit `195154f9`, recorded in the ticket's proof draft): full IntegrationTests 529 passed / 16 skipped / 0 failed.

## Not claimed

No deployment, no live Worker execution, no cloud write. The bicep role removal is source only.
