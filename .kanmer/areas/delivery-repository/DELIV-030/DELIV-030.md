---
id: DELIV-030
type: ticket
title: >-
  Record the Integrated Operations Workspace in current-architecture and
  operations docs
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - docs
  - wave-5
groups:
  - EPIC-011
links:
  - PLAT-067
  - 'https://github.com/collisionengineers/pegasus/pull/645'
refs:
  - docs/frd/frd-12-operator-experience.md
archived: true
created: '2026-08-28T08:35:24.226Z'
updated: '2026-09-03T14:12:17.621Z'
---

## What

Wave 5 of [[EPIC-011]]. Refresh `docs/current-architecture.md` and `docs/operations.md` to the shipped shell, routes, stores (AiJobs, CaseValuations, PrincipalApiCredentials, estimate columns), MCP scope and gated capabilities; final check of the design README's source-and-runtime map; release record per the runbook when `dev`→`main` is promoted (needs `MERGE AUTH GRANTED`).

## Owns

`docs/current-architecture.md`, `docs/operations.md`, release record.

## Blocked by

The removal ticket.

## Outcome

Wholly delivered by [[PLAT-067]] through PR #645. Release 38 refreshed `docs/current-architecture.md` and `docs/operations.md`, recorded the release, and verified both current-state snapshots against production.
