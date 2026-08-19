## Root cause (verified in code, with citations)

Two independent per-member durable work items process the two group members
concurrently. Each one, on its own, runs the full pipeline and reaches a
**redundant, uncoordinated** attempt to apply the *whole group's* outcome —
there is no single owner of "apply this group's outcome once." The concrete
break is a **swallowed, non-retried failure**, not the per-receipt
`NeedsSorting` decision itself (that part is correct/expected).

1. **Per-receipt evaluation always lands on `NeedsSorting` first — by design.**
   `ProcessIntake.AssessAsync` (`src/Pegasus.Core/Intake/ProcessIntake.cs:436-476`)
   never runs a mail route for a bare image (no transport evidence), so
   `EstablishPrincipalContext` returns `null` and the method returns
   `IntakeDecision.NeedsSorting` with reason *"No accepted intake route
   established the principal for automatic case creation."* — the exact text
   in the production evidence for both receipts. This is committed durably by
   `receiptStore.StoreAsync` (`ProcessIntake.cs:260-262`) for **every**
   image-only receipt, group member or not. `RegisterUnidentifiedIfTerminalAsync`
   (`ProcessIntake.cs:278-290`) deliberately skips registering it as
   Unidentified here — `IsUnidentifiedEligible` (`ProcessIntake.cs:301-307`)
   excludes image-only `NeedsSorting` material so `ImageIntakeAutomation` gets
   first chance. **This step is not the bug** — it is the designed transitional
   state the rest of the ticket's "must not terminal-decide" language refers to
   becoming permanent.

2. **Every member's own work item redundantly re-applies the whole group.**
   `ProcessQueuedIntake.ExecuteAsync` (`src/Pegasus.Core/Intake/DurableIntake.cs:415-583`)
   calls `ApplyImageIntakeAutomationAsync` → `ImageIntakeAutomation.ApplyAsync`
   (`src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs:47-114`), which — because
   `groupStore` is supplied — calls `TryApplyGroupAsync` (`ImageIntakeAutomation.cs:116-238`).
   `TryApplyGroupAsync` has **no lease/lock over the group**: whichever member's
   work item happens to run it, if it observes every member's receipt already
   persisted (`receiptQueries.FindBySourceIdentityAsync` for each member,
   `ImageIntakeAutomation.cs:141-155`), computes the group's routing decision
   and **loops over every image member** attempting registration
   (`ImageIntakeAutomation.cs:218-227`):
   ```
   foreach (var (memberReceipt, suggestions) in scans)
   {
       await TryRegisterAndAssociateAsync(memberReceipt, routing.NormalizedRegistration, suggestions, activity, routing.Decision, cancellationToken);
   }
   ```
   Both members' work items do this independently and concurrently, so the
   group's single VRM registration is attempted from (at least) two directions
   at once for both receipts.

3. **The per-member concurrency conflict is silently dropped — no retry, ever.**
   `EfImageIntakeStore.RegisterAsync` (`src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs:37-195`)
   runs under `IsolationLevel.Serializable` and allocates the next Image Intake
   Reference sequence from the single shared `ImageIntakeSequences` row for the
   VRM (`EfImageIntakeStore.cs:117-132`). Two concurrent registration attempts
   for the group's two different receipts genuinely contend on that one row;
   SQL Server aborts the loser with a serialization/deadlock exception. The
   loser is caught here:
   ```
   catch (Exception exception) when (IntakeExceptionPolicy.IsRecoverable(exception))
   {
       activity?.SetTag("image_intake.outcome", "registration_failed");
       return null;
   }
   ```
   (`ImageIntakeAutomation.cs:452-456`, inside `TryRegisterAndAssociateAsync`).
   **`IntakeExceptionPolicy.IsRecoverable` catches almost everything** (only
   excludes `OperationCanceledException`/`OutOfMemoryException`/
   `AccessViolationException`, `IntakeContracts.cs:529-532`) — a genuine,
   expected transient DB contention exception is swallowed exactly like a
   permanent bug would be. Critically, the caller at
   `ImageIntakeAutomation.cs:218-227` **discards the return value** of the
   failed call entirely — no retry is scheduled, no defer, nothing. The losing
   member's receipt is left exactly as `ProcessIntake` set it:
   `Decision = NeedsSorting`, reason "No accepted intake route..." —
   permanently, because the work item that produced it is already
   `Completed` (`EfIntakeWorkStore.CompleteProcessingAsync`,
   `DurableIntake.cs:519-524`, ran *before* automation) and nothing ever
   revisits a completed work item.

4. **The `Unidentified` catch-all is *also* silently swallowed, not merely
   skipped.** After `ApplyImageIntakeAutomationAsync` returns the receipt still
   at `NeedsSorting`, `ProcessQueuedIntake.ExecuteAsync` calls
   `SynchronizeUnidentifiedAsync` (`DurableIntake.cs:627-712`), which — for
   image-only `NeedsSorting` material — registers it as `Unidentified`
   (`DurableIntake.cs:631-647`). This call is *itself* wrapped in an
   `IntakeExceptionPolicy.IsRecoverable` catch with no retry
   (`DurableIntake.cs:641-644`). Given the same VRM-sequence-row contention can
   recur (both members' passes attempt this too), this call can also lose a
   race and be silently dropped — which is why the production JPEG receipt
   ended up with **neither** a registered ImageIntake **nor** a U-reference:
   both of its only two possible destinations were attempted at some point and
   both were lost to the same class of swallowed, unretried transient failure.

**Net:** there is no bug in the per-receipt `NeedsSorting` decision, and no bug
in the group *routing policy* (`ImageIntakeGroupRoutingPolicy.Evaluate`,
`src/Pegasus.Core/ImageIntake/ImageIntakeGroupRouting.cs`, is pure and correct
given its inputs). The defect is that **the only two mechanisms that can ever
give an image-only receipt a real destination (ImageIntake registration, or
Unidentified registration) are both "advisory, non-blocking" catches with no
retry**, on the mistaken premise that failure here is harmless because "the
receipt's own outcome stands regardless" — for ordinary receipts that is true
(a Case-created receipt already has a home), but for image-only material these
*are* the receipt's only possible homes.

## The aggregate model (do not invent a second store)

`ImageIntakeRecord`/`ImageIntakeEntity` (`ImageIntakeContracts.cs:19-29`,
`src/Pegasus.Infrastructure/Persistence/ImageIntakeEntities.cs`) is genuinely
**one row per origin receipt** — `RegisterAsync`'s existing-check
(`EfImageIntakeStore.cs:65-68`) is keyed on `OriginReceiptId`/source identity,
not VRM. This is confirmed as the *intended* shape by the existing INTK-006
test `OneEligibleCaseAssociatesEveryGroupMember`
(`tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs:246-263`),
which asserts `Assert.Equal(2, harness.Register.Requests.Count)` for a
2-member group — i.e. **every image member of a group already gets its own
Image Intake registration** (its own VRM-sequence reference, e.g. `G6KDL-01`,
`G6KDL-02`), not one shared row. "The group is the evidence unit" is realized
by every member independently reaching the *same* outcome (registered, or
associated to the same case), not by a shared aggregate row. The group
linkage that already exists is `IIntakeSubmissionGroupStore`
(`src/Pegasus.Core/Intake/GroupedIntake.cs:34-85`,
`EfIntakeSubmissionGroupStore.cs`) — `ListMembersAsync`/`FindForMemberSourceAsync`.
**This ticket does not change the aggregate shape.** The fix is: make sure
every member's own registration (or association, or Unidentified fallback)
actually completes — deterministically, atomically as a set — instead of
silently losing members to unretried contention.

## Files in scope

- `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` — `TryApplyGroupAsync`/
  `TryRegisterAndAssociateAsync`: report back whether a member's own outcome is
  still pending (retriable) instead of silently discarding the signal.
- `src/Pegasus.Core/Intake/DurableIntake.cs` — `ProcessQueuedIntake.ExecuteAsync`:
  gate `SynchronizeUnidentifiedAsync`'s immediate Unidentified registration
  behind the new "pending" signal; defer via the existing durable-work
  reevaluation mechanism (`IIntakeWorkStore.ScheduleReevaluationAsync`,
  `DurableIntake.cs:221-224`, `EfIntakeWorkStore.cs:464-487`) with the same
  bounded `RetryDelays`/`AttemptCount` shape already used for reader faults in
  this same method; fall through to the existing Unidentified registration
  once retries are exhausted (the existing poison-path escape).
- `src/Pegasus.Core/Intake/IntakeContracts.cs` — new
  `IIntakeWorkStore.ScheduleReevaluationForReceiptAsync(Guid intakeReceiptId, ...)`
  overload for the reconciliation sweep (receipt id → staged receipt id join,
  mirroring the join `EfIntakeMutationStore.ScheduleReevaluationAsync`
  already does inline at `EfIntakeMutationStore.cs:212-223`).
- `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` — implements
  the new overload.
- New: `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs` (or appended to
  `DurableIntake.cs`) — the reconciliation use case: pages
  `IIntakeReceiptQueries.ListAsync(IntakeDecision.NeedsSorting, ...)`, filters
  to `ImageIntakeLifecycleRules.IsImageOnlyMaterial` group members
  (`groupStore.FindForMemberSourceAsync`), and re-arms their durable work via
  the new work-store overload. This is the mechanism that recovers the
  production straggler receipt `5b4c8cbd-c40a-43a0-b5c0-73c1c447ada2` into
  `G6KDL-01` without manual SQL.
- `src/Pegasus.Worker/IntakeFunctions.cs` — extend
  `StagedArtifactReconciliationFunction.RunAsync` to also invoke the new
  reconciliation use case on the same existing timer trigger (no new schedule
  setting).
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — register the new
  reconciliation use case.

## Grants gap (Worker reads the group tables at runtime — confirmed)

`src/Pegasus.Infrastructure/DependencyInjection.cs:62-63` registers
`IIntakeSubmissionGroupStore` → `EfIntakeSubmissionGroupStore` once, shared by
both the Web and Worker composition roots (`Pegasus.Core`/`Pegasus.Infrastructure`
own policy; Web/Worker are both composition roots depending on both). The
Worker's `ProcessQueuedIntake` → `ImageIntakeAutomation.ApplyAsync` →
`TryApplyGroupAsync` (`ImageIntakeAutomation.cs:121,129`) calls
`groupStore.FindForMemberSourceAsync`/`ListMembersAsync` **at runtime, from the
Worker process** — directly contradicting the comment in
`scripts/Invoke-AzureDatabaseBootstrap.ps1:246-249` ("the Worker never
references either table"). That comment/census entry is wrong and is fixed:
add a grant-only migration granting `pegasus_worker_runtime_role` `SELECT` on
`IntakeSubmissionGroups` and `IntakeSubmissionGroupMembers` (mirroring
`20260819180000_GrantEvaHandoffDownloadOperations.cs`'s pattern), and update
the census in `Invoke-AzureDatabaseBootstrap.ps1` to match. (Production
evidence shows the PNG member's registration succeeded, so the Worker's actual
runtime access was not fully blocked — but the documented/tracked grant is
missing regardless, and `Test-MigrationGrants.ps1`/the census must reflect the
real caller.)

## Reuse inventory

- Durable retry/defer: `IntakeWorkItem` `DueAtUtc`/`AttemptCount`/lease shape,
  `IIntakeWorkStore.ScheduleReevaluationAsync`, the existing `RetryDelays`
  array and `isFinalAttempt` pattern already in `ProcessQueuedIntake.ExecuteAsync`.
  No new work-item state.
- Reconciliation: extend `StagedArtifactReconciliationFunction` (existing
  timer, existing schedule setting) rather than adding a new Function/schedule.
- Grant migration pattern: `20260819180000_GrantEvaHandoffDownloadOperations.cs`.
- Concurrency test style: `QdosAllocationRecoveryTests.DistinctParallelRetriesResolveToOneCaseAggregate`
  (`tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs:747-800`) —
  `Task.WhenAll` over independent DI scopes, retry loop on `SqlException.Number == 1205`.
- No new abstraction, no new table for "group outcome" — the group table
  already exists and already is the join.
