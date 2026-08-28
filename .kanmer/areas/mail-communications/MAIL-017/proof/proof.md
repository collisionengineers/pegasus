---
kind: proof-record
merged_sha: "61d8053961bc8cf476e531d1e02468ee32f95961"
environment: "detached worktree ../pegasus-worktrees/verify-mail-017 at 61d8053961bc8cf476e531d1e02468ee32f95961; Windows 11, PowerShell 7, .NET 10 SDK, LocalDB; read-only prod SQL via AAD token"
verified_at: "2026-08-27T17:14:00Z"
result: PASS
attempts:
  - attempted_at: "2026-08-27T17:05:00Z"
    command: "gh pr view 571 --json state,mergeCommit,url"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "state MERGED, mergeCommit.oid 61d8053961bc8cf476e531d1e02468ee32f95961"
  - attempted_at: "2026-08-27T17:06:00Z"
    command: "git fetch origin; git worktree add --detach ../pegasus-worktrees/verify-mail-017 61d8053961bc8cf476e531d1e02468ee32f95961; rev-parse HEAD; symbolic-ref -q HEAD; status --short --branch"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "HEAD = 61d8053961bc8cf476e531d1e02468ee32f95961, detached (no symbolic ref), status clean"
  - attempted_at: "2026-08-27T17:06:30Z"
    command: "git diff a9184315 61d80539 --stat -- src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs"
    cwd: "../pegasus-worktrees/verify-mail-017"
    exit_code: 0
    result: PASS
    summary: "empty output — model snapshot unchanged (no model change)"
  - attempted_at: "2026-08-27T17:09:39Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: "../pegasus-worktrees/verify-mail-017"
    exit_code: 0
    result: PASS
    summary: "restored all projects under locked mode"
  - attempted_at: "2026-08-27T17:09:50Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: "../pegasus-worktrees/verify-mail-017"
    exit_code: 0
    result: PASS
    summary: "Build succeeded. 0 Warning(s), 0 Error(s), 00:01:21"
  - attempted_at: "2026-08-27T17:11:12Z"
    command: "dotnet ef migrations list --project src/Pegasus.Infrastructure --startup-project src/Pegasus.Web --no-build --configuration Release"
    cwd: "../pegasus-worktrees/verify-mail-017"
    exit_code: 0
    result: PASS
    summary: "last entry 20260827100901_ReactivateBoundApprovedMailboxes (Pending) — the migration head"
  - attempted_at: "2026-08-27T17:11:25Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"FullyQualifiedName~IntakePersistenceIntegrationTests\""
    cwd: "../pegasus-worktrees/verify-mail-017"
    exit_code: 0
    result: PASS
    summary: "Passed! Failed 0, Passed 10, Skipped 0, Total 10, 2 m 4 s — single run, no LocalDB timeout"
  - attempted_at: "2026-08-27T17:12:00Z"
    command: "Invoke-Sqlcmd (AAD token) against pegasus-prod-sql-252ow37gij.database.windows.net/pegasus: SELECT ApprovedMailboxes; ApprovedMailboxSubscriptions; TOP 1 __EFMigrationsHistory ORDER BY MigrationId DESC"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "ApprovedMailboxes 49f47eb9…: State Approved, ActivatedAtUtc 2026-08-27 10:20:33Z, Version 6, both identities non-null; ApprovedMailboxSubscriptions one row 09018cc2… Active expiring 2026-09-02 10:25Z; __EFMigrationsHistory head 20260826151807_ApprovedMailboxStableIdentityAndSubscriptions (read-only, no writes)"
---

# Proof — MAIL-017 (PR #571)

## Scope verified

Data-repair migration `20260827100901_ReactivateBoundApprovedMailboxes` (+
Designer), migration-head assertion in
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`, and the
`docs/operations.md` release-33 note, at merge SHA
`61d8053961bc8cf476e531d1e02468ee32f95961` in a disposable detached worktree.

## Result

PASS at the code level: locked restore, Release build with 0 warnings, no
model-snapshot diff against `a9184315`, the new migration is the EF head, and
the focused persistence integration test passed 10/10 on its first run.

## Live state (read-only prod, not a deployment claim)

The migration is **not yet deployed**: prod `__EFMigrationsHistory` head is
still `20260826151807…`. Prod is already repaired by the operator re-save at
2026-08-27 10:20:33Z (`ActivatedAtUtc` set, Version 6) and one `Active` Graph
subscription exists. On the next release the migration's `UPDATE` is expected
to match zero rows in prod; live proof of the migration applying belongs to
that release's evidence, not this ticket.

## Cleanup

Verification worktree `../pegasus-worktrees/verify-mail-017` removed after
this record was read back. Implementation worktree and branch left for
closeout.

## Merge record

- PR: https://github.com/collisionengineers/pegasus/pull/571
- Merged into `dev`: 2026-08-27T17:07:44Z at
  `61d8053961bc8cf476e531d1e02468ee32f95961`
- Closeout 2026-08-27: implementation worktree and branch
  `task/mail-017-reactivate-mailbox` (local + origin) removed; ticket released.
