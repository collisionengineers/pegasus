## Author freeze — 8 September 2026

Ready whole execution packet: plan a5e40190fbaa6886 / files4689efb462748ddf,
base and current HEAD19e6f523bf6760cab39104b4dca3674b0ac8a512. Isolated branch
CASE-049-native-handoff, exact worktree .worktrees/case-049. Lease
e470faea-e8e9-4b06-bea3-408641c00770 retained; frozen controller run
20260907T231141Z-native-handoff. No foreign workspace or claim changed.
The current ENG-041 seven-file correction remains disjoint.

26 declared files changed; no undeclared paths. Native AssignEngineer is one
Core-selected ReportPreparation transition using the existing assignment
transaction, state event, discriminator, lease/version/replay and persisted
readiness guard. No EVA proxy fabricated. Its native action/dialog reuses
eligible options, posts authenticated antiforgery/lease/version identity and a
server-selected handoff reason. Assignment removed from optional EVA dialog;
redundant StartWork UI/POST removed, existing headless Core transition retained.

Assessment access now holds lifecycle state only; both old export/history
subqueries and duplicate CanOpenReports gate are removed. Retained workspace
loads in all states; edits remain available only With Engineer and role/lease
guards remain. Existing FRD01/11/12 and design text aligned; protected operator
notes untouched.

Root owns all builds/tests. No author build, test, snapshot generation, cloud,
provider, mail, commit, PR or merge has run. Source is frozen pending compiler
and focused runtime evidence. Checkboxes remain unticked until proof of their
test clauses. git diff --check returned no errors; final standalone check
recorded separately below. Review/proof remain owed.

### Exact focused filters

Core:
`FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssignCaseEngineerTests`

Integration (includes minimal captures, not the whole CaseDetails class):
`FullyQualifiedName~CaseWorkflowPersistenceTests.NativeHandoffIsAtomicGuardedAndReplaySurvivesLaterDisablement|FullyQualifiedName~CaseWorkflowPersistenceTests.ReviewGatedTransitionsRefuseOnIncompletePersistedFacts|FullyQualifiedName~CaseWorkflowPersistenceTests.MissingDisabledOrNonEngineerStaffCannotBeAssigned|FullyQualifiedName~CaseWorkflowPersistenceTests.StartMovesDirectlyToReportPreparationAndRetainedSentEvidenceNeedsNoApproval|FullyQualifiedName~CaseWorkflowPersistenceTests.StartRejectsEngineerDisabledAfterAssignment|FullyQualifiedName~AssessmentPersistenceIntegrationTests.AssessmentAccessUsesNativeStateAndRetainsWorkspaceWithoutAnExport|FullyQualifiedName~AssessmentPersistenceIntegrationTests.AssessmentWorkspaceLoadsInExactlySixReaderCommands|FullyQualifiedName~AssessmentPersistenceIntegrationTests.ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt|FullyQualifiedName~CaseWorkspacePersistenceTests.ReviewGatedTransitionsReadThePersistedFactsNotThePostedOnes|FullyQualifiedName~CaseEngineerSectionsWebTests|FullyQualifiedName~CaseDetailsWebTests.NativeHandoffDialogPostsWithoutEvaOrASeparateReviewAction|FullyQualifiedName~CaseDetailsWebTests.WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement|FullyQualifiedName~CaseDetailsWebTests.LifecyclePostsBindHoldReleaseAndNativeHandoffToAuthenticatedLease|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor`

### Snapshot capture owners

Default: CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey.
Its Review/edit fixture now includes one eligible Engineer so the new native
action appears. Conflict:
CaseDetailsWebTests.WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement
already invokes AssertLostLeaseClearsEditModeAsync; the shared actual
CaseMutationPageModel refusal contains the manifest's required 'case changed'.
Unavailable: TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor.

Retain capture artifacts from the focused run, then root may use
scripts/Update-TestUiSnapshots.ps1 -SkipCapture -Scope case-details, followed by
-Verify -SkipCapture -Scope case-details and scripts/Test-UiCatalogue.ps1.
No broad script automatic OR cohort. Expected generated paths are the three
case-details HTML files plus docs/design/test-ui/index.html.

Next: root focused verification; preserve every failed attempt, fix only
reported failures, then author report/commit/PR to dev after root authorization
and stop for independent review. No self-review or merge.

Standalone lightweight check: `git -c core.safecrlf=false diff --check`, exact .worktrees/case-049, exit0, 0.6166s, no output (8 September author freeze). No builds/tests have run.

## Review handoff — 8 September 2026

PR https://github.com/collisionengineers/pegasus/pull/690 targets dev at exact head 24eb2f77276fd7eb847f8c1746e6909113866b58. Report 9f6dc85004bf5c07 and checklist 4fdb7ce5a5daaa18 whole readback matched written content. Root author checks PASS as recorded; no failed runtime attempt in this author batch. Fresh enter-review gates passed. 28 normalized changed paths all declared; regenerated unchanged index/unavailable were added only to refresh Git normalization metadata, leaving no staged delta. Worktree clean and claim retained. Author stops; root independent review next, no self-review/merge/deploy.

## Transitions

- 2026-09-08T01:21:51.030Z lease-phase implementing → verifying (lease e470faea-e8e9-4b06-bea3-408641c00770 rev 5; expires 2026-09-08T01:51:51.024Z)

Closeout: root-authorized exact CASE-049 author and detached verification worktrees were clean and removed normally after preserving all four TRXs under pegasus_pack/current/proofs/CASE-049 with source/destination SHA256 checks. Local CASE-049-native-handoff branch -d succeeded (warning: merged to its remote tracking branch but stale shared HEAD); no force, reset or stash. Exact remote branch deleted; fetch/prune and empty dry-run worktree prune succeeded. No foreign workspace/claim changed. Body four acceptance boxes and integrated dev3a5ce645cfc0872d7a4324c6818497360c39cca4 traceability now match final PASS proof c9b63f3798849fdf. Pre-merge autopush guard exit1 and limits preserved. Claim release is the final ownership action; FRD-01 note remains TICK-035 scope.
