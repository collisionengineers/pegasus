# Research — TICK-200: reduce remaining GitHub Actions wall-clock time

## Question

What currently determines the `repository-check` workflow's elapsed time, which parts are genuinely optimizable without weakening evidence, and how must this ticket sequence around the other EPIC-001 workflow changes and the active UI revamp?

## Findings

- The epic limits this ticket to CI workflows, repository validation scripts, non-UI tests, and governing documentation; it explicitly excludes `src/Pegasus.Web/**`, UI browser/snapshot tests, `design/**`, and `.stitch/**` (`EPIC-001/context.md`).
- The current workflow already contains the major prior optimizations: build-relevant path detection, three parallel SQL integration shards, an explicit shard-coverage join, a shared locked restore/build composite action with NuGet caching, Playwright browser caching, and small independent documentation/reference-data lanes (`.github/workflows/ci.yml`, `.github/actions/dotnet-build/action.yml`, `scripts/Invoke-TestShard.ps1`).
- Recent execution evidence does not show a single stable wall-clock baseline. Main run 31602770223 completed in about 12m14s, and PR run 31789945646 completed (failed) in about 11m57s. Successful PR run 31664462528 took about 45m20s end to end, but its Windows build/test lanes began roughly 35 minutes after the fast metadata jobs; once scheduled, the longest job ran about 9.2 minutes. That outlier is predominantly hosted-runner queue delay, not workflow execution (`gh run view` for runs 31602770223, 31789945646, and 31664462528).
- On ordinary full runs, the critical path is a SQL shard plus the coverage join. The sampled SQL shards take roughly 6.4–11.2 minutes; their repeated locked restore/build consumes about 2.6–3.8 minutes per Windows runner, and their tests consume about 3.3–7.5 minutes. Unit and browser lanes finish earlier in the same samples.
- The workflow intentionally rebuilds the complete solution independently in every test lane. This provides simple isolation and cache correctness but repeats about three minutes of compilation across unit, browser, and three SQL runners. Any attempt to share compiled output would need a trustworthy artifact contract keyed to the exact revision, SDK, configuration, and lock graph; it must not trade elapsed time for stale or incomplete evidence.
- More SQL shards are not automatically faster. Git history records that the repository tried dropping the shard matrix after enabling within-run parallelism and then restored it when CI disproved that change (`f191dd72`, `94152326`). The current comments also record a measured whole-lane result of 11m55s versus roughly four minutes per shard. Future tuning must use current per-test/shard evidence rather than repeat the rejected approach.
- Queue delay is outside repository control on GitHub-hosted runners. A workflow change can reduce execution duration or required Windows-runner demand, but it cannot promise elimination of hosted-runner scarcity; acceptance must report execution and queue time separately.
- All four sibling tickets can reshape `.github/workflows/ci.yml`: TICK-194 adds a main-history guard to `changes`; TICK-195 adds a diff-aware documentation check and deeper checkout; TICK-197 may establish an infrastructure lane; DELIVE-001 may move the QDOS pressure lane off the per-PR path. Optimizing before those changes land would benchmark and tune a temporary workflow. TICK-195's research explicitly says TICK-200 should follow the stable lane.
- The current checkout's UI-revamp material is under excluded `.stitch/**` and `design/**`; existing active registered worktrees do not claim the CI action or shard script. The only known non-obvious UI overlap in this epic is DELIVE-001's `CapacitySoakTests.cs`, which TICK-200 does not need to edit.
- The ticket's body cites retired `NOW.md`; KANMER-001 owns retargeting those stale references. That citation is not authority for implementation and must not expand this ticket into documentation cleanup.

## Implications

- Sequence TICK-200 last in EPIC-001, after DELIVE-001, TICK-194, TICK-195, and TICK-197 have landed or their final workflow shapes are known.
- Establish a fresh baseline from successful full runs of that final workflow. Record event-to-job queue delay separately from job execution, then optimize only a measured critical-path component.
- The leading candidate is reducing repeated restore/build work while preserving exact-revision isolation, but the plan must compare the artifact-transfer overhead and failure boundary against the current independent composite action before selecting it. Rebalancing SQL shards is a second candidate only if current enumeration/runtime evidence shows skew.
- Preserve complete test selection, the shard partition proof, locked restore semantics, failure diagnostics, and all new sibling policy/infra lanes. Do not optimize by path-skipping required evidence, suppressing failures, or touching UI-owned tests.
- Define success as a reproducible reduction in median execution critical-path time across multiple comparable full runs, with no regression in selected-test coverage. Treat hosted-runner queue time as reported context, not an acceptance target.

## Open questions

- None require operator input. Exact optimization and numeric target should be chosen during planning from the post-sibling baseline rather than guessed before the workflow stabilizes.
