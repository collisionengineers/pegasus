---
id: TICK-007
type: ticket
title: >-
  EVAL-05 — Display the rule-generated category and evidence beside the human
  review once rules exist
status: review
area: mail-communications
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-20T05:38:49.143Z'
  review: '2026-08-20T06:03:53.363Z'
taken_at: '2026-08-20T05:54:09.299Z'
branch: task/tick-007-eval-05-rule-suggestion
worktree: ../pegasus-worktrees/tick-007
labels:
  - capability
  - EVAL-05
  - now
groups:
  - HZN-003
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/463'
archived: false
created: '2026-08-12T15:03:52.910Z'
updated: '2026-08-20T06:03:53.363Z'
---

## What

Plan and research **EVAL-05**: Display the rule-generated category and evidence beside the human review once rules exist

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — EVAL-05.
- Canonical owner: [QDOS-alpha evaluation boundary](docs/frd/frd-08-email-mailbox-and-background-processing.md#qdos-alpha-evaluation-boundary)
- Activation/boundary: Separately owned prerequisite; not QDOS delivery. The retained `Now` target records the evaluator allocation boundary above, not a QDOS implementation gate.
