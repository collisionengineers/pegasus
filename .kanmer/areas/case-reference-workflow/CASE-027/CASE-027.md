---
id: CASE-027
type: ticket
title: 'Port the Case Vehicle, Inspection address, Case Files and Notes views'
status: verifying
area: case-reference-workflow
order: 120
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-29T16:42:56.070Z'
  review: '2026-08-29T17:53:05.230Z'
  verifying: '2026-08-29T20:56:12.072Z'
taken_at: '2026-08-29T16:45:29.122Z'
branch: task/case-027-case-detail-views
worktree: ../pegasus-worktrees/case-027-case-detail-views
labels:
  - ui
  - wave-2
  - case
groups:
  - EPIC-011
links: []
blocks:
  - CASE-012
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
commits:
  - 497d2387
  - 94563ed4
  - ac864f35
  - 700ae9d8
  - 0ed6faa4
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/631'
deployment: production
archived: false
created: '2026-08-28T08:35:23.925Z'
updated: '2026-09-01T14:44:16.395Z'
---

## What

Wave 2 lane E2 of [[EPIC-011]] (after [[CASE-012]] delivers the workspace frame and `_CaseWorkspaceNav`). Port the `?section=vehicle|inspection|files|notes` views to `context.md` §1.8: Vehicle facts + "Vehicle checks" panel (Refresh DVLA / Refresh DVSA-MOT posting the one existing lookup handler; Run Experian check rendered disabled as a named seam, ENG-001, D7; Vehicle History = `narrative.history_check`), Inspection address (recorded value, provider default, previous values, edit), Case Files (documents rows with custody chip/Preview/Save as, upload requests, image gallery + viewer dialog with rotate classes, correspondence rows — Compose/Reply/Forward buttons come in wave 4), Notes (entries with Date/Time/ID; Add note / Record chase using existing handlers). Valuations tab and the merged timeline come from wave 3/4 tickets.

## Owns

`src/Pegasus.Web/Pages/Cases/Vehicle.*`, `Custody.*`, `Tasks.*`, `Cases/Shared/_CaseDocuments.cshtml`, `Cases/Documents/**`, tests `CaseVehicleWebTests.cs`, `CaseCustodyWebTests.cs`, `CaseTasksWebTests.cs`.

## Blocked by

[[CASE-012]].

## Verification

- [ ] Every button maps to an existing handler or an approved D7 seam.
- [ ] No clipped text/overflow at 1580/1100/760.
