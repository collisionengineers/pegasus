# Plan — INTK-061: durable intake destinations

## Objective

An intake attempt reaches one intended durable destination or retains a durable
retry/failure owner, without duplicate allocation or loss of custody evidence.

## Starting state

Baseline: 3da60bd0c270111d5168dc17246dc831882108ea, origin/dev.
Evidence: research/research.md@779c3ad4ab6d090e; files/files.md@2e052634be4b2c1d.
EPIC-014 context and the current user request authorize this bounded remediation.
The shared checkout is stale and dirty; create an isolated packet worktree.

## Governing docs

Meets docs/frd/frd-02-intake-and-source-identity.md: exactly one destination,
fail-closed ambiguous/failed association, retained grouped evidence and bounded
recoverable processing. Clarifies required destination completion ownership.
Meets docs/frd/frd-05-documents-extraction-and-custody.md: source-qualified
custody claims, immutable OCR output, safe repeated analysis without duplicate
provider submission. No business meaning or protected operator notes changes.

## Required changes

Pass full custody occurrence identity to the existing conditional claim and
route to its actual table. Separate evaluation persistence from marking the
intake work complete: retain the lease and existing retry state through required
destination writes, replay the stable evaluation on retry, and only then delete
staging. Propagate required postprocessing failures into that retry owner.
A recorded UniqueMatch cannot fall through to allocation. Carry one canonical
group Unidentified request through image automation and register by group origin.
Query oldest eligible unresolved groups before limiting, excluding registered
Image Intake and existing group Unidentified results. Persist completed OCR
output without finishing its external work until analysis succeeds; failed
analysis stays retryable and reuses that output.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | `src/Pegasus.Core/Intake/RetainIncomingArtifact.cs` | Route claims using the complete operation identity. |
| Modify | `src/Pegasus.Core/Intake/DurableIntake.cs` | Persist evaluation separately from successful destination completion; retain retry ownership. |
| Modify | `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Refuse new allocation for a recorded unique match. |
| Modify | `src/Pegasus.Core/Intake/IntakeContracts.cs` | Existing intake query/group contracts only where needed for eligible reconciliation. |
| Modify | `src/Pegasus.Core/Intake/ProcessIntake.cs` | Reuse group registration/reason construction; no new provider routing. |
| Modify | `src/Pegasus.Core/Intake/ReconcileGroupedImageIntake.cs` | Recover eligible oldest groups once and retain group outcome. |
| Modify | `src/Pegasus.Core/Intake/IntakeOcr.cs` | Resume analysis from retained completed OCR output. |
| Modify | `src/Pegasus.Core/Intake/GroupedIntake.cs` | Existing submission-group query contract if required. |
| Modify | `src/Pegasus.Core/ImageIntake/ImageIntakeAutomation.cs` | Emit one group-level Unidentified registration with canonical reason. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | Claim intake assets directly without probing Web-owned tables. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfIntakeWorkStore.cs` | Separate evaluation persistence from terminal work completion using current columns. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` | Eligible recovery query if the existing group store cannot carry it. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfIntakeSubmissionGroupStore.cs` | Bound oldest eligible groups before paging. |
| Modify | `src/Pegasus.Infrastructure/Persistence/EfIntakeOcrOperationStore.cs` | Keep external work retryable until analysis follows stored OCR output. |
| Modify | `src/Pegasus.Worker/IntakeFunctions.cs` | Ensure processing outcomes correspond to durable scheduling and log defects. |
| Modify | `tests/Pegasus.Core.Tests/Intake/*.cs` | Focused policy/store-contract fakes and intake/OCR regressions only. |
| Modify | `tests/Pegasus.IntegrationTests/*.cs` | Actual Worker-role custody, queued routing failure, group and OCR regressions plus affected interface callers only. |
| Modify | `docs/frd/frd-02-intake-and-source-identity.md` | Clarify durable routing completion and single group outcome. |
| Modify | `docs/frd/frd-05-documents-extraction-and-custody.md` | Clarify custody claim identity and recoverable OCR analysis completion. |

## Do not modify

- `docs/operator-notes.md`
- `corpus/**`
- `src/Pegasus.Core/Cases/CaseContracts.cs`
- `src/Pegasus.Infrastructure/Persistence/Migrations/**`
- `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
- `src/Pegasus.Web/Pages/**`
- `infra/**`

## Constraints

Use existing state/columns, SQL fixtures, operation keys, group origin and Core
owners. No new package, Worker grant, queue, schema, route policy or runtime.
All tests stay focused on these realistic failure paths. Root alone runs builds,
tests and any required snapshots. No live provider or cloud writes.

## Ordered steps

1. Correct source-qualified custody claiming and update affected callers/fakes;
   add an actual restricted Worker-role store test and claim replay coverage.
2. Retain intake work ownership through routing and reuse stable evaluation on
   retry; fail closed on unique-match association failure; test eventual Triage
   and Unidentified completion and no duplicate Case allocation.
3. Carry group terminal reason/identity through automation and recover only
   eligible oldest groups; test one group result under replay and no starvation.
4. Resume OCR analysis from retained output until success; test failure after
   OCR completion followed by replay with one provider submission.
5. Update canonical behavior descriptions, inspect diff/formatting, and hand
   exact focused filters and worktree to root for required execution evidence.

## Acceptance checks

The actual production callers are ProcessIntake -> RetainIncomingArtifact,
UnifiedWorkFunction -> ProcessQueuedIntake, the grouped reconciliation timer,
and external work -> ProcessIntakeOcr. Restricted-role tests execute the actual
retention store. A transient destination write cannot remove the durable retry
owner; a unique association failure allocates zero additional Cases; one
image group yields one U record; old pending groups progress; OCR followup
replays output without another OCR call. Keep existing meaningful assertions.

## Commands

Implementation lane: git diff --check; targeted source/interface search.
Root executes from this ticket's worktree:
dotnet restore ./Pegasus.slnx --locked-mode
dotnet build ./Pegasus.slnx --configuration Release --no-restore
dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~RetainIncomingArtifactTests|FullyQualifiedName~IntakeOcrTests|FullyQualifiedName~ProcessIntakeTests"
dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~IncomingArtifactCustodyTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests|FullyQualifiedName~QdosAllocationRecoveryTests|FullyQualifiedName~GroupedImageIntakeConcurrencyTests|FullyQualifiedName~OcrIntakeRecoveryTests"
Final required solution rail belongs to root after related lanes converge;
no duplicate full rail or stress suite in this lane.

## Failure and deviation rules

Report any need for schema/model/CaseContracts or shared admin changes before
editing. Report failed checks with their original exit; never weaken an
assertion. New business requirements and unrelated bugs go to their owning
ticket. Root holds heavy verification and supplies its results.

## Stop condition

Implement the bounded change and focused regression tests, then provide root
the exact worktree, changed files and commands. Retain the taken record while
root verifies. Do not open a PR, move to Review or claim PASS before root gives
verification evidence. The final handoff is ready for independent review.
