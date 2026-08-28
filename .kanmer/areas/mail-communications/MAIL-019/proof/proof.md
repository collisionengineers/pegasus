---
kind: proof-record
merged_sha: "be507fafe0de46ce54b23b25ac317d821558f330"
environment: "Detached worktree .worktrees/verify-mail-019-be507fafe0de46ce54b23b25ac317d821558f330 (Windows 11, pwsh 7, dotnet net10.0 Release); prod SQL read-only via az AAD token"
verified_at: "2026-08-27T17:03:00Z"
result: PASS
attempts:
  - attempted_at: "2026-08-27T16:57:00Z"
    command: "gh pr view 573 --json state,mergeCommit,url"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "state MERGED, mergeCommit.oid be507fafe0de46ce54b23b25ac317d821558f330, https://github.com/collisionengineers/pegasus/pull/573"
  - attempted_at: "2026-08-27T16:57:30Z"
    command: "git fetch origin; git worktree add --detach .worktrees/verify-mail-019-be507fafe0de46ce54b23b25ac317d821558f330 be507fafe0de46ce54b23b25ac317d821558f330; rev-parse HEAD; symbolic-ref --short -q HEAD; status --short --branch"
    cwd: "C:/Users/Alex/Documents/GitHub/pegasus"
    exit_code: 0
    result: PASS
    summary: "HEAD be507fafe0de46ce54b23b25ac317d821558f330, symbolic-ref empty (detached), status '## HEAD (no branch)' clean"
  - attempted_at: "2026-08-27T16:58:00Z"
    command: "pwsh -NoProfile -Command \"[scriptblock]::Create((Get-Content -Raw scripts/Invoke-ProductionSmoke.ps1)) | Out-Null\""
    cwd: ".worktrees/verify-mail-019-be507fafe0de46ce54b23b25ac317d821558f330"
    exit_code: 0
    result: PASS
    summary: "Merged smoke script parses"
  - attempted_at: "2026-08-27T16:58:10Z"
    command: "git grep -n -i liveness -- docs/runbook.md .agents/skills/pegasus-release/SKILL.md scripts/Invoke-ProductionSmoke.ps1"
    cwd: ".worktrees/verify-mail-019-be507fafe0de46ce54b23b25ac317d821558f330"
    exit_code: 0
    result: PASS
    summary: "runbook.md:933 and :1078 name the inbox intake liveness gate; SKILL.md:222 names it; script lines 132-179 carry the MAIL-019 block"
  - attempted_at: "2026-08-27T16:58:30Z"
    command: "dotnet build tests/Pegasus.ArchitectureTests -c Release; dotnet test tests/Pegasus.ArchitectureTests -c Release --no-build --filter FullyQualifiedName~WorkerActivationReleaseContractTests"
    cwd: ".worktrees/verify-mail-019-be507fafe0de46ce54b23b25ac317d821558f330"
    exit_code: 0
    result: PASS
    summary: "Build 0 warnings 0 errors (3m51s); Passed! Failed 0, Passed 14, Skipped 0, Total 14, 35 s"
  - attempted_at: "2026-08-27T17:02:33Z"
    command: "pwsh -NoProfile -File <temp copy of merged script lines 141-179: Invoke-Sqlcmd read-only liveness block with az account get-access-token --resource https://database.windows.net/> against tcp:pegasus-prod-sql-252ow37gij.database.windows.net,1433 / pegasus"
    cwd: ".worktrees/verify-mail-019-be507fafe0de46ce54b23b25ac317d821558f330"
    exit_code: 0
    result: PASS
    summary: "Inbox intake liveness smoke passed (last poll 2026-08-27 17:00:02Z, subscription expires 2026-09-02 10:25:00Z)."
---

# MAIL-019 proof — PR #573 at be507faf

Verified at the exact GitHub merge commit in a disposable detached worktree.
Script/docs-only ticket: the merged `scripts/Invoke-ProductionSmoke.ps1`
parses; the runbook and release-skill text name the new gate; the mocked
`WorkerActivationReleaseContractTests` (14) pass at the merge SHA; and the
new intake-liveness block, run verbatim and read-only against production,
reports PASS on the live state (poll age 2.5 min at run time, one unexpired
Active subscription). No Azure writes were made. The full integration suite
was not run (no .NET file changed; review R1 accepted-risk stands).

## Closeout

PR: https://github.com/collisionengineers/pegasus/pull/573 — merged into
`dev` 2026-08-27T16:55:38Z at be507fafe0de46ce54b23b25ac317d821558f330.
