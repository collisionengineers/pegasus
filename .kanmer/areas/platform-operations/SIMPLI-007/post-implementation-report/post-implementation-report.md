# Post-implementation report — SIMPLI-007

Branch `task/simpli-007-acceptance-gate` @ `c9e657c3` on `dev` `5e59f933`. PR #388. Diff: 6 files, +279/−516.

## What changed, file by file

| File | Change | Why |
| --- | --- | --- |
| `src/Pegasus.Core/CoreAssembly.cs` | Reduced to the 6-line `CoreAssembly` marker: the `using System.Collections.ObjectModel`, `QdosAlphaCapabilityEvidenceOutcome`, `QdosAlphaCapabilityObservation`, `QdosAlphaExternalGateEvidence`, `QdosAlphaAcceptanceRequest`, `QdosAlphaAcceptanceDecision`, `IQdosAlphaAcceptanceGate`, `QdosAlphaAcceptanceGate` (117-ID roster, gate lists, `Evaluate`) removed. Marker retained for `DependencyDirectionTests` (`typeof(CoreAssembly)`). | Plan step 1. Core no longer owns a tooling checklist or the wire `kind` string. |
| `src/Pegasus.Web/Program.cs` | `builder.Services.AddSingleton<QdosAlphaAcceptanceGate>();` removed. | Plan step 1 — Web composition registers no acceptance gate. |
| `tests/Pegasus.IntegrationTests/QdosAlphaAcceptanceGateTests.cs` | Deleted (registration fact, three in-memory policy facts, the `RunnerManifestInvokesCoreGateThroughActualWebHost` manifest fact and its skip attribute). | The registration and Web-host facts die with the registration; the policy is now the script's, exercised by the harness below. |
| `scripts/Invoke-QdosAlphaAcceptance.ps1` | Added the offline-candidate acceptance contract constants (`$acceptanceManifestKind`, outcome tokens, `$externalGateCapabilityIds`, `$offlineGateIds`, `$releaseGateIds`) above their first reader; `Get-AlphaCapabilityIds` (reads `docs/capabilities.md` rows whose "Target release" column — index 4 of the eight-field split — is `0.1.0-alpha.1`); `Test-LowerHex`, `Add-Blocker`, `Get-OrdinalSorted`, `Test-EvidenceFile` (resolves each `evidenceReference` against the manifest directory and re-hashes it); `Assert-AlphaCapabilityCoverage` (ports the deleted `Evaluate`: one observation per required capability, `passed`/`deferredToExternalGate`, caller present, evidence hashed, deferral only for the external-gate capabilities, offline gates present with approval + hashed evidence; release verdict computed and returned, not enforced). `Assert-OfflineCandidatePrerequisites` returns the parsed manifest and uses the hoisted kind constant. Removed the `PEGASUS_QDOS_ACCEPTANCE_MANIFEST`/`_SOURCE_REVISION` reads, checks, sets and restores; the `dotnet test --filter 'Category=QdosAlphaAcceptance'` run stays as the acceptance test lane with new wording. `evidence.json` gains `acceptanceCoverage` (register, target version, required count, offline/release verdict, release blockers); limitation wording no longer says "the Core gate". Stale hashed-file list replaced with the four acceptance-lane files that exist. | Plan step 2 (+ simplification pass). |
| `docs/runbook.md` | The "Web-host gate compares … `/diagnostics/version`" sentence and the inherited-env-var requirement removed; a new paragraph after the local-manifest paragraph describes the runner-owned coverage check (register-derived roster, the two outcome tokens, deferral rule, offline gates, release recorded not enforced) and states no gate exists in Core or Web. | Plan step 3. |
| `docs/operations.md` | `OfflineCandidate` paragraph: coverage check owned by the runner, reads the roster from `docs/capabilities.md`, application registers no gate. Trait list unchanged (`QdosAlphaAcceptance` kept). | Plan step 3. |

## Deviations from the plan

- Both open questions were decided by the planner (delete, not move; roster derived from the register) and are recorded ticked in `open-questions` for the reviewer to confirm.
- The stale hashed-file list (`:505-514`) sat inside the `finally` block already being edited, so it was fixed here (parked question) rather than filed.
- No new tooling module: the functions live in the existing script (no rename, so `Get-CiChangeFlags.ps1` is untouched).

## Verification on `c9e657c3`

- `dotnet restore ./Pegasus.slnx --locked-mode`; `dotnet build ./Pegasus.slnx --configuration Release --no-restore`: 0 warnings, 0 errors.
- `Pegasus.Core.Tests`: 572 passed. `Pegasus.ArchitectureTests`: 94 passed. `Pegasus.IntegrationTests --filter Category=QdosAlphaAcceptance`: 13 passed (the lane still selects the Recovery/Triage tests).
- Script: PowerShell parser clean. Harness `scratchpad/test-007-coverage.ps1` (dot-sources the constants and coverage functions): roster count 131, contains INT-25 and MCP-01, excludes DOC-06; an incomplete manifest fails closed naming `capability:DOC-06:not-qdos-owned`, `capability:OPS-01:cannot-defer`, `capability:INT-01:missing`, `external-gate:approved-capacity-dataset:evidence-hash-mismatch`, `external-gate:accepted-genuine-route-evidence:missing` (and not INT-25); a complete synthetic manifest returns required=131, offline accepted, release not accepted with 10 blockers incl. `external-gate:qdos-operator-acceptance:missing` and `capability:OPS-10:external-evidence-required`.
- `rg -n "QdosAlphaAcceptanceGate|IQdosAlphaAcceptanceGate|AcceptanceManifestKind|PEGASUS_QDOS_ACCEPTANCE" src tests scripts docs`: no matches. `git diff --check`: clean.

## Not claimed

The full `OfflineCandidate` runner was not executed end to end (it needs the approved dataset manifest and a run-owned local manifest); the coverage function was exercised in isolation. No change to OPS-10/24/25 release status; no deployment or cloud write.
