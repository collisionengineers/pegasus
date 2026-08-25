- [x] Remove the duplicate IVehicleEvidenceQueries dependency and query.
- [x] Read shared vehicle fields and evidence from the IGetCase result.
- [x] Implement saved → Confirmed → Fact → lookup precedence without Suggestion.
- [x] Keep mileage value, unit, and source provenance-consistent.
- [x] Retain lookup fallback for year, engine capacity, and fuel.
- [x] Expand Assessment vehicle-prefill integration tests for every implemented tier and disagreement.
- [x] Run focused tests, Release build, full non-corpus tests, and simplification pass.
- [ ] Record the post-implementation report and open the reviewed PR to dev.
- [ ] Verify the merged behavior and record proof.

## Progress notes

2026-08-21: Full suites were initially run concurrently across three worktrees and produced unrelated QDOS regex and LocalDB connection timeouts. The Core suite passed 867/867 when rerun serially; focused Assessment tests passed 2/2. Release build passed.

<!-- kanmer-groom:release-take:ENG-007:2026-08-25 -->
### Board-hygiene claim release — 2026-08-25

Audit record written before releasing this completed ticket's stale take. Previous assignee: `codex-mcp-client`; branch: `task/eng-007-assessment-prefill`; worktree: `../pegasus-worktrees/eng-007`; taken at: `2026-08-21T10:00:08.571Z`. The branch and worktree coordinates are preserved here; this groom does not delete either.
