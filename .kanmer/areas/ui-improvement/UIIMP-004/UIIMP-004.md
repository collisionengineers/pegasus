---
id: UIIMP-004
type: ticket
title: Generate Test UI snapshots from current Razor rendering
status: implementing
area: ui-improvement
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-26T14:16:14.522Z'
taken_at: '2026-08-26T14:17:08.925Z'
branch: task/uiimp-004-razor-snapshots
worktree: ../pegasus-worktrees/uiimp-004-razor-snapshots
labels:
  - ui
  - design
  - razor
  - fix
links:
  - UIIMP-002
refs:
  - docs/frd/frd-12-operator-experience.md
deployment: n/a
archived: false
created: '2026-08-26T14:16:09.318Z'
updated: '2026-08-26T14:17:08.925Z'
---

## What

Replace the hand-authored Test UI HTML with deterministic snapshots generated from the current Razor application for the existing 60 named visual states.

## Why

[[UIIMP-002]] proved route coverage but not parity. The committed prototypes omit current layouts, scripts, SVG markup, Tag Helper output, data hooks, form attributes, and current conditional rendering. Test UI must be generated from the same Razor implementation as Live UI.

## Approach

- Reuse the existing integration WebApplicationFactory, test authentication, fixed clock, repository fixtures, and Playwright.
- Preserve the current 52-route classification and 60 selected visual states.
- Capture post-JavaScript rendered DOM; change only local asset/navigation URLs and normalize opaque volatile values.
- Generate the catalogue and snapshots from one manifest; reject hand edits through regeneration comparison.
- Keep Test mode runtime-isolated and absent from publish output.

## Verification

- [ ] All 60 states originate from current Razor rendering.
- [ ] Normalized DOM parity passes for every visual state.
- [ ] Standard live/offline screenshots match for every state.
- [ ] Forms, scripts, SVGs, data hooks, layouts, and accessibility structure match Live UI.
- [ ] Regeneration is clean and Test UI remains excluded from deployment.

## Outcome
