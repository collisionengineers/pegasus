# Plan — TICK-200: Reduce remaining GitHub Actions wall-clock time

## Approach

Replace alphabetical round-robin class assignment with a deterministic snake distribution of classes sorted by descending enumerated test count. Fresh baseline run 31996804786 had no queue delay and a SQL critical job of 8m47s. A first greedy test-count balancer was rejected after run 31997820713 increased the SQL critical job to 12m22s by clustering medium database-heavy classes. The retained timing evidence from both runs selected the snake distribution, which keeps adjacent large classes on different runners without greedily concentrating the remaining classes. Final run 31998887285 proved SQL jobs of 7m56s, 7m15s, and 7m47s: a 51-second (9.7%) reduction in the controlled SQL critical path while preserving all checks. The unrelated browser lane varied to 11m18s and determined that run's overall completion time, so the evidence does not claim elimination of all workflow variance.

## Governing docs

TICK-200 has no linked PRD, FRD, or ADR; its existing `docs_todo` satisfies the backlog gate. The change does not create product behaviour or a new architectural boundary. It preserves the executable locked restore, Windows test, partition-coverage, and evidence rules already governed by `docs/runbook.md` and `docs/engineering.md`, so those documents do not need modification.

## Steps

1. Create `task/reduce-actions-wall-clock` from current `origin/dev` in `C:/Users/PC/Documents/GitHub/pegasus-worktrees/reduce-actions-wall-clock` and record the claim; this format-3 board keeps the full plan and checklist only in TICK-200's Kanmer documents.
2. Change `scripts/Invoke-TestShard.ps1` to group the enumerated tests by class, sort classes by descending test count with a class-name tie-break, and snake-deal each row across the three shards with a reversed direction on alternating rows.
3. Add focused script tests that prove determinism, near-even test-count allocation, whole-class assignment, empty-shard handling, and exact partition verification without weakening existing failure behavior.
4. Run the focused script tests, locked restore, Release build, live list-only allocation, and comparable GitHub Actions runs; reject any candidate that does not reduce the controlled SQL critical path and report queue delay separately.
5. Merge current `origin/dev` normally, confirm the PR diff contains no UI or repository-plan paths, push, record traceability, and leave TICK-200 in Review.

## Verification

Run `pwsh ./scripts/Test-TestShard.ps1`, `pwsh ./scripts/Test-CiChangeFlags.ps1`, `dotnet restore ./Pegasus.slnx --locked-mode`, and `dotnet build ./Pegasus.slnx --configuration Release --no-restore`. Run list-only allocation for all three shards and the partition verifier; expect 165/163/163 and all 491 tests exactly once. Compare full GitHub run 31998887285 with baseline 31996804786, reporting dependency/runner queue delay, each SQL job's execution time, the coverage join, and unrelated lane variance separately.

## Risks / open questions

- Test count remains a proxy for duration, so retained live timings and the regression test—not equal counts alone—are the acceptance evidence.
- The browser lane can independently become the workflow critical path; this ticket does not touch UI tests and makes no claim about that lane's run-to-run variance.
- TICK-195 is parked behind KANMER-002; this script-only optimization leaves the workflow's `changes` job available for its later validation step.
- No operator questions remain.
