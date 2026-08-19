---
id: KANMER-004
type: ticket
title: Consolidate Pegasus board areas around durable ownership
status: done
area: kanmer-meta
order: 300
assignee: codex
profile: chore
stageEntered:
  implementing: '2026-08-17T06:39:23.675Z'
  review: '2026-08-17T06:45:08.077Z'
  verifying: '2026-08-17T06:46:49.235Z'
  done: '2026-08-17T06:47:11.129Z'
labels: []
links: []
deployment: n/a
archived: false
created: '2026-08-17T06:38:11.255Z'
updated: '2026-08-19T09:39:15.272Z'
---

## Why

The board's 38 areas mix durable ownership domains with temporary initiatives, UI slices, defect labels, and pre-groups-era categories. This makes filtering and ownership harder than necessary.

## Deliverable

Consolidate every active and archived ticket into nine durable areas, move cross-domain initiatives into explicit groups, preserve all non-area ticket state, and verify the migration is complete and idempotent.

## Acceptance

- Exactly nine configured areas remain.
- All 245 pre-migration tickets are mapped with their active/archive state preserved.
- Existing groups remain intact and approved new groups have reviewed exact rosters.
- No workflow stages, claims, profiles, documents, links, dependencies, labels, or ticket IDs change unintentionally.
- A second classification pass proposes zero changes.

## Governing context

This is repository workflow and board governance owned by `AGENTS.md`; it does not require or justify a product PRD, FRD, or ADR.

## Outcome

Completed the live board migration from 38 areas to nine durable ownership areas across all 245 pre-existing tickets plus this meta ticket. Added six reviewed cross-domain groups, preserved EPIC-001/HZN-001, retained all 98 archived tickets, and verified zero warnings, zero off-board records, exact rosters and zero idempotency changes. The Intake prefix is INTK because INTAKE collided with a retired prefix. No product source, PR, deployment or follow-up ticket was required.
