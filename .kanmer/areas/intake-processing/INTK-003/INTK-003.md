---
id: INTK-003
type: ticket
title: Recover dispatched intake work whose queue message never arrives
status: backlog
area: intake-processing
assignee: ''
profile: fix
labels: []
groups:
  - EPIC-002
links:
  - SIMPLI-009
  - SIMPLI-010
archived: false
created: '2026-08-17T11:46:06.025Z'
updated: '2026-08-17T11:46:06.025Z'
---

## What

A work item in `dispatched` (message enqueued, lease cleared by `MarkDispatchedAsync`) has no reconciliation path if its queue message never reaches the Worker (message TTL expiry after a long Worker outage, manual queue clearing). `RecoverExpiredLeasesAsync` covers `dispatching|processing` leased rows; dispatch candidates are `pending|retry_scheduled`. Such a row stays "Received" forever.

## Why

Found in the PR #385 review of [[SIMPLI-009]] (T3). A 2026-08-17 read-only production count showed **0** such rows, so this is resilience, not repair — hence a small separate ticket rather than scope on [[SIMPLI-010]].

## Approach

Generalise `RecoverExpiredLeasesAsync` (or a sibling in the same reconciliation timer) to return unleased `dispatched` rows older than a chosen age (e.g. 1 h since `DueAtUtc`) to `pending`. Safe: a duplicate message no-ops because `ClaimProcessingAsync` refuses settled or leased work. Choose the age against the queue visibility timeout and message TTL in `docs/operations.md`; one `RecoveryTests` case; FRD-02 sentence if behaviour is stated there.

## Verification

- [ ] A `dispatched` row older than the threshold with no lease is re-dispatched by the reconciliation timer and processed once.
- [ ] A freshly dispatched row is left alone.

## Outcome
