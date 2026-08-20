---
id: TICK-057
type: ticket
title: >-
  UI-14 — Provide detailed classified-email views with distinct Needs sorting
  and Triage queues
status: preparing
area: mail-communications
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-19T08:10:48.884Z'
labels:
  - capability
  - UI-14
  - next
  - post-alpha
  - blocked
groups:
  - EPIC-003
  - EPIC-006
links:
  - TICK-009
  - TICK-010
blocks:
  - TICK-056
refs:
  - docs/frd/frd-08-email-mailbox-and-background-processing.md
archived: false
created: '2026-08-12T15:05:19.394Z'
updated: '2026-08-20T09:41:08.202Z'
---

## What

Plan and research **UI-14**: Categorised email queues for Receiving work, Queries, and Other

## Why

This is allocated to **Next / 0.3.0** in `docs/capabilities.md`. It is **not designated until post-alpha** and is blocked from implementation pending its activation decision and evidence.

## Approach

- At activation, define the Core policy owner, caller, contract, failure behavior, and acceptance evidence.
- Re-check the exact activation boundary in `docs/capabilities.md`; allocation alone is not implementation or deployment.

## Verification

- [ ] A task-level plan covers the capability's exact contract and tests.
- [ ] All activation conditions are accepted before implementation starts.

## Notes

- Source: `docs/capabilities.md` — UI-14.
- Blocked by: [[TICK-009]] — Categorised queues require the shared classification foundation.
- Blocked by: [[TICK-010]] — Categorised queues require the settled detailed taxonomy.
