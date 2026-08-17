# Plan — SIMPLI-008 / SIMPLI-009: queued intake ownership and status

## Approach

Use one durable path: Web validates and stages every source as Pending, Worker dispatches and processes it, and Web observes a bounded staged-receipt status projection. Delete the inline path rather than preserving compatibility for disposable test data. Deliver both tickets in this explicitly authorised combined branch.

## Steps

1. Collapse IIntakeSubmission onto ReceiveIntake.ExecuteAsync returning ReceivedIntake; delete ProcessIntakeSubmission, processed dispositions, inline receive, and request-local completion polling.
2. Delete ReceiveForProcessingAsync and receive-time promotion to unleased Dispatched; keep claims, leases, retries, duplicate handling, and poison reconciliation.
3. Add processor-specific fault classification: cancellation propagates; integrity/invalid data fail terminally; explicit I/O, timeout, HTTP, retention and named concurrency failures use scheduled retries; everything else becomes terminal unexpected_intake_processing_failure.
4. Return a bounded outcome from ProcessQueuedIntake and log sanitized unexpected outcomes in IntakeWorkFunction.
5. Remove Web processor registrations and the unused Web intake-queue sender role; retain Web staging/blob access and Worker queue composition.
6. Add IQueuedIntakeStatusQueries with staged ID, safe source metadata, public status, processed receipt ID, case ID, and failure code. Map Pending/Dispatching/Dispatched/RetryScheduled to Received, Processing to Processing, Completed to Complete, and Failed to Failed.
7. Add authorised /Upload/Status/{id}: 404 unknown IDs; safe state copy; manual refresh; external-script two-second refresh only while nonterminal; case link when allocated, otherwise retained-receipt link when complete.
8. Redirect every successful manual Upload, including duplicates, to staged status. Preserve validation, antiforgery, staging errors, identity conflicts, and limits.
9. Separate Web submission from Worker dispatch/process in helpers. Cover stage-only Web, all status states, destinations, duplicate delivery, crash after stage, lease expiry, retry exhaustion, poison, unexpected failure, composition, permissions, and capacity.
10. Update FRD-02, design, current architecture, and source-level operations statements without deployment/live claims.
11. Run restore, Release build, focused tests, full tests, negative symbol searches, documentation checks, then write proof and obtain independent review.

## Verification

dotnet restore; dotnet build --configuration Release; focused Core/Architecture/Integration/Performance tests; dotnet test --configuration Release --no-build; negative searches for deleted inline symbols and Web processor callers; documentation validation.

## Risks / open questions

No open implementation decisions. Preserve the enqueue-before-mark race and do not conflate completed evaluation with later advisory failures.

## Simplification pass — 2026-08-17 (`/simplify` four lenses + `code-simplifier` agent, run over PR #385 during review; applied by claude-code)

Five independent read-only reviewers (reuse, simplification, efficiency, altitude, and the generic code-simplifier) were run over the net diff after the review blockers were fixed. Findings were deduplicated and applied where they did not change intended behaviour; the rest are routed below. **Two plan steps are amended as a result.**

### Plan amendments

- **Step 3 (fault taxonomy) — one policy, not three lists.** The processor now has exactly two classifiers, each the single place its exception types are named: `TerminalInputFailureCode(exception) → string?` (integrity, invalid data, source-identity conflict → their codes) and `IsTransientProcessingFailure(exception)` (named intake conflicts, `IntakeDependencyUnavailableException`, `IOException`, `TimeoutException`, `DbException`, **looking through `InnerException`** so EF's `DbUpdateException` wrapper classifies as its cause). The catch-all is guarded by the shared `IntakeExceptionPolicy.IsRecoverable` (so OOM/AccessViolation propagate as everywhere else) instead of a hand-rolled `is not OperationCanceledException`. `FailProcessingAsync` takes the code, not the exception. `IntakeExceptionPolicy.IsRecoverable` itself is untouched (it is a catch-safety gate, not a retry taxonomy).
- **Step 4 (bounded outcome + Worker log) — rethrow after persist, no result record.** The "carry the exception in a result record so the Worker can log it" shape was a bandaid: every other Core result the Worker consumes is bounded data, and Core has no logging port. The simpler, deeper mechanism: persist the terminal `unexpected_intake_processing_failure` (staff see Failed immediately), then `throw;`. The Functions host logs the fault in full with its native failed-invocation telemetry; the redelivery that follows finds the item failed and no-ops (`ClaimProcessingAsync` returns null → `NoOp`), so the message is consumed, not poisoned. `QueuedIntakeProcessingOutcome` loses `UnexpectedFailed`; `IntakeWorkFunction` is unchanged from `origin/dev` and drops out of the diff. Test `UnexpectedProcessingFailureIsPersistedThenRethrown` proves the persist → throw → NoOp sequence.

### Applied in PR #385 (behaviour-preserving)

| Where | Change | Lens |
| --- | --- | --- |
| `DurableIntake.cs` | `ReceiveIntake.ExecuteAsync` inlined (the `ReceiveCoreAsync` forwarder existed only for the removed `processInline` flag); `using` order; `QueuedIntakeStatusKinds.FromWorkState(IntakeWorkState)` added beside the enum so the "every waiting state reads Received" collapse is Core policy, explicit and fail-closed on unknown states. | simplification, altitude |
| `EfQueuedIntakeStatusQueries.cs` | One round-trip: case link folded into the projection as a correlated subquery (same pattern as `EfImageIntakeStore`); state mapped through `EfIntakeWorkStore.ParseState` (now `internal`) → `IntakeWorkState` → Core kind, so the persisted state strings live in one table. | reuse, efficiency, altitude |
| `AzureBlobIntakeArtifactStore.cs` | One `DependencyUnavailable(RequestFailedException)` factory; upload path flattened by extracting `UploadOrVerifyAsync` (verify-path faults still wrapped by the outer catch). | reuse, simplification |
| `EfIntakeWorkStore.cs` | `_ = x ?? throw` discard idiom → plain `if (x is null) throw`. | simplification |
| `Upload.cshtml.cs` / `UploadStatus.cshtml(.cs)` | Duplicate notice carried as `?duplicate=true` on the redirect (the convention `/Received/{id}` already uses; the test driver already parses it) instead of one-shot `TempData`, which vanished on the page's own 2 s auto-refresh. `Message` split into `StateMessage` + duplicate prefix (no ternary-plus-switch). Refresh link is a tag helper preserving the flag. `InstructionDraftWebTests` replay assertion restored to the stronger "was already received". | reuse, simplification, altitude |
| Tests | One `ImmediateIntakeWorkEnqueuer` (internal, in `IntakeWebTestSupport`) instead of three; `IntakeWebDriver.CreateProcessor(services)` centralises "Web no longer registers `ProcessQueuedIntake`" (was 9× `ActivatorUtilities`); `IntakeWebDriver.DrainStagedAsync` is the one dispatch-until-evaluation loop, used by `ProcessQueuedAsync` and `AllocationTestData.SubmitAndProcessAsync` (which now returns `Guid`; unread `ProcessedSubmission.IsDuplicate` deleted); dead create-screen/case landing branches, `CreateScreenReceiptId`, `CaseId(UploadResult)`, `IsCreateScreen` and the legacy `receiptId`/`received`/`queuedReceiptId` query keys (no producer in `src/`) removed — `UploadLanding` is `(StagedReceiptId, IsDuplicate)`; the four-case fault theory split into a straight-line transient theory (`io`, `dependency`, `wrapped-database` → `RetryScheduled`, code `intake_processing_failure`) and one unexpected fact; `<h1>Received</h1>` asserted instead of the always-true `"Received"`. | reuse, simplification, efficiency |

### Considered, deliberately not applied here

- `IIntakeSubmission` (one implementation, two Web callers) is a leftover abstraction — fold Upload/MCP onto `ReceiveIntake` in a follow-up chore; not worth widening this PR.
- Adapter-wide fault naming (blob adapter translates only read/upload; `FileSystemIntakeArtifactStore` throws raw `IOException`; EF stores raw SQL faults) so Core would match only intake types — right direction, follow-up.
- `DependencyDirectionTests` fact asserting Web composes no queue client and no `ProcessQueuedIntake` (today only asserted inside a feature test) — follow-up.
- Auto-refresh policy: fixed 2 s while `retry_scheduled` (up to 2 h) reads as Received is wasteful and slightly dishonest; project `DueAtUtc` and clamp the interval, and/or a fifth staff-visible state — routed to the SIMPLI-008 follow-up ticket with T1 (auto-associated receipts link to the receipt, not the case).
- Refresh via the shared `data-refresh-form` component (feedback + double-submit guard) rather than an anchor — cosmetic, note only.
- Unused `client` locals in two `RecoveryTests` facts; `Model.Status.Status` stutter — convention, left.

### Route to simplification (what worked, for AGENTS.md)

The over-engineering here was not in the product code so much as in the *shape around it*: a result record to smuggle an exception past a design constraint, three exception lists where the language gives one, a second copy of a state-string table, a fresh TempData convention beside an existing route-value one, three copies of a test fake. Every one of these was cheaper to see with a lens that asks "what already exists?" and "is this a special case on top of a mechanism?" than with a correctness review. Run the pass *before* the PR is opened, on the branch's own diff, and record it here — not as a separate review stage.
