# Post-implementation report — TICK-200

## Summary

The SQL integration matrix now sorts whole test classes by descending enumerated test count and snake-deals alternating rows across three runners. Run 31998887285 on implementation head `30933616c3caa10c2a4744afbd9b800c7ecc4c99` proved SQL job times of 7m56s, 7m15s, and 7m47s versus baseline 31996804786's 8m47s, 6m27s, and 6m47s, reducing the controlled SQL critical path by 51 seconds (9.7%). Every check passed, all 491 tests remained covered exactly once, and the PR contains no UI or repository-plan paths. That run's unrelated browser lane varied to 11m18s and determined overall completion, so this report claims only the measured SQL-lane improvement. The exact final reviewed PR head is `2db2b0eaebc9f6c07c394743974630ea9fb3bc16`, which adds the required normal merge of current `origin/dev` without changing the five-path ticket diff.

## Changes

| File | Change | Why |
|---|---|---|
| `.github/workflows/ci.yml` | Runs the focused shard assignment regression in the fast `changes` job. | Prevents allocation or partition regressions before expensive Windows lanes run and leaves room for TICK-195's later validation step. |
| `scripts/Invoke-TestShard.ps1` | Replaces alphabetical round-robin allocation with descending-size snake distribution; adds a test-list input seam. | Separates adjacent large classes without the disproven greedy clustering behavior, while retaining whole-class allocation and independent runner provenance. |
| `scripts/Test-TestShard.ps1` | Adds synthetic checks for balanced allocation, repeatability, whole classes, exact coverage, and empty shards. | Makes the scheduling contract independently executable. |
| `scripts/Get-CiChangeFlags.ps1` / `scripts/Test-CiChangeFlags.ps1` | Classifies the new shard test as build-relevant and covers the rule. | Ensures changes to the regression check exercise the complete build workflow. |

## Governing docs

The ticket has no linked PRD, FRD, or ADR and introduces no product behavior or architectural boundary. The implementation preserves the existing locked restore, Windows runner, exact partition, and evidence contracts described by `docs/runbook.md` and `docs/engineering.md`; neither document required a change.

## Risks / follow-ups

The rejected greedy run demonstrates that equal test counts alone do not guarantee equal runtime. The retained regression protects deterministic, whole-class, near-even allocation, while live workflow timings remain the performance evidence. Browser variability is outside this script's scope and should not be attributed to the SQL optimization. TICK-195 remains parked and is not duplicated here.

## Verification hand-off

On merged `dev`, run:

- `pwsh ./scripts/Test-TestShard.ps1`
- `pwsh ./scripts/Test-CiChangeFlags.ps1`
- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Run list-only allocation for shards 1–3 with the non-Corpus/non-Browser filter, then `-VerifyPartition`; expect 165/163/163 and all 491 tests exactly once.
- Recheck a comparable full workflow run. Keep runner/dependency queue, SQL execution, coverage join, and browser execution as separate measurements.

Pre-review evidence: exact final reviewed head `2db2b0eaebc9f6c07c394743974630ea9fb3bc16`; measured implementation run 31998887285 at ancestor `30933616c3caa10c2a4744afbd9b800c7ecc4c99` green; local focused scripts green after the base merge; locked restore green; Release build green with 0 warnings/errors; no UI or temp-plan path in the PR diff.
