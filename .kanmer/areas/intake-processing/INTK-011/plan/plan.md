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

## Design revision — 2026-08-19 (during implementation)

Step 2's original plan (reuse `IIntakeWorkStore.ScheduleReevaluationAsync` to
defer a pending group member, with `RetryDelays`/`AttemptCount` bounding) was
built, then **disproven by its own integration test**: `ScheduleReevaluationAsync`
moves the work item's state back to `Pending`, forcing a future re-claim
through `ProcessQueuedIntake.ExecuteAsync`'s artifact-reading branch — but by
the time a receipt's group outcome is evaluated, `TryDeleteCompletedStagingAsync`
has already deleted its staged artifact (it runs unconditionally right after
`CompleteProcessingAsync`, before image automation). Re-arming produced a
reproducible terminal `staged_artifact_integrity_failure`, confirmed against
real LocalDB. Corrected design (implemented, all tests green):

- `ApplyImageIntakeWithDeferralAsync` no longer touches the work item at all.
  When `GroupPending`, it just skips this pass's `SynchronizeUnidentifiedAsync`
  call — the work item is left exactly as `CompleteProcessingAsync` set it
  (`Completed`), which is cheap and safe to revisit later: a later
  `ProcessQueuedIntake.ExecuteAsync(stagedReceiptId)` call finds nothing to
  claim and takes the existing "claimed is null" replay branch, which re-runs
  automation without ever touching staging.
- New `IProcessQueuedIntake` interface (implemented by `ProcessQueuedIntake`)
  so `ReconcileGroupedImageIntake` can re-drive that safe replay branch
  without depending on every concrete adapter the full class requires — one
  real second caller (the reconciliation sweep), not a speculative abstraction.
- `IIntakeWorkStore.ScheduleReevaluationForReceiptAsync` (step 3's planned
  mutating join) was replaced by a **read-only** `FindStagedReceiptIdForReceiptAsync`
  — the reconciliation sweep only needs the staged-receipt id to call
  `IProcessQueuedIntake.ExecuteAsync` directly; it must never move a
  `Completed` work item back to `Pending` for the same staging reason above.
- Bounded retry / poison escape moved from work-item `AttemptCount` to
  `ReconcileGroupedImageIntake.EscapeAfter` (2h wall-clock age of the
  receipt's own `ProcessedAtUtc`, the same longest delay already used by
  `ProcessQueuedIntake.RetryDelays`): past that age, the reconciliation sweep
  registers Unidentified directly instead of retrying again. This still means
  no member is ever left invisible forever, and it still never touches the
  work item.

Root cause and reuse inventory in `files.md` remain accurate. `enter-review`'s
`post-implementation-report` will state the corrected design as delivered.

## A discovered, out-of-scope gap: ordinal-0 group lookup

While tracing the race, confirmed (with a green concurrency test either way)
that `EfIntakeSubmissionGroupStore.FindForMemberSourceAsync`
(`src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs:37-53`)
can never recognise an **ordinal-0** member as belonging to a group from its
own identity: `GroupedIntakeMemberToken.Create` gives ordinal 0 the bare
submission token (no `:N` suffix), and `FindForMemberSourceAsync` requires
that suffix to resolve a group. An ordinal-0 receipt's own automation pass
therefore always falls through to the single-receipt path
(`ImageIntakeAutomation.ApplyAsync`, below the `TryApplyGroupAsync` call),
never `TryApplyGroupAsync` — this is why the production PNG (ordinal 0)
registered solo while the JPEG (ordinal 1) went through the group path and
lost the race. It also means `ReconcileGroupedImageIntake` cannot recognise an
ordinal-0 straggler as a group candidate (`group is null` short-circuit) if
ordinal 0 were ever the one that lost a race instead.

This is a distinct, pre-existing defect in the token-encoding scheme (a bare
token is inherently ambiguous between "ordinal-0 group member" and "genuinely
standalone upload" — not fixable by a small patch to `FindForMemberSourceAsync`
alone) and is **not what the production evidence for this ticket exhibited**
(the stranded member was ordinal 1). Left out of scope deliberately, per the
plan's "explicitly out of scope" discipline, and flagged here plus in the
post-implementation report for a follow-up ticket rather than silently
shipped as if this fix covers every ordinal.

## Simplification pass — 2026-08-19

Ran two independent lenses over the branch diff before PR: a manual
reuse/simplification/efficiency/altitude read of every changed file, and the
`code-simplifier` agent over the same diff.

Manual review findings: no dead code, no leftover diagnostics (temporary
debug-diagnostic blocks used while chasing the staged-artifact and
`IntakeMutationHistory`-uniqueness bugs during test development were removed
before commit), naming consistent with existing conventions
(`ApplyImageIntakeWithDeferralAsync` alongside `ApplyImageIntakeAutomationAsync`,
`ReconcileGroupedImageIntake` alongside `ReconcileStagedArtifacts`/
`ReconcilePoisonedIntakeWork`). No new abstraction without a second concrete
caller (`IImageIntakeAutomation` already had one; `IProcessQueuedIntake` gets
its second real caller in this same ticket). No new "group outcome" schema —
confirmed unnecessary by the corrected design above.

code-simplifier agent findings: recorded once the agent's run completes;
either "no changes" with reasoning, or applied fixes, both to be listed here
verbatim before this ticket leaves Review.

## Simplification pass — code-simplifier agent results, 2026-08-19

Dispatched the `code-simplifier` agent over the full branch diff (production
code + tests). It applied six behaviour-preserving fixes and verified with a
clean `dotnet build` (0 warnings/errors) plus `Pegasus.Core.Tests` (685
passed) and `Pegasus.ArchitectureTests` (97 passed) — I independently
reconfirmed all of these plus the SQL Server-tagged integration filter after
its changes (see post-implementation-report). Applied fixes:

1. `DurableIntake.cs` — deleted the `ApplyImageIntakeWithDeferralAsync`
   wrapper: it only re-labelled `ImageIntakeAutomationOutcome(Receipt,
   GroupPending)` as a tuple through a one-line method with no other logic.
   Both call sites now read `imageOutcome.Receipt`/`.GroupPending` directly;
   the load-bearing `<remarks>` explaining why deferral must never touch the
   work item moved onto `ApplyImageIntakeAutomationAsync`, now the single
   documented owner. Net −14 lines, one fewer indirection.
2. `ImageIntakeAutomation.cs` — the new record's `<param>` docs had been
   inserted under `IImageIntakeAutomation`'s existing `<summary>`, silently
   reassigning that doc comment to the record. Gave the record its own
   summary, restored the interface's, trimmed the `GroupPending` doc's
   restatement of caller behaviour (now owned by `DurableIntake`), dropped
   the redundant explicit `GroupPending: false` argument (`new(receipt)`
   reads the same via the default).
3. `ReconcileGroupedImageIntake.cs` — **real bug**: `IRegisterUnidentified`
   was optional (`= null`), but the escape path incremented `Escaped` even
   when it was null, so the log would report an escape that never happened.
   It is registered in `Pegasus.Infrastructure/DependencyInjection.cs` for
   both hosts, so made it a required constructor parameter and removed the
   null branch. Also fixed two stale doc claims: a reference to the deleted
   `ApplyImageIntakeWithDeferralAsync`, and `EscapeAfter` claiming to "reuse"
   `ProcessQueuedIntake.RetryDelays` when it is a literal (now says it
   matches the longest delay instead of falsely claiming reuse).
4. `EfIntakeWorkStore.cs` — inlined `ScheduleReevaluationCoreAsync` back into
   `ScheduleReevaluationAsync`; the extraction had exactly one caller and no
   second was added, leftover from iteration. Diff is now purely the
   additive `FindStagedReceiptIdForReceiptAsync`.
5. `GroupedImageIntakeConcurrencyTests.cs` — the local `ProcessWithRetryAsync`
   and `ImmediateEnqueuer` carried two copies of the same SqlException-1205
   retry loop; unified into one `ProcessWithDeadlockRetryAsync` helper.
   (`IntakeWebDriver.ImmediateIntakeWorkEnqueuer` was considered but doesn't
   fit — it binds one processor to one scope and has no retry.)
6. `StagedArtifactReconciliationFunctionTests.cs` — added
   `UnreachableRegisterUnidentified`, matching the file's existing
   unreachable-fake convention, now that the parameter is required.

Reviewed, no change needed: the migration's grant-only shape/comment/`Down`
ordering matches `20260819180000_GrantEvaHandoffDownloadOperations.cs`
exactly; the reconciler correctly reuses `ImageIntakeLifecycleRules.IsImageOnlyMaterial`
and `ProcessIntake.BuildUnidentifiedRegistrationRequest` rather than
duplicating either; no debug/dead code found elsewhere.
