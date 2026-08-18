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

- [x] Independent review passed; PR #402 merged (`74613fbd`, 2026-08-18T11:22:17Z).
- [x] Verified on merged `main` (proof) — no `qdos-pressure` workflow, no scheduled workflows.

## Closeout — DELIV-007 (2026-08-18)

- [x] PR #402 MERGED
- [x] proof.md finalised; moved to Done; Outcome recorded
- [x] Worktree `../pegasus-worktrees/deliv-007-retire-qdos-pressure` removed; local + remote branch deleted; prune
- [x] Released
