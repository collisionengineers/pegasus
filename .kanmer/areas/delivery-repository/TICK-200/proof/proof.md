# Proof — TICK-200 (verified on merged `main` `f1e116c6`, 2026-08-18)

- `pwsh ./scripts/Test-TestShard.ps1` → `Test-shard assignment passed.`
- `pwsh ./scripts/Test-CiChangeFlags.ps1` → `CI change classification passed.`
- `dotnet restore ./Pegasus.slnx --locked-mode` → success; `dotnet build ./Pegasus.slnx -c Release --no-restore` → 0 warnings, 0 errors.
- Shard allocation `Invoke-TestShard.ps1 -Filter 'Category!=Corpus&Category!=Browser' -Shard 1..3 -ShardCount 3 -ListOnly` enumerated the three whole-class partitions on `f1e116c6` (partition regression covered by `Test-TestShard.ps1`; CI runs `-VerifyPartition` in the `changes` job, which passed on run 32133221206).
- Live wall-clock on the release SHA (PR #400 run): `sql-integration (1)` 8m05s, `(2)` 6m46s, `(3)` 7m09s, `sql-integration-coverage` 12s, `unit` 3m52s, `browser` 6m58s — a full `repository-check` completes in ~9 minutes.

PR #381 merged 2026-08-17T06:07:40Z; on `main` since #394.
