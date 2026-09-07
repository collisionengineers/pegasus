# Post-implementation report — INTK-061

## State

Implementation drafted on `INTK-061-intake-recovery`, worktree
`.worktrees/intk-061`, based on origin/dev
`3da60bd0c270111d5168dc17246dc831882108ea`. Source is frozen for the root
verification owner. No application build, test, snapshot, provider call, cloud
write, commit, PR, Review move, or delivery claim has been made by this worker.

## Changes and reuse

- `RetainIncomingArtifact` passes the operation identity to
  `EfPublicUploadRetentionStore`. An intake identity conditionally claims only
  its receipt/asset row; public uploads claim only their occurrence. Worker
  grants are unchanged. The existing SQL-role test now exercises the actual EF
  store under the restricted Worker user and checks wrong-receipt refusal and
  single claim.
- `ProcessQueuedIntake` records an evaluation before routing but acknowledges
  completion only after required association, allocation, Triage and holding
  writes. The existing work row retains pending evaluation identity across a
  transient retry; retries read that receipt rather than reprocess staging or
  allocate a new evaluation. Explicit staff reevaluation remains a new revision.
  Required write exceptions are no longer converted into advisory success.
- `AllocateIntake` refuses new-case allocation for a recorded unique match even
  before its association lands. The SQL caller test injects a first association
  timeout and proves retry uses the same evaluation and one existing case.
- `ImageIntakeAutomation` carries the group-origin registration and canonical
  reason from existing `ImageIntakeGroupRoutingPolicy`. Every member converges
  on the same operation identity and one U-reference. The existing group store
  selects one oldest eligible member per unresolved image group before paging.
  The sweep replays first, then uses a group-level technical outcome for a
  still-pending group older than its existing two-hour bound. A group with a U
  outcome leaves the candidate set.
- OCR output completion stores its existing output JSON but leaves paired
  external work Pending. Analysis completion is recorded in that JSON only
  after a useful analysis outcome. Source-unavailable/conflict/exception paths
  retain output and schedule the existing bounded retry. Redelivery never
  resubmits retained output to the provider. The operation/current receipt
  version gives analysis a bounded, stable key without truncation collisions.
- FRD-02 and FRD-05 state the above completion and recovery requirements. The
  obsolete deferred-OCR statement is replaced with the operator-authorized
  Document Intelligence boundary, not a claim that cloud activation is proved.

## Durable retry owners and production callers

`UnifiedWorkFunction` still ignores the returned enum, intentionally. Queue
acknowledgement is not the fix. On a transient required write failure,
`FailProcessingAsync` now executes before final work completion and persists
`RetryScheduled`, a due time and a cleared lease. The existing
`PendingWorkRecoveryFunction -> DispatchPendingWork -> DispatchPendingIntakeWork`
selects due Pending/RetryScheduled rows and publishes their stable identifier.
A host interruption leaves the existing expiring processing lease recoverable.

A waiting image group is separately owned by the existing grouped-image sweep:
its completed source work is cheap to replay, and the group query is durable and
bounded. OCR is similarly owned by the existing external-work dispatcher;
`CompleteAsync` leaves its paired row Pending until `CompleteAnalysisAsync`.
There is no new queue, worker, timer, schema, migration, package, or permission.
No change to `IntakeFunctions.cs` was necessary.

## Focused tests added or strengthened

- `AzureSqlRuntimeRoleMigrationTests.WebRuntimeCanInsertPairedOcrWorkRowsButCannotProcessOcr`:
  actual restricted Worker custody claim, wrong receipt and duplicate claim.
- `QdosAllocationRecoveryTests.UniqueExistingCaseAssociationBypassesNewAllocationExactlyOnce`:
  transient unique-association failure, no duplicate allocation, durable retry.
- `QdosAllocationRecoveryTests.DestinationFailureStaysDurableAndRetriesTheSameEvaluation`:
  allocation-start timeout remains retryable and uses one evaluation.
- `AutomaticImageIntakeTests.ConflictingImageMembersReturnOneGroupOriginAndTheConflictingReason`:
  both members yield identical group origin/key and conflicting reason.
- `GroupedImageIntakeConcurrencyTests.UnreadableImageGroupHasOneUnidentifiedOutcomeAndLeavesTheSweep`:
  real group registration creates one U row and no Image Intake.
- `GroupedImageIntakeConcurrencyTests.ReconciliationRecoversAStrandedGroupMember`:
  newer unrelated NeedsSorting documents cannot starve an older group with
  a candidate limit of one.
- `IntakeOcrTests`: analysis failure, typed incomplete outcomes and crash after
  provider completion recover from retained output without another provider call.
- `OcrIntakeRecoveryTests.RetainedOutputKeepsExternalWorkPendingUntilAnalysisIsApplied`:
  real SQL paired-work state remains Pending across the interrupted boundary.

Known interface consumers and existing test doubles were updated, not duplicated.
Existing fixture bytes and provider-boundary fakes are reused. No corpus changes.

## Validation and stop condition

`git diff --check` completed with exit code 0 on this worktree. Runtime
verification is **NOT RUN** by this worker, per EPIC-014's one heavy verification
owner. Root has the exact focused Core/Integration filters and will supply
compiler/test evidence before source is committed or a PR/Review handoff occurs.
No PASS or Done claim follows from the static check.

## Scope and simplification

The existing `ImageIntakeGroupRouting.cs` proved the correct owner for the reason
mapping; this is a bounded file-map refinement from the planned possible
`ProcessIntake` helper and was explicitly reported to root. No new policy owner
was introduced. `EfIntakeReceiptStore`, `IntakeContracts`, and Worker required no
changes. Principal/domain expansion, Triage auto-linking, null CaseType,
mailbox/wipe policy, deployment and provider writes remain separately owned.

The simplification pass removed superseded advisory-failure commentary and the
unused failed-Triage outcome, retained existing stores/dispatchers and current
columns, and introduced no parallel pipeline. Stop after root validation and
independent-review preparation; do not merge this ticket's own PR.

## Verification attempts supplied by root

1. Locked restore passed. First Release build failed with exit code 1 after
   18 seconds: 27 parser diagnostics from the single LINQ query range identifier
   `group` in `EfIntakeSubmissionGroupStore.cs` (CS1001/CS1525 cascade). No tests
   ran. Corrected that contextual-keyword identifier consistently to
   `submissionGroup`; no semantics changed. Static query-context inspection and
   `git diff --check` then completed with exit code 0. Root owns the build rerun;
   the failed attempt remains recorded regardless of its later result.
