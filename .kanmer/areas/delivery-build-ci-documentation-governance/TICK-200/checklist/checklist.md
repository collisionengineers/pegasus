# Checklist — TICK-200

- [x] Create the dedicated branch/worktree and record the Kanmer claim; retain the full plan only in the ticket document.
- [x] Implement deterministic largest-class-first test-count balancing in `Invoke-TestShard.ps1`.
- [x] Add focused assignment and partition-regression tests.
- [x] Run focused tests, locked restore, Release build, live list-only allocation, and confirm no UI path changed.
- [x] Write the post-implementation report, commit, push, open the dev-targeting PR, record traceability, and move to Review.

## Progress notes

- Baseline run 31996804786 had no workflow queue delay and completed in 9m30s. SQL shards ran 8m47s, 6m27s, and 6m47s.
- Retained baseline artifacts assigned 203, 149, and 139 tests. The revised live list-only allocation assigns 164, 164, and 163 tests across 70 whole classes, and exact partition verification reports all 491 tests assigned once.
- `pwsh ./scripts/Test-TestShard.ps1`, `pwsh ./scripts/Test-CiChangeFlags.ps1`, locked restore, and Release build all passed. Build completed with 0 warnings and 0 errors.
- Diff inspection found no change under `src/Pegasus.Web/**`, integration-test source, `design/**`, or `.stitch/**`.
- Commits `0ea9c0af` and `8a29c1f8` pushed; PR #381 targets `dev`.
- Removed the obsolete repository temporary plan after confirming this format-3 board retains the complete plan and checklist in Kanmer.
