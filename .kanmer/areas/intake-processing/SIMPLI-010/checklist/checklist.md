# Checklist — SIMPLI-010 (simplified plan, 2026-08-17)

Branch `task/simpli-010-intake-state` @ `fc144848` (fast-forwarded to `dev` after PR #385).

- [x] 1. Read-only production counts recorded (`draft_ready` = 0; unleased `dispatched` = 0) — see `research`.
- [ ] 2. Delete the `draft_ready` alias: `EfIntakeReceiptStore` (`DecisionCodes`, `ParseDecision` branch, filter), `EfOperationsStore` succeeded-set, `EfCaseAcceptanceStore` comment, `IntakeContracts` legacy paragraph. Make stale unleased `dispatched` rows dispatch candidates again in `FindNextDispatchCandidateAsync` (resilience against a lost queue message; idempotent because `ClaimProcessingAsync` no-ops on settled work) — with one `RecoveryTests` case.
- [ ] 3. Fixtures: every incidental `draft_ready` value → `case_created` (13 integration files + `ProcessIntakeTests`); no assertion or migration target changes.
- [ ] 4. Docs: `docs/design/README.md` and `docs/current-architecture.md` — `case_created` is the sole persisted code and is not case-existence authority.
- [ ] 5. Verify: locked restore, Release build, Core tests, focused integration filter (IntakeStablePersistence, QdosIntakeWeb, OperationsPersistence, CaseWorkflowMigration, TypedCaseDataMigration, Recovery), Architecture tests; `rg -n "draft_ready|DraftReady" src tests docs/current-architecture.md docs/design/README.md` → no matches; `git diff --check`.
- [ ] 6. Simplification pass over the diff (four lenses + code-simplifier), findings appended to `plan`; post-implementation report; PR to `dev`; independent plan-vs-diff review; merge.

## Progress notes
