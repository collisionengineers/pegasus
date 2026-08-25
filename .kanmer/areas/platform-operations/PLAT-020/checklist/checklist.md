- [x] Reconcile the append-only Web/Worker runtime grants without duplicating the concurrently merged vehicle grant.
- [x] Update exhaustive role-grant and DELETE-denial tests.
- [x] Verify EnqueueDueAsync suppression is restricted to SQL duplicate-key errors 2601/2627 by merged PR #493.
- [x] Add focused ImageIntakes runtime-role tests.
- [x] Run restore, Release build, focused tests, full non-corpus tests, and simplification pass.
- [ ] Record the post-implementation report and open the reviewed PR to dev.
- [ ] After exact approval, deploy the migration and verify effective production grants.
- [ ] After exact queue-write approval, recover the stranded custody work and verify due lookups drain.
- [ ] Observe two scheduled sweeps and refresh current-state documentation.
- [ ] Record merged-main production proof.

## Progress notes

2026-08-21: origin/dev already contained PR #493, which grants Worker INSERT on VehicleLookupRequests and narrows duplicate suppression. This branch adds only the missing ImageIntakes UPDATE grants. Focused runtime-role tests passed 10/10 and Release build passed. Concurrent full-suite execution caused unrelated shared-LocalDB timeouts; no changed-path failure occurred.

<!-- kanmer-groom:release-take:PLAT-020:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `codex-mcp-client`; branch: `task/plat-020-runtime-grants`; worktree: `../pegasus-worktrees/plat-020`; taken at: `2026-08-21T10:00:08.531Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
