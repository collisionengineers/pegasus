# Reduce GitHub Actions wall-clock time

Kanmer: TICK-200

## Scope

Rebalance the three SQL integration shards using their enumerated test counts. Preserve locked restore/build provenance, exact test partition verification, existing workflow lanes, and the UI-revamp exclusions in EPIC-001.

## Sequence

1. Replace alphabetical round-robin class allocation with deterministic largest-class-first balancing.
2. Add a focused PowerShell regression check for balanced, deterministic, whole-class assignment and exact coverage, and run it in the workflow's fast change-detection job.
3. Compare the revised allocation with retained run 31996804786, then run locked restore/build and focused checks.
4. Let the PR workflow provide live execution timing; report runner queue delay separately.

## Acceptance

- The current 491-test baseline changes from 203/149/139 to 164/164/163 tests per shard.
- Every enumerated test remains assigned exactly once and each class stays whole.
- No test, build, policy, infrastructure, or documentation lane is removed or weakened.
- No Web, UI-test, design, or Stitch path changes.

## Verification commands

- `pwsh ./scripts/Test-TestShard.ps1`
- `dotnet restore ./Pegasus.slnx --locked-mode`
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore`
- Run all three list-only assignments against the built integration project and verify the retained partition.
