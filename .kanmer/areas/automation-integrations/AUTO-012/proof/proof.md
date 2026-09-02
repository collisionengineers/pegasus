---
kind: proof-record
merged_sha: "b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
environment: "Windows PowerShell 7; detached worktree .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3; .NET 10"
verified_at: "2026-09-02T16:14:22.7393164Z"
result: INCONCLUSIVE
failure_class: inconclusive
attempts:
  - attempted_at: "2026-09-02T16:10:30Z"
    command: "gh pr view 635 --json state,mergeCommit,url,mergedAt,headRefName"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "PR 635 is MERGED at b9dcfec95f66d22623ab5ab9be72cfc974c11dc3; https://github.com/collisionengineers/pegasus/pull/635."
  - attempted_at: "2026-09-02T16:11:00Z"
    command: "git -C .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3 rev-parse HEAD"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "HEAD exactly b9dcfec95f66d22623ab5ab9be72cfc974c11dc3."
  - attempted_at: "2026-09-02T16:11:01Z"
    command: "git -C .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3 symbolic-ref --short -q HEAD"
    cwd: "."
    exit_code: 1
    result: PASS
    summary: "Expected empty output and exit 1 confirm detached HEAD."
  - attempted_at: "2026-09-02T16:11:02Z"
    command: "git -C .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3 status --short --branch"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Clean detached worktree: ## HEAD (no branch)."
  - attempted_at: "2026-09-02T16:11:10Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
    exit_code: 0
    result: PASS
    summary: "Locked restore completed for all solution projects."
  - attempted_at: "2026-09-02T16:11:20Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with 0 warnings and 0 errors."
  - attempted_at: "2026-09-02T16:12:10Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
    exit_code: 1
    result: INCONCLUSIVE
    summary: "Core passed 1178/1178 and Architecture passed 100/100. Integration: 399 passed, 821 failed, 3 skipped of 1223 because SQL Client could not locate a LocalDB runtime (error 52); the host lacks SQL Server Express LocalDB."
---

# Verification proof — AUTO-012

The merged implementation cannot receive a truthful PASS on this host because
the required integration suite could not create its SQL Server LocalDB test
database. The failures are environmental and do not distinguish a product
regression from a missing prerequisite, so the result is INCONCLUSIVE, not
FAIL or transient.

No failed command was rerun. AUTO-012 must remain in Verifying. A conclusive
attempt requires the same exact SHA and canonical commands on the repository's
supported Windows verification host with SQL Server Express LocalDB available,
or authoritative hosted evidence accepted by the operator.
