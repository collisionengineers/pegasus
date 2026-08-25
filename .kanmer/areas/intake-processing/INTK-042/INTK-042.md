---
id: INTK-042
type: ticket
title: Publish committed intake and custody work immediately
status: backlog
area: intake-processing
assignee: ''
profile: feature
labels: []
groups:
  - EPIC-002
links: []
blocks:
  - MAIL-013
  - INTK-001
  - INTK-043
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
archived: false
created: '2026-08-25T15:18:39.858Z'
updated: '2026-08-25T15:18:47.581Z'
---

## What
After the durable database commit, immediately publish newly pending intake and relevant external/custody work to the existing queues, while retaining slow reconciliation for missed publication.

## Why
Today a timer must rediscover committed work before queue processing can start. Shortening that timer raised cost without removing the extra hand-off latency.

## Acceptance
- Commit remains the durability boundary; publication never precedes it.
- Web and Worker reuse Infrastructure queue adapters rather than duplicate business policy.
- Duplicate delivery is an idempotent no-op and missed publication is recovered within one minute.
- Existing ordinary intake semantics and custody guarantees remain authoritative.

## Outcome
