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
