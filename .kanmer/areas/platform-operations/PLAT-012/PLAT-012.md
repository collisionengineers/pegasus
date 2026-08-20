---
id: PLAT-012
type: ticket
title: >-
  Dashboard received counters count only their own channel — manual uploads
  never increment emails received
status: preparing
area: platform-operations
assignee: claude-code
profile: fix
taken_at: '2026-08-20T05:05:17.879Z'
branch: task/plat-012-channel-counters
worktree: ../pegasus-worktrees/plat-012
labels:
  - defect
  - dashboard
  - operator-reported
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-20T03:16:37.586Z'
updated: '2026-08-20T05:05:17.879Z'
---

## What

Operator, 2026-08-20, verbatim: *"Uploading images manually seems to be adding to the counter on the Dashboard for emails received when it should not, as I uploaded only images. Regardless of what I upload, this shouldn't add to the email counter."*

Fix the dashboard metric so the emails-received counter counts only mailbox-channel material; manual uploads count under their own measure (or none).

## Verification

- [ ] A manual upload changes no email counter.
- [ ] Email arrival still increments it.
- [ ] Test pinning the channel filter of each dashboard counter.
