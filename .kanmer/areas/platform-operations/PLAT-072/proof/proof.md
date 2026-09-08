---
kind: proof-record
merged_sha: "d442366787d452da22d36719272d4eb79dc1afde"
environment: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde; Windows x64; PowerShell 7"
verified_at: "2026-09-08T00:08:21Z"
result: PASS
attempts:
  - attempted_at: "2026-09-08T00:01:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde"
    exit_code: 0
    result: PASS
    summary: "Locked restore succeeded."
  - attempted_at: "2026-09-08T00:01:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde"
    exit_code: 0
    result: PASS
    summary: "64.59 seconds; zero warnings/errors."
  - attempted_at: "2026-09-08T00:02:36Z"
    command: "Focused Core test command below"
    cwd: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde"
    exit_code: 0
    result: PASS
    summary: "24 passed, zero failed/skipped."
  - attempted_at: "2026-09-08T00:02:41Z"
    command: "Focused integration test command below"
    cwd: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde"
    exit_code: 0
    result: PASS
    summary: "36 passed, zero failed/skipped; 109 seconds."
  - attempted_at: "2026-09-08T00:04:33Z"
    command: "pwsh -NoProfile -File ./scripts/Update-TestUiSnapshots.ps1 -Verify -SkipCapture -Scope 'case-create,case-details'"
    cwd: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde"
    exit_code: 0
    result: PASS
    summary: "Fresh actual routed responses; 2 snapshot checks passed."
  - attempted_at: "2026-09-08T00:04:45Z"
    command: "pwsh -NoProfile -File ./scripts/Test-UiCatalogue.ps1"
    cwd: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde"
    exit_code: 0
    result: PASS
    summary: "60 routed sources; 67 prototypes; zero broken references."
  - attempted_at: "2026-09-08T00:04:45Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1"
    cwd: ".worktrees/verify-plat-072-d442366787d452da22d36719272d4eb79dc1afde"
    exit_code: 0
    result: PASS
    summary: "102 migration files; every created table granted or exempted."
---

# PLAT-072 exact integrated proof

PASS on dev PR688 merge d442366787d452da22d36719272d4eb79dc1afde,
confirmed by GitHub merged2026-09-07T23:59:10Z. Root read complete independent
review318c3bd04afeb794 for author278f605333f7569fb1927c3d4ed360d080903453
before merge and refreshed head, plan, threads, effective rules/checks and
pushed board state. No required check was configured; empty CI was not green.

## Workspace and attempts

Fresh detached exact-merge worktree, correct common Git directory and clean
state before and after verification. The initially created short local name
plat-072-verify was corrected with normal git worktree move to the mandated
full-SHA name before commands; both absolute paths were explicitly validated,
the destination was absent, and no work/data was discarded. No mutable
shared checkout, board worktree or author worktree was changed by verification.

Attempt times other than TRX starts are approximate UTC command-boundary times;
elapsed build/test durations and exits are actual recorded outputs.

## Exact test commands

Core:
`dotnet test ./tests/Pegasus.Core.Tests/Pegasus.Core.Tests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~Pegasus.Core.Tests.Cases.AutomaticCaseReadinessTests|FullyQualifiedName~Pegasus.Core.Tests.Cases.CaseDataOperationsTests' --logger 'trx;LogFileName=plat-072-merged-core.trx' --results-directory ./artifacts/verification`

Integration:
`dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter 'FullyQualifiedName~CaseCreateWebTests|FullyQualifiedName~CaseDataCompletenessPersistenceTests|FullyQualifiedName~CaseAcceptanceReplayTests|FullyQualifiedName=Pegasus.IntegrationTests.IntakePersistenceIntegrationTests.CommittedMigrationCreatesTheSqlServerSchema|FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss|FullyQualifiedName=Pegasus.IntegrationTests.QdosAllocationRecoveryTests.InterruptedPendingOperationResumesThroughIdempotentAtomicAcceptance|FullyQualifiedName=Pegasus.IntegrationTests.RetainedMailPersistenceTests.ASucceededAllocationAttemptResolvesTheCaseWithoutALinkRow|FullyQualifiedName~RailCountsWebTests|FullyQualifiedName=Pegasus.IntegrationTests.ImageIntakeWebTests.ConfidentReadAutoRegistersAndAutoAssociatesTheUnambiguousCase|FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.HoldingTheEditLeaseRendersEverySectionAndDefersNone|FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.ARefusedCompletenessChangeKeepsUncheckedProposalsBesideTheCurrentValues|FullyQualifiedName=Pegasus.IntegrationTests.TestUiFocusedRenderTests.CaseUnavailableAndErrorStatesRenderThroughRazor|FullyQualifiedName=Pegasus.IntegrationTests.CaseDetailsWebTests.CustodyRetryAndExportRoutesBindAntiforgeryHumanActorLeaseWorkflowVersionReasonAndKey' --logger 'trx;LogFileName=plat-072-merged-integration.trx' --results-directory ./artifacts/verification`

Capture env: PEGASUS_TEST_UI_CAPTURE_DIR injected as this worktree's
artifacts/test-ui-capture; PEGASUS_TEST_UI_SCOPE=case-create,case-details.
UI mode unset during capture. Snapshot verification reuses this fresh capture,
not the author output.

## Artifact identities

- plat-072-merged-core.trx: SHA25649D661C1A3FDE365CA2FC35A837460BDF0BF179F32C181D66D6BC913B2C2E7ED; UTC00:02:36.6999030–00:02:39.1630458.
- plat-072-merged-integration.trx: SHA2561D50C815EBA1CBBFCE40D6A225FF9D27907D4B5256E5AB0A7BC1702C71753725; UTC00:02:41.3808378–00:04:33.0567636.

## Claim proved

Actual Create/accept/replay/completeness/current SQL callers work without
retired confirmation flags. The migration drops exactly four obsolete columns;
Down/Up preserves real case identity, facts, origin, state/version and history.
Current model matches, operation replay remains guarded, and historical schema
fixtures remain unchanged. Existing table grants suffice. Snapshots agree
with the actual merged Create and Details routes.

No manual visual, production migration, deployment or full v1 acceptance is
claimed. CASE-049 separately owns native handoff/access and must start from
this accepted integrated shape.

## Prior author failures retained

The author verification report74ccb3321b12fb9e and scratch/verify remain complete.
Initial scoped snapshot update FAILED exit1 because the chosen cohort lacked
a Review/edit-lease default (1 passed/1 failed). One existing route supplied
the missing response; no production or assertion change. Corrected capture1
passed, then update2/verify2 passed. That failure is not erased by this fresh
merged PASS. Earlier patch/context, diagnostic truncation/syntax and normalized
designer line-ending comparison attempts remain in the author report; none was
a silently discarded product-test failure.

## Closeout

Root has read this whole proof and accepts ordinary Done on dev. Preserve
both merged TRXs plus all three author TRXs with hash checks under ignored
pegasus_pack/current/proofs/PLAT-072 before cleaning only this ticket's verified
worktrees/branch, then release its claim last.
