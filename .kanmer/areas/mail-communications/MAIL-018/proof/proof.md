---
kind: proof-record
merged_sha: "3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
environment: "detached worktree .worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f on Windows 11 / PowerShell 7 / .NET 10 SDK, LocalDB; prod read via AAD token"
verified_at: "2026-08-27T19:30:00Z"
result: PASS
attempts:
  - attempted_at: "2026-08-27T19:05:00Z"
    command: "gh pr view 577 --json state,mergeCommit,url"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "state MERGED, mergeCommit.oid 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
  - attempted_at: "2026-08-27T19:06:00Z"
    command: "git worktree add --detach .worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f; rev-parse HEAD; symbolic-ref -q HEAD; status --short --branch"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "HEAD = merge SHA, symbolic-ref empty (detached), status clean"
  - attempted_at: "2026-08-27T19:08:00Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
    exit_code: 0
    result: PASS
    summary: "restored with committed locks"
  - attempted_at: "2026-08-27T19:09:00Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
    exit_code: 0
    result: PASS
    summary: "0 Warning(s), 0 Error(s), 1 m 36 s"
  - attempted_at: "2026-08-27T19:11:00Z"
    command: "grep 'Activated|Subscription' docs/design/test-ui/pages/administration-mailboxes--default.html"
    cwd: ".worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
    exit_code: 0
    result: PASS
    summary: "line 100 <th>Activated</th>, line 102 <th>Subscription</th> present in the committed snapshot"
  - attempted_at: "2026-08-27T19:12:00Z"
    command: "dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build"
    cwd: ".worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
    exit_code: 0
    result: PASS
    summary: "Passed 1002/1002"
  - attempted_at: "2026-08-27T19:13:00Z"
    command: "dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build"
    cwd: ".worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
    exit_code: 0
    result: PASS
    summary: "Passed 100/100"
  - attempted_at: "2026-08-27T19:15:00Z"
    command: "dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build --filter \"FullyQualifiedName~ApprovedMailboxAdministrationWebTests\""
    cwd: ".worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
    exit_code: 0
    result: PASS
    summary: "Passed 6/6 first attempt, 49 s, no LocalDB timeout"
  - attempted_at: "2026-08-27T19:16:00Z"
    command: "dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build --filter \"FullyQualifiedName~GraphMailWebhookTests\""
    cwd: ".worktrees/verify-mail-018-3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f"
    exit_code: 0
    result: PASS
    summary: "Passed 12/12 first attempt, 1 m 13 s"
  - attempted_at: "2026-08-27T19:18:00Z"
    command: "read-only SQL (AAD token, System.Data.SqlClient) against pegasus-prod-sql-252ow37gij/pegasus: SELECT ... FROM ApprovedMailboxSubscriptions s FULL JOIN ApprovedMailboxes m"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "one row: mailbox 49f47eb9-c5b0-464f-b8f0-8c90ba061728, LifecycleState Active, ExpiresAtUtc 2026-09-02T10:25:00Z, LastMaintenanceFailureCode null, ActivatedAtUtc 2026-08-27T10:20:33Z"
  - attempted_at: "2026-08-27T19:18:00Z"
    command: "manual: live Mailboxes page screenshot showing the new columns"
    cwd: "n/a"
    exit_code: null
    result: NOT_APPLICABLE
    summary: "page change is merged to dev only, not deployed; live proof belongs to the next release"
---

# MAIL-018 proof — PR #577 at 3a1a017c

Verified at the exact GitHub merge SHA in a disposable detached worktree.
Locked restore, Release build with 0 warnings, Core 1002/1002, Architecture
100/100, focused `ApprovedMailboxAdministrationWebTests` 6/6 and
`GraphMailWebhookTests` 12/12, each on the first attempt. The committed
Test UI snapshot for the Mailboxes page carries the `Activated` and
`Subscription` headers. The full integration suite was not re-run here; the
controller's serial run at 47ebad54 (987/988, unrelated regex timeout, class
rerun 7/7) is on record in the post-implementation report.

## What the page will display once deployed

Read-only prod state on 2026-08-27: the single approved mailbox
`49f47eb9-c5b0-464f-b8f0-8c90ba061728` shows Activated
`27 Aug 2026 11:20` (office time of 10:20:33Z) and Subscription
`Active. Expires 02 Sep 2026 11:25.` with no failure code. Consistent with the
EPIC-010 context facts (re-activated 10:20:33Z, subscription Active to
2026-09-02).

## Deployment status

The change is on `dev` only. A live screenshot of `/Administration/Mailboxes`
is due with the next release's evidence, not this proof.

## Merge record

- PR: https://github.com/collisionengineers/pegasus/pull/577
- Merged into `dev`: 2026-08-27T18:38:59Z at 3a1a017c8dea0cde21aa94cbbe15e82f07a6f54f
- Closed out 2026-08-27; follow-up [[MAIL-023]] (deferred snapshot regenerations).
