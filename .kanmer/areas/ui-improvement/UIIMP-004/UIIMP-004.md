---
id: UIIMP-004
type: ticket
title: Generate Test UI snapshots from current Razor rendering
status: done
area: ui-improvement
order: 2570
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-26T14:16:14.522Z'
  review: '2026-08-26T15:32:42.141Z'
  verifying: '2026-08-27T09:22:26.989Z'
  done: '2026-08-27T09:24:34.432Z'
labels:
  - ui
  - design
  - razor
  - fix
links:
  - UIIMP-002
  - MAIL-016
  - UIIMP-005
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - fe1d35cb
  - 35292cff
  - 44d16f46
  - f7c87173
  - c7b47a29
  - f5d072c5
  - 77d2a04a
  - f840d48a
  - e46845c2
  - d119bd39
  - 1daa29fa
  - 130b5195
  - 60d6ebea
  - 74371f98
  - 5287ee81
prs:
  - '#562'
deployment: n/a
archived: false
created: '2026-08-26T14:16:09.318Z'
updated: '2026-09-01T14:44:34.150Z'
---

## What

Replace the hand-authored Test UI HTML with deterministic snapshots generated from the current Razor application.

## Why

[[UIIMP-002]] proved route coverage but not parity. Its prototypes omitted current layouts, scripts, SVG markup, Tag Helper output, data hooks, form attributes, and current conditional rendering.

## Delivered

- One manifest covers 52 routed Razor sources and 57 current visual states.
- Actual integration-test Razor responses generate the committed static pages.
- Three obsolete, no-longer-renderable states were removed.
- Three reworked outcomes use their current unavailable/needs-decision names.
- Update and verify commands reject missing states and manual drift.
- Test mode remains runtime- and deployment-isolated.

## Verification

- [x] All 57 current states originate from current Razor rendering.
- [x] Normalized byte parity passes for every visual state.
- [x] Forms, scripts, SVGs, data hooks, layouts, and accessibility structure come from Live Razor output.
- [x] Fresh recapture and regeneration are clean.
- [x] Test UI remains excluded from deployment.

## Outcome

PR #562. Deployment n/a.
