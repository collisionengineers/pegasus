# Checklist — SIMPLI-007

Branch `task/simpli-007-acceptance-gate`; worktree `../pegasus-worktrees/simpli-007-acceptance-gate`.

- [ ] 0. Fast-forward the branch to `origin/dev` (now past `5e59f933`).
- [ ] 1. Core + Web deletion: `CoreAssembly.cs` → marker only; `Program.cs` registration removed; `QdosAlphaAcceptanceGateTests.cs` deleted; Release build 0/0.
- [ ] 2. Script owns validation: `Assert-AlphaCapabilityCoverage` derived from `docs/capabilities.md`; env-var gate plumbing removed; `dotnet test --filter Category=QdosAlphaAcceptance` kept as a test lane.
- [ ] 3. Docs: `docs/runbook.md:677-716`, `docs/operations.md:58,67-79` (trait list keeps `QdosAlphaAcceptance`).
- [ ] 4. Verify: locked restore, build, Core, Architecture, `Category=QdosAlphaAcceptance` filter, script fail-closed + pass runs, `rg` residue check.
- [ ] 5. Simplification pass recorded in `plan`; post-implementation report; PR to `dev`.
- [ ] 6. Independent review; CI green; merge; verify on merged `dev`; proof; closeout.

## Progress notes

- 2026-08-17 — research/files/open-questions/plan written; both open questions decided by the planner (delete; derive roster from the register) — reviewer to confirm.
