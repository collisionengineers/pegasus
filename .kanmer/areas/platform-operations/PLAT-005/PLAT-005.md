---
id: PLAT-005
type: ticket
title: Capture visual screenshots from a local DevelopmentOffline run
status: implementing
area: platform-operations
assignee: codex-mcp-client
profile: chore
stageEntered:
  preparing: '2026-08-20T08:49:11.185Z'
taken_at: '2026-08-24T14:51:06.946Z'
branch: task/plat-005-visual-proof
worktree: .worktrees/plat-005
labels:
  - ui
  - design
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
archived: true
created: '2026-08-18T09:39:12.370Z'
updated: '2026-08-25T06:58:52.876Z'
---

## What

Run the Web project locally under `DevelopmentOffline` and capture screenshots of the operator rail and one screen per family, supplementing the browser test suite's Playwright evidence with human-readable visual proof.

## Why

[[PLAT-001]]'s proof.md records that the browser suite (32 tests) drives the real application through Playwright and confirms axe compliance and journey completion, but visual screenshots were not captured. The ticket's verification checklist item "Local `DevelopmentOffline` run; visual proof of the rail and one screen per family" remains unticked. Screenshots are not a substitute for the browser suite but are a supplement — a reader of the proof should be able to see the result without running anything.

## Approach

- Run `dotnet run --project src/Pegasus.Web` under the `DevelopmentOffline` profile.
- Capture screenshots of: the rail, Dashboard, Inbox, Queues, Cases, Case Details, Assessment, Administration, Upload.
- Verify the marks render beside their text (no broken-image indicators).
- Save screenshots to the ticket's proof folder and reference them in a visual proof document.

## Verification

- [ ] Screenshots captured for the rail and one screen per family.
- [ ] Marks render correctly beside their text.
- [ ] Visual proof document references the screenshots with routes to reproduce.

## Outcome
