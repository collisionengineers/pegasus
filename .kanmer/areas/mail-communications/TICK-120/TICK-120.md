---
id: TICK-120
type: ticket
title: Activate production due-by and seven-day chasing
status: backlog
area: mail-communications
assignee: ''
profile: feature
labels:
  - now
  - source-now
  - requires-live-approval
  - blocked
groups:
  - HZN-003
links:
  - TICK-116
archived: false
created: '2026-08-12T15:08:02.478Z'
updated: '2026-08-17T06:41:56.074Z'
---

## What

Activate the production workflow that retains due-by dates, identifies the seven-day chase condition, and provides the approved staff chasing behavior.

## Why

Local policy tests do not establish that deployed intake, persisted case state, scheduling, staff visibility, and chase output work together in production.

## Approach

- Ensure due-by evidence flows through the real intake/case caller into persisted Core-owned state.
- Ensure the seven-day chase schedule and copyable staff chaser behavior use that state idempotently.
- Correct composition, scheduling, persistence, or staff-view gaps found during activation.
- Keep automatic outbound sending out of scope unless separately approved; the current path is staff-sent copyable text.

## Verification

- [ ] A production-path case retains and displays the correct due-by date.
- [ ] The seven-day condition becomes visible at the correct time without duplicate effects.
- [ ] Staff can generate the approved copyable chaser and the system does not claim it was sent.
- [ ] Live evidence records environment, caller, timing, and limitations.

## Notes

- Source: the retired pre-Kanmer tracker QDOS production path step 6.
- Live mailbox, Azure, credential, deployment, or external operations require fresh approval for exact targets.


## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.
