---
kind: proof-record
merged_sha: "cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
environment: "Windows PowerShell 7; detached worktree .worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
verified_at: "2026-09-02T18:16:33.2687355Z"
result: PASS
attempts:
  - attempted_at: "2026-09-02T17:47:44Z"
    command: "gh pr view 644 --json state,mergeCommit,url"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "PR #644 was MERGED with mergeCommit cad00be9d42dbeaee9edf34c2d24de222d7ddb9d."
  - attempted_at: "2026-09-02T17:48:16Z"
    command: "git rev-parse HEAD; git symbolic-ref --short -q HEAD; git status --short --branch"
    cwd: ".worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
    exit_code: 0
    result: PASS
    summary: "HEAD matched the exact merge SHA, was detached, and the worktree was clean."
  - attempted_at: "2026-09-02T17:48:51Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
    exit_code: 0
    result: PASS
    summary: "All solution projects restored from committed locks."
  - attempted_at: "2026-09-02T17:49:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with 0 warnings and 0 errors."
  - attempted_at: "2026-09-02T17:49:31Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
    exit_code: 0
    result: PASS
    summary: "Core 1185/1185 and Architecture 100/100 passed; Integration passed 1224 with 3 skips and 0 failures."
  - attempted_at: "2026-09-02T18:04:42Z"
    command: "pwsh ./scripts/Update-TestUiSnapshots.ps1 -Verify"
    cwd: ".worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
    exit_code: 0
    result: PASS
    summary: "Browser capture 119/119 in 5:20; non-browser capture 296/296 in 5:55; snapshot verifier 1/1 in 0:47."
  - attempted_at: "2026-09-02T18:16:24Z"
    command: "pwsh ./scripts/Test-UiCatalogue.ps1"
    cwd: ".worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
    exit_code: 0
    result: PASS
    summary: "Catalogue valid: 54 routed sources, 58 prototypes, 0 broken local references."
  - attempted_at: "2026-09-02T18:16:33Z"
    command: "git rev-parse HEAD; git symbolic-ref --short -q HEAD; git status --short --branch"
    cwd: ".worktrees/verify-uiimp-013-cad00be9d42dbeaee9edf34c2d24de222d7ddb9d"
    exit_code: 0
    result: PASS
    summary: "Post-verification worktree remained detached, clean, and at the exact merge SHA."
---

# Verification proof — UIIMP-013

## Result

PASS. The exact GitHub squash merge commit was verified in a clean detached
worktree. The canonical solution gates, fresh Test UI capture/verify, and Test
UI catalogue all passed without retry.

## Shipped behavior

- The full build-relevant Test UI gate remains active.
- The capture partition is complete and disjoint: 119 browser plus 296
  non-browser tests equals the original 415-test selection.
- Browser capture retains its two-thread cap; non-browser capture inherits the
  project cap; the build and once-wiped capture directory are reused.
- The workflow step and its failure diagnostic both name the 35-minute budget;
  the job budget remains 40 minutes.

## Traceability

- PR: https://github.com/collisionengineers/pegasus/pull/644
- Merged at: `2026-09-02T17:44:13Z`
- Reviewed head: `8116ac7b5545149670eb318708a2a4181bdba786`
- Merged SHA: `cad00be9d42dbeaee9edf34c2d24de222d7ddb9d`
