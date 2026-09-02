---
kind: proof-record
merged_sha: "41a17163b31a76c6e28307c7767cdceff3602950"
environment: "Windows detached worktree .worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950; SQL Server 2022 LocalDB tools prepended to PATH"
verified_at: "2026-09-02T17:37:36.627Z"
result: PASS
attempts:
  - attempted_at: "2026-09-02T17:04:27.7770988Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 0
    result: PASS
    summary: "All seven solution projects restored successfully in locked mode."
  - attempted_at: "2026-09-02T17:04:35.2663620Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with 0 warnings and 0 errors."
  - attempted_at: "2026-09-02T17:05:17.5236288Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 1
    result: FAIL
    summary: "Core: 1 failed, 1070 passed; Architecture: 100 passed; Integration: 1086 passed, 3 skipped. ImmediateIntakeDispatchTests.ImmediatePublicationRecordsTheReceiptIdentifierAndBoundedOutcome failed because Assert.Single observed process_intake and publish_committed_intake_work activities."
  - attempted_at: "2026-09-02T17:21:40.3309299Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 0
    result: PASS
    summary: "Attempt 2 restored all seven solution projects successfully in locked mode."
  - attempted_at: "2026-09-02T17:21:55.9184617Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 0
    result: PASS
    summary: "Attempt 2 Release build succeeded with 0 warnings and 0 errors."
  - attempted_at: "2026-09-02T17:22:43.1086236Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 0
    result: PASS
    summary: "Attempt 2 passed: Core 1071/1071; Architecture 100/100; Integration 1086 passed, 3 expected skips, 0 failed."
  - attempted_at: "2026-09-02T17:36:24.9065690Z"
    command: "./scripts/Test-MigrationGrants.ps1"
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 0
    result: PASS
    summary: "All 80 migration files checked; every created table is granted or exempted."
  - attempted_at: "2026-09-02T17:36:32.7293225Z"
    command: "./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local"
    cwd: ".worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950"
    exit_code: 0
    result: PASS
    summary: "Local Azure deployment-plan validation passed; Worker Disabled settings render true."
---

# Verification proof — TICK-061

GitHub PR [#592](https://github.com/collisionengineers/pegasus/pull/592) was
merged at 2026-08-28T12:41:28Z. GitHub reported merge commit
`41a17163b31a76c6e28307c7767cdceff3602950` and source head
`c0a55807b514193d929d485630fb03fcf06a0a7e`.

Both attempts used the disposable verification worktree detached, clean, and
at that exact full merge SHA.

## Attempt 1 — retained non-PASS evidence

The first canonical solution test command failed. Core reported 1 failed and
1,070 passed; Architecture reported 100 passed; Integration reported 1,086
passed and 3 skipped. The Core failure was
`ImmediateIntakeDispatchTests.ImmediatePublicationRecordsTheReceiptIdentifierAndBoundedOutcome`:
its `Assert.Single` observed both `process_intake` and
`publish_committed_intake_work` activities.

That attempt was correctly recorded as `FAIL` with
`failure_class: inconclusive`. It did not establish whether the extra
diagnostic activity was an implementation defect or cross-test
instrumentation interference. Per the stop rule, no command was rerun in that
attempt and the remaining plan-specific script checks were not started.

## Attempt 2 — PASS

The authorized same-SHA rerun passed locked restore, Release build, the full
non-Corpus solution test command, migration-grant validation, and local Azure
deployment-plan validation. The previously failing Core test passed. The
canonical test totals were Core 1,071 passed; Architecture 100 passed; and
Integration 1,086 passed, 3 expected skips, 0 failed.

## Transient disposition of attempt 1

The prior red run is discharged as transient only after satisfying all three
required tests:

1. The same canonical job was rerun at the identical merge SHA, with no code
   change, and passed.
2. PR #592's changed-file census does not include
   `tests/Pegasus.Core.Tests/Intake/ImmediateIntakeDispatchTests.cs`.
3. Later commit
   `79a4aaf9c3dfa586966bae82079e5cf21fc927bf` documents the mechanism: the
   test listened to the process-wide `Pegasus.Core.Intake` ActivitySource
   while xUnit ran test classes in parallel, so a concurrent
   `ProcessIntakeTests` `process_intake` span could be counted beside the
   publication span. The dispatch under test cannot reach `ProcessIntake`;
   its store double throws from every processing method. That later test-only
   fix scopes collection by trace and does not alter TICK-061 production code.

The final result for the shipped merge SHA is therefore `PASS`.
