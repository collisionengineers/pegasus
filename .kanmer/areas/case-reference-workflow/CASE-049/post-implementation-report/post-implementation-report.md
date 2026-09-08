# CASE-049 post-implementation report

## Result and traceability

Native Hand to Engineer is the review action: one guarded assignment advances
a ready Case to With Engineer without EVA. Retained Engineer sections remain
viewable outside editable states. No separate reviewed checkbox or mandatory
Start report preparation action remains.

- Plan: a5e40190fbaa6886; files: 4689efb462748ddf.
- Base: 19e6f523bf6760cab39104b4dca3674b0ac8a512 (dev).
- Commit: 24eb2f77276fd7eb847f8c1746e6909113866b58.
- Branch/worktree: CASE-049-native-handoff / .worktrees/case-049.
- Frozen controller: 20260907T231141Z-native-handoff. Claim retained.
- PR targets dev; independent review and exact integrated proof remain owed.
- Root authorized [skip ci] on this bounded author commit; the converged
  integration/release gate remains controller-owned. No CI PASS is inferred.

## Actual caller and bounded implementation

Case Details native dialog posts to existing Workflow.AssignEngineer using
antiforgery, authenticated human actor, posted case version, edit lease and
operation key. Core AssignCaseEngineer selects ReportPreparation and the
existing EfCaseWorkflowStore mutation atomically persists Engineer, sign-off,
state and one state_ReportPreparation event. It checks persisted readiness,
eligible staff, actor/lease/version and exact replay; no EVA proxy is created.
The existing state event preserves later report-Sent chronology.

AssessmentAccessState is the sole lifecycle access owner. Both old export/
review-history queries and duplicated CanOpenReports policy are removed.
EfAssessmentWorkspaceSource retains projections outside action eligibility;
editing remains limited to ReportPreparation/PostReport with existing guards.
The legitimate headless StartCaseWork command remains available through the
existing lifecycle API; only the redundant mandatory UI/POST is removed.

FRD-01, FRD-11, FRD-12 and existing design text align with current operator
authority. Protected operator notes, current ENG-041 custody/report-store
correction, schema/grants, optional EVA transport and other tickets are
untouched. No new layer, dependency, store, runtime or feature flag.

## Changed files

| Path | Purpose |
| --- | --- |
| src/Pegasus.Core/Lifecycle/CaseLifecycle.cs | Core assignment supplies its ReportPreparation destination to the existing persistence port. |
| src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs | Existing assignment port carries Core's chosen destination; no new abstraction. |
| tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs | Update the existing direct assignment-store caller. |
| src/Pegasus.Core/Assessment/AssessmentWorkspace.cs | Remove export tuple and duplicate report access policy; retain one lifecycle/read-only rule. |
| src/Pegasus.Core/Reports/AssessmentReportProjection.cs | Use the single access owner. |
| src/Pegasus.Infrastructure/Persistence/EfAssessmentAccessSource.cs | Query lifecycle state without EVA/history subqueries. |
| src/Pegasus.Infrastructure/Persistence/EfAssessmentWorkspaceSource.cs | Same native projection; no export dependency. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs | Assignment becomes atomic existing ReportPreparation transition with assignment/sign-off and readiness guard. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | Single native section guard and known tuple consumers. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml | Native handoff action/dialog in existing action bar. |
| src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs | Current handoff completion label; no redundant StartWork UI handler. |
| src/Pegasus.Web/Pages/Cases/Shared/_EvaHandoff.cshtml | Remove native assignment from optional EVA dialog; retain actual EVA/sign-off controls. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml | Remove mandatory second Start report preparation control/dialog. |
| src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs | Existing action labels if required by caller search. |
| tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs | Native access no-export policy cases. |
| tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs | Known assignment contract coverage. |
| tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs | Update access-state constructor fixture. |
| tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs | Update known tuple fixture and native section coverage. |
| tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs | Real native workspace without export, reopen/read-only evidence. |
| tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs | Atomic handoff, replay/readiness/lease/version and report-history acceptance. |
| tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs | Actual assignment POST and handoff label. |
| tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs | Replace obsolete StartWork handler/capture expectations. |
| docs/frd/frd-01-case-identity-and-lifecycle.md | Native handoff replaces mandatory EVA progression. |
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Native estimate/report access agrees. |
| docs/frd/frd-12-operator-experience.md | One human handoff action. |
| docs/design/README.md | Action/dialog mapping reflects user-authorized native handoff. |
| docs/design/test-ui/pages/case-details--default.html | Scoped generated capture. |
| docs/design/test-ui/pages/case-details--conflict.html | Scoped generated capture. |
| src/Pegasus.Core/Lifecycle/CaseLifecycle.cs | Assignment eligibility/sign-off resolution and legitimate existing headless StartCaseWork caller. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs | Existing mutation/replay/lease and report-evidence temporal guards share the authoritative state event. |

28 normalized changed files, all in the approved map. Root regenerated all
three case-details states and index.html; only default/conflict normalized
bytes changed, so unavailable/index have no committed delta.

## Verification attempts

Windows / PowerShell 7, exact recorded worktree, root sole heavy verifier.
Author inspected retained TRX counters and independently recomputed both hashes.
No author build/test, live Glass/mail/OCR/cloud call or deployment occurred.

| Check | Result |
| --- | --- |
| Locked solution restore | PASS, exit 0 |
| Release solution build, no restore | PASS, exit 0, 65.07s, zero warnings/errors |
| Focused Core, filter below | PASS, exit 0, 56/56, zero failures/skips, 110ms |
| Focused SQL/Web, filter below | PASS, exit 0, 32/32, zero failures/skips, 81s |
| Update-TestUiSnapshots -SkipCapture -Scope case-details | PASS, exit 0, 2 tests, 186ms |
| Update-TestUiSnapshots -Verify -SkipCapture -Scope case-details | PASS, exit 0, 2 tests, 6s |
| Test-UiCatalogue | PASS, exit 0, 60 routes / 67 prototypes / 0 broken |
| Author standalone git -c core.safecrlf=false diff --check | PASS, exit 0, 0.6166s, no output |
| Final staged diff --check | PASS, exit 0, no output |

No failed runtime attempt was reported for this CASE-049 author batch.
No whole test rail, repeated broad capture cohort, manual visual pass,
provider acceptance or deployment success is claimed.

Root commands: dotnet restore ./Pegasus.slnx --locked-mode;
dotnet build ./Pegasus.slnx --configuration Release --no-restore.
Focused project tests used --configuration Release --no-build --filter
with the exact values below, --logger trx;LogFileName=case-049-core.trx or
case-049-handoff.trx, and --results-directory ./artifacts/verification.

Core:
`FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssignCaseEngineerTests`

Integration:
`FullyQualifiedName~CaseWorkflowPersistenceTests.NativeHandoffIsAtomicGuardedAndReplaySurvivesLaterDisablement|FullyQualifiedName~CaseWorkflowPersistenceTests.ReviewGatedTransitionsRefuseOnIncompletePersistedFacts|FullyQualifiedName~CaseWorkflowPersistenceTests.MissingDisabledOrNonEngineerStaffCannotBeAssigned|FullyQualifiedName~CaseWorkflowPersistenceTests.StartMovesDirectlyToReportPreparationAndRetainedSentEvidenceNeedsNoApproval|FullyQualifiedName~CaseWorkflowPersistenceTests.StartRejectsEngineerDisabledAfterAssignment|FullyQualifiedName~AssessmentPersistenceIntegrationTests.AssessmentAccessUsesNativeStateAndRetainsWorkspaceWithoutAnExport|FullyQualifiedName~AssessmentPersistenceIntegrationTests.AssessmentWorkspaceLoadsInExactlySixReaderCommands|FullyQualifiedName~AssessmentPersistenceIntegrationTests.ReportDraftGenerationThroughProductionProjectionResolvesSignOffAndFailsClosedWithoutIt|FullyQualifiedName~CaseWorkspacePersistenceTests.ReviewGatedTransitionsReadThePersistedFactsNotThePostedOnes|FullyQualifiedName~CaseEngineerSectionsWebTests|FullyQualifiedName~CaseDetailsWebTests.NativeHandoffDialogPostsWithoutEvaOrASeparateReviewAction|FullyQualifiedName~CaseDetailsWebTests.WorkflowPageBindsReviewReturnEngineerAssignmentFindingAndLinkedReplacement|FullyQualifiedName~CaseDetailsWebTests.LifecyclePostsBindHoldReleaseAndNativeHandoffToAuthenticatedLease|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor`

Retained local evidence:

- artifacts/verification/case-049-core.trx:
  EEE5754A1286B5E5367B0AF4F03EDE596D7A9BDCC9CD8B960480F8B1D4E9CFC2.
- artifacts/verification/case-049-handoff.trx:
  2CFA92669142BA7DB422E95D55F8617D55D2772A6436BF1E9C67CF1D49E42336.
- artifacts/test-ui-capture: fresh default, conflict and unavailable captures
  produced with the real capture environment variable. Exact owning test
  names and source-freeze evidence remain in scratch/execution.md.

## Acceptance mapping

- Atomic eligible Engineer/sign-off/state and one event; stale version,
  invalid token, SystemWorker, missing/disabled/non-Engineer and incomplete
  readiness refusal: focused Core and SQL mutation cases.
- Exact replay after later staff disablement; changed replay discriminator
  refused; workflow version/history/lease and pre/post-handoff Sent timing:
  NativeHandoffIsAtomicGuardedAndReplaySurvivesLaterDisablement.
- Native access without export, retained read-only values across five states,
  report projection without export and unchanged six-reader query count:
  focused AssessmentPersistenceIntegrationTests and assessment policy.
- Actual native dialog/POST, no EVA dependency/second action, authenticated
  lease/readiness binding and read-only sections: focused Web cohort and the
  three actual Test UI captures.

## Risks, remaining work and handoff

Independent reviewer must read exact head/diff and evidence; no self-review.
After authorized merge, kanmer-verify owns exact merge-SHA acceptance,
reusing the focused filters above in proportion to integration risk.
Existing ENG-041 correction is separate and must retain its failed-proof
history. This ticket does not deliver TICK-085 PDF import or activate cloud
services. No foreign claim/worktree changed. Stop at Review; do not merge or
clean the retained author worktree.
