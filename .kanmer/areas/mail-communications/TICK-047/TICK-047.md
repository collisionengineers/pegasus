---
id: TICK-047
type: ticket
title: MAIL-05 — Recommend the designated Outlook folder for a classified message
status: done
area: mail-communications
order: 480
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:36.697Z'
  review: '2026-08-20T12:19:02.709Z'
  verifying: '2026-08-20T12:55:23.959Z'
  done: '2026-08-21T15:10:40.717Z'
labels:
  - capability
  - MAIL-05
  - next
  - requires-live-approval
groups:
  - EPIC-003
  - EPIC-006
links: []
blocks:
  - TICK-050
  - TICK-049
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 75c33641
  - 4bc3f158
prs:
  - '474'
deployment: production
archived: false
created: '2026-08-12T15:05:19.177Z'
updated: '2026-09-01T14:44:32.093Z'
---

## What

Implement **MAIL-05**: recommend the current policy-designated Outlook folder for one classified retained message.

## Why

This remains allocated to **Next / 0.3.0**, and the operator activated its narrow local read-only implementation after MAIL-23 merged. Staff need an honest message-level recommendation without coupling classification to the later Outlook move.

## Approach

- Reuse the MAIL-23 Core logical-folder policy and approved-mailbox typed bindings through the existing authorized `GetRetainedMail` exact-message read.
- Display the canonical logical folder or an accessible unavailable reason on `/Inbox/{id}`.
- Keep MAIL-06/07 confirmation/move, persistence, Graph, deployment, and live-mailbox writes outside this ticket.

## Verification

- [x] Task research, exact file map, plan, and open questions refreshed against merged MAIL-23 symbols.
- [x] Focused Core and authenticated Web caller evidence covers configured, unavailable, and re-derived outcomes.
- [x] No external write is performed or claimed.

## Notes

- Source: `docs/capabilities.md` — MAIL-05.
