# Checklist — SIMPLI-007

Branch `task/simpli-007-acceptance-gate` @ `c9e657c3` (on `dev` `5e59f933`). PR #388.

- [x] 0. Fast-forwarded to `origin/dev` (`5e59f933`).
- [x] 1. Core + Web deletion: `CoreAssembly.cs` → marker only; `Program.cs` registration removed; `QdosAlphaAcceptanceGateTests.cs` deleted; Release build 0/0.
- [x] 2. Script owns validation: `Assert-AlphaCapabilityCoverage` derived from `docs/capabilities.md` (131 IDs); env-var gate plumbing removed; `Category=QdosAlphaAcceptance` kept as a test lane; stale hashed-file list fixed.
- [x] 3. Docs: `docs/runbook.md`, `docs/operations.md` (trait list keeps `QdosAlphaAcceptance`).
- [x] 4. Verify: locked restore, build 0/0, Core 572, Architecture 94, `Category=QdosAlphaAcceptance` 13 passed, harness fail-closed + pass, `rg` residue none, `git diff --check` clean.
- [x] 5. Simplification pass recorded in `plan`; post-implementation report; PR #388 opened to `dev`.
- [ ] 6. Independent review; CI green; merge; verify on merged `dev`; proof; closeout.

## Progress notes

- 2026-08-17 — research/files/open-questions/plan written; both open questions decided by the planner (delete; derive roster from the register) — reviewer to confirm.
- 2026-08-17 12:3x UTC — implemented, simplified, PR #388 open; ticket → review.
