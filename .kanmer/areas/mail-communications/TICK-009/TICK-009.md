---
id: TICK-009
type: ticket
title: >-
  MAIL-21 — Minimum shared Core classification foundation: versioned rules,
  decision evidence, ambiguity outcome, and acceptance co…
status: verifying
area: mail-communications
assignee: grok-shell-kanmer
profile: feature
stageEntered:
  review: '2026-08-17T13:32:42.052Z'
  verifying: '2026-08-17T13:59:46.773Z'
taken_at: '2026-08-17T13:24:17.755Z'
branch: task/tick-009-mail-21-classification-foundation
worktree: ../pegasus-worktrees/tick-009-mail-21-classification-foundation
labels:
  - capability
  - MAIL-21
  - now
  - requires-live-approval
groups:
  - EPIC-003
  - HZN-003
links:
  - TICK-010
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - b8ed3110
prs:
  - '391'
archived: false
created: '2026-08-12T15:03:52.949Z'
updated: '2026-08-17T13:59:46.773Z'
---

## What

Plan and research **MAIL-21**: Minimum shared Core classification foundation: versioned rules, decision evidence, ambiguity outcome, and acceptance cohort

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-21.
- Canonical owner: [Owning FRD](docs/frd/frd-08-email-mailbox-and-background-processing.md#email-mailbox-and-background-processing)
- Activation/boundary: Implemented on dev for the QDOS route (versioned rules, per-message decision evidence, explicit ambiguity outcome); acceptance cohort, deployment, and live verification remain separate evidence states.
