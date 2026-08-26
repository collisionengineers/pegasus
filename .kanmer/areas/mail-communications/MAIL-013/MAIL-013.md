---
id: MAIL-013
type: ticket
title: Wake approved mailbox intake through Graph change notifications
status: implementing
area: mail-communications
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-25T15:26:56.915Z'
taken_at: '2026-08-26T14:57:23.032Z'
branch: task/mail-013-graph-wake
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/mail-013-graph-wake'
labels: []
groups:
  - EPIC-006
links: []
blocks:
  - DELIV-021
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/adr/0024-stable-approved-mailbox-identity-and-explicit-baseline.md
  - docs/adr/0032-near-real-time-durable-intake-triggering.md
archived: false
created: '2026-08-25T15:18:39.919Z'
updated: '2026-08-26T14:57:23.032Z'
---

## What
Add Microsoft Graph basic change notifications as a wake-up signal for approved Inbox intake, with the warm Web app accepting callbacks and the Worker retaining sole mailbox cursor and processing ownership.

## Why
Fifteen-second polling still makes ordinary e-mail visibly slow and materially increased Functions cost.

## Acceptance
- POST /hooks/microsoft-graph/mail validates the Graph token handshake and clientState, queues subscription/mailbox identifiers, and returns promptly.
- Persist one subscription per approved Inbox in existing SQL; maintain every six hours and renew within 48 hours.
- Recover lifecycle missed/removed notifications through the existing delta/cursor path.
- Keep five-minute fallback polling and never expose the forwarding desk as the sender while identity is unresolved.

## Outcome
