# Post-implementation report — TICK-200

## Summary

The SQL integration matrix now assigns whole test classes with deterministic largest-class-first balancing by enumerated test count. Against the exact 491-test enumeration from the current code, this changes the allocation from 203/149/139 to 164/164/163 while preserving three independent locked restore/build runners and the exact partition coverage join. A fast synthetic regression check now protects the assignment contract in every repository-check run.

## Changes

| File | Change | Why |
|---|---|---|
| `.github/workflows/ci.yml` | Runs the focused shard assignment regression in the fast `changes` job. | Prevents an allocation or partition regression before expensive Windows lanes run and leaves room for TICK-195's later validation step. |
| `scripts/Invoke-TestShard.ps1` | Replaces alphabetical round-robin allocation with deterministic largest-class-first test-count balancing; adds a test-list input seam. | Removes the measured critical-path shard skew without changing selected tests, shard count, or build provenance. |
| `scripts/Test-TestShard.ps1` | Adds synthetic checks for even allocation, repeatability, whole classes, exact coverage, and empty shards. | Makes the new scheduling contract independently executable. |
| `scripts/Get-CiChangeFlags.ps1` / `scripts/Test-CiChangeFlags.ps1` | Classifies the new shard test as build-relevant and covers the rule. | Ensures changes to the regression check exercise the complete build workflow. |
| `docs/temp-plans/reduce-actions-wall-clock.md` | Records task scope, sequence, acceptance, and verification. | Satisfies the repository task workflow for a non-docs-only change. |

## Governing docs

The ticket has no linked PRD, FRD, or ADR and introduces no product behavior or architectural boundary. The implementation preserves the existing locked restore, Windows runner, exact partition, and evidence contracts described by `docs/runbook.md` and `docs/engineering.md`; neither document required a change.

## Risks / follow-ups

Test count is a duration proxy, not a promise that every future run is perfectly balanced. Current retained TRX evidence supports it: the same class timings reduce summed-duration distribution from 1107/685/670 seconds to 855/849/758. The PR workflow must provide the live runtime result, with queue delay reported separately. TICK-195 remains parked and is not duplicated here.

## Verification hand-off

On merged `dev`, run:

- `pwsh ./scripts/Test-TestShard.ps1`
- `pwsh ./scripts/Test-CiChangeFlags.ps1`
- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Run `Invoke-TestShard.ps1 -ListOnly` for shards 1–3 with the non-Corpus/non-Browser filter, then `-VerifyPartition`; expect 164/164/163 assignments and all 491 tests exactly once.
- Inspect the PR repository-check timing: distinguish event-to-job queue delay from job execution and compare the longest SQL shard with run 31996804786's 8m47s baseline.

Pre-PR verification passed: focused scripts green, locked restore green, Release build green with 0 warnings/errors, and no UI-owned path changed.
