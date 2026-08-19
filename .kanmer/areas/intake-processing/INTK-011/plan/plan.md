## Goal

Fix the race in `files.md`: a group member must never terminal-decide through
the instruction/Unidentified fallback while its group registration attempt
merely lost a transient concurrency race. Reuse the existing durable-work
retry/defer conventions; add a reconciliation pass for stragglers; fix the
confirmed Worker grant gap. No new schema for a "group outcome" record — the
existing per-receipt operation-key idempotency plus a bounded retry is
sufficient and is the minimal change.

## Steps

1. **`ImageIntakeAutomation`: report pending instead of silently absorbing failure.**
   - File: `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs`.
   - Reuse: the existing `TryApplyGroupAsync`/`TryRegisterAndAssociateAsync`
     control flow, `IntakeExceptionPolicy.IsRecoverable`, the existing
     `ImageIntakeGroupRoutingDecision` enum — no new taxonomy.
   - Change `IImageIntakeAutomation.ApplyAsync` to return a small wrapper
     `ImageIntakeAutomationOutcome(IntakeReceipt Receipt, bool GroupPending = false)`
     instead of a bare `IntakeReceipt`. `GroupPending` is true only when this
     receipt is a group member and its own outcome did not complete this pass:
     waiting for sibling members/recognition, or its own
     `TryRegisterAndAssociateAsync` call returned null (failed/threw
     recoverable). `GroupPending` is always false for the non-group path and
     for a group that resolved to `RouteToUnidentified`/`TechnicalFailure`
     (those are legitimate resolved outcomes, unchanged, INTK-007's scope).
   - Update `NoImageIntakeAutomation` to match the new return shape (`Pending: false`).
   - This is the ONE caller of the interface outside DI/tests
     (`ProcessQueuedIntake`), so this is not a new abstraction — it is
     enriching the existing single-caller contract with the signal it was
     always missing.

2. **`ProcessQueuedIntake`: defer via the existing durable-work mechanism.**
   - File: `src/Pegasus.Core/Intake/DurableIntake.cs`.
   - Reuse: the existing `RetryDelays` array and `isFinalAttempt =
     workItem.AttemptCount >= RetryDelays.Length` pattern already used for
     reader faults in this same method (`DurableIntake.cs:506`); the existing
     `IIntakeWorkStore.ScheduleReevaluationAsync`.
   - In both places `ApplyImageIntakeAutomationAsync` is called (the fresh
     "claimed" branch and the "claimed is null" replay branch), read the new
     `GroupPending` flag. When pending and the attempt budget is not
     exhausted: call `workStore.ScheduleReevaluationAsync(stagedReceiptId,
     dueAtUtc)` with the SAME `RetryDelays` backoff, skip
     `SynchronizeUnidentifiedAsync` this pass, and return
     `QueuedIntakeProcessingOutcome.RetryScheduled`. When pending and the
     budget IS exhausted (the existing poison escape), fall through to
     `SynchronizeUnidentifiedAsync` exactly as today — a receipt whose group
     genuinely never resolves still gets a U-reference eventually, so nothing
     is newly stranded.
   - The replay branch does not have `workItem`/`AttemptCount` in scope;
     fetch it via the existing read-only `workStore.FindWorkItemAsync(stagedReceiptId, ...)`
     before deciding (already a defined port method, no new one needed there).
   - `ScheduleReevaluationAsync`'s own guard (`item.State == "processing" &&
     leaseExpiresAtUtc > dueAtUtc` throws) cannot fire here: by this point in
     both branches the work item is already `Completed`
     (`CompleteProcessingAsync` already ran). Wrap the call in the same
     `IntakeExceptionPolicy.IsRecoverable` catch used everywhere else in this
     method; on failure to even schedule, fall through to the existing
     Unidentified path rather than losing the receipt silently a second way.

3. **Reconciliation for stragglers — extend the existing function, no new store.**
   - New file: `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs`.
   - Reuse: `IIntakeReceiptQueries.ListAsync(IntakeDecision.NeedsSorting, page,
     pageSize, ...)` (existing paged query), `ImageIntakeLifecycleRules.IsImageOnlyMaterial`
     (existing rule, one owner), `IIntakeSubmissionGroupStore.FindForMemberSourceAsync`
     (existing).
   - Add `IIntakeWorkStore.ScheduleReevaluationForReceiptAsync(Guid
     intakeReceiptId, DateTimeOffset dueAtUtc, CancellationToken)` to
     `IntakeContracts.cs`/`DurableIntake.cs`'s `IIntakeWorkStore` interface,
     implemented in `EfIntakeWorkStore.cs` by mirroring the exact
     receipt-id→staged-receipt-id join `EfIntakeMutationStore.ScheduleReevaluationAsync`
     already performs inline (`EfIntakeMutationStore.cs:212-223`), then
     delegating to the same logic as the existing `ScheduleReevaluationAsync(stagedReceiptId, ...)`.
   - `ReconcileGroupedImageIntake.ExecuteAsync(maximumItems, cancellationToken)`:
     page `NeedsSorting` receipts, keep only image-only material that resolves
     to a group via `FindForMemberSourceAsync`, and call the new
     `ScheduleReevaluationForReceiptAsync` with `dueAtUtc = now` for each. Idempotent
     and safe to run repeatedly (a receipt whose group genuinely still isn't
     resolved just gets re-armed and stays `NeedsSorting`; nothing duplicates
     because registration is receipt-operation-key-idempotent).
   - Wire into `src/Pegasus.Worker/IntakeFunctions.cs`:
     `StagedArtifactReconciliationFunction.RunAsync` also calls the new use
     case on its existing timer trigger — no new schedule app-setting, per the
     ticket's explicit instruction to extend the existing function.
   - Register in `src/Pegasus.Infrastructure/DependencyInjection.cs`.
   - This is the mechanism that recovers the production straggler receipt
     `5b4c8cbd-c40a-43a0-b5c0-73c1c447ada2` into `G6KDL-01` post-deploy — no
     manual SQL.

4. **Grants: fix the confirmed Worker gap.**
   - New migration `Grant<Timestamp>WorkerIntakeSubmissionGroupRead` under
     `src/Pegasus.Infrastructure/Persistence/Migrations/`, mirroring
     `20260819180000_GrantEvaHandoffDownloadOperations.cs`'s shape exactly:
     `GRANT SELECT ON OBJECT::[dbo].[IntakeSubmissionGroups] TO
     [pegasus_worker_runtime_role];` and the same for
     `IntakeSubmissionGroupMembers`; `Down()` revokes. Comment cites this
     ticket, `ImageIntakeAutomation.cs:121,129`, and the wrong "Worker never
     references either table" comment it corrects.
   - Update `scripts/Invoke-AzureDatabaseBootstrap.ps1`'s census
     (`~line 246-254`) to add the two `pegasus_worker_runtime_role|G|SELECT|...`
     entries and correct the comment.
   - No new table/column — `Test-MigrationGrants.ps1` is unaffected (the
     tables already have a Web grant recorded); `Invoke-AzureDatabaseBootstrap.ps1`
     census and `Test-AzureDeploymentPlan.ps1 -Mode Local` are the checks that
     matter here.

5. **Tests.**
   - `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs`:
     update the harness/assertions for the new `ApplyAsync` return shape
     (`.Receipt`); add a case where one member's `TryRegisterAndAssociateAsync`
     is made to fail (fake register store throws a recoverable exception for
     one receipt) and assert `GroupPending == true` for that receipt and
     `Decision` unchanged at `NeedsSorting` — never `image_intake_registered`
     or a fabricated fallback.
   - `tests/Pegasus.IntegrationTests/GroupedIntakeWebTests.cs` or a new
     `tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs`:
     the actual race, following `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`'s
     style — real LocalDB via `IntakeWebApplicationFactory`, submit a 2-member
     image group (one readable VRM), then `Task.WhenAll` two independent
     `ProcessQueuedIntake.ExecuteAsync` calls (one per member's staged receipt
     id) each in its own DI scope with the standard SqlException 1205 retry
     wrapper. Assert: no member ever reaches `needs_sorting` with the generic
     instruction-fallback reason as its FINAL state after redispatching any
     `RetryScheduled` outcomes to completion; both members end up registered
     (their own `G6KDL-0n` Image Intakes) or otherwise consistently resolved;
     repeat the parallel pair across several fresh groups in the same test (a
     loop of at least 10 fresh 2-member groups) so the race is exercised
     meaningfully rather than once.
   - New reconciliation test: seed a stale group exactly like production (one
     member registered, sibling stuck at `NeedsSorting` with the
     instruction-fallback reason — simulate this directly by not going through
     the race, just by constructing that DB state), run
     `ReconcileGroupedImageIntake.ExecuteAsync`, redispatch the resulting
     `RetryScheduled` work item to completion, and assert the straggler is now
     registered into the same VRM's Image Intake sequence.

6. **Commands.**
   - `dotnet build`
   - `dotnet test tests/Pegasus.Core.Tests -c Release`
   - `dotnet test tests/Pegasus.IntegrationTests -c Release --filter "FullyQualifiedName~GroupedIntake|FullyQualifiedName~ImageIntake|FullyQualifiedName~IntakePersistence|FullyQualifiedName~QdosIntake|FullyQualifiedName~IntakeWebNegative"`
   - `pwsh ./scripts/Test-MigrationGrants.ps1`
   - `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`
   - State honestly anything that cannot run in this environment.

## Explicitly out of scope

- Upload pages / `site.js` (INTK-010), Triage/Unidentified pages (INTK-009).
- Changing the ImageIntake aggregate to hold multiple origin receipts —
  confirmed by the INTK-006 test as intentionally one-row-per-member.
- INTK-007's grouped `conflicting_vrms` Unidentified U<n> contract — untouched,
  `RouteToUnidentified`/`TechnicalFailure` group outcomes are unchanged by this fix.
- Any change to `IntakeDecisionPolicy`/fail-closed/INT-28 accepted-bar rules.

## Simplification pass

To be run over the branch diff before PR, findings recorded here with a dated
heading, per repository convention.
