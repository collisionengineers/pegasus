---
id: MAIL-032
type: ticket
title: Keep the selected Inbox preview available after pointerleave or blur
status: implementing
area: mail-communications
assignee: claude-code/20260901T215000Z-claude-controller/implementer-a1
profile: fix
stageEntered:
  preparing: '2026-09-02T00:59:22.673Z'
taken_at: '2026-09-02T01:27:52.605Z'
branch: task/mail-028-inbox-preview-pin
worktree: ../pegasus-worktrees/mail-028-inbox-preview-pin
claim_expires_at: '2026-09-02T01:57:52.605Z'
claim_controller: claude-code/20260901T215000Z-claude-controller/implementer-a1
lease_id: 9c2f8a19-84c3-4ede-bdf4-acd560bb73c6
lease_revision: 1
lease_workspace: >-
  worktree:c:\users\pguser\documents\github\pegasus-worktrees\mail-028-inbox-preview-pin
lease_phase: implementing
lease_heartbeat_at: '2026-09-02T01:27:52.605Z'
labels: []
groups:
  - EPIC-011
links:
  - MAIL-025
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - df9716e3ab4b83074ea3175cae6c3539c9006727
  - ad3779c999afa4caee9447ec577f42e957525174
  - ed19e77ff2da8c6a5f87eb20a0222eae17ff15b2
  - 3bf282441ddd3ba8c0355b8e59d06bea3d501cfb
prs:
  - '640'
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.052Z'
updated: '2026-09-02T02:56:22.951Z'
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
