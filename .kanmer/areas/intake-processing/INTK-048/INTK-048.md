---
id: INTK-048
type: ticket
title: Resolve manually linked Unidentified receipts to their Case destination
status: implementing
area: intake-processing
order: 10
assignee: codex
profile: fix
stageEntered:
  preparing: '2026-08-28T14:00:00.743Z'
  review: '2026-08-28T15:57:23.265Z'
  implementing: '2026-08-30T20:10:31.210Z'
taken_at: '2026-08-28T14:01:40.583Z'
branch: task/intk-048-unidentified-manual-link
worktree: ../pegasus-worktrees/intk-048-unidentified-manual-link
labels:
  - regression
  - unidentified
  - production
  - qdos26030
links:
  - INTK-018
  - INTK-029
  - DELIV-031
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - 14e0ad6f522a8b39c735f31535e842d8b0738fc8
prs:
  - '#601'
  - '#639'
archived: false
created: '2026-08-28T13:59:07.307Z'
updated: '2026-09-01T14:51:05.550Z'
---

## What

Ensure an open Unidentified item is resolved when staff link its receipt to an
existing Case, even when the receipt retains its original Unidentified intake
decision.

## Why

Live production U38 and U39 have active manual associations and durable
`intake_case_linked` workflow events for QDOS26030, but both queue items remain
Open. [[INTK-018]] introduced the reconciliation owner but its case mapping
requires `Decision == CaseCreated`; a manual association changes the effective
`CurrentCaseId` without rewriting that immutable processing outcome. This is a
regression in the supersession behavior, not a failed case link.

## Approach

- Make the existing reconciliation owner recognize the effective current Case
  association before applying original-decision eligibility.
- Add focused Core and SQL integration coverage for the staff-link shape.
- Leave linking, persistence, schedules, and the unrelated intake deadlock
  unchanged.

## Verification

- [ ] A manually linked receipt resolves its open U-item to the effective Case
  and records the Case reference in resolution history.
- [ ] A receipt with no real destination remains open, and reconciliation replay
  remains idempotent.
- [ ] Canonical Release restore, build, and non-Corpus tests pass.

## Outcome
