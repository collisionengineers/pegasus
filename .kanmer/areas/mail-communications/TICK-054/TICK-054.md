---
id: TICK-054
type: ticket
title: >-
  MAIL-13 — Change read state, Outlook categories, flags, or delete messages in
  the app
status: preparing
area: mail-communications
order: 110
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:46.951Z'
labels:
  - capability
  - MAIL-13
  - now
  - requires-live-approval
  - work-pack-activated
groups:
  - EPIC-003
  - EPIC-006
  - EPIC-011
links: []
blocks:
  - MAIL-031
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-12T15:05:19.318Z'
updated: '2026-09-01T14:50:16.752Z'
---

## What

Deliver MAIL-13 for one opened exact message: read/unread, configured approved-category add/remove, flag/unflag, recoverable deletion to Deleted Items, and restoration to the server-recorded prior approved folder.

## Why

The operator committed the prototype's staff-driven message-state behavior for the EPIC-011 work pack. The checked open-questions document already settles the safety boundary: no permanent deletion, no arbitrary category/folder input, and no local or unapproved mailbox write.

## Approach

- Reuse the existing retained-message identity, approved category catalogue, MAIL-07 folder mover and durable operation/history conventions.
- Keep immutable arrival evidence separate from latest-known Outlook state and freshness.
- Require exact message identity, expected Pegasus/provider state, actor, authorization, reason where required, and operation key.
- Preserve unrelated Outlook categories when adding/removing one approved category.
- Use fake Graph HTTP and LocalDB locally. Permission/RBAC activation and every live action remain separately exact-target approval gated.
- Refresh the existing plan/checklist before implementation: their older permanent-delete lines are superseded by the checked open-questions boundary and must not be executed.
- [[MAIL-031]] owns Administration policy; this ticket owns the message mutations.

## Verification

- [ ] The settled exact-message action set is implemented through one Core owner.
- [ ] Retained Pegasus evidence and history survive every Outlook state change.
- [ ] Stale/replayed/unknown external outcomes recover or fail closed without duplicate mutation.
- [ ] No permanent-delete action exists for any role.
- [ ] No local/test profile mutates Outlook.

## Outcome
