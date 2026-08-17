---
id: KANMER-004
type: ticket
title: Consolidate Pegasus board areas around durable ownership
status: implementing
area: kanmer-meta
assignee: codex
profile: chore
stageEntered:
  implementing: '2026-08-17T06:39:23.675Z'
taken_at: '2026-08-17T06:38:45.266Z'
branch: task/kanmer-004-area-consolidation
worktree: 'C:/Users/PC/Documents/GitHub/pegasus-worktrees/kanmer-004-area-consolidation'
labels: []
links: []
deployment: n/a
archived: false
created: '2026-08-17T06:38:11.255Z'
updated: '2026-08-17T06:39:23.675Z'
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
