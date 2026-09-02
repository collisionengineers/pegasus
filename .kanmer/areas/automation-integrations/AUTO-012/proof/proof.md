---
kind: proof-record
merged_sha: "b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
environment: "Windows PowerShell 7; detached worktree .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3; .NET 10; SQL Server Express LocalDB 16.0.1000.6"
verified_at: "2026-09-02T16:41:28.1391576Z"
result: PASS
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
    summary: "Core passed 1178/1178 and Architecture passed 100/100. Integration: 399 passed, 821 failed, 3 skipped of 1223 because SQL Client could not locate a LocalDB runtime (error 52); the host lacked SQL Server Express LocalDB."
  - attempted_at: "2026-09-02T16:25:00Z"
    command: "gh pr view 635 --json state,mergeCommit,url,mergedAt"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "PR 635 remains MERGED at exact merge SHA b9dcfec95f66d22623ab5ab9be72cfc974c11dc3; merged 2026-08-30T08:02:58Z; https://github.com/collisionengineers/pegasus/pull/635."
  - attempted_at: "2026-09-02T16:25:01Z"
    command: "git -C .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3 rev-parse HEAD"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Retained worktree HEAD exactly b9dcfec95f66d22623ab5ab9be72cfc974c11dc3."
  - attempted_at: "2026-09-02T16:25:02Z"
    command: "git -C .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3 symbolic-ref --short -q HEAD"
    cwd: "."
    exit_code: 1
    result: PASS
    summary: "Expected empty output and exit 1 confirm retained worktree remains detached."
  - attempted_at: "2026-09-02T16:25:03Z"
    command: "git -C .worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3 status --short --branch"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Retained detached worktree clean: ## HEAD (no branch)."
  - attempted_at: "2026-09-02T16:26:00Z"
    command: "PATH=<SQL Server 160 Tools Binn prepended>; dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
    exit_code: 0
    result: PASS
    summary: "Locked restore completed; all solution projects were up-to-date."
  - attempted_at: "2026-09-02T16:26:05Z"
    command: "PATH=<SQL Server 160 Tools Binn prepended>; dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with 0 warnings and 0 errors."
  - attempted_at: "2026-09-02T16:26:15Z"
    command: "PATH=<SQL Server 160 Tools Binn prepended>; dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-auto-012-b9dcfec95f66d22623ab5ab9be72cfc974c11dc3"
    exit_code: 0
    result: PASS
    summary: "Core 1178/1178 passed; Architecture 100/100 passed; Integration 1220 passed, 3 skipped, 0 failed of 1223. Integration duration 13m43s."
---

# Verification proof — AUTO-012

PR [#635](https://github.com/collisionengineers/pegasus/pull/635) merged on
2026-08-30 at the exact verified merge SHA.

Attempt 1 was inconclusive because the supported Windows host did not yet have
SQL Server Express LocalDB. That attempt and its non-zero integration exit
remain recorded above.

Attempt 2 used the same exact detached merge SHA after SQL Server Express
LocalDB 16.0.1000.6 was installed and the LocalDB tools directory was injected
into the verification process PATH. The canonical locked restore, Release
build, and non-corpus solution test all exited 0. The shipped result therefore
receives a truthful PASS.
