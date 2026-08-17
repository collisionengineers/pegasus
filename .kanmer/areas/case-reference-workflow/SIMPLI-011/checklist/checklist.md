# Checklist — SIMPLI-011

Branch `task/simpli-011-case-details`; worktree `../pegasus-worktrees/simpli-011-case-details`.

- [x] 0. Fast-forward to `origin/dev` (`7bb184cb`+); restore; build.
- [x] 1. `CaseMutationPageModel` base (pure move); `DetailsModel` inherits; build + `CaseDetailsWebTests` green.
- [x] 2. `Cases/Workflow` page (7 handlers) + 7 forms retargeted; tests retargeted; green.
- [x] 3. `Cases/Tasks` (7), `Cases/Custody` (6), `Cases/Vehicle` (3), `Cases/Closure` (4) — pages generated verbatim from the HEAD handlers in one slice (the per-page commits collapsed: the extraction script produced all five pages at once and the whole solution built first time); green.
- [x] 4. `Cases/Eva/Download` page; download link/form + Browser assertion retargeted.
- [x] 5. `DetailsModel` trimmed to 11 deps / `OnGetAsync` + 5 workspace handlers (1938 → 633 lines).
- [x] 6. Behavioural tests for the 22 uncovered handlers (five new test files + one shared harness file).
- [x] 7. Docs: current-architecture implementation map row added; design README lists no page files (nothing to change).
- [ ] 8. Verify: build 0/0, Core, Architecture, Case* integration filter, Browser journey (local or CI), form/handler counts.
- [ ] 9. Simplification pass (full: four lenses + code-simplifier) recorded in `plan`; post-implementation report; PR to `dev`.
- [ ] 10. Independent review; CI green; merge; verify on merged `dev`; proof; closeout.

## Progress notes

- 2026-08-17 — research/files/open-questions/plan written; five design questions decided by the planner (named handlers per family; one abstract base; EVA download as its own page; edit-mode stays on `DetailsModel`; `CaseDetailsStatus` filed separately if not trivial).
- 2026-08-17 — commit `919faed1`: base class, six new pages (handlers moved verbatim by script from `git show HEAD:…Details.cshtml.cs`), 29 partial forms gained `asp-page`, existing tests retargeted. Retargeted suite green: 63/63 (`CaseDetailsWebTests|CaseReportApprovalWebTests|CaseCreateWebTests|Browser`). Architecture test `WebCustodialPagesHaveNoDormantTransportPath` retargeted to `CustodyModel` for the custody ports.
- 2026-08-17 — new tests: `CaseCapabilityPagesTestSupport.cs` (shared `EnterEditModeAsync` harness, `Substitute<T>`, the two base refusal checks, `NextFailure` arm on the one recording store), `CaseWorkflowWebTests`, `CaseTasksWebTests`, `CaseCustodyWebTests` (+ empty-upload refusal), `CaseVehicleWebTests` (+ EVA download page: file, headers, refused, not-found), `CaseClosureWebTests`. One test per page walks every handler; the store is one `partial` fake extended per file (no second copy). Handler-form count 35 → 34: the EVA download form now posts to `/Cases/{id}/Eva/Download` without a handler.
