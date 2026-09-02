---
kind: proof-record
merged_sha: "0d985c9e0b3284f211f824d387e2f36460c0c826"
environment: "Windows; detached worktree .worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826; .NET SDK 10; SQL Server 2022 Express LocalDB"
verified_at: "2026-09-02T17:56:39.4991813Z"
result: FAIL
failure_class: plan
attempts:
  - attempted_at: "2026-09-02T17:34:55Z"
    command: "gh pr view 594 --json state,mergeCommit,url,mergedAt"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "PR #594 is MERGED at 0d985c9e0b3284f211f824d387e2f36460c0c826; merged 2026-08-29T14:24:43Z."
  - attempted_at: "2026-09-02T17:35:02Z"
    command: "git worktree add --detach .worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826 0d985c9e0b3284f211f824d387e2f36460c0c826; exact-SHA/detached/clean assertions"
    cwd: "."
    exit_code: 0
    result: PASS
    summary: "Worktree HEAD exactly matched the merge SHA, symbolic-ref was empty, and status was clean detached HEAD."
  - attempted_at: "2026-09-02T17:35:10Z"
    command: "dotnet restore ./Pegasus.slnx --locked-mode"
    cwd: ".worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826"
    exit_code: 0
    result: PASS
    summary: "All solution projects restored from committed locks."
  - attempted_at: "2026-09-02T17:35:14Z"
    command: "dotnet build ./Pegasus.slnx --configuration Release --no-restore"
    cwd: ".worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826"
    exit_code: 0
    result: PASS
    summary: "Release build succeeded with 0 warnings and 0 errors."
  - attempted_at: "2026-09-02T17:35:52Z"
    command: "dotnet test ./Pegasus.slnx --configuration Release --no-build --filter \"Category!=Corpus\""
    cwd: ".worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826"
    exit_code: 0
    result: PASS
    summary: "Core 1158/1158 passed; Architecture 100/100 passed; Integration 1147 passed, 3 skipped, 0 failed (1150 total, 13m54s)."
  - attempted_at: "2026-09-02T17:55:32Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MigrationGrants.ps1"
    cwd: ".worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826"
    exit_code: 0
    result: PASS
    summary: "85 migration files checked; every created table was granted or exempted."
  - attempted_at: "2026-09-02T17:55:36Z"
    command: "pwsh -NoProfile -File ./scripts/Test-AzureDeploymentPlan.ps1 -Mode Local"
    cwd: ".worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826"
    exit_code: 0
    result: PASS
    summary: "Local Azure deployment-plan validation passed; informational Bicep update warning only."
  - attempted_at: "2026-09-02T17:55:47Z"
    command: "pwsh -NoProfile -File ./scripts/Test-MarkdownPlacement.ps1 -Base origin/dev -Head HEAD"
    cwd: ".worktrees/verify-tick-058-0d985c9e0b3284f211f824d387e2f36460c0c826"
    exit_code: 1
    result: FAIL
    summary: "The mutable origin/dev base is now cad00be9d42dbeaee9edf34c2d24de222d7ddb9d, a descendant of the verified merge. Its later removals make four .agents/skills/kanmer-review/assets Markdown files and .azure/deployment-plan.md appear as additions in the reverse historical diff, so the placement script rejects unrelated files."
---

# Verification proof — TICK-058

## Verdict

**FAIL — plan.** The shipped implementation passed the locked restore, Release
build, complete non-corpus solution suite, migration-grant census, and local
Azure deployment-plan validation. The remaining packet check is not
reproducible at an historical merge SHA because it names the moving
`origin/dev` ref as its base.

The exact merge SHA is an ancestor of the current `origin/dev`. The failing
paths are unrelated to TICK-058; the command compares the later integration
branch backwards to the historical merge and consequently reports files absent
from current `origin/dev` as additions at the historical head.

No retry or substituted base was run. Make the packet deterministic by binding
the Markdown-placement base to the exact integration parent/base SHA recorded
for the PR, then verify again through a fresh attempt.
