# Files — SIMPLI-007

Surveyed 2026-08-17. Small, deletion-heavy change; estimated diff ~ 6 files, roughly +40/−480.

## Definite

| File | Change | Ripple |
| --- | --- | --- |
| `src/Pegasus.Core/CoreAssembly.cs` | Delete `:1` (`using System.Collections.ObjectModel;`) and `:10-316` (every gate type incl. `IQdosAlphaAcceptanceGate` and `QdosAlphaAcceptanceGate`). **Keep** the 8-line `public static class CoreAssembly;` marker — `DependencyDirectionTests.cs:44,186,225` use `typeof(CoreAssembly)`. | Core loses two tooling concerns (wire `kind` string, capability roster). |
| `src/Pegasus.Web/Program.cs:547` | Delete `builder.Services.AddSingleton<QdosAlphaAcceptanceGate>();`. | Web composition no longer registers the gate (ticket verification line 1). |
| `tests/Pegasus.IntegrationTests/QdosAlphaAcceptanceGateTests.cs` | Delete the file (167 lines incl. the `QdosAlphaAcceptanceManifestFact` skip attribute). Registration fact and Web-host fact die with the registration; the three in-memory policy facts die with the class unless the validator stays in C#. | The `QdosAlphaAcceptance` trait remains on `RecoveryTests`, `QdosTriage*Tests` — the script's filter keeps selecting them. |
| `scripts/Invoke-QdosAlphaAcceptance.ps1` | `OfflineCandidate` path: replace `:414-427` (env vars + `dotnet test` gate run + "QDOS Core acceptance gate failed") and the env save/restore `:381-390,:479-490` with the in-script validation the script already performs (`Assert-OfflineCandidatePrerequisites`, `Assert-LocalRunEvidence`); drop the `DOC-06` expectation wherever the roster is retained; keep `dotnet test --filter 'Category=QdosAlphaAcceptance'` **only** as the acceptance test-lane run (it still selects Recovery/Triage tests), not as a gate invocation. | `scripts/Get-CiChangeFlags.ps1:11` matches the script name — no change unless the file is renamed. `.github/workflows/qdos-pressure.yml` unaffected (`CiPressure` only). |
| `docs/runbook.md:677-716` | Rewrite `:700-706`: validation is script-owned; there is no Web-host gate; `/diagnostics/version` sentence goes. | — |
| `docs/operations.md:58,67-79` | Reword the Performance-profile / Checkpoint 12 / OfflineCandidate rows to describe script-owned validation; **keep** `QdosAlphaAcceptance` in the trait list `:81-83`. | — |

## Conditional

| File | Condition |
| --- | --- |
| New `scripts/*.ps1` validator module | Only if the roster is re-derived from `docs/capabilities.md` (open question 2). If added and named differently, add it to `Get-CiChangeFlags.ps1:11` and check `Test-CiChangeFlags.ps1`. |
| `docs/adr/0013-qdos-alpha-implementation-contract.md` | No supersession needed (no ADR placed the gate in composition). Optional one-line clarification is not required. |
| `CHANGELOG.md` | Entry if the repo's convention expects one for tooling changes (check recent entries). |
| `docs/current-architecture.md`, `docs/engineering.md` | No mentions — no change. |

## Out of scope

- The pre-existing stale file list at `Invoke-QdosAlphaAcceptance.ps1:505-514` (hashes three non-existent test files) — note in the PR, file separately if not trivially fixed while editing the same function.
- `CiPressure` profile, `qdos-pressure.yml`, PerformanceTests staging.
- Any deployment or live-acceptance claim: this ticket changes where validation code lives, not the release status of OPS-10/24/25.
