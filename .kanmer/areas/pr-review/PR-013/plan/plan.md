# Plan — PR-013

Estimated diff: 2 files, about 60–100 lines.

1. Reuse the loaded tracked navigation and diff it by `FolderType`: update retained entity identity, remove absent entities, add only new keys.
2. Extend the existing relational mailbox persistence test to refresh a set containing unchanged, changed, removed, and added logical types, then reload and assert the exact set plus version/replay behavior.
3. Run focused persistence tests, Release build if needed, `git diff --check`, and four lenses (reuse, simplification, efficiency, altitude). Commit and push PR #468; write PIR and move PR-013 to Review.

No governing-doc change: this corrects implementation of existing replace semantics.

## Simplification pass — 2026-08-20

- Reuse: updates the already tracked child entity; no parallel persistence path.
- Simplification: removes clear/recreate behavior and uses one keyed diff.
- Efficiency: linear work over the bounded 13-binding collection.
- Altitude: EF tracking mechanics remain in Infrastructure; business contracts are unchanged.

No unapplied findings.
