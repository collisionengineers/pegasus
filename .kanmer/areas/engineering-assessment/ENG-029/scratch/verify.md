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
