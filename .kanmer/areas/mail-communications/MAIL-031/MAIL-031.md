---
id: MAIL-031
type: ticket
title: Administer mailbox synchronisation and message-state policy
status: backlog
area: mail-communications
order: 670
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-011
links:
  - TICK-054
  - MAIL-027
  - MAIL-028
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-12-operator-experience.md
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.018Z'
updated: '2026-09-03T15:15:28.252Z'
---

## What

Add Administration controls for approved mailbox synchronization and staff-driven message state: read/unread, categories, flags, Deleted Items handling, logical folder scope and freshness.

## Why

The prototype combines these controls with baseline mailbox display settings, while TICK-054 and MAIL-027 own the underlying mutations. A separate policy surface is needed so PLAT-026 is not broadened after verification.

## Approach

- Reuse the approved mailbox/folder bindings, delta state, category catalogue and retained-mail mutation ports.
- Preserve the immutable retained Pegasus message/custody record when Outlook state changes.
- Require explicit production composition and live approval for every Graph write.
- Keep recovery and stale/delta failures visible; never infer successful synchronization.

## Verification

- [ ] Each displayed switch/value maps to one persisted Core-owned policy.
- [ ] Read/category/flag/delete changes are authorized, version-checked and attributable.
- [ ] Delta reset/recovery retains the correct immutable evidence and does not duplicate work.
- [ ] Unavailable production composition renders the action absent, not inert.

## Outcome
