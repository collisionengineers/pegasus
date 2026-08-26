---
id: INTK-042
type: ticket
title: Publish committed intake and custody work immediately
status: verifying
area: intake-processing
order: 10
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-25T15:19:34.322Z'
  review: '2026-08-26T08:31:00.717Z'
  verifying: '2026-08-26T09:38:11.599Z'
taken_at: '2026-08-25T16:35:16.630Z'
branch: task/intk-042-immediate-publication
worktree: ../pegasus-worktrees/intk-042-immediate-publication
labels: []
groups:
  - EPIC-002
links: []
blocks: []
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
commits:
  - c0508d3f
  - 4e1cc7c4
  - dfda320d
  - eae300f9
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/553'
archived: false
created: '2026-08-25T15:18:39.858Z'
updated: '2026-08-26T12:48:52.870Z'
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
