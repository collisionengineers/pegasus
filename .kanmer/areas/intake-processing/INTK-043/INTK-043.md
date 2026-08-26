---
id: INTK-043
type: ticket
title: Remove intake and custody warm-path delay for the five-second target
status: review
area: intake-processing
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-08-25T15:26:55.430Z'
  review: '2026-08-26T14:31:20.634Z'
taken_at: '2026-08-26T12:24:00.138Z'
branch: task/intk-043-warm-intake
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/intk-043-warm-intake'
labels: []
groups:
  - EPIC-002
links:
  - AUTO-008
blocks:
  - DELIV-021
  - MAIL-013
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/prd/pegasus-product.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/adr/0032-near-real-time-durable-intake-triggering.md
commits:
  - 6c42d53d
  - ec39cc18
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/560'
archived: false
created: '2026-08-25T15:18:40.610Z'
updated: '2026-08-26T14:31:20.634Z'
---

## What
Consolidate the shared queued intake and normal custody path into one warm Worker route, instrument every material stage, and remove measured processing/custody delay for e-mail and manual upload.

## Why
The observed 30-second route is dominated by Flex queue cold starts, heavyweight source reading, and a second cold/sequential custody path. Polling changes and immediate publication removed only a small portion.

## Acceptance
- Manual upload reaches confirmed custody through the warm unified route within the Pegasus-controlled five-second p95 budget.
- E-mail uses the same route after mailbox discovery; total arrival-to-custody is measured with Outlook/Graph and Box latency attributed separately.
- Identification, classification, extraction, allocation, integrity, idempotency and fail-closed behaviour remain Core-owned and equivalent.
- Every processing UI state is backed by a truthful persisted outcome; [[INTK-001]] owns its display correction.

## Outcome
