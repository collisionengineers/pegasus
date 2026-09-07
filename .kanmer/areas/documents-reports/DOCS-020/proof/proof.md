---
kind: proof-record
merged_sha: "522e67f270ab4d6086d9fba04095988db3598888"
environment: "Windows PowerShell 7, .NET 10 Release, existing SQL fixtures; .worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888"
verified_at: "2026-09-07T22:44:00Z"
result: FAIL
attempts:
  - attempted_at: "2026-09-07"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888"
    exit_code: 0
    result: PASS
    summary: "Fresh exact-merge locked restore passed."
  - attempted_at: "2026-09-07"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888"
    exit_code: 0
    result: PASS
    summary: "Fresh exact-merge solution build passed, zero warnings/errors, 139.38 seconds."
  - attempted_at: "2026-09-07"
    command: "dotnet test ./tests/Pegasus.IntegrationTests/Pegasus.IntegrationTests.csproj --configuration Release --no-build --filter \"FullyQualifiedName=Pegasus.IntegrationTests.CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss|FullyQualifiedName~CaseReportGenerationPersistenceTests|FullyQualifiedName~AssessmentReportDraftWebTests|FullyQualifiedName~AzureSqlRuntimeRoleMigrationTests.LatestMigrationGivesFoundationTablesTheirExactRuntimePermissions|FullyQualifiedName~CaseArtifactCustodyRecoveryTests\" --logger \"trx;LogFileName=docs-020-merge-attempt1.trx\""
    cwd: ".worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888"
    exit_code: 1
    result: FAIL
    failure_kind: implementation
    summary: "53 total: 52 passed, one failed, zero skipped, 134 seconds. Existing exact pending-migration expectation omits DOCS-020's new permission migration."
---

# DOCS-020 exact-merge verification — attempt 1

PR682 merged into configured integration branch dev at
522e67f270ab4d6086d9fba04095988db3598888. Reviewed head was
1bf9ac613a2b7610d2ddc23e8acfd9f4b462ef79; independent PASS
scratch/review@fdf4270802857212. Exact detached checkout and immutable source
comparison are recorded in scratch/verify@cdc31dff8403a156. Root performed
the fresh restore, build and focused test commands above; this is not CI or
relabelled pre-merge evidence. Individual attempts use date precision because
exact start instants were not captured. TRX retains test instants.

## Failure and bounded remedy

CaseWorkflowMigrationTests.CustodyEvidenceOrdinalsAndOperationsMigrateFromPreviousSchemaWithoutIdentityLoss
fails at its exact pending-migration list assertion, line126. Expected six
migrations after its historical CaseSignOffEngineer target; actual seven,
including 20260907210000_ReportInputInvalidationPermissions. The actual
migration is correct and must not be removed. Add that exact ID to the
existing assertion and retain its historical target and all identity/table
assertions. This known internal consumer belongs to DOCS-020, not PLAT-072.
Return this same ticket/branch/worktree to Implementing for a two-line
follow-up PR; PR682 is already merged and its review remains historical.

The other 52 selected cases passed, including report source/snapshot races,
source/signatory invalidation, actual runtime roles, artifact recovery and
London preview dates. They do not erase the failing migration consumer.
After the test-only correction, rerun the one failing case; do not repeat
these unchanged 52 cases solely for ritual. Final proof must identify the
follow-up exact merge and preserve this failed attempt.

## Retained evidence and scope

TRX: .worktrees/verify-docs-020-522e67f270ab4d6086d9fba04095988db3598888/tests/Pegasus.IntegrationTests/TestResults/docs-020-merge-attempt1.trx.
The pre-merge compiler, harness and snapshot failures and their corrections
remain in post-implementation-report@08e9663b3940a805. No full solution test
rail, manual visual pass, external provider call, cloud write or deployment
is claimed. Result remains FAIL; no Done or cleanup authorized.
