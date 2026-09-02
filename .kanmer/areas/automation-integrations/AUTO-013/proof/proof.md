---
kind: proof-record
merged_sha: "8b6d41345ee3afd1d7a1eb875ed3416516d50375"
environment: "Windows detached worktree .worktrees/verify-auto-013-8b6d41345ee3afd1d7a1eb875ed3416516d50375; .NET 10 Release; SQL Server Express LocalDB 16.0.1000.6"
verified_at: "2026-09-02T17:00:54.9366734Z"
result: PASS
attempts:
  - attempted_at: "2026-09-02T16:45:48Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-auto-013-8b6d41345ee3afd1d7a1eb875ed3416516d50375"
    exit_code: 0
    result: PASS
    summary: "All seven solution projects restored from the committed package locks."
  - attempted_at: "2026-09-02T16:45:52Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-auto-013-8b6d41345ee3afd1d7a1eb875ed3416516d50375"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with 0 warnings and 0 errors."
  - attempted_at: "2026-09-02T16:46:31Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-auto-013-8b6d41345ee3afd1d7a1eb875ed3416516d50375"
    exit_code: 0
    result: PASS
    summary: "Core 1,167 passed; Architecture 100 passed; Integration 1,216 passed and 3 skipped; 0 failed."
---

# Proof — AUTO-013

PR [#634](https://github.com/collisionengineers/pegasus/pull/634) merged into
`dev` on 2026-08-29T22:34:58Z at exact GitHub merge commit
`8b6d41345ee3afd1d7a1eb875ed3416516d50375`.

The merge commit is reachable from the verification branch `main`. Verification
used a clean detached worktree at that exact SHA. `symbolic-ref --short -q
HEAD` returned no branch, and `git status --short --branch` reported only
`## HEAD (no branch)`.

The command environment prepended
`C:/Program Files/Microsoft SQL Server/160/Tools/Binn` to `PATH`.
`MSSQLLocalDB` was running as SQL Server Express LocalDB 16.0.1000.6.

The canonical non-corpus solution gate passed in one attempt: 2,483 tests
passed, 3 documented corpus-dependent integration tests skipped, and none
failed. This independently reproduces the ticket's merged behavior and
satisfies the verification packet.
