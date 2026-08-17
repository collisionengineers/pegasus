# Files — TICK-200

## Where the change lands

| Path | Why |
|---|---|
| `.github/workflows/ci.yml` | Apply the measured critical-path optimization to the final EPIC-001 workflow while preserving every required policy, infrastructure, test, and coverage lane. |
| `.github/actions/dotnet-build/action.yml` | Adjust the shared restore/build boundary only if measurement proves repeated compilation is the chosen bottleneck; retain locked restore and exact cache invalidation. |
| `scripts/Invoke-TestShard.ps1` | Change shard partitioning or emit timing evidence only if the post-sibling baseline proves current class allocation is materially skewed; preserve exact partition verification. |
| `docs/engineering.md` or `docs/runbook.md` (conditional) | Update executable CI guidance only when the final optimization changes an operator/developer command or durable lane contract, and only after clearing active documentation ownership. |

## Context files

| Path | What it tells the implementer |
|---|---|
| `EPIC-001/context.md` | Binding exclusion of Web/UI/design work, one-ticket-per-PR rule, and requirement to recheck overlap immediately before implementation. |
| TICK-194, TICK-195, TICK-197, and DELIVE-001 research/files documents | The workflow additions and scheduling changes that must settle before TICK-200 establishes its baseline. |
| `.github/workflows/ci.yml` | Current dependency graph, runner OS choices, timeouts, cache use, shard count, coverage join, and path gating. |
| `.github/actions/dotnet-build/action.yml` | The exact locked restore/build and NuGet cache contract currently repeated in each test lane. |
| `scripts/Invoke-TestShard.ps1` | How test classes are enumerated, divided, recorded, and reassembled into exact coverage. |
| `tests/Pegasus.IntegrationTests/xunit.runner.json` | The within-shard concurrency cap that interacts with matrix sharding and LocalDB load. |
| `docs/runbook.md` | Locked verification commands, test parallelism constraints, and evidence limits that optimization cannot weaken. |
| Git history commits `0af53cd9`, `f191dd72`, and `94152326` | Prior optimization, the attempted matrix removal, and the evidence-driven restoration that must not be repeated blindly. |
| GitHub Actions runs 31602770223, 31789945646, and 31664462528 | Samples separating ordinary 12-minute full runs and job execution from a 35-minute hosted-runner queue delay. |

## Ripple effects

- Every EPIC-001 ticket may change the same workflow, so sequencing and rebasing are mandatory even though their scripts/tests remain independent.
- A build-artifact design would affect all .NET test lanes, artifact retention/transfer time, cache behavior, and failure diagnosis; it must prove exact revision/configuration provenance.
- A shard change affects assignment artifacts and `sql-integration-coverage`; selected tests must still run exactly once.
- Reducing Windows-runner demand may indirectly reduce exposure to queue scarcity, but GitHub-hosted queue time remains external and cannot be guaranteed.
- The final PR itself changes a build-relevant workflow path, so it exercises the complete workflow it modifies.

## Out of scope

- `src/Pegasus.Web/**`, UI-focused browser/snapshot test source, `design/**`, `.stitch/**`, and the UI revamp's copied worktrees.
- Removing required tests, weakening filters, suppressing shard coverage, accepting flaky failures, or using path filters to avoid evidence that the changed paths require.
- GitHub-hosted runner capacity, paid/self-hosted runner procurement, branch-protection changes, credentials, deployments, and other external writes.
- The individual behavior owned by TICK-194, TICK-195, TICK-197, and DELIVE-001; TICK-200 consumes their final workflow shape rather than duplicating them.
- Retargeting the retired `NOW.md` source note or broad documentation cleanup owned by KANMER-001/KANMER-002.
