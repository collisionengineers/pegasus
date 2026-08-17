# Checklist — SIMPLI-011

Branch `task/simpli-011-case-details`; worktree `../pegasus-worktrees/simpli-011-case-details`.

- [ ] 0. Fast-forward to `origin/dev` (`7bb184cb`+); restore; build.
- [ ] 1. `CaseMutationPageModel` base (pure move); `DetailsModel` inherits; build + `CaseDetailsWebTests` green.
- [ ] 2. `Cases/Workflow` page (7 handlers) + 7 forms retargeted; tests retargeted; green.
- [ ] 3. `Cases/Tasks` (7), `Cases/Custody` (6), `Cases/Vehicle` (3), `Cases/Closure` (4) — one commit each; green after each.
- [ ] 4. `Cases/Eva/Download` page; download link/form + Browser assertion retargeted.
- [ ] 5. `DetailsModel` trimmed to 11 deps / `OnGetAsync` + 5 workspace handlers.
- [ ] 6. Behavioural tests for the 22 uncovered handlers (five new test files).
- [ ] 7. Docs: current-architecture implementation map; design README page inventory if listed.
- [ ] 8. Verify: build 0/0, Core, Architecture, Case* integration filter, Browser journey (local or CI), form/handler counts.
- [ ] 9. Simplification pass (full: four lenses + code-simplifier) recorded in `plan`; post-implementation report; PR to `dev`.
- [ ] 10. Independent review; CI green; merge; verify on merged `dev`; proof; closeout.

## Progress notes

- 2026-08-17 — research/files/open-questions/plan written; five design questions decided by the planner (named handlers per family; one abstract base; EVA download as its own page; edit-mode stays on `DetailsModel`; `CaseDetailsStatus` filed separately if not trivial).
