---
id: MAIL-033
type: ticket
title: Advance the Graph delta cursor when sparse messages omit receivedDateTime
status: backlog
area: mail-communications
assignee: ''
profile: fix
labels: []
groups:
  - EPIC-011
links:
  - MAIL-025
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
prs:
  - '641'
deployment: not-deployed
archived: false
created: '2026-09-01T14:40:45.067Z'
updated: '2026-09-01T14:40:45.067Z'
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

- [ ] A sparse entry is skipped, produces no MIME fetch, and does not wedge the poller.
- [ ] The page's cursor advances exactly once and replay is idempotent.
- [ ] Ordinary complete messages and removal/change evidence retain their existing behavior.
- [ ] All required PR checks are green.

## Outcome
