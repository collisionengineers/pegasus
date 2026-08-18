---
id: TICK-010
type: ticket
title: >-
  MAIL-22 — User-confirmed detailed Received/Sent categories and subtypes,
  mirrored Reply classifications, Other name/reason behavi…
status: done
area: mail-communications
assignee: grok-shell-kanmer
profile: feature
stageEntered:
  review: '2026-08-17T13:37:03.714Z'
  verifying: '2026-08-17T13:51:20.226Z'
  done: '2026-08-18T12:22:30.228Z'
taken_at: '2026-08-17T13:32:55.563Z'
branch: task/tick-010-mail-22-taxonomy
worktree: ../pegasus-worktrees/tick-010-mail-22-taxonomy
labels:
  - capability
  - MAIL-22
  - now
  - requires-live-approval
groups:
  - EPIC-003
  - HZN-003
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - ea25816b
  - 376bef3f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/392'
deployment: production
archived: false
created: '2026-08-12T15:03:52.968Z'
updated: '2026-08-18T12:25:00.575Z'
---

## What

Plan and research **MAIL-22**: User-confirmed detailed Received/Sent categories and subtypes, mirrored Reply classifications, `Other` name/reason behavior, and category/destination separation

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [x] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [x] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-22.
- Canonical owner: [Settled mailbox taxonomy and correction](docs/frd/frd-08-email-mailbox-and-background-processing.md#settled-mailbox-taxonomy-and-correction)
- Activation/boundary: This row owns allocation only; the linked requirements clause owns behavior and routes to accepted provenance.

## Outcome

Taxonomy persistence for Other/Sent classification categories shipped via PR #392 (merged 2026-08-17T13:51:11Z, `376bef3f`); verified on `main` `f1e116c6` (15 taxonomy + 3 persist/reload tests) and deployed to production by release 9. Live user-confirmed classification against the deployed estate is a separate evidence state. Closed out 2026-08-18.
