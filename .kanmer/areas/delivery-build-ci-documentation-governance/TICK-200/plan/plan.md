# Plan — TICK-200: Reduce remaining GitHub Actions wall-clock time

## Approach

Replace alphabetical round-robin class assignment with deterministic largest-class-first balancing by enumerated test count. The latest complete workflow run (31996804786) had no queue delay but SQL shard 1 took 8m47s while shards 2 and 3 took 6m27s and 6m47s. Its retained TRX evidence shows the current allocation assigned 203, 149, and 139 tests; simulating the proposed algorithm over the exact 491-test enumeration produces 164, 164, and 163 tests and reduces the summed-duration skew from 437 seconds to 98 seconds. This is preferable to sharing compiled artifacts because it preserves each runner's locked restore/build provenance and failure isolation, and preferable to adding shards because prior repository evidence found the existing three-shard shape faster.

## Governing docs

TICK-200 has no linked PRD, FRD, or ADR; its existing `docs_todo` satisfies the backlog gate. The change does not create product behaviour or a new architectural boundary. It preserves the executable locked restore, Windows test, partition-coverage, and evidence rules already governed by `docs/runbook.md` and `docs/engineering.md`, so those documents do not need modification.

## Steps

1. Create `task/reduce-actions-wall-clock` from current `origin/dev` in `C:/Users/PC/Documents/GitHub/pegasus-worktrees/reduce-actions-wall-clock` and record the claim; this format-3 board keeps the full plan and checklist only in TICK-200's Kanmer documents.
2. Change `scripts/Invoke-TestShard.ps1` to group the enumerated tests by class, sort classes by descending test count with a class-name tie-break, and greedily assign each class to the currently lightest shard with a lowest-shard-number tie-break.
3. Add focused script tests that prove determinism, near-even test-count allocation, whole-class assignment, empty-shard handling, and exact partition verification without weakening existing failure behavior.
4. Run the focused script tests and the repository's locked restore, Release build, and applicable focused tests; inspect the diff to prove no UI-owned path changed.
5. Push the branch, open a PR targeting `dev`, record the implementation report and traceability, and move TICK-200 to Review.

## Verification

Use the retained run-31996804786 artifacts as the before dataset and run the revised list-only assignment against the built integration project to capture the new 164/164/163 distribution. Run the new focused PowerShell tests, `dotnet restore ./Pegasus.slnx --locked-mode`, `dotnet build ./Pegasus.slnx --configuration Release --no-restore`, and the script's partition verification fixtures. The PR workflow is the first live timing evidence; report queue delay separately from execution duration and do not claim a runtime reduction until that run completes.

## Risks / open questions

- Test count is a proxy for duration, but the retained baseline shows it tracks the present skew closely; exact partition checks remain authoritative if runtime distribution later drifts.
- Sorting and tie-breaks must be explicit so every runner independently computes identical assignments from the same enumeration.
- TICK-195 is parked behind KANMER-002; this script-only optimization leaves the workflow's `changes` job available for its later validation step.
- No operator questions remain; the implementation choice is supported by current run artifacts and does not change user-visible behavior.
