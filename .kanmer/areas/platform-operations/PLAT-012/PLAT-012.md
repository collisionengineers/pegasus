---
id: PLAT-012
type: ticket
title: >-
  Dashboard received counters count only their own channel — manual uploads
  never increment emails received
status: done
area: platform-operations
order: 1500
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-20T05:06:52.820Z'
  review: '2026-08-20T05:10:57.978Z'
  verifying: '2026-08-20T05:38:15.035Z'
  done: '2026-08-20T12:46:51.246Z'
labels:
  - defect
  - dashboard
  - operator-reported
links: []
refs:
  - docs/frd/frd-12-operator-experience.md
prs:
  - '457'
deployment: production
archived: false
created: '2026-08-20T03:16:37.586Z'
updated: '2026-08-26T14:34:45.339Z'
---

## What

Operator, 2026-08-20, verbatim: *"Uploading images manually seems to be adding to the counter on the Dashboard for emails received when it should not, as I uploaded only images. Regardless of what I upload, this shouldn't add to the email counter."*

Fix the dashboard metric so the emails-received counter counts only mailbox-channel material; manual uploads count under their own measure (or none).

## Verification

- [ ] A manual upload changes no email counter.
- [ ] Email arrival still increments it.
- [ ] Test pinning the channel filter of each dashboard counter.
