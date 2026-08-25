---
id: INTK-041
type: ticket
title: Define near-real-time two-stage durable intake
status: verifying
area: intake-processing
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-25T15:19:21.411Z'
  review: '2026-08-25T15:23:42.110Z'
  verifying: '2026-08-25T15:36:07.836Z'
taken_at: '2026-08-25T15:20:41.591Z'
branch: task/intk-041-near-real-time-intake
worktree: ../pegasus-worktrees/intk-041-near-real-time-intake
labels: []
groups:
  - EPIC-002
  - EPIC-006
links: []
blocks:
  - INTK-003
  - INTK-042
  - MAIL-013
  - INTK-001
  - INTK-043
refs:
  - docs/prd/pegasus-product.md
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
  - docs/adr/0002-dotnet-modular-monolith-on-azure.md
docs_todo: true
commits:
  - 5c4f4990
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/547'
archived: false
created: '2026-08-25T15:18:14.748Z'
updated: '2026-08-25T15:36:07.836Z'
---

## What
Define the authoritative product, functional, and architectural contract for near-real-time e-mail and manual-upload intake through identification, classification, extraction, case creation, and custody publication.

## Why
The current polling path adds visible stale states and avoidable latency while sharply increasing Functions cost. The target must preserve durable recovery and Worker ownership without retaining competing generations of intake behaviour.

## Acceptance
- Add capability INT-33 and update the owning PRD plus FRD-02 and FRD-08.
- Add ADR-0032 for Graph wake-up plus immediate durable outbox publication, partially superseding ADR-0002's polling choice.
- Specify truthful sender/status behaviour, p95 latency, recovery timers, telemetry, and cost guardrails.

## Outcome
