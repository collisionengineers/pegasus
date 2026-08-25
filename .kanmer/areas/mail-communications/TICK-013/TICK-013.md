---
id: TICK-013
type: ticket
title: MAIL-14 — Detect an exact Outlook Sent item as report-sent evidence
status: done
area: mail-communications
order: 2080
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-20T05:43:44.797Z'
  implementing: '2026-08-20T05:44:56.863Z'
  review: '2026-08-20T05:45:12.206Z'
  verifying: '2026-08-20T05:45:15.446Z'
  done: '2026-08-20T05:46:23.320Z'
labels:
  - capability
  - MAIL-14
  - now
  - requires-live-approval
groups:
  - HZN-003
links: []
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
deployment: production
archived: false
created: '2026-08-12T15:03:53.026Z'
updated: '2026-08-25T01:27:00.904Z'
---

## What

Plan and research **MAIL-14**: Detect an exact Outlook Sent item as report-sent evidence

## Why

The capability inventory allocates this outcome to **Now / 0.1.0-alpha.1**. This is a current allocation with incomplete evidence or activation work; plan the remaining caller, contract, and acceptance proof before implementation.

## Approach

- Establish the current Core policy owner, real caller, persistence/infrastructure boundary, and acceptance evidence before proposing implementation.
- Recover and resolve the stated activation boundary without treating allocation, registration, or a build as deployment or acceptance.

## Verification

- [ ] A task-level plan records the exact feature contract, caller, failure behavior, and required tests.
- [ ] The activation criteria have been satisfied or explicitly accepted before implementation begins.

## Notes

- Source: `docs/capabilities.md` — MAIL-14.
- Canonical owner: [Outbound correspondence evidence](docs/frd/frd-08-email-mailbox-and-background-processing.md#outbound-correspondence-evidence)
- Activation/boundary: Allocated but non-blocking for `0.1.0-alpha.1` acceptance; post-report tracking starts manual via MAIL-15.
