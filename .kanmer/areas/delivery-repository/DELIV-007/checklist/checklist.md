# Checklist — DELIV-007

- [x] Delete `.github/workflows/qdos-pressure.yml`.
- [x] Delete `tests/Pegasus.PerformanceTests/CapacitySoakTests.cs` and `FailureInjectionTests.cs`.
- [x] Remove the `CiPressure` path from `scripts/Invoke-QdosAlphaAcceptance.ps1`; keep `OfflineCandidate` unchanged.
- [x] Drop `Invoke-QdosAlphaAcceptance` from `scripts/Get-CiChangeFlags.ps1` build pattern.
- [x] Update `docs/operations.md` and `docs/runbook.md` lane text; documentation links pass.
- [x] Local checks: Test-CiChangeFlags, Test-DocumentationLinks, OfflineCandidate fail-closed message, PS parse.
- [x] Commit `1d20a556`, push, PR #402 to `dev`.
- [ ] Independent review passes; PR merged.
- [ ] Verified on merged `main` (proof) — no `qdos-pressure` workflow, no scheduled workflows.

## Progress notes

- 2026-08-18: Implemented and pushed; PR https://github.com/collisionengineers/pegasus/pull/402.
