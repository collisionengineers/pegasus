# Checklist — SIMPLI-011

Branch `task/simpli-011-case-details`; worktree `../pegasus-worktrees/simpli-011-case-details`.

- [x] 0. Fast-forward to `origin/dev` (`7bb184cb`+); restore; build.
- [x] 1. `CaseMutationPageModel` base (pure move); `DetailsModel` inherits; build + `CaseDetailsWebTests` green.
- [x] 2. `Cases/Workflow` page (7 handlers) + 7 forms retargeted; tests retargeted; green.
- [x] 3. `Cases/Tasks` (7), `Cases/Custody` (6), `Cases/Vehicle` (3), `Cases/Closure` (4) — pages generated verbatim from the HEAD handlers in one slice (the per-page commits collapsed: the extraction script produced all five pages at once and the whole solution built first time); green.
- [x] 4. `Cases/Eva/Download` page; download link/form + Browser assertion retargeted.
- [x] 5. `DetailsModel` trimmed to 10 deps / `OnGetAsync` + 5 workspace handlers (1938 → ~630 lines).
- [x] 6. Behavioural tests for the 22 uncovered handlers (five new test files + one shared harness file).
- [x] 7. Docs: current-architecture implementation map row added; design README lists no page files (nothing to change).
- [x] 8. Verify: build 0/0; Core 580/580; Architecture 94/94; integration filter (CaseDetails|ReportApproval|QdosCustodial|CaseCreate|CasesIndex) 44/44 on `a30e3a13`; Browser lane 32/32 on `9feca869` (re-run on `a30e3a13` in progress); handler-form count 34 (35 − download form), 6 ambient workspace forms.
- [x] 9. Simplification pass (four lenses + code-simplifier) recorded in `plan`; 15 applied / 12 skipped-or-deferred (PLAT-002, CASE-001 filed); post-implementation report written; PR #395 to `dev`.
- [ ] 10. Independent review; CI green; merge; verify on merged `dev`; proof; closeout.

## Progress notes

- 2026-08-17 — research/files/open-questions/plan written; five design questions decided by the planner (named handlers per family; one abstract base; EVA download as its own page; edit-mode stays on `DetailsModel`; `CaseDetailsStatus` filed separately if not trivial).
- 2026-08-17 — commit `919faed1`: base class, six new pages (handlers moved verbatim by script from `git show HEAD:…Details.cshtml.cs`), 29 partial forms gained `asp-page`, existing tests retargeted. Retargeted suite green: 63/63 (`CaseDetailsWebTests|CaseReportApprovalWebTests|CaseCreateWebTests|Browser`). Architecture test `WebCustodialPagesHaveNoDormantTransportPath` retargeted to `CustodyModel` for the custody ports.
- 2026-08-17 — new tests: `CaseCapabilityPagesTestSupport.cs` (shared `EnterEditModeAsync` harness, `Substitute<T>`, the two base refusal checks, `NextFailure` arm on the one recording store), `CaseWorkflowWebTests`, `CaseTasksWebTests`, `CaseCustodyWebTests` (+ empty-upload refusal), `CaseVehicleWebTests` (+ EVA download page: file, headers, refused, not-found), `CaseClosureWebTests`. One test per page walks every handler; the store is one `partial` fake extended per file (no second copy). Handler-form count 35 → 34: the EVA download form now posts to `/Cases/{id}/Eva/Download` without a handler.
- 2026-08-17 — `9feca869` merge of `origin/dev` (MAIL-22 landed meanwhile; `OperatorJourneyTests.cs` auto-merged). `a30e3a13` simplification pass (−100 lines; `Documents/Export` adopted the base; details in `plan`). PR #395 opened; ticket → Review; independent reviewer launched. Follow-ups filed: [[PLAT-002]] (one staff-actor root), [[CASE-001]] (unread `CaseDetailsStatus`).

- [x] 10. Independent review PASS (scratch-review); CI green (attempt 3 of run 32041587054); merged `b763157a`; verified on merged `dev` (proof); ticket Done.

## Closeout

- [x] PR #395 state MERGED (`b763157a`, 2026-08-17 15:48 UTC).
- [x] `proof` final; `commits`/`prs`/`deployment` recorded; Outcome written; follow-ups [[PLAT-002]], [[CASE-001]] linked.
- [ ] Worktree `../pegasus-worktrees/simpli-011-case-details` removed; branch `task/simpli-011-case-details` deleted locally and on origin.
- [ ] Ticket released.

- 2026-08-17 — closeout complete: worktree removed, `task/simpli-011-case-details` deleted locally and on origin, ticket released.
