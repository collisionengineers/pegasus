---
kind: proof-record
merged_sha: "3a5ce645cfc0872d7a4324c6818497360c39cca4"
environment: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4; Windows x64 / PowerShell 7 / .NET 10 / SQL Server LocalDB"
verified_at: "2026-09-08T01:25:38Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08T01:20:00Z"
    command: "GitHub PR690 mergeCommit readback; git worktree add --detach .worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4 3a5ce645cfc0872d7a4324c6818497360c39cca4; git rev-parse HEAD; git symbolic-ref --short -q HEAD; git status --short --branch"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "Exact merged SHA, clean detached checkout; symbolic-ref expected1/empty confirms detached, all other commands exit0."
  - attempted_at: "2026-09-08T01:20:00Z"
    command: "git diff --exit-code 24eb2f77276fd7eb847f8c1746e6909113866b58 3a5ce645cfc0872d7a4324c6818497360c39cca4"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "Entire merged source tree equals independently reviewed author head."
  - attempted_at: "2026-09-08T01:20:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "Locked solution restore completed."
  - attempted_at: "2026-09-08T01:20:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "55.36 seconds, zero warnings/errors."
  - attempted_at: "2026-09-08T01:21:00Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssignCaseEngineerTests' --logger 'trx;LogFileName=case-049-merged-core.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "56 passed, zero failed/skipped,91ms."
  - attempted_at: "2026-09-08T01:21:54Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CaseWorkflowPersistenceTests.NativeHandoffIsAtomicGuardedAndReplaySurvivesLaterDisablement|FullyQualifiedName~CaseWorkflowPersistenceTests.ReviewGatedTransitionsRefuseOnIncompletePersistedFacts|FullyQualifiedName~CaseWorkflowPersistenceTests.MissingDisabledOrNonEngineerStaffCannotBeAssigned|FullyQualifiedName~CaseWorkflowPersistenceTests.StartMovesDirectlyToReportPreparationAndRetainedSentEvidenceNeedsNoApproval|FullyQualifiedName~CaseWorkflowPersistenceTests.StartRejectsEngineerDisabledAfterAssignment|FullyQualifiedName~AssessmentPersistenceIntegrationTests.AssessmentAccessUsesNativeStateAndRetainsWorkspaceWithoutAnExport|FullyQualifiedName~AssessmentPersistenceIntegrationTests.AssessmentWorkspaceLoadsInExactlySixReaderCommands|FullyQualifiedName~AssessmentPersistenceIntegrationTests.ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt|FullyQualifiedName~CaseWorkspacePersistenceTests.ReviewGatedTransitionsReadThePersistedFactsNotThePostedOnes|FullyQualifiedName~CaseEngineerSectionsWebTests|FullyQualifiedName~CaseDetailsWebTests.NativeHandoffDialogPostsWithoutEvaOrASeparateReviewAction|FullyQualifiedName~CaseDetailsWebTests.WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement|FullyQualifiedName~CaseDetailsWebTests.LifecyclePostsBindHoldReleaseAndNativeHandoffToAuthenticatedLease|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor' --logger 'trx;LogFileName=case-049-merged-handoff.trx' --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "32 passed, zero failed/skipped,80s; three actual routed captures included."
  - attempted_at: "2026-09-08T01:23:00Z"
    command: "pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "2 passed,5s, using this exact-merge run's fresh captures."
  - attempted_at: "2026-09-08T01:23:00Z"
    command: "pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "60 routes,67 prototypes,zero broken local references."
  - attempted_at: "2026-09-08T01:25:00Z"
    command: "git status --short --branch; git rev-parse HEAD; git rev-parse --git-common-dir; Get-FileHash -LiteralPath artifacts/verification/case-049-merged-core.trx,artifacts/verification/case-049-merged-handoff.trx"
    cwd: ".worktrees/verify-case-049-3a5ce645cfc0872d7a4324c6818497360c39cca4"
    exit_code: 0
    result: PASS
    summary: "Clean detached exactSHA, expected common Git, both retained TRX hashes read."
---

# CASE-049 exact integrated acceptance

PR690 is MERGED to configured integration branch dev at the exact SHA above.
Independent root review75f0a427edde5aba covers author
24eb2f77276fd7eb847f8c1746e6909113866b58. Root is verifier, not author.
Plan a5e40190fbaa6886/report9f6dc85004bf5c07 bind the bounded commands.
The source tree was compared in full, but runtime checks were also executed
in this separate exact-merge checkout; no mutable shared checkout was updated.
No other heavy verifier ran. Command times above are recorded to the observed
minute except the TRX-backed Integration start; exact test instants are retained.

## Actual result

Ready native handoff assigns Engineer/sign-off and enters ReportPreparation
in one attributable transaction with the existing state event, expected version
and lease. Persisted incomplete facts, stale/invalid authority, unauthorized/
disabled/non-Engineer actors and changed replay input are refused. Exact replay
after later disablement is retained; earlier Sent evidence is refused and later
evidence can enter PostReport. No EVA proxy is created or required.

Native policy, real workspace readers and actual dialog/POST agree. Retained
values remain readable across five tested states; editing remains restricted.
Existing six-reader query count and production report projection without export
remain valid. Minimal default/conflict/unavailable captures verify against
committed snapshots; no manual visual claim is made.

## Retained evidence

- artifacts/verification/case-049-merged-core.trx:
  84988EA66C196143D62E01A218740E4B7BD25EAE8BE8A7924A77088CEC044C51.
- artifacts/verification/case-049-merged-handoff.trx:
  0F7E27611610B0B140D5989A79B151E799F37B3FAA3A2B57BE9870A4614C811F.
- Integration exact instants: 2026-09-08T01:21:54.2913050Z through
  2026-09-08T01:23:16.8238227Z.
- Author Core/Integration TRXs and hashes remain in the report and author
  artifacts; closeout must retain all four before removing generated outputs.
- artifacts/test-ui-capture contains this merge's fresh scoped capture inputs.

## Attempts and limits

No verification command failed. Before merge, one guarded command exited1
because the board autopush was still ahead1; it stopped before gh pr merge.
After synchronization, the unchanged-head/check/thread checks passed and the
normal GitHub merge succeeded. That pre-merge stop was not a failed source
test, not hidden by this proof. The first independent review F-001 remains
assigned to TICK-035: stale unrelated QDOS-only correspondence wording in
FRD-01. No native handoff blocker remains.

This is integrated functional acceptance, not deployed/live Glass/OCR/mail,
full v1 completion or hosted-CI evidence. No cloud or mailbox write occurred.
ENG-041's separate original failed proof remains untouched. No foreign claim
or workspace was modified. Closeout follows after the fresh Done gate and must
preserve evidence, clean only this ticket's exact owned worktrees/branch, then
release the lease last.
