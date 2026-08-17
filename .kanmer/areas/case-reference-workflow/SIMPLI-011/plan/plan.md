# Plan — SIMPLI-011: decompose the Case Details workspace by capability

Diff estimate: ~20 files, ~+2000 / −1350 — of which ~770 lines are new behavioural tests for the 22 currently-untested handlers (the ticket's real cost). Web-only; no Core/Infrastructure/DB change; no ADR.

## Approach

Mechanical extraction along the seams the survey found: every mutation is already PRG and every form already posts the case id, so each of the 28 non-workspace handlers moves unchanged onto a page for its capability, each form gains one `asp-page`, and `Details.cshtml`/the visible workspace do not change. Shared plumbing moves once into an abstract `CaseMutationPageModel` (the third-copy rule); `EvaDownload` becomes its own download page. Then the missing tests are written in the existing `CaseDetailsWebTests` idiom, one file per capability. Reuses: `Cases/Documents/Export.cshtml.cs` (page shape), `Cases/Documents/Download.cshtml.cs` (file response), `Administration/AdministrationPageModel.cs` (base), `CaseDetailsWebTests` helpers (`AssertPrg`, antiforgery/lease setup).

Governing docs: `docs/design/README.md#case` (workspace content — unchanged), FRD-01 (behaviour — unchanged); `docs/current-architecture.md` implementation map updated. Verified premises: all `file:line` in `research`; assumed: none material.

## Steps (staged so the build and `CaseDetailsWebTests` are green after every stage)

1. **Base.** Add `Pages/Cases/CaseMutationPageModel.cs` by *moving* the shared members from `DetailsModel` (actor, command execution, redirect, lease TempData block, proposed-value retention write side, operation-key helpers, logging). `DetailsModel` inherits it. Build + `CaseDetailsWebTests` green with **zero handler moves** — proves the base is a pure move.
2. **Workflow page.** `Cases/Workflow.cshtml{.cs}` with the 7 handlers moved verbatim; the 7 forms in `_CaseWorkflow.cshtml` gain `asp-page="/Cases/Workflow"`; `CaseDetailsWebTests` URLs for `Hold`/`ReleaseHold`/`StartWork` retargeted; green.
3. **Tasks, Custody, Vehicle, Closure pages** — same recipe, one page per commit; the constructor-port assertion in `CaseDetailsWebTests.cs:69-73` retargets to the custody/vehicle models; `handler=` HTML assertions follow the new `asp-page` targets.
4. **EVA download page** `Cases/Eva/Download.cshtml{.cs}` (file response, headers preserved); download form/link retargeted; `Browser/OperatorJourneyTests.cs:127` and the `CaseDetailsWebTests` download assertions follow.
5. **`DetailsModel` trim** — remove the 27 unused ports and dead helpers; confirm 11 deps and only `OnGetAsync` + the five workspace handlers remain.
6. **Tests for the 22 uncovered handlers** — `CaseWorkflowWebTests`, `CaseTasksWebTests`, `CaseCustodyWebTests`, `CaseVehicleWebTests`, `CaseClosureWebTests`: each handler gets GET (antiforgery) → `ClaimLease` (where the command needs edit authority) → POST → `AssertPrg` + one persisted-state or TempData assertion; the lease-loss path once per page via the base.
7. **Docs** — `docs/current-architecture.md` implementation-map row; `docs/design/README.md` page inventory if it lists page files (content section untouched).
8. **Verify** — Release build 0/0; `Pegasus.Core.Tests`; `Pegasus.ArchitectureTests`; integration filter `CaseDetailsWebTests|CaseReportApprovalWebTests|Case*WebTests|CaseCreateWebTests|CasesIndexWebTests`; the Browser lane `OperatorJourneyTests` if the machine has Playwright, else CI's browser job; `rg "asp-page-handler" src/Pegasus.Web/Pages/Cases/Shared` count unchanged (35) and every moved form has `asp-page`.
9. **Simplification pass** over the diff (four lenses + code-simplifier; this diff is large enough for the full pass), findings appended here; post-implementation report; PR to `dev`.
10. **Independent review; CI green; merge; verify on merged `dev`; proof; closeout.**

## Verification (ticket acceptance)

- "The visible workspace remains intact": `Details.cshtml` unchanged; the four partials change only `asp-page` attributes; the design README `#case` list and the state-matrix rows still describe what renders; the Browser journey passes.
- "Extracted operations are covered by behavioural tests": every moved handler has an endpoint test; the 22 gaps are closed.
- `DetailsModel` loads and displays: `OnGetAsync` + edit-mode + completeness/save only; 11 dependencies.

## Risks / stop rules

- Stop and reassess if any handler turns out to re-render with `ModelState` (none found) or if a form lacks the hidden case id (all have it).
- Do not change handler names, form fields, TempData keys, or redirect targets — behaviour-preserving by construction.
- Do not touch Core use cases or FRDs.
