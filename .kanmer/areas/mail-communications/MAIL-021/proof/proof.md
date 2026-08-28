---
kind: proof-record
merged_sha: "86113ea15af69c27ae676b2c11b3a6bfb90e41e1"
environment: "Detached worktree .worktrees/verify-mail-021-86113ea15af69c27ae676b2c11b3a6bfb90e41e1 at the PR #575 merge commit; Windows 11, PowerShell 7, dotnet SDK (net10.0)"
verified_at: "2026-08-27T18:05:00Z"
result: PASS
attempts:
  - attempted_at: "2026-08-27T17:55:00Z"
    command: "gh pr view 575 --json state,mergeCommit,url"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "state MERGED, mergeCommit.oid 86113ea15af69c27ae676b2c11b3a6bfb90e41e1"
  - attempted_at: "2026-08-27T17:56:00Z"
    command: "git fetch origin; git worktree add --detach .worktrees/verify-mail-021-86113ea15af69c27ae676b2c11b3a6bfb90e41e1 86113ea15af69c27ae676b2c11b3a6bfb90e41e1; rev-parse HEAD; symbolic-ref --short -q HEAD; status --short --branch"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "HEAD = 86113ea15af69c27ae676b2c11b3a6bfb90e41e1, detached (no symbolic ref), status clean"
  - attempted_at: "2026-08-27T17:58:00Z"
    command: "Read src/Pegasus.Core/Intake/RetainedMail.cs StaleAfter remarks (lines 649-659)"
    cwd: ".worktrees/verify-mail-021-86113ea15af69c27ae676b2c11b3a6bfb90e41e1"
    exit_code: null
    result: PASS
    summary: "Remarks state Graph change notifications are the primary wake, the recovery poll (InboxRecoveryFunction, ApprovedInboxPollSchedule) runs every five minutes, and fifteen minutes is three consecutive missed recovery ticks; PROVISIONAL/open-decisions sentence retained; TimeSpan.FromMinutes(15) unchanged"
  - attempted_at: "2026-08-27T17:59:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-mail-021-86113ea15af69c27ae676b2c11b3a6bfb90e41e1"
    exit_code: 0
    result: PASS
    summary: "All projects restored under locked mode"
  - attempted_at: "2026-08-27T18:01:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-mail-021-86113ea15af69c27ae676b2c11b3a6bfb90e41e1"
    exit_code: 0
    result: PASS
    summary: "0 Warning(s), 0 Error(s)"
  - attempted_at: "2026-08-27T18:04:00Z"
    command: "dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build"
    cwd: ".worktrees/verify-mail-021-86113ea15af69c27ae676b2c11b3a6bfb90e41e1"
    exit_code: 0
    result: PASS
    summary: "Passed 1002, Failed 0, Skipped 0 (Pegasus.Core.Tests, net10.0); includes the mail freshness policy tests"
---

# MAIL-021 proof

Verified at the exact PR #575 merge commit
`86113ea15af69c27ae676b2c11b3a6bfb90e41e1` in a disposable detached
worktree. The change is comment-only in
`src/Pegasus.Core/Intake/RetainedMail.cs`: the `StaleAfter` remarks now
state the current schedule (notification wakes primary, five-minute
recovery poll, fifteen minutes = three missed recovery ticks) and the
threshold value is unchanged. Locked restore, Release build (0 warnings)
and the Core unit tests all exit 0. The integration suite was not run for a
comment-only diff; CI on PR #575 (repository-check, all shards SUCCESS) is
the clean integration signal recorded in `scratch/review`. Review finding
RF-1 (stale `docs/open-decisions.md` sentence) is deferred to [[MAIL-022]].

Result: PASS.

## Closeout

PR: https://github.com/collisionengineers/pegasus/pull/575 — merged into
`dev` 2026-08-27T17:07:30Z at `86113ea15af69c27ae676b2c11b3a6bfb90e41e1`.
