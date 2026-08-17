# Checklist — TICK-200

- [x] Create the dedicated branch/worktree, record the Kanmer claim, and add the root temporary plan.
- [x] Implement deterministic largest-class-first test-count balancing in `Invoke-TestShard.ps1`.
- [x] Add focused assignment and partition-regression tests.
- [x] Run focused tests, locked restore, Release build, live list-only allocation, and confirm no UI path changed.
- [x] Write the post-implementation report, commit, push, open the dev-targeting PR, record traceability, and move to Review.

## Progress notes

- Baseline run 31996804786 had no workflow queue delay and completed in 9m30s. SQL shards ran 8m47s, 6m27s, and 6m47s.
- Retained baseline artifacts assigned 203, 149, and 139 tests. The revised live list-only allocation assigns 164, 164, and 163 tests across 70 whole classes, and exact partition verification reports all 491 tests assigned once.
- `pwsh ./scripts/Test-TestShard.ps1`, `pwsh ./scripts/Test-CiChangeFlags.ps1`, locked restore, and Release build all passed. Build completed with 0 warnings and 0 errors.
- Diff inspection found no change under `src/Pegasus.Web/**`, integration-test source, `design/**`, or `.stitch/**`.
- Commit `0ea9c0af` pushed and PR #381 opened against `dev`.
