---
id: MAIL-033
type: ticket
title: Advance the Graph delta cursor when sparse messages omit receivedDateTime
status: implementing
area: mail-communications
assignee: claude-code/20260901T215000Z-claude-controller/implementer-a1
profile: fix
stageEntered:
  preparing: '2026-09-02T00:59:23.940Z'
taken_at: '2026-09-02T01:27:50.829Z'
branch: task/mail-029-graph-received-datetime
worktree: ../pegasus-worktrees/mail-029-graph-received-datetime
claim_expires_at: '2026-09-02T01:57:50.829Z'
claim_controller: claude-code/20260901T215000Z-claude-controller/implementer-a1
lease_id: 28ea0888-b3e4-4432-aeac-67ce12df01d6
lease_revision: 1
lease_workspace: >-
  worktree:c:\users\pguser\documents\github\pegasus-worktrees\mail-029-graph-received-datetime
lease_phase: implementing
lease_heartbeat_at: '2026-09-02T01:27:50.829Z'
labels: []
groups:
  - EPIC-011
links:
  - MAIL-025
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - 712bfcf3a695ab67c0bcde570ebd30ac9b25e740
  - c6842a8c3a36fe806a3103d067fef207d22651d3
prs:
  - '641'
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.067Z'
updated: '2026-09-02T01:47:51.966Z'
---

## What

Skip sparse Microsoft Graph delta message entries that omit `receivedDateTime` without fetching MIME content, while still advancing and persisting the returned delta cursor.

## Why

PR #641 implements this recovery but incorrectly identifies itself as MAIL-029; live MAIL-029 owns missing Inbox attachment columns and must retain that meaning. Graph delta responses may contain sparse change/removal representations that are not complete messages.

## Approach

- Associate PR #641 with this fresh ticket.
- Keep the delta link as the only cursor owner and persist it only after the page is handled consistently.
- Do not perform an unnecessary MIME fetch for an entry that cannot be processed as a received message.
- Rerun the cancelled SQL integration shard and complete independent review.

## Verification

- [x] A sparse entry is skipped, produces no MIME fetch, and does not wedge the poller.
- [x] The page's cursor advances exactly once and replay is idempotent.
- [x] Ordinary complete messages and removal/change evidence retain their existing behavior.
- [x] All required PR checks are green.

## Outcome
