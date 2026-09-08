---
kind: proof-record
schema: 2
merged_sha: "05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
environment: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2; Windows PowerShell 7; .NET SDK 10.0.302"
verified_at: "2026-09-08T16:55:37Z"
result: INCONCLUSIVE
failure_class: inconclusive
receipts: []
attempts:
  - attempted_at: "2026-09-08T16:18:45.0286135Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Locked restore succeeded for seven projects."
  - attempted_at: "2026-09-08T16:18:59.8277097Z"
    exit_code: null
    result: INCONCLUSIVE
    authority: supporting
    failure_class: inconclusive
    summary: "Build A actually started, but its yielded session handle was discarded by the wrapper. Completion/exit are unavailable, not inferred PASS."
  - attempted_at: "2026-09-08T16:20:08.2180129Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 1
    result: FAIL
    authority: supporting
    failure_class: transient
    summary: "Build B overlapped unsettled compiler activity and failed CS2012 on Worker output. Source was unchanged; vanished parents, idle CPU and released exact file lock preceded successful same-command same-SHA retry. No process was killed."
  - attempted_at: "2026-09-08T16:23:38.2075578Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Root-authorized unchanged-SHA build retry succeeded with zero warnings/errors."
  - attempted_at: "2026-09-08T16:24:05.8356902Z"
    command: "dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssessmentReportProjectionTests\" --logger \"trx;LogFileName=eng-029-core-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx\" --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Focused Core 100/100 PASS, no skips."
  - attempted_at: "2026-09-08T16:26:23.2545806Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~CaseWorkspacePersistenceTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~CaseEngineerSectionsWebTests|FullyQualifiedName~CaseDetailsWebTests.WorkspaceSaveUsesCoreFindingAndEligibleSignOffAuthority|FullyQualifiedName~CaseDetailsWebTests.CaseSavePreservesUnpostedAcceptedFactsWithoutPromotingSuggestions|FullyQualifiedName~CaseDetailsWebTests.EngineeringEditorsShareTheCaseSaveAndRetainClearsAndFalseOnConflict|FullyQualifiedName~CaseDetailsWebTests.CraftedEngineeringSaveIsRefusedOutsideTheCoreEditStates|FullyQualifiedName~CaseDetailsWebTests.InvalidTypedEngineeringValueCannotBecomeASilentClear|FullyQualifiedName~CaseDetailsWebTests.ASaveCarriesTheClaimantContactNumberAndAddressThroughToTheCommand|FullyQualifiedName~CaseDetailsWebTests.ARefusedSaveKeepsTheProposedValuesForComparisonAndOffersNoApplyControl|FullyQualifiedName~CaseDetailsWebTests.AStaleVersionRefusalRequiresEditModeToBeEnteredAgain|FullyQualifiedName~CaseDetailsWebTests.ARefusalOnOneCaseSurvivesAVisitToAnother|FullyQualifiedName~CaseDetailsWebTests.RefusedRetentionKeepsEditorialValuesAndNeverIdentifiersOrRoutingFields|FullyQualifiedName~CaseDetailsWebTests.ARetainedValueTooLongToKeepIsReportedRatherThanTrimmedQuietly|FullyQualifiedName~CaseDetailsWebTests.TheRecordRendersOneEditorForEverySection|FullyQualifiedName~CaseDetailsWebTests.HoldingTheEditLeaseRendersEverySectionAndDefersNone|FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor|FullyQualifiedName~CaseDetailsWebTests.AnAssessmentPathOutsideTheCaseEditorRefusesTheWholeSave|FullyQualifiedName~CaseDetailsWebTests.AnAuthorizationRefusalKeepsTheProposedCaseValuesWithoutKeepingEditAuthority|FullyQualifiedName~CaseDataCompletenessPersistenceTests.ConfirmAndSaveUseSharedVersionLeaseReplayAndImmutableHistory|FullyQualifiedName~AssessmentReadinessSummaryBrowserTests.NotReadyReportDraftControlsStateTheConditionAndTheShellRenders|FullyQualifiedName~AssessmentEstimateImportWebTests.UseEstimateRecordsTheEngineersAcceptance\" --logger \"trx;LogFileName=eng-029-integration-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx\" --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Focused Integration 65/65 PASS, no skips; includes relevant actual partial CaseDetails class methods from CaseEditModeWebTests.cs."
  - attempted_at: "2026-09-08T16:39:00.8810816Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~Pegasus.IntegrationTests.TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor\" --logger \"trx;LogFileName=eng-029-capture-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx\" --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Three capture methods 3/3 PASS produced 20 fresh files; environment restored."
  - attempted_at: "2026-09-08T16:40:07.1282273Z"
    command: "pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 1
    result: FAIL
    authority: supporting
    failure_class: plan
    summary: "Verify-first 2 PASS/1 FAIL: conflict snapshot differed because the narrowed input selection omitted the committed Save-conflict scenario and supplied a completeness-toggle conflict. Verification-input plan error, not a runtime defect or transient; no baseline update."
  - attempted_at: "2026-09-08T16:50:37.8438438Z"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.AStaleVersionRefusalRequiresEditModeToBeEnteredAgain\" --logger \"trx;LogFileName=eng-029-capture-stale-version-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx\" --results-directory ./artifacts/verification"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Missing existing Save-conflict capture 1/1 PASS. Original 20 files unchanged; six added; no source/baseline change."
  - attempted_at: "2026-09-08T16:51:25.7184396Z"
    command: "pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Same verification command passed 3/3 against unchanged committed snapshots after completing capture inputs."
  - attempted_at: "2026-09-08T16:51:48.6582129Z"
    command: "pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1"
    cwd: ".worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2"
    exit_code: 0
    result: PASS
    authority: supporting
    summary: "Catalogue PASS: 60 routes, 67 prototypes, zero broken local references."
  - attempted_at: "2026-09-08T16:55:37Z"
    exit_code: null
    result: INCONCLUSIVE
    authority: authoritative
    failure_class: inconclusive
    summary: "F-005 manual acceptance at 1580/1100/760 for editable, read-only and conflict states remains unperformed on a supported accessible surface. Prior local-file browser security refusal is retained; no workaround, substitute snapshot claim or waiver. A supported local/deployed surface and actual visual/keyboard/recovery inspection are needed. No manual process ran in this post-merge lane."
---

# ENG-029 exact-merge verification

PR #700 was independently reviewed and merged to dev at 05995d325cc4c1ccd44096bf69d05fd42eeda3d2. The configured pr.yml/verify/push exact-SHA lookup returned HTTP 404 before verification Git operations; no qualifying receipt exists. The sole host verifier ran the missing scoped checks in a clean exact detached worktree. Source and committed snapshots remained unchanged.

## Obligations

Locked restore, Release compilation, 100 focused Core cases, 65 focused Integration cases, complete scoped Case captures, committed snapshot comparison (3/3), and catalogue (60 routes/67 prototypes/0 broken references) now pass. CaseEditModeWebTests.cs declares a partial CaseDetailsWebTests class; its relevant methods were selected under actual FQNs. The intended Save-conflict capture came from its existing test; no new test/helper/framework or snapshot regeneration was needed.

Manual F-005 remains outstanding, so overall INCONCLUSIVE leaves the ticket Verifying, not Done. Approved integration/deployment-first sequencing does not waive it; it must complete before D8 acceptance. This proof neither authorizes deployment nor supplies a passing release-candidate rail.

## Retained attempts and artifacts

Every post-merge non-PASS is recorded above. Build A's missing completion is explicitly unknown; B's actual CS2012 exit 1 is preserved with observed overlap/file-lock mechanism and unchanged-SHA successful retry. Two JavaScript composition parse errors invoked no shell/test process and remain tool-operation notes in scratch. The initial snapshot verification failure remains an incomplete capture-selection error; the unchanged baseline passed after adding its existing Save-conflict input. No failure is erased.

Earlier author-head runtime/fixture failures, PR700 CI34196369756 failures, F-004 corrections and manual local-file refusal remain in historical scratch/report/review. They are not exact-merge runs.

Full setup, commands, times, exits and authority corrections: scratch/verify.md@6d27753e8b9e2e0a. Four TRXs and 26 capture files are retained outside the worktree under artifacts/verification/eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2/.

- Core TRX: 1D8399DAAB76C44D5BFEAFFF06E604C5FB7E0EF5D15A38DA9FEB8DB3E9146D73.
- Integration TRX: B479188CAA42D5CA49565ECCA8E9088BF5C4C841A5E6E336F3665F74A6433C13.
- Initial capture TRX: 26A6E0FD2A14469428A411D77A872D8D82238D4E53A7D0B04C5DA0C205DBE9EE.
- Added Save-conflict TRX: CEAC3F40243BFF3FD5DF3EE93B3390B2ED110847777AADFE0D1EE5D55CA06647.
- Final 26-file capture manifest: FF9EF56AEBC4165249A8ACA3A24EBD81ED9EC685C0B41090AAB44528086F5796; original 20 unchanged.

Retain implementation and detached verification worktrees and the taken claim for resumption. No cleanup, archive, release, stage change or visual PASS follows from this record.
