# Checklist — SIMPLI-010 (simplified plan, 2026-08-17)

Branch `task/simpli-010-intake-state` @ `1e5372ce` (on `dev` `fc144848`). PR #387.

- [x] 1. Read-only production counts recorded (`draft_ready` = 0; unleased `dispatched` = 0) — see `research`.
- [x] 2. Delete the `draft_ready` alias: `EfIntakeReceiptStore` (`DecisionCodes`, `ParseDecision` branch, filter), `EfOperationsStore` succeeded-set, `EfCaseAcceptanceStore` comment, `IntakeContracts` legacy paragraph. Stale unleased `dispatched` re-dispatch → **not here**: production count = 0, filed as [[INTK-003]] (resilience, not repair).
- [x] 3. Fixtures: every incidental `draft_ready` value → `case_created` (13 integration files); three test names renamed.
- [x] 4. Docs: `docs/design/README.md`, `docs/current-architecture.md`, `CONTEXT.md`; dead `_StatusChip` arm removed.
- [x] 5. Verify: locked restore, Release build 0/0, Core 572, focused integration 69/6/0, Architecture 94; case-insensitive `rg` → no matches; `git diff --check` clean.
- [x] 6a. Simplification pass (one combined-lens check) recorded in `plan`; post-implementation report written; PR #387 opened to `dev`.
- [ ] 6b. Independent plan-vs-diff review; CI green; merge; verify on merged `dev`; proof; closeout.

## Progress notes

- 2026-08-17 12:0x UTC — implementation, pass, PR opened; ticket → review.
