# Files

## Modify

- `src/Pegasus.Infrastructure/Email/GraphApprovedSources.cs` — extend the existing Deleted search catch policy only.
- `tests/Pegasus.IntegrationTests/ProductionGraphSourceTests.cs` — prove malformed JSON, missing identity/time, foreign parent folder, and escaped next-link responses become unavailable.

## Overlap and dependencies

- Both files are already owned by [[TICK-053]] / PR #469 and are intentionally shared on its branch.
- No dependency beyond the landed TICK-053 implementation. No migration, UI, or governing-doc change.
