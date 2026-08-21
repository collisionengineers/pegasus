---
id: PLAT-030
type: ticket
title: Cut the intake processing chain from 30-60s to seconds
status: preparing
area: platform-operations
assignee: ''
profile: fix
labels:
  - regression
  - qdos26008
  - performance
links: []
docs_todo: true
deployment: not-deployed
archived: false
created: '2026-08-21T18:17:18.391Z'
updated: '2026-08-21T18:17:18.391Z'
---

## Why

The operator measured 30-60s from email arrival to the case appearing, and longer for the Box folder. A previous mostly-TypeScript iteration was far faster.

**Root cause.** A chain of minute-granularity hops, each adding its own wait:

- `ApprovedInboxPollSchedule` = `45 * * * * *` — once per minute, so up to 60s just to notice the mail.
- `PendingWorkDispatchSchedule` = `*/15 * * * * *` — up to 15s.
- `host.json` sets **no** `extensions.queues.maxPollingInterval`, so the default 60s idle back-off applies **per queue hop**, and there are two (`intake-work`, `external-work`).
- `IntakeStagedArtifactReconciliationSchedule` = `30 * * * * *` — once per minute, and Box custody sits behind this tick *and* a second queue hop.

## Decision taken

Operator chose queue-polling and timer tightening only — no always-ready instance, no Graph change notifications. Cold start therefore remains and must be stated honestly, not hidden.

## How to verify

Measured end-to-end: a test email's received time to case-visible time, and to Box-folder-created time, before and after. Numbers in the proof, not adjectives. State the per-execution cost arithmetic against the £75 budget before merge.
