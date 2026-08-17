# Checklist — TICK-200

- [x] Create the dedicated branch/worktree and record the Kanmer claim; retain the full plan only in the ticket document.
- [x] Implement deterministic descending-size snake distribution in `Invoke-TestShard.ps1`.
- [x] Add focused assignment and partition-regression tests.
- [x] Run focused tests, locked restore, Release build, live allocation, comparable GitHub timing, and confirm no UI or temp-plan path changed.
- [x] Merge current `origin/dev` normally, update the post-report, push, record traceability, and leave the dev-targeting PR in Review.

## Progress notes

- Baseline run 31996804786 had effectively no queue delay and completed in 9m30s. SQL shards ran 8m47s, 6m27s, and 6m47s.
- Greedy candidate run 31997820713 was green but rejected: SQL shards ran 8m38s, 10m13s, and 12m22s.
- Final run 31998887285 was green. Windows jobs started four seconds after the changes dependency completed; SQL shards ran 7m56s, 7m15s, and 7m47s, reducing the SQL critical job by 51 seconds (9.7%). Exact coverage passed in 12s.
- The final run's browser lane independently varied to 11m18s and set overall workflow time; no browser/UI path is in this PR, so no browser improvement is claimed.
- Final live allocation is 165/163/163 across 70 whole classes; all 491 tests are assigned exactly once.
- `pwsh ./scripts/Test-TestShard.ps1`, `pwsh ./scripts/Test-CiChangeFlags.ps1`, locked restore, and Release build passed after merging `origin/dev`; the successful retry built with 0 warnings and 0 errors after shutting down a stale local MSBuild node that held `Pegasus.Core.dll`.
- PR diff against current `origin/dev`: `.github/workflows/ci.yml`, `scripts/Get-CiChangeFlags.ps1`, `scripts/Invoke-TestShard.ps1`, `scripts/Test-CiChangeFlags.ps1`, and new `scripts/Test-TestShard.ps1` only.
