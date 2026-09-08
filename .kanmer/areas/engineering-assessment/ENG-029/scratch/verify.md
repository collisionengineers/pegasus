Author source handoff filters (not executed; root is the verifier).

Core:
```text
FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssessmentReportProjectionTests
```

Integration:
```text
FullyQualifiedName~CaseWorkspacePersistenceTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~CaseEngineerSectionsWebTests|FullyQualifiedName~CaseDetailsWebTests.WorkspaceSaveUsesCoreFindingAndEligibleSignOffAuthority|FullyQualifiedName~CaseDetailsWebTests.CaseSavePreservesUnpostedAcceptedFactsWithoutPromotingSuggestions|FullyQualifiedName~CaseDetailsWebTests.EngineeringEditorsShareTheCaseSaveAndRetainClearsAndFalseOnConflict|FullyQualifiedName~CaseDetailsWebTests.CraftedEngineeringSaveIsRefusedOutsideTheCoreEditStates|FullyQualifiedName~CaseDetailsWebTests.InvalidTypedEngineeringValueCannotBecomeASilentClear|FullyQualifiedName~CaseDetailsWebTests.ASaveCarriesTheClaimantContactNumberAndAddressThroughToTheCommand|FullyQualifiedName~CaseDetailsWebTests.ARefusedSaveKeepsTheProposedValuesForComparisonAndOffersNoApplyControl|FullyQualifiedName~CaseDetailsWebTests.AStaleVersionRefusalRequiresEditModeToBeEnteredAgain|FullyQualifiedName~CaseDetailsWebTests.ARefusalOnOneCaseSurvivesAVisitToAnother|FullyQualifiedName~CaseDetailsWebTests.RefusedRetentionKeepsEditorialValuesAndNeverIdentifiersOrRoutingFields|FullyQualifiedName~CaseDetailsWebTests.ARetainedValueTooLongToKeepIsReportedRatherThanTrimmedQuietly|FullyQualifiedName~CaseDetailsWebTests.TheRecordRendersOneEditorForEverySection|FullyQualifiedName~CaseDetailsWebTests.HoldingTheEditLeaseRendersEverySectionAndDefersNone|FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor
```

The final three named existing capture inputs cover conflict, Review+lease default, and unavailable. Use one fresh retained capture environment on the integration lane, then Update-TestUiSnapshots -SkipCapture -Scope case-details and -Verify -SkipCapture with the same capture directory, plus Test-UiCatalogue. Do not auto-OR a broad CaseDetails class. Root must inspect actual capture presence before claiming snapshot PASS. New Case-edit fixture methods contain10 xUnit cases (one3-case role/signoff theory, one4-case strict-state theory and three single cases). Existing test assertions are preserved; no runtime result or visual PASS is claimed yet.

## Corrected source freeze — 2026-09-08 06:10 UTC

Root-approved three first-runtime corrections are now frozen, plus the independent source finding: move proposed-value retention out of the unrelated ClaimLeaseAsync authorization catch into the actual ExecuteCommandAsync catch used by Case Save. Keep its Forbid result and clear authority. Existing RecordingCaseDetailsStore now uses its established ThrowNextFailure before recording a workspace save so one focused actual Web test can prove 403 plus retained editorial proposal, no write and no retained lease. No prior assertion was removed or weakened. Added one bounded forbidden-editor-path test preserving whole-save refusal after explicit prefix binding. Diff check PASS exit 0; 23 mapped files remain dirty. No author runtime.

Root correction filter (21 previously failed xUnit cases from eight method names; two new focused cases; two existing proposal checks; three known capture inputs):

```text
FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.ARefusedSaveKeepsTheProposedValuesForComparisonAndOffersNoApplyControl|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.ASaveCarriesTheClaimantContactNumberAndAddressThroughToTheCommand|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.AStaleVersionRefusalRequiresEditModeToBeEnteredAgain|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.CaseSavePreservesUnpostedAcceptedFactsWithoutPromotingSuggestions|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.WorkspaceSaveUsesCoreFindingAndEligibleSignOffAuthority|FullyQualifiedName~Pegasus.IntegrationTests.CaseEngineerSectionsWebTests.EveryLifecycleStateRendersAllEngineerSectionsAndRecordedValues|FullyQualifiedName~Pegasus.IntegrationTests.CaseEngineerSectionsWebTests.NewEstimateGetRendersReadOnlyEditorWhenNotEditable|FullyQualifiedName~Pegasus.IntegrationTests.Reports.AssessmentReportDraftWebTests.WorkspaceSavedReportAndSettlementFieldsReachTheActualPreview|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.AnAssessmentPathOutsideTheCaseEditorRefusesTheWholeSave|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.AnAuthorizationRefusalKeepsTheProposedCaseValuesWithoutKeepingEditAuthority|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.EngineeringEditorsShareTheCaseSaveAndRetainClearsAndFalseOnConflict|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.RefusedRetentionKeepsEditorialValuesAndNeverIdentifiersOrRoutingFields|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~Pegasus.IntegrationTests.TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor
```

Use a unique correction TRX; preserve original eng-029-integration.trx (38 pass / 21 fail). No claim of submitted-section non-confirmation SQL proof or manual visual PASS.

## Root initial runtime and independent corrections — 2026-09-08 06:14 UTC

Windows/PowerShell7, exact author worktree .worktrees/eng-029 on aefe base; sole heavy owner root. Session72086: diff --check and locked restore7 PASS; full Release build63.93s PASS0warnings0errors. Session42335: Core100 PASS131ms; focused Integration38PASS/21FAIL/0skip (59total), duration1m57s, exit1. Core artifact artifacts/verification/eng-029-core.trx SHA256397195ACE12E391DB5632B37A73A0138DCBDC09AA10740BDEB1439A280AB3FE9; Integration eng-029-integration.trx SHA256BE35CA056D98924E3A02B3A1224172B110C99D84EE04CDE9F538F3C661F7CECD, start2026-09-08T05:59:56.5356676Z finish06:01:56.3415878Z. Original captures retained; no snapshot acceptance from this failed run.

Actual failures separate into shared causes: optional assessment dictionary binding consumed unrelated ordinary form fields before Save/version check; accepted estimate fixture omitted recorded totals; authority test singleton tried resolving scoped staff query; root's minimal CaseDataHarness omitted mandatory report incident/instruction dates. Fixes target actual owners/fixtures, not production guards or assertions. Root's two preview cases now record the existing report fixture dates through real Overview save and use saved Case identity. Their assessment/Case fields are read from SQL; current estimate/signatory profile remain explicit existing source-fixture seams, not a claim of full persisted account resolution.

Independent source review additionally found actual Save authorization refusal missing proposed-value retention, and equal carried Fact/Confirmed values gaining refreshed staff attribution. Existing mapped catch is corrected; approved map4b2258b967332368 adds only shared EfCaseDataStore.SetConfirmed owner and exact prior assertion consumer (awaiting INTK064 handoff). Root added actual submitted-Overview SQL provenance/correction test and strengthened first workspace test to assert unchanged Facts/no fabricated confirmation. No runtime PASS for those corrections yet. Final selective rerun must include all21failed cases, known changed SQL consumers, proposal/unknown-key guards and3canonical captures; do not rerun unchanged100Core merely for fixture fixes.

## Final correction freeze after INTK-064 handoff

INTK-064 PR699 merged at 96777888bfa7ee7f85d63979a4a09ae10cda7d13 (2026-09-08T06:16:37Z). Root explicitly released only CaseDataCompletenessPersistenceTests.ConfirmAndSaveUseSharedVersionLeaseReplayAndImmutableHistory equal-name/VRM assertions. Whole plan b5d165f5ae417cd4 / files 9a7f6a62591c25fe updated and read back before edit; no stale no-overlap claim remains. Author changed only those two superseded expectations into equality of the whole original Fact/source and no redundant Confirmed; every other assertion remains. This worktree still has author base aefe4c32, and no dev merge/cherry-pick was performed. Preserve INTK-064's accepted constructor changes during root's later coordinated checkpoint/integration.

Root's SubmittedOverviewPreservesAcceptedProvenanceAndDoesNotConfirmAnUnpostedSuggestion and the existing OneWorkspaceSaveWritesOneWorkflowEventAndBumpsTheVersionExactlyOnce were reread, as were both override-date instances of WorkspaceSavedReportAndSettlementFieldsReachTheActualPreview and their mandatory-date fixture correction. Root's files were not edited by pack_reconcile. Writer no-op preserves actual changes, explicitly chosen suggestions and existing clear handling. No runtime PASS claimed yet.

Add these three exact method selectors to the prior 15-method correction filter:

```text
FullyQualifiedName~CaseWorkspacePersistenceTests.SubmittedOverviewPreservesAcceptedProvenanceAndDoesNotConfirmAnUnpostedSuggestion|FullyQualifiedName~CaseWorkspacePersistenceTests.OneWorkspaceSaveWritesOneWorkflowEventAndBumpsTheVersionExactlyOnce|FullyQualifiedName~CaseDataCompletenessPersistenceTests.ConfirmAndSaveUseSharedVersionLeaseReplayAndImmutableHistory
```

Final diff check PASS exit0. Exactly 25 mapped source/doc/test files are dirty; no capture files have yet been regenerated. Source is frozen. Root remains sole heavy verifier and owns capture/update/verify/catalogue.

## Corrected root runtime — 2026-09-08 06:38 UTC

Root session52531: existing Integration project Release build --no-restore PASS, exit0,54.67s,0warnings/errors. Unchanged100Core were not rerun. Session48296: exact18-method correction filter (stored above),31executed/31PASS/0skip,2m16s,exit0. artifacts/verification/eng-029-integration-corrected.trx SHA256045402CA0DBE8570923D8AF88CEDA07FE4F5F742BA57F44664F790977D0FF65A, actual UTC start06:33:05.7876076Z finish06:35:25.1320806Z. Covers all21originalfailed cases plus actual unchanged-provenance SQL, previous SaveCase consumer, authority/unknown-field/proposal guards and3canonical capture methods. Prior100Core+38Integration passes and21FAIL retained, not rewritten.

Capture script source inspection shows it deliberately uses artifacts/test-ui-capture, ignoring an externally supplied alternate directory. Root safely renamed the initial failed-run capture directory to artifacts/test-ui-capture-initial-failed (resolved exact paths within author artifacts, no overwrite) and made a new empty canonical capture directory before corrected tests. This preserves old evidence without accidentally regenerating from it. Session59742 scoped Update -SkipCapture -Scope case-details PASS3checks,exit0, fresh outputs; verify/catalogue session38933 is still pending.

Manual CUA local-file navigation was rejected by browser URL security policy; no workaround used. Manual1580/1100/760visual inspection is INCONCLUSIVE, no process execution/exit code. Existing offline1440 snapshot render checks are not a substitute for a claimed human visual review. Record this boundary in report; do not mark the ticket Done unless remaining plan acceptance is genuinely satisfied. This does not erase source/runtime acceptance or authorize deployment.

## F-004 source-frozen focused handoff

Root only: incremental Integration Release build, then the two existing actual failing callers with a unique correction TRX (no author execution):

```text
FullyQualifiedName~Pegasus.IntegrationTests.Browser.AssessmentReadinessSummaryBrowserTests.NotReadyReportDraftControlsStateTheConditionAndTheShellRenders|FullyQualifiedName~Pegasus.IntegrationTests.AssessmentEstimateImportWebTests.UseEstimateRecordsTheEngineersAcceptance
```

Both files use existing fixture/production owners, all prior assertions preserved. Browser source adds one positive metadata-read assertion and aligns current state; accepted estimate fake records one computed breakdown/basis and retains already accepted totals. No Razor/source/snapshot delta in this correction, so no automatic broad capture or screenshot regeneration is requested. Existing manual visual acceptance remains outstanding. Source freeze+diffcheckPASS is not runtime PASS. Preserve prior root TRXs and all CI34196369756 failures; do not rerun their unrelated cohorts.

## Sole-host F-004 verification — PASS (2026-09-08)

Verifier: `/root/agent_config_verifier`
Queue lane: 3/4
Frozen worktree: `.worktrees/eng-029`
Frozen branch: `ENG-029-case-workspace-editors`
Frozen head: `f86054c0e7cc73cb6245355dd21c03e58196d582`

Preflight at `2026-09-08T14:48:27.6971066Z` found only idle reusable MSBuild nodes left by the preceding serialized lane and no testhost/vstest or active competing command. Source status was exactly the two approved F-004 fixture files, 32 insertions/10 deletions; `git diff --check` exited 0 (repository LF→CRLF advisory only). Binary diff hash: `bc859ac1a9125523c1f49e7bab02837053e89dd2`.

### Commands

1. `dotnet build ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-restore`
   - attempted_at: `2026-09-08T14:48:35.8600003Z`
   - exit_code: **0**
   - result: PASS
   - summary: Build succeeded; 0 warnings, 0 errors.
2. `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Pegasus.IntegrationTests.Browser.AssessmentReadinessSummaryBrowserTests.NotReadyReportDraftControlsStateTheConditionAndTheShellRenders|FullyQualifiedName~Pegasus.IntegrationTests.AssessmentEstimateImportWebTests.UseEstimateRecordsTheEngineersAcceptance" --logger "trx;LogFileName=eng-029-f004-host-20260908.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T14:49:58.0108709Z`
   - exit_code: **0**
   - result: PASS
   - summary: Failed 0, Passed 2, Skipped 0, Total 2.
   - TRX: `artifacts/verification/eng-029-f004-host-20260908.trx`
   - TRX SHA-256: `976C6702C25F578C496438F6E6DA4E9D1377BF5E3885474F828EE41B5722EF9F`

Postcheck at `2026-09-08T14:50:56.5167692Z` retained the same two-file status, clean diff check, and exact binary diff hash. Only idle reusable MSBuild nodes remained. No broader browser/capture/SQL cohort, rerun, source write, commit, push, PR update, merge, or stage mutation was performed. All previously recorded failures remain untouched.

Disposition for the bounded F-004 caller lane: **PASS**. This does not satisfy or waive ENG-029's separately recorded multi-width manual visual acceptance, which remains INCONCLUSIVE/outstanding.

## Merged-head F-004/documentation revalidation — PASS (2026-09-08)

Verifier: `/root/agent_config_verifier`
Frozen worktree: `.worktrees/eng-029`
Branch: `ENG-029-case-workspace-editors`
Exact merged head: `2cde68831485bfc426e062a83030edea661f976d`
Parents: fixture commit `fef8909e2c256388c4d738ddf78f176e0e1b5cef`; accepted dev `a1f0bfe260ea05df531df6e0ca3109141e7697da`.

Preflight at `2026-09-08T15:06:23.1922307Z` confirmed the exact clean branch/head and no active testhost/vstest command. The `fef8909…2cde688` range contained no `*.cs`, `*.csproj`, `*.props`, `*.targets`, `*.sln`, `global.json`, or `NuGet.config` change, so the already completed matching Release build was reused as authorized. The combined read-only probe exited 1 only because it also looked for the now-removed `scripts/verify_docs_links.py`; current HEAD owns `scripts/Test-DocumentationLinks.ps1`, which was inspected and then run below. This was not a product or documentation check failure and caused no retry of a verification command.

### Commands

1. `pwsh -NoProfile -File ./scripts/Test-DocumentationLinks.ps1`
   - attempted_at: `2026-09-08T15:06:52.4419821Z`
   - exit_code: **0**
   - result: PASS
   - summary: all relative Markdown links resolve; 140 files checked.
2. `pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base fef8909e2c256388c4d738ddf78f176e0e1b5cef -Head 2cde68831485bfc426e062a83030edea661f976d`
   - attempted_at: `2026-09-08T15:07:02.4896615Z`
   - exit_code: **0**
   - result: PASS
3. `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Pegasus.IntegrationTests.Browser.AssessmentReadinessSummaryBrowserTests.NotReadyReportDraftControlsStateTheConditionAndTheShellRenders|FullyQualifiedName~Pegasus.IntegrationTests.AssessmentEstimateImportWebTests.UseEstimateRecordsTheEngineersAcceptance" --logger "trx;LogFileName=eng-029-merged-f004-host-20260908.trx" --results-directory artifacts/verification`
   - attempted_at: `2026-09-08T15:07:14.8383702Z`
   - exit_code: **0**
   - result: PASS
   - summary: Failed 0, Passed 2, Skipped 0, Total 2.
   - TRX SHA-256: `548339BFEE2D8945366FB063CA602D620C334D65AF73C03A63A0E3B12CBEB077`.

Postcheck at `2026-09-08T15:08:07.2365852Z` confirmed exact HEAD and clean source status. Only idle reusable MSBuild nodes remained. No build, restore, source write, capture, broader test, commit, push, merge, or stage mutation was performed.

Disposition: **PASS** for the merged-head documentation and two F-004 callers. The separately recorded 1580/1100/760 manual visual acceptance remains **INCONCLUSIVE/outstanding** and is neither satisfied nor waived by this pass.

2026-09-08 post-merge handoff: root confirmed PR700 MERGED into dev at 05995d325cc4c1ccd44096bf69d05fd42eeda3d2, ticket Verifying. Read current plan07f29f3b2342f40f/gates and dry-run reconcile: no recommendation, unavailable reachability/check facts retained. Bound contract pr.yml/verify/push exact-SHA lookup exited1 HTTP404 absent workflow BEFORE any verification Git operation. No qualifying receipt; current focused/runtime/scoped snapshot and manual obligations therefore remain missing for exact-merge proof, not inferred from PR-head results. No verification worktree or new ENG command started; sole host remains on D56/D58/INTK checks. F005 1580/1100/760 editable/read-only/conflict acceptance remains INCONCLUSIVE and blocks Done/D8; approved integration-first sequencing is not a visual waiver. Retain existing author worktree/branch/claim as traceability and any exact-merge work must use a separate validated detached target after receipt classification.

## Exact-merge runtime verification — 2026-09-08

Target: GitHub merge commit `05995d325cc4c1ccd44096bf69d05fd42eeda3d2`.
Environment: `C:\Users\Alex\Documents\GitHub\pegasus\.worktrees\verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2`, Windows PowerShell 7 / .NET SDK 10.0.302.
Receipt classification was completed before Git: declared `pr.yml` / `verify` / `push` exact-SHA lookup returned HTTP 404, so every packet obligation was missing and no receipt was rejected.

Ordered setup and runtime ledger:

1. 2026-09-08T16:17:43.8735947Z–2026-09-08T16:17:44.7698528Z — `git fetch origin` from the normal source checkout, exit 0.
2. 2026-09-08T16:17:54.9718071Z–2026-09-08T16:17:57.0761477Z — `git worktree add --detach .worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2 05995d325cc4c1ccd44096bf69d05fd42eeda3d2`, exit 0.
3. 2026-09-08T16:18:12.1716833Z–2026-09-08T16:18:12.3631128Z — preflight: `git rev-parse HEAD` exit 0 returned the exact target; `git symbolic-ref --short -q HEAD` exit 1/empty proved detached; `git status --short --branch` exit 0 returned only `## HEAD (no branch)`. Host-process census at 2026-09-08T16:18:30.2627712Z found zero Pegasus/dotnet command lines referencing the exact worktree.
4. 2026-09-08T16:18:45.0286135Z–2026-09-08T16:18:50.4127979Z — `dotnet restore ./Pegasus.slnx --locked-mode`, exit 0; seven projects restored.
5. 2026-09-08T16:18:59.8277097Z — `dotnet build ./Pegasus.slnx --configuration Release --no-restore` attempt A ran and emitted the Core output, then exceeded the 30-second command-yield boundary. The wrapper forwarded only its output field and did not retain the returned native session object, so its completion timestamp and exit are unavailable. Record this attempt as INCONCLUSIVE/unknown exit; do not infer PASS or erase it.
6. 2026-09-08T16:20:08.2180129Z–2026-09-08T16:20:35.2154901Z — the same exact build command was mistakenly started before attempt A's complete compiler lifetime had been established. Exit 1, 0 warnings/1 error: CS2012 could not open `src/Pegasus.Worker/obj/Release/net10.0/Pegasus.Worker.dll` for writing because another process used it. This FAIL is retained.
7. Read-only mechanism/settle diagnostics: at 2026-09-08T16:22:03.6222754Z, six persistent MSBuild nodes from 17:18:45 local had vanished parent PID 16276, six nodes from 17:20:09 had vanished parent PID 25076, and VBCSCompiler PID 7684 had vanished parent PID 19460; `handle.exe Pegasus.Worker.dll` and `openfiles /query` found no holder. At 2026-09-08T16:23:15.6322760Z all 13 retained build-server/compiler processes showed CPU delta 0 over two seconds and the exact Worker output opened successfully with exclusive access.
8. 2026-09-08T16:23:38.2075578Z–2026-09-08T16:23:47.7598287Z — one operator-authorized unchanged-SHA retry of `dotnet build ./Pegasus.slnx --configuration Release --no-restore`, exit 0: Build succeeded, 0 warnings, 0 errors. Together with unchanged source, idle/lock evidence and the exact successful retry, this establishes the retained CS2012 attempt as environmental overlap/file locking; attempt A remains honestly unknown.
9. 2026-09-08T16:24:05.8356902Z–2026-09-08T16:24:08.4193929Z — `dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter "FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssessmentReportProjectionTests" --logger "trx;LogFileName=eng-029-core-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx" --results-directory ./artifacts/verification`, exit 0: 100 passed, 0 failed, 0 skipped.
10. Two orchestration composition attempts made after Core failed in JavaScript parsing at 0.0 seconds (`Invalid or unexpected token`; then `Unexpected string`). Neither invoked PowerShell, dotnet, a test host or any shell command. They are retained as tool-operation notes, not test attempts or test failures.
11. 2026-09-08T16:26:23.2545806Z–2026-09-08T16:28:53.2811100Z — `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~CaseWorkspacePersistenceTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~CaseEngineerSectionsWebTests|FullyQualifiedName~CaseDetailsWebTests.WorkspaceSaveUsesCoreFindingAndEligibleSignOffAuthority|FullyQualifiedName~CaseDetailsWebTests.CaseSavePreservesUnpostedAcceptedFactsWithoutPromotingSuggestions|FullyQualifiedName~CaseDetailsWebTests.EngineeringEditorsShareTheCaseSaveAndRetainClearsAndFalseOnConflict|FullyQualifiedName~CaseDetailsWebTests.CraftedEngineeringSaveIsRefusedOutsideTheCoreEditStates|FullyQualifiedName~CaseDetailsWebTests.InvalidTypedEngineeringValueCannotBecomeASilentClear|FullyQualifiedName~CaseDetailsWebTests.ASaveCarriesTheClaimantContactNumberAndAddressThroughToTheCommand|FullyQualifiedName~CaseDetailsWebTests.ARefusedSaveKeepsTheProposedValuesForComparisonAndOffersNoApplyControl|FullyQualifiedName~CaseDetailsWebTests.AStaleVersionRefusalRequiresEditModeToBeEnteredAgain|FullyQualifiedName~CaseDetailsWebTests.ARefusalOnOneCaseSurvivesAVisitToAnother|FullyQualifiedName~CaseDetailsWebTests.RefusedRetentionKeepsEditorialValuesAndNeverIdentifiersOrRoutingFields|FullyQualifiedName~CaseDetailsWebTests.ARetainedValueTooLongToKeepIsReportedRatherThanTrimmedQuietly|FullyQualifiedName~CaseDetailsWebTests.TheRecordRendersOneEditorForEverySection|FullyQualifiedName~CaseDetailsWebTests.HoldingTheEditLeaseRendersEverySectionAndDefersNone|FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor|FullyQualifiedName~CaseDetailsWebTests.AnAssessmentPathOutsideTheCaseEditorRefusesTheWholeSave|FullyQualifiedName~CaseDetailsWebTests.AnAuthorizationRefusalKeepsTheProposedCaseValuesWithoutKeepingEditAuthority|FullyQualifiedName~CaseDataCompletenessPersistenceTests.ConfirmAndSaveUseSharedVersionLeaseReplayAndImmutableHistory|FullyQualifiedName~AssessmentReadinessSummaryBrowserTests.NotReadyReportDraftControlsStateTheConditionAndTheShellRenders|FullyQualifiedName~AssessmentEstimateImportWebTests.UseEstimateRecordsTheEngineersAcceptance" --logger "trx;LogFileName=eng-029-integration-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx" --results-directory ./artifacts/verification`, exit 0: 65 passed, 0 failed, 0 skipped, duration 2m27s. Native session 33549 was retained through four empty 30-second polls and its final exit object.

Retained artifacts:

- `artifacts/verification/eng-029-core-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx` — SHA-256 `1D8399DAAB76C44D5BFEAFFF06E604C5FB7E0EF5D15A38DA9FEB8DB3E9146D73`.
- `artifacts/verification/eng-029-integration-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx` — SHA-256 `B479188CAA42D5CA49565ECCA8E9088BF5C4C841A5E6E336F3665F74A6433C13`.

This scratch entry records only the authorized exact-SHA runtime chunk. Snapshot/capture, manual F-005, checklist, proof, stage and cleanup remain untouched.

## Exact-merge fresh capture and verify-first result — 2026-09-08

Target/worktree remained `05995d325cc4c1ccd44096bf69d05fd42eeda3d2` / `.worktrees/verify-eng-029-05995d325cc4c1ccd44096bf69d05fd42eeda3d2`. At 2026-09-08T16:38:34.2009858Z the canonical `artifacts/test-ui-capture` directory was absent and no scoped dotnet/testhost/vstest process was active. It was created fresh, never overwritten.

The capture-only environment set `PEGASUS_TEST_UI_CAPTURE_DIR` to that canonical resolved directory for the exact test process and restored the prior environment in `finally`.

- 2026-09-08T16:39:00.8810816Z–2026-09-08T16:39:40.5278538Z — `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~Pegasus.IntegrationTests.CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~Pegasus.IntegrationTests.TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor" --logger "trx;LogFileName=eng-029-capture-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx" --results-directory ./artifacts/verification`, exit 0: 3 passed, 0 failed, 0 skipped; environment restored.
- Capture census at 2026-09-08T16:39:53.3882938Z found 20 files (10 `response.html` + `response.json` pairs), all freshly timestamped. Ordered relative-path/length/content-hash manifest SHA-256: `6D863F150A2496C8C96E28E65047061B177C2991E26B27D16FE1789C54C8F1F0`.
- Capture TRX SHA-256: `26A6E0FD2A14469428A411D77A872D8D82238D4E53A7D0B04C5DA0C205DBE9EE`.

Verify-first then ran against committed snapshots:

- 2026-09-08T16:40:07.1282273Z–2026-09-08T16:40:10.7248905Z — `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details`, exit 1: 2 passed, 1 failed. Authoritative failure: `Generated Test UI file is stale: pages/case-details--conflict.html`; `CapturedRazorResponsesMatchCommittedTestUiSnapshots` failed at `TestUiSnapshotTests.cs:118`. The script threw `Test UI phase 'Snapshot verify' failed with exit code 1`.

Stopped immediately. `Test-UiCatalogue.ps1` was not run; snapshot update/new baseline was not run. Fresh captures remain preserved. Post-failure `git status --short --branch` returned only `## HEAD (no branch)`; `git diff -- docs/design/test-ui/pages/case-details--conflict.html` was empty because verify mode did not mutate committed files. Result remains FAIL/outstanding in Verifying; no manual workaround, proof or stage action occurred.

## Verification-selector correction — root approval, 8 September 2026

The failed 16:40 verify-first attempt is retained. Independent read-only diagnosis by correction_ownership and root literal source inspection found that the three-method verification capture omitted the committed Save-conflict scenario: its completeness-toggle conflict renders a different valid state. The committed conflict snapshot instead has only reason `Corrected claimant spelling` and claimant `Rebecca Proposed`, retained server authority and `Recover editing`. Existing `CaseDetailsWebTests.AStaleVersionRefusalRequiresEditModeToBeEnteredAgain` claims the lease, submits exactly those values, and asserts the recovery state. This is a missing verification capture input, not evidence of wrong runtime behavior or authority to regenerate the committed baseline.

The governing writer/conflict acceptance and committed snapshot remain unchanged. Root authorizes the sole host verifier, after its active D56 lane finishes, to capture ONLY that existing exact method at merge SHA 05995d325cc4c1ccd44096bf69d05fd42eeda3d2 using the same capture-only environment and a unique TRX. Preserve the original 20 files and their hashes: TestUiResponseCapture uses request-plus-HTML hash identities and write-once output, so adding the missing input needs no deletion, replacement, repeated default/unavailable capture or source change. Then run the same `Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details` against the unchanged committed bytes, followed by `Test-UiCatalogue.ps1` only on PASS. Stop and report a remaining mismatch; do not update snapshots, change the state matcher/normalizer, broaden selectors or claim F-005 visual PASS. The earlier failed attempt remains a failed verification-input selection and is not reclassified as transient.

Authority clarification: the controlled earlier build retry was authorized by root/the coordinating agent under the existing task, not by a fresh operator message.

## Exact-merge capture-selector correction — PASS — 2026-09-08

Root/controller authorized one additional exact capture method after read-only mismatch diagnosis established that the first three-method selection did not produce the committed Save-conflict candidate. No source or baseline update was authorized.

Preflight 2026-09-08T16:50:05.7007305Z–2026-09-08T16:50:06.0765828Z confirmed the retained canonical capture set had exactly 20 files with unchanged ordered manifest SHA-256 `6D863F150A2496C8C96E28E65047061B177C2991E26B27D16FE1789C54C8F1F0` and no scoped process.

- 2026-09-08T16:50:37.8438438Z–2026-09-08T16:51:14.0683968Z — `dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter "FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.AStaleVersionRefusalRequiresEditModeToBeEnteredAgain" --logger "trx;LogFileName=eng-029-capture-stale-version-05995d325cc4c1ccd44096bf69d05fd42eeda3d2.trx" --results-directory ./artifacts/verification`, exit 0: 1 passed, 0 failed, 0 skipped, 33s. Session `26266` retained to actual exit; capture environment restored. Write-once census: original 20 changed 0/missing 0; six new files (three response pairs), final count 26.
- 2026-09-08T16:51:25.7184396Z–2026-09-08T16:51:33.8913251Z — `pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope case-details`, exit 0: 3 passed, 0 failed, 0 skipped.
- 2026-09-08T16:51:48.6582129Z–2026-09-08T16:51:50.5667176Z — `pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1`, exit 0: 60 routed sources, 67 prototypes, 0 broken local references.

Additional capture TRX SHA-256: `CEAC3F40243BFF3FD5DF3EE93B3390B2ED110847777AADFE0D1EE5D55CA06647`. Final 26-file ordered capture manifest SHA-256: `FF9EF56AEBC4165249A8ACA3A24EBD81ED9EC685C0B41090AAB44528086F5796`. Final Git status remained only `## HEAD (no branch)`.

The earlier verify-first failure remains retained and is explained by incomplete capture selection, not erased. No snapshot update/new baseline, source edit, broad recapture, build, manual waiver/workaround, proof, stage move or cleanup occurred. Runtime + scoped snapshot/catalogue obligations now PASS at the exact merge SHA; mandatory F005 multi-width manual inspection remains separately outstanding.
