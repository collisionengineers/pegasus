---
id: DELIV-042
type: ticket
title: >-
  Create EPIC-012, its context and run record; add existing members and link
  dependencies
status: done
area: delivery-repository
order: 2670
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-09-02T20:34:13.460Z'
  implementing: '2026-09-02T20:35:33.877Z'
  review: '2026-09-02T20:35:35.387Z'
  verifying: '2026-09-02T20:35:36.866Z'
  done: '2026-09-02T20:35:38.382Z'
labels:
  - kanmer
  - phase-0
  - case-workspace-v2
groups:
  - EPIC-012
  - EPIC-011
links: []
refs:
  - docs/engineering.md
archived: false
created: '2026-09-02T20:31:39.027Z'
updated: '2026-09-03T09:06:58.275Z'
---

## What

Board-only: create the EPIC-012 group with context.md, file the fourteen new tickets, amend the existing tickets, add memberships and `blocks` links, write the automation run record.

## Why

`kanmer-auto` needs one explicit group with context before any lane runs.

## Approach

- Follow kanmer-tickets; search before creating; expected_updated on every amendment.

## Verification

- [ ] `get_group EPIC-012` lists every member; `get_links` shows the dependency edges.

## Outcome
