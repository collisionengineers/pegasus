---
id: AUTO-003
type: ticket
title: Expose the completed email-workspace actions through the Automation Actor
status: preparing
area: automation-integrations
order: 10
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T09:23:50.263Z'
labels:
  - follow-up
  - MCP-05
  - mail-workspace
  - next
groups:
  - EPIC-005
  - EPIC-006
links:
  - TICK-047
  - TICK-049
  - TICK-050
  - TICK-051
  - TICK-052
  - TICK-053
  - TICK-054
  - TICK-056
  - TICK-057
  - TICK-064
  - TICK-088
  - TICK-062
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
archived: false
created: '2026-08-20T09:23:33.334Z'
updated: '2026-09-01T14:50:16.681Z'
---

## What

Complete MCP-05 by exposing the email-workspace Core queries and actions that were deliberately absent from [[TICK-062]] because their owning MAIL capabilities had not landed.

## Why

TICK-062 delivered retained-mail list/detail and classification correction only. EPIC-006 also requires thin Automation Actor callers for the completed folder recommendation/move, suggested actions, Case association and correction, message-state management, and outbound-mail capabilities without duplicating business policy.

## Approach

- Wait for each owning MAIL capability to land, then reuse its Core use case directly.
- Add only typed, scoped Automation Actor tools; do not introduce a generic mail-mutation framework or accept arbitrary destinations or recipients.
- Preserve the existing automation.mail authorization, exact-message identity, operation-key, concurrency, attribution and failure conventions.

## Verification

- [ ] Tool inventory and scope-denial tests cover every newly exposed action.
- [ ] Web and Automation callers produce equivalent Core outcomes and permanent history.
- [ ] No tool broadens Outlook/cloud authority beyond its owning MAIL capability.

## Outcome
