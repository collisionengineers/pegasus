---
kind: proof-record
merged_sha: "baafa29e0f7002b8235aa43bf333f5d9bb172828"
environment: ".worktrees/verify-eng-041-baafa29e0f7002b8235aa43bf333f5d9bb172828; Windows x64; PowerShell 7"
verified_at: "2026-09-08T00:26:34Z"
result: FAIL
failure_class: implementation
attempts:
  - attempted_at: "2026-09-08T00:20:40Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    exit_code: 0
    result: PASS
    summary: "Locked restore succeeded in clean exact-merge detached worktree."
  - attempted_at: "2026-09-08T00:20:40Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    exit_code: 0
    result: PASS
    summary: "61.48 seconds; zero warnings/errors."
  - attempted_at: "2026-09-08T00:21:54Z"
    command: "Focused Core EstimateTests command below"
    exit_code: 0
    result: PASS
    summary: "56 passed, zero failed/skipped; 212 ms."
  - attempted_at: "2026-09-08T00:21:58Z"
    command: "Focused integrated Glass/estimate/capture command below"
    exit_code: 1
    result: FAIL
    summary: "159 total, 157 passed, 2 failed, zero skipped; 2m42s."
---

# ENG-041 exact integrated verification — attempt 1

FAIL at PR683's confirmed dev merge baafa29e0f7002b8235aa43bf333f5d9bb172828.
Clean detached worktree with exact HEAD and correct shared Git common directory,
not the mutable author, shared checkout or board worktree. Postflight Git status
remains clean. Independent review27fbd21c5f009f14 passed author
8bbceb4fd190ae80a8b656540fd0ae5973f49895; it does not replace this merged check.

## Commands and observed failures

Core:
`dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Pegasus.Core.Tests.Assessment.EstimateTests' --logger 'trx;LogFileName=eng-041-merged-core.trx' --results-directory ./artifacts/verification`

Integration:
`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~GlassRepairEstimateGatewayTests|FullyQualifiedName~GlassRepairEstimatePersistenceTests|FullyQualifiedName~GlassRepairEstimateCallbackWebTests|FullyQualifiedName~AssessmentEstimateImportWebTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests.EarlierEstimateUpdateReplaysItsRecordedIdentityWithoutRevertingLaterEdits|FullyQualifiedName~CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey|FullyQualifiedName~CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName~TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor' --logger 'trx;LogFileName=eng-041-merged-integration.trx' --results-directory ./artifacts/verification`

Two failures, both in GlassRepairEstimateCallbackWebTests:

- TheSameReturnDeliveredTwiceRecordsNothingASecondTime, line389.
- TheProvidersReturnLandsTheDraftKeepsBothDocumentsAndCompletesTheSession,
  line325.

Both expected Completed, received AwaitingImport. Root has assigned read-only
diagnosis of the integrated custody/import interaction; the precise cause is
not yet confirmed. No assertion was weakened and no fresh PASS is claimed.

The shell guards stopped after the failed test command. Snapshot verification
and catalogue commands were NOT run. Root also observed that this command
mistakenly set unused PEGASUS_TEST_UI_CAPTURE/PEGASUS_TEST_UI_CAPTURE_ROOT
variables instead of the actual PEGASUS_TEST_UI_CAPTURE_DIR. Therefore the
passing route tests are functional evidence only, not fresh capture evidence.
The later correction must use the existing actual capture variable and selected
three route tests; do not rerun the whole passing cohort just to capture HTML.

## Artifacts and chronology

All artifacts currently remain in this verification worktree's
artifacts/verification. UTC TRX start/end instants below are converted from
recorded +01:00 values; approximate restore/build boundary times are marked
as such by this statement.

- eng-041-merged-core.trx:
  SHA256 426FB59B3880AC1DDC1967D5047E8CE9C37FECA68591DD8E79BEBE23FC985D54;
  00:21:54.7287689Z–00:21:56.5038977Z.
- eng-041-merged-integration.trx:
  SHA256 0C07D7F78B1928A9CE2FCD324223298612F7F34FCE7788F0A8C440BF735D2ED5;
  00:21:58.1139049Z–00:24:42.1965869Z.

Earlier author compiler failures, clock-double mismatch, missing-default
capture, review F-001/F-002 and CA1068 correction remain in the full original
reportf437cfd8a750979b and independent review history. This merged failure is
additional evidence and is not erased by those earlier passes.

## Disposition

Remain not Done, not deployed. No live Glass call, cloud mutation, source edit
or claim cleanup was performed by this verification. Preserve both worktrees,
both TRXs and the recorded author claim. After confirming the cause, route the
bounded correction through the existing ticket with a new dev-targeting PR,
independent review and focused exact-follow-up proof. CASE-049 remains untaken
until this shared-file owner is released.
