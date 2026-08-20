---
id: TICK-047
type: ticket
title: MAIL-05 — Recommend the designated Outlook folder for a classified message
status: implementing
area: mail-communications
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:36.697Z'
taken_at: '2026-08-20T11:44:11.993Z'
branch: task/tick-047-mail-05-folder-recommendation
worktree: ../pegasus-worktrees/tick-047
labels:
  - capability
  - MAIL-05
  - next
  - post-alpha
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
archived: false
created: '2026-08-12T15:05:19.177Z'
updated: '2026-08-20T12:17:16.790Z'
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
