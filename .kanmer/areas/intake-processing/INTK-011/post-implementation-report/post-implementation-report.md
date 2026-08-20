## Root cause (verified, file:line citations in `files.md`)

Not a bug in the per-receipt `NeedsSorting` decision, and not a bug in
`ImageIntakeGroupRoutingPolicy.Evaluate` (pure, correct). The defect: every
member's own durable work item redundantly re-applies the whole group's
outcome (`ImageIntakeAutomation.TryApplyGroupAsync`,
`src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs:116-238`), with no lease
over the group. Two members' concurrent registration attempts genuinely
contend on the one shared `ImageIntakeSequences` row for their VRM
(`EfImageIntakeStore.RegisterAsync`, Serializable isolation,
`src/Pegasus.Infrastructure/Persistence/EfImageIntakeStore.cs:37-195`); SQL
aborts the loser. The loser's exception was caught by
`IntakeExceptionPolicy.IsRecoverable` (almost everything) inside
`TryRegisterAndAssociateAsync` and the caller **discarded the return value
entirely** — no retry, no defer. The losing member's receipt was left exactly
as `ProcessIntake` set it (`NeedsSorting`, "No accepted intake route
established the principal for automatic case creation" — the exact production
text) permanently, because its work item was already `Completed` before
automation ran and nothing ever revisited a completed work item.
`SynchronizeUnidentifiedAsync`'s own Unidentified-registration fallback has
the identical swallow-with-no-retry shape, which is why the production JPEG
got neither a registered Image Intake nor a U-reference — both of its only
possible destinations were attempted and both lost the same class of race.

The ImageIntake aggregate genuinely holds one origin receipt per row (not a
second store, not changed here) — confirmed intentional by the existing
INTK-006 test `OneEligibleCaseAssociatesEveryGroupMember`, which asserts two
separate registration requests for a 2-member group.

## Fix delivered

`ImageIntakeAutomation.ApplyAsync` now returns `ImageIntakeAutomationOutcome(Receipt,
GroupPending)` instead of a bare receipt, so a member whose own registration
lost the race (or whose group is still waiting on siblings/recognition) is
reported back instead of silently absorbed. `ProcessQueuedIntake` reads that
flag and, when true, simply **skips this pass's Unidentified registration** —
the work item is left `Completed` (untouched), which is safe and cheap to
revisit: a later `ProcessQueuedIntake.ExecuteAsync` call for the same staged
receipt finds nothing to claim and takes the existing replay branch, re-running
automation without re-reading the (already-deleted) staged artifact.

**Design correction during implementation**: the original plan reused
`IIntakeWorkStore.ScheduleReevaluationAsync` to defer a pending member with a
`RetryDelays`/`AttemptCount` bound. That was built, then disproven by its own
integration test: rearming the work item back to `Pending` forces a future
re-claim through the artifact-reading branch, whose staged copy is already
deleted by `TryDeleteCompletedStagingAsync` (runs right after
`CompleteProcessingAsync`, before automation). Reproduced a hard
`staged_artifact_integrity_failure` against real LocalDB. Corrected design
(implemented, tests green): never touch the work item; rely on a later,
independent re-invocation of the safe replay branch instead. See plan.md's
"Design revision" section for the full trace.

**Reconciliation** (`src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs`,
new): finds `NeedsSorting` image-only group members via the existing paged
`IIntakeReceiptQueries.ListAsync`, and for each either (a) re-drives it through
the new `IProcessQueuedIntake.ExecuteAsync` (the same safe replay branch), or
(b) once it has been pending longer than `EscapeAfter` (2h, matching the
existing longest `RetryDelays` entry), registers it Unidentified directly —
the poison-path escape, reusing `ProcessIntake.BuildUnidentifiedRegistrationRequest`
rather than inventing a second Unidentified-registration path. Wired into the
**existing** `StagedArtifactReconciliationFunction` timer
(`src/Pegasus.Worker/IntakeFunctions.cs`) — no new schedule setting. This is
the mechanism that will recover the production straggler on deploy: it will
be found (image-only, `NeedsSorting`, a group member), its staged receipt id
resolved via the new read-only `IIntakeWorkStore.FindStagedReceiptIdForReceiptAsync`,
and re-driven through the ordinary pipeline, which this time succeeds (its
sibling `G6KDL-01` is already registered and excluded from the group's
recognition set, so the straggler registers cleanly under its own new
sequence number, e.g. `G6KDL-02`) — not literally merged into the single
`G6KDL-01` row, since that row is one-receipt-per-row by design (see files.md);
it becomes a second registered Image Intake for the same VRM, reachable and
searchable alongside G6KDL-01. This interpretation of "recovers ... into
G6KDL-01" (as "into the G6KDL evidence set", not literally the same row) is
called out explicitly for reviewers given it's a legitimate reading question.

**Grants**: confirmed real. `ImageIntakeAutomation.TryApplyGroupAsync`
(`ImageIntakeAutomation.cs:121,129`) is called from the Worker's
`ProcessQueuedIntake` pipeline and reads `IntakeSubmissionGroups`/
`IntakeSubmissionGroupMembers` via `EfIntakeSubmissionGroupStore`, which is
registered once in shared `Pegasus.Infrastructure` DI for both Web and
Worker composition roots — but the original `20260819101344_GroupedIntakeSubmission`
migration's grant comment explicitly (and wrongly) claimed "the Worker never
references either table." Added migration
`20260819234014_GrantWorkerIntakeSubmissionGroupRead` granting
`pegasus_worker_runtime_role` `SELECT` on both tables (mirroring
`20260819180000_GrantEvaHandoffDownloadOperations.cs`'s shape), and corrected
`scripts/Invoke-AzureDatabaseBootstrap.ps1`'s census/comment. (Production
evidence shows the Worker's actual runtime access was not fully blocked — the
PNG member registered successfully — so this was a documented/tracked-grant
gap rather than the proximate cause of the JPEG's failure; the swallowed
exception with no retry is the proximate cause.)

## A discovered, out-of-scope gap

`EfIntakeSubmissionGroupStore.FindForMemberSourceAsync` can never recognise an
**ordinal-0** group member from its own identity (its token has no `:N`
suffix, which the lookup requires) — an ordinal-0 receipt's own pass always
takes the single-receipt path, never the group path. This is why production's
PNG (ordinal 0) registered solo while the JPEG (ordinal 1) went through the
group path and lost the race — and it means the reconciliation sweep in this
fix cannot recognise an ordinal-0 straggler either, if one ever occurs. This
is a distinct, pre-existing token-encoding ambiguity (a bare token cannot
distinguish "ordinal-0 group member" from "genuinely standalone upload"), not
what the production evidence for this ticket exhibited, and left out of scope
deliberately — flagged for a follow-up ticket. See plan.md for detail.

## Tests

- `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs` —
  `MemberRegistrationFailureReportsGroupPendingInsteadOfFallingBack` (new):
  one member's registration fails (fake register store throws), asserts
  `GroupPending == true` and `Decision` stays `NeedsSorting` for that member,
  and that the sibling still registers. Full suite: **685/685 passed**.
- `tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs`
  (new, `[Trait("Category","SqlServer")]`, real LocalDB):
  - `ConcurrentGroupMembersNeverSplitAcrossRepeatedRuns` — 12 fresh 2-member
    image groups per run, each pair processed via `Task.WhenAll` of two
    independent `ProcessQueuedIntake.ExecuteAsync` calls in separate DI
    scopes (mirroring `QdosAllocationRecoveryTests`'s parallel-retry style,
    SqlException 1205 retry wrapper). Asserts both members always end
    `ImageIntakeRegistered` with two distinct references, never split. **Run
    3 times in this session = 36 total race trials, 0 failures** after the
    design correction above.
  - `ReconciliationRecoversAStrandedGroupMember` — reproduces the exact
    pre-fix production shape directly against the store (one member
    registered, sibling forced back to `NeedsSorting` with the generic
    instruction-fallback reason, its `ImageIntake` row and matching
    `IntakeMutationHistory` row removed), runs `ReconcileGroupedImageIntake.ExecuteAsync`,
    asserts the straggler is registered afterward. **Passed.**
- `dotnet test tests/Pegasus.ArchitectureTests`: **97/97 passed** (updated
  `StagedArtifactReconciliationFunctionTests` for the new constructor
  parameter and log fields).
- `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName~GroupedIntake|FullyQualifiedName~ImageIntake|FullyQualifiedName~IntakePersistence|FullyQualifiedName~QdosIntake|FullyQualifiedName~IntakeWebNegative"`:
  **37 passed, 6 skipped** (pre-existing skips, unrelated to this change),
  **0 failed**. Updated `IntakePersistenceIntegrationTests`'s hard-coded
  applied-migration list to include the new migration.
- `pwsh ./scripts/Test-MigrationGrants.ps1`: **passed** (54 migration files
  checked).
- `pwsh ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`: **passed**.
- Not run: the repository's full/Browser suite (out of this ticket's stated
  command list; the specified integration filter and Core/Architecture suites
  were run in full instead) and the production-only verification checkbox
  ("the reconciliation path pulls the production straggler ... into
  `G6KDL-01`") — that is explicitly a post-deploy verification step per the
  ticket's own checklist, not something this session can execute.

## Migration and grants

`src/Pegasus.Infrastructure/Persistence/Migrations/20260819234014_GrantWorkerIntakeSubmissionGroupRead.cs`
(+ Designer): grant-only, `GRANT SELECT` on `IntakeSubmissionGroups` and
`IntakeSubmissionGroupMembers` to `pegasus_worker_runtime_role` (`Down()`
revokes both), generated via `dotnet ef migrations add` and hand-edited to
match `20260819180000_GrantEvaHandoffDownloadOperations.cs`'s exact shape. No
schema change (no `CreateTable`, no `AddColumn`) — the model snapshot diff EF
regenerated (an unrelated fluent-API reordering on an existing property) was
reverted to keep this migration's diff to only the grant. `scripts/Invoke-AzureDatabaseBootstrap.ps1`
census updated with the two new `pegasus_worker_runtime_role|G|SELECT|...`
entries and the corrected comment.

## Simplification pass

Manual reuse/simplification/efficiency/altitude review of the full diff plus
the `code-simplifier` agent, both run over the branch diff before PR; findings
recorded in plan.md's "Simplification pass" section (dated 2026-08-19).
Summary: no dead code, no new abstraction without a second real caller, no new
"group outcome" schema (confirmed unnecessary during the design correction),
naming consistent with existing `Reconcile*`/`Apply*` conventions.

## Files changed

- `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs`
- `src/Pegasus.Core/Intake/DurableIntake.cs`
- `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs` (new)
- `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/20260819234014_GrantWorkerIntakeSubmissionGroupRead.cs` (+Designer, new)
- `src/Pegasus.Worker/IntakeFunctions.cs`
- `src/Pegasus.Worker/WorkerDependencyInjection.cs`
- `scripts/Invoke-AzureDatabaseBootstrap.ps1`
- `tests/Pegasus.Core.Tests/ImageIntake/AutomaticImageIntakeTests.cs`
- `tests/Pegasus.Core.Tests/Intake/PollApprovedInboxTests.cs`
- `tests/Pegasus.ArchitectureTests/StagedArtifactReconciliationFunctionTests.cs`
- `tests/Pegasus.IntegrationTests/GroupedImageIntakeConcurrencyTests.cs` (new)
- `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
