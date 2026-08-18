# Proof — DELIVE-001 (verified on merged `main` `f1e116c6`, 2026-08-18)

- `dotnet restore ./Pegasus.slnx --locked-mode` → success; `dotnet build ./Pegasus.slnx -c Release --no-restore` → 0 warnings, 0 errors.
- `dotnet test tests/Pegasus.ArchitectureTests/… -c Release --no-build` → 96/96 passed (author result 87/87 at the time; later tickets added scenarios). `WorkerActivationReleaseContractTests` included.
- The Worker validator contract this ticket aligned (`minReplicas: 1`, `maxReplicas: 1`) is what production runs: `az containerapp show` after release 9 → `min: 1, max: 1`; `Test-AzureDeploymentPlan.ps1 -Mode Local/Artifact/PreUpload/PreMigration/PreProvision` all passed on `f1e116c6`.
- Hosted-runner behaviour on the release SHA: `sql-integration (1..3)` shards on PR #400 → 8m05s / 6m46s / 7m09s green (`QdosAllocationRecoveryTests` deadlock retry in place); the main-push run 32133221206 → all three shards success. One unrelated hosted LocalDB connection-timeout flake occurred on PR #402 shard 1 (`EvaHandoffPersistenceTests`, `VehicleWorkflowTerminalTests`) and passed on re-run — outside the four slices this ticket hardened.
- The `qdos-pressure` diagnostic lane this ticket registered was retired on 2026-08-18 by operator decision ([[DELIV-007]]); `repository-check` has no pressure job.
- `Test-DocumentationLinks.ps1` → 222 files resolve.

PR #378 merged 2026-08-17T04:50:07Z; on `main` since #394.
