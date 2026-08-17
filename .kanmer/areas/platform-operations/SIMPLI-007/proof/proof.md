# Proof — SIMPLI-007 (verified on merged `dev`)

## What landed

PR #388 (https://github.com/collisionengineers/pegasus/pull/388), merged into `dev` as **`d677a39d`** on 2026-08-17 12:49 UTC. Commits `c9e657c3` (implementation + simplification pass), `88fcde2a` (review nits: ordinal id matching, register column names). Independent review: **PASS**, both planner decisions confirmed (`scratch-review`). Net diff 6 files.

- `src/Pegasus.Core/CoreAssembly.cs` — the `CoreAssembly` marker only; every `QdosAlpha*` type and `IQdosAlphaAcceptanceGate` gone.
- `src/Pegasus.Web/Program.cs` — no `AddSingleton<QdosAlphaAcceptanceGate>()`.
- `tests/Pegasus.IntegrationTests/QdosAlphaAcceptanceGateTests.cs` — deleted.
- `scripts/Invoke-QdosAlphaAcceptance.ps1` — owns the offline-candidate coverage check (register-derived roster of 131 alpha capabilities; per-capability caller + re-hashed evidence; deferral only for OPS-10/24/25; offline gates with approval + hashed evidence; ordinal blocker sort; release verdict recorded in `evidence.json`); `PEGASUS_QDOS_ACCEPTANCE_*` env contract removed; `Category=QdosAlphaAcceptance` kept as the acceptance test lane; stale hashed-file list fixed.
- `docs/runbook.md`, `docs/operations.md` — describe the runner-owned check; no gate in Core or Web.

## Verification on `d677a39d` (ticket worktree detached at the merge commit; 2026-08-17 13:51–14:07 BST)

| Command | Result |
| --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | up to date |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | Build succeeded — 0 warnings, 0 errors |
| `dotnet test tests/Pegasus.Core.Tests --configuration Release --no-build` | **572 passed** |
| `dotnet test tests/Pegasus.ArchitectureTests --configuration Release --no-build` | **94 passed** (`DependencyDirectionTests` still resolve `typeof(CoreAssembly)`) |
| `dotnet test tests/Pegasus.IntegrationTests --configuration Release --no-build` (full) | **526 passed, 15 skipped, 0 failed** — 541 total, 14m27s. Five fewer tests than the previous run (546): the four deleted gate facts plus the deleted manifest fact (which was one of the skips). |
| `dotnet test … --filter Category=QdosAlphaAcceptance` (branch head, before merge) | 13 passed — the Recovery/Triage lane still selects |
| Coverage harness (`scratchpad/test-007-coverage.ps1`, dot-sourcing the script's functions) | roster 131 (no DOC-06; INT-25 and MCP-01 present); incomplete manifest fails closed naming `capability:DOC-06:not-qdos-owned`, `capability:OPS-01:cannot-defer`, `capability:INT-01:missing`, `external-gate:approved-capacity-dataset:evidence-hash-mismatch`, `external-gate:accepted-genuine-route-evidence:missing`; complete synthetic manifest → offline accepted, release not accepted with 10 recorded blockers |
| Reviewer probes on the merged script | `Approved-Capacity-Dataset` no longer accepted as a gate id (ordinal); `duplicate`, `invalid-outcome`, `evidence-file-missing`, `not-qdos-owned`, `cannot-defer`, `evidence-hash-mismatch`, gate `missing` all reproduced; a fully gated manifest yields `ReleaseAccepted=True` with `[]` blockers |
| CI on PR head `88fcde2a` (same tree modulo #389's docs) | pass: unit, browser, sql-integration (1)(2)(3), sql-integration-coverage, documentation, reference-data, changes; infrastructure skipped |
| `rg -n "QdosAlphaAcceptanceGate|IQdosAlphaAcceptanceGate|AcceptanceManifestKind|PEGASUS_QDOS_ACCEPTANCE" src tests scripts docs` on the merged tree | no matches |

Logs: `verify-d677a39d.log`, `verify-d677a39d-integration-full.log` (session scratchpad).

## Ticket verification line — "application composition no longer registers the acceptance gate and release validation remains available"

- Composition: no registration in Web (or Worker); no type in Core; asserted by build + `rg`.
- Release validation: available in `scripts/Invoke-QdosAlphaAcceptance.ps1 -Profile OfflineCandidate`, stronger than before (real evidence-file re-hash; roster from the register instead of a stale hard-coded list that demanded retired DOC-06 and missed 15 alpha rows).

## Not claimed

The full `OfflineCandidate` runner was exercised through its coverage function and prerequisites, not end to end with an approved dataset and run-owned local manifest. No change to OPS-10/24/25 release status; no deployment or cloud write.
