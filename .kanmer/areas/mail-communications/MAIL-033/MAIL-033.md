---
id: MAIL-033
type: ticket
title: Advance the Graph delta cursor when sparse messages omit receivedDateTime
status: done
area: mail-communications
assignee: claude-code/20260901T215000Z-claude-controller/implementer-a1
profile: fix
stageEntered:
  preparing: '2026-09-02T00:59:23.940Z'
  review: '2026-09-02T01:51:24.337Z'
  verifying: '2026-09-02T02:53:00.444Z'
  done: '2026-09-02T13:00:52.991Z'
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
deployment: production
archived: false
created: '2026-09-01T14:40:45.067Z'
updated: '2026-09-03T08:47:30.401Z'
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

PR [#641](https://github.com/collisionengineers/pegasus/pull/641) merged into `dev` at 2026-09-02T02:52:43Z; verification passed and the clean ticket workspace is closed out. No follow-up arose from closeout.
