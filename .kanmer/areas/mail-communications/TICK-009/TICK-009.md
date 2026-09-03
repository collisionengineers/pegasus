---
id: TICK-009
type: ticket
title: >-
  MAIL-21 — Minimum shared Core classification foundation: versioned rules,
  decision evidence, ambiguity outcome, and acceptance co…
status: done
area: mail-communications
order: 700
assignee: grok-shell-kanmer
profile: feature
stageEntered:
  review: '2026-08-17T13:32:42.052Z'
  verifying: '2026-08-17T13:59:46.773Z'
  done: '2026-08-18T12:22:26.383Z'
labels:
  - capability
  - MAIL-21
  - now
  - requires-live-approval
groups:
  - EPIC-003
  - HZN-003
  - EPIC-006
links:
  - TICK-010
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
commits:
  - b8ed3110
  - a6d801b4
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/391'
deployment: production
archived: false
created: '2026-08-12T15:03:52.949Z'
updated: '2026-09-03T09:06:45.779Z'
---

## What

Plan and research **MAIL-21**: Minimum shared Core classification foundation: versioned rules, decision evidence, ambiguity outcome, and acceptance cohort

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [x] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [x] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-21.
- Canonical owner: [Owning FRD](docs/frd/frd-08-email-mailbox-and-background-processing.md#email-mailbox-and-background-processing)
- Activation/boundary: Implemented on dev for the QDOS route (versioned rules, per-message decision evidence, explicit ambiguity outcome); acceptance cohort, deployment, and live verification remain separate evidence states.

## Outcome

Local volume-cohort evidence slice shipped via PR #391 (merged 2026-08-17T13:59:38Z, `a6d801b4`); verified on `main` `f1e116c6` (Core filter 29/29, cohort volume fact passed, labelled facts skip without the labelled tree) and deployed to production by release 9. Labelled holdout and operator acceptance remain parked (need the labelled corpus tree + operator review); staff confirmation/correction UI stays on MAIL-04/05/02/23 and UI-10/14. Closed out 2026-08-18.
