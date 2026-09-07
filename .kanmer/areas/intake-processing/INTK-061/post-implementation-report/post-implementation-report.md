# Post-implementation report — INTK-061

## State

Implementation verified by root on `INTK-061-intake-recovery`, worktree
`.worktrees/intk-061`, based on origin/dev
`3da60bd0c270111d5168dc17246dc831882108ea`. Source passed root's focused verification. This worker ran no application
build, test, snapshot, provider call, or cloud write. Root authorized the
commit/PR and independent-review handoff after receiving the results below.

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
owner. Root supplied the focused compiler/test evidence below and authorized commit
and PR handoff. This is pre-merge evidence, not the merged proof or a Done claim.

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

2. Root Release build retry failed with exit code 1 after 15.8 seconds: one
   CA1310 diagnostic for the query's `StartsWith(string)` overload. No tests ran.
   Replaced it with the existing EF-translatable `EF.Functions.Like` pattern
   using Core's literal image media-type prefix plus `%` (the prefix contains
   no LIKE wildcard characters). This does not use an untranslatable
   StringComparison overload. `git diff --check` completed with exit code 0;
   the real-SQL grouped-image tests remain required on root's rerun.

3. Root Release build retry 3 passed with zero warnings/errors. Focused Core
   run then exited 1: 139 passed, one failed in the new conflicting-group test.
   `ActionActor` is a sealed class with reference equality, so whole-request
   record equality compared two separate actor instances despite identical
   values. The assertion now explicitly checks both origins, canonical reasons,
   safe detail, operation keys, exact timestamps, actor kind/subject and role
   sets; no substantive assertion was removed. Root's SQL run is still pending.

4. Root accepted the own-review concurrent-association edge correction: both
   helpers refresh a receipt on AlreadyAssociated as well as Associated. This
   prevents later holding work seeing stale CurrentCaseId when a competing write
   committed between the initial read and association response. Existing live
   and completed-replay association tests now simulate that exact response and
   assert the receipt handed to downstream image/holding automation has the
   current case. The unique-match failure test includes the same race. Two
   missing table cases were added to the existing test-count helper for the new
   IntakeEvaluations/ImageIntakes assertions. Source is frozen pending root's
   targeted rebuild/retest; no tests were run by this worker.

5. Root's frozen SQL run exited 1 after 2m47s: 46 passed, two failed of 48.
   Both failures were the anticipated test-count helper ArgumentOutOfRange(table)
   in `UnreadableImageGroupHasOneUnidentifiedOutcomeAndLeavesTheSweep` and
   `DestinationFailureStaysDurableAndRetriesTheSameEvaluation`. The original
   assertions remain, now backed by the two exact table counters recorded above.
   Restricted Worker actual custody execution, OCR recovery and the real SQL
   grouped-image reconciliation cases otherwise passed. Root is rebuilding the
   frozen corrections and will rerun the one Core group case, three affected
   association integration cases, and the two affected counter cases only.

6. Corrected frozen-source Release build passed with exit code 0, zero warnings
   and errors, in 30.72 seconds. Root is running only the previously failing
   Core group case and affected SQL cases against that build; their results
   remain pending.

7. Targeted reruns passed against the corrected frozen build: the one Core
   conflicting-group case passed (1/1, exit 0, 55 ms), and all five Integration
   cases passed (5/5, exit 0, 51 seconds): the two prior counter-helper failures
   and all three unique-match/live-mail/completed-mail association paths.
   Final `git diff --check` again returned exit 0. Earlier failed attempts are
   retained above, not erased by these results.

## Changed-file census

Paths below are repository-relative; grouped tests update existing contracts
unless a regression is identified above.

- `docs/frd/frd-02-intake-and-source-identity.md` — durable destination and
  grouped outcome requirements.
- `docs/frd/frd-05-documents-extraction-and-custody.md` — custody identity and
  retained OCR analysis requirements.
- `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` — return group outcome.
- `src/Pegasus.Core/ImageIntake/ImageIntakeGroupRouting.cs` — canonical group
  registration builder.
- `src/Pegasus.Core/Intake/DurableIntake.cs` — evaluation/completion split,
  required routing retry and association refresh.
- `src/Pegasus.Core/Intake/GroupedIntake.cs` — eligible group query port.
- `src/Pegasus.Core/Intake/IntakeAllocation.cs` — unique-match allocation guard.
- `src/Pegasus.Core/Intake/IntakeOcr.cs` — retained-output analysis retry.
- `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs` — eligible old sweep.
- `src/Pegasus.Core/Intake/RetainIncomingArtifact.cs` — operation-qualified claim.
- `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` — actual
  source table routing.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs` — paired
  work remains pending until analysis completes.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs` —
  bounded oldest eligible SQL query.
- `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` — durable pending
  evaluation and final completion.
- `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` — canonical
  group outcome regression and query fake.
- `tests/Pegasus.Core.Tests/Intake/AnalyzeRetainedInstructionTests.cs` — OCR fake.
- `tests/Pegasus.Core.Tests/Intake/GroupedIntakeTests.cs` — group query fake.
- `tests/Pegasus.Core.Tests/Intake/ImmediateIntakeDispatchTests.cs` — work fake.
- `tests/Pegasus.Core.Tests/Intake/IntakeOcrTests.cs` — output replay regressions.
- `tests/Pegasus.Core.Tests/Intake/MailboxImageIntakeSubmissionTests.cs` — group
  query fake.
- `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs` — work fake only;
  root's MAIL-036 independently owns mailbox behavior.
- `tests/Pegasus.Core.Tests/Intake/ProcessIntakeTests.cs` — custody fake.
- `tests/Pegasus.Core.Tests/Intake/RetainIncomingArtifactTests.cs` — identity
  assertions and custody fake.
- `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` — actual
  restricted Worker custody regression.
- `tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs` — one
  group outcome and oldest candidate regressions.
- `tests/Pegasus.IntegrationTests/IncomingArtifactCustodyTests.cs` — identity
  claim callers.
- `tests/Pegasus.IntegrationTests/OcrIntakeRecoveryTests.cs` — paired pending-work
  output replay regression.
- `tests/Pegasus.IntegrationTests/PublicUploadRetentionWebTests.cs` — identity
  claim callers.
- `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` — durable retry
  and concurrent-association regressions, existing count helper and work fake.
- `tests/Pegasus.IntegrationTests/StagedArtifactReconciliationFunctionIntegrationTests.cs`
  — work/group fakes.
- `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — evaluation/completion
  seed setup.

## Independent review and merged verification

Review the production caller boundaries, especially retained evaluation replay,
AlreadyAssociated refresh, restricted Worker routing and OCR paired-work state.
Use the plan's focused Core and SQL class filters against the merged dev SHA;
include AutomaticImageIntakeTests in Core. Root owns the final converged
solution rail. No deployment, provider activation or full v1 delivery is claimed
by this isolated ticket. Remaining domain routing, Triage linking and deployment
work belongs to the separate original-owner tickets.
