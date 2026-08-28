- 2026-08-28 Delta re-review (round 2): APPROVE. One record-keeping gap
  closed: the "Explicitly reported" item — the script-off case→EVA-Send
  link loss, worked around in OperatorJourneyTests by URL navigation — is
  routed to [[TICK-223]] (dialog triggers must keep a static link target);
  board link CASE-012 ↔ TICK-223 added.

## Ruling on `task/case-012-case-workspace-parallel` — 2026-08-28

**The parallel branch is superseded for the Case workspace and must not be
merged.** It is 5 commits (head 866fe459), 47 behind `origin/dev`, has no PR
and has never run CI. `git merge-tree` predicts 9 conflicts, including an
ADD/ADD on `_CaseWorkspaceNav.cshtml` and a MODIFY/DELETE on
`_CaseWorkflow.cshtml` (the parallel run deleted that partial; dev's merged
`Details.cshtml` renders it at line 425). Merging it would also revert
MAIL-025, CASE-025, ENG-026 and PLAT-023.

Three pieces of it are salvageable, because they are untouched base to dev and
so port without conflict, and only these are salvaged on
`task/case-012-eva-send-salvage`:

- `Pages/Cases/Eva/Send.cshtml` (+ `.cs`) — ported to the design system.
- `Pages/Cases/Create.cshtml` — ported to the design system.
- the two-line `docs/design/test-ui/catalogue.json` branch-text fix.

Deliberately **not** salvaged:

- Its `Eva/Send` Engineer-assignment form. Dev's merged handoff dialog on
  `Details.cshtml` already carries that selector and posts the same
  `Workflow/AssignEngineer` handler; a second copy is a duplicate
  implementation of one control.
- Its widened `Eva/Send` state gate (Review + ReportPreparation + PostReport +
  PostReportComplete). The shipped bar and the salvaged
  `SendToEvaRendersOnlyInReview` pin both say Review only.
- Its `?tab=` section aliases. No link in the product writes one; a
  compatibility path with no caller is one AGENTS.md forbids.
- `_CaseVehicle.cshtml` and `_CaseFiles.cshtml` — lane E2 (CASE-027) scope.
  Noted there as prior art instead.
- Everything else it did to `Details.*`, `_CaseSummary`, `_CaseHistory`,
  `_CaseWorkspaceNav` and `_CaseWorkflow`: superseded by PR #599.

## Which report describes which run

The `post-implementation-report/` folder holds two files and they are not
alternatives:

| File | Run | Status |
| --- | --- | --- |
| `report.md` | PR #599, `task/case-012-case-workspace` | merged to dev — this is what is in the product |
| `post-implementation-report.md` | `task/case-012-case-workspace-parallel` | superseded, never merged, must not be merged |

`post-implementation-report.md` has been retitled and banner-annotated so it
cannot be read as the shipped record. Neither file is deleted.

## Why CASE-012 goes back to `implementing` — 2026-08-28

The ticket sat at `verifying` after PR #599, but lane E1's file allocation in
`EPIC-011/waves.md` is `Pages/Cases/Details.*`,
`Cases/Shared/_CaseSummary/_CaseWorkflow/_CaseHistory`, `_CaseWorkspaceNav`,
`Workflow.*`, `Closure.*`, `Create.*`, `Eva/Send.*`. PR #599 left four of
those files at base == dev:

- `Pages/Cases/Create.cshtml` (+ `.cs`)
- `Pages/Cases/Eva/Send.cshtml` (+ `.cs`)
- `Pages/Cases/Workflow.cshtml` (+ `.cs`)
- `Pages/Cases/Closure.cshtml` (+ `.cs`)

Create and Eva/Send were still drawn in the pre-EPIC-011 vocabulary; Create
used `page-heading`, which wave 1 defines nowhere, so it rendered unstyled.
The lane was not done, so the stage was wrong. Round 3 runs on
`task/case-012-eva-send-salvage`, worktree
`../pegasus-worktrees/case-012-eva-send-salvage`.

## Board record correction

`CASE-012.md` recorded worktree `../pegasus-worktrees/case-012-case-workspace`
on branch `task/case-012-case-workspace`. That worktree is in fact checked out
on `task/case-012-case-workspace-parallel` at 866fe459 — the superseded
implementation — so the record was a resume target pointing at the losing
branch. The record now names this round's branch and worktree. The stale
worktree and branch are left in place for the orchestrator to remove after
this PR merges; this lane does not delete another run's work.

## Workflow.cshtml and Closure.cshtml — nothing to port

Both are two-line `@page` + `@model` files with no markup: they are POST-only
handler pages, and `docs/design/test-ui/catalogue.json` already classifies
them `redirect` with the reason "Compatibility route redirects to the
canonical case detail surface." Their handlers are the live POST targets of
the lifecycle dialogs PR #599 shipped on `Details.cshtml` — Hold, ReleaseHold,
AssignEngineer, CreateLinkedReplacement, Close, Reopen. They are not subsumed
and must not be deleted; deleting them would break every one of those dialogs.
Three of their handlers (`StartWork`, `ReturnToReview`, `Archive`) are reached
only from `_CaseWorkflow.cshtml`, which `Details.cshtml` still renders, so
those have callers too.
