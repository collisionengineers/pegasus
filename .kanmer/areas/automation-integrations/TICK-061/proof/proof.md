---
kind: proof-record
merged_sha: "41a17163b31a76c6e28307c7767cdceff3602950"
environment: "Windows detached worktree .worktrees/verify-tick-061-41a17163b31a76c6e28307c7767cdceff3602950; SQL Server 2022 LocalDB tools prepended to PATH"
verified_at: "2026-09-02T17:18:30.504Z"
result: FAIL
failure_class: inconclusive
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
---

# Verification proof — TICK-061

GitHub PR [#592](https://github.com/collisionengineers/pegasus/pull/592) was
merged at 2026-08-28T12:41:28Z. GitHub reported merge commit
`41a17163b31a76c6e28307c7767cdceff3602950` and source head
`c0a55807b514193d929d485630fb03fcf06a0a7e`.

The disposable verification worktree was detached, clean, and at the exact
full merge SHA before checks began.

## Result

The canonical solution test command failed. The failing assertion is in the
Core test project and the same run completed all Integration tests successfully
once LocalDB was available. This single attempt does not establish whether the
extra diagnostic activity is a deterministic implementation defect or
cross-test instrumentation interference, so the failure class is
`inconclusive`.

Per the verification stop rule, the failed command was not rerun and the
remaining plan-specific script checks were not started. A same-SHA isolated
rerun or focused diagnostic investigation is required to classify the failure
conclusively. TICK-061 remains Verifying.
