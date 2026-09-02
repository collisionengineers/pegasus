---
id: MAIL-032
type: ticket
title: Keep the selected Inbox preview available after pointerleave or blur
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels: []
groups:
  - EPIC-011
links:
  - MAIL-025
refs:
  - docs/frd/frd-12-operator-experience.md
prs:
  - '640'
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.052Z'
updated: '2026-09-01T14:40:45.052Z'
---

## What

Keep the selected Inbox preview rendered after pointerleave or blur so `Open full message` and `Open linked Case` remain reachable.

## Why

PR #640 implements this fix but incorrectly identifies itself as MAIL-028; live MAIL-028 owns production folder-mover activation and must retain that meaning.

## Approach

- Associate PR #640 with this fresh ticket.
- Preserve keyboard, pointer and focus selection behavior without creating a second preview state owner.
- Resolve the failing `test-ui` job and obtain independent review before merge.

## Verification

- [ ] The selected preview survives pointerleave and blur until another row is selected or the view changes.
- [ ] Both preview actions remain keyboard and pointer reachable.
- [ ] UI snapshots/tests and the full required PR checks are green.

## Outcome
