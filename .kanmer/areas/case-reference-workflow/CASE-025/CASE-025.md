---
id: CASE-025
type: ticket
title: Port the Cases queues page (/Cases) with workflow rail groups and filters
status: verifying
area: case-reference-workflow
assignee: zcode
profile: feature
stageEntered:
  preparing: '2026-08-28T11:25:42.203Z'
  review: '2026-08-28T14:19:29.199Z'
  verifying: '2026-08-28T17:13:10.884Z'
  done: '2026-08-29T10:30:22.568Z'
taken_at: '2026-08-28T13:42:54.483Z'
branch: task/case-025-cases-queues
worktree: ../pegasus-worktrees/case-025-cases-queues
labels:
  - ui
  - wave-2
  - queues
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
archived: false
created: '2026-08-28T08:35:23.861Z'
updated: '2026-08-29T13:03:27.523Z'
---

## What

Wave 2 lane C1 of [[EPIC-011]]. Port `Pages/Cases/Index.cshtml(.cs)` (moved from Triage/Index by PLAT-029) to `context.md` §1.4: rail groups Workflow (Not ready, Review, With Engineer, Complete) / Pre-Case work (Triage) / Exceptions (Held, Unidentified — Blocked intake rows listed uncounted per D14); Principal and Missing filters; per-kind rows; quick-detail pane (compact workflow stepper, outstanding requirements, current work). With Engineer / Complete tabs need the D3 state groupings in the queue queries (`SearchCasesQuery`/`CaseStageCounts` — coordinate with the wave-3 counts ticket; add the grouping here if that ticket has not merged).

## Owns

`src/Pegasus.Web/Pages/Cases/Index.cshtml(.cs)`, `src/Pegasus.Web/Pages/Unidentified/Index.cshtml(.cs)`, `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`.

## Blocked by

[[PLAT-029]].

## Verification

- [ ] Every tab count is a queried figure; Unidentified count excludes Blocked intake.
- [ ] No clipped text/overflow at 1580/1100/760.
