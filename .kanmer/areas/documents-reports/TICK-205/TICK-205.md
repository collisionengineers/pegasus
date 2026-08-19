---
id: TICK-205
type: ticket
title: >-
  Resolve the canonical repair-specification versus dual-Audit-specification
  conflict
status: verifying
area: documents-reports
assignee: codex-mcp-client
profile: feature
stageEntered:
  preparing: '2026-08-19T09:02:10.979Z'
  review: '2026-08-19T09:31:01.506Z'
  verifying: '2026-08-19T09:31:14.297Z'
taken_at: '2026-08-19T09:30:04.851Z'
branch: task/tick-205-audit-spec-decision
worktree: ../pegasus-worktrees/tick-205-audit-spec-decision
labels:
  - now
  - source-now
  - decision-required
groups:
  - EPIC-004
links:
  - TICK-093
  - SIMPLI-015
  - TICK-098
  - TICK-207
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/adr/0025-integrate-renderer-and-extractor-into-the-application.md
  - docs/frd/frd-06-vehicle-and-engineering-evidence.md
deployment: n/a
archived: false
created: '2026-08-12T15:08:05.306Z'
updated: '2026-08-19T09:31:14.297Z'
---

## What

Resolve the canonical repair-specification versus dual-Audit-specification conflict.

## Why

ENG-01's single canonical repair-specification rule and RPT-03's intentional Audit comparison pair appeared contradictory until their roles and ownership were made explicit.

## Approach

An ordinary accepted assessment has exactly one canonical repair specification per purpose/version. An Audit intentionally retains two immutable, role-labelled accepted versions—`conservative` and `maximised`—and Pegasus.Core derives their monetary uplift once from their accepted totals. Neither version overwrites, aliases, or silently replaces the other.

## Verification

- [x] The owned decision, downstream owners, failure boundary, and decision-tier evidence are defined.
- [x] Completion is recorded as decision/ownership reconciliation only; no aggregate, persistence, Audit renderer, template, deployment, or acceptance is claimed.

## Notes

- [[TICK-093]] owns the shared versioned repair-specification aggregate, source provenance, acceptance, and correction lineage under FRD-06.
- [[TICK-098]] owns later Audit pair selection, compatible-basis validation, Core-derived monetary uplift, report binding, and FRD-11 behaviour.
- [[TICK-207]] owns the deferred representative Audit template/wording decision. Audit rendering remains unavailable until approved evidence exists.
- Percentage uplift remains undefined and unavailable until its denominator and rounding are separately accepted.
- [[SIMPLI-014]] remains assessment/fee-note only and does not acquire an Audit model or template from this ticket.

## Tracker migration

Authority references were retargeted by [[KANMER-001]] after the legacy tracker was retired.

## Outcome

The apparent conflict is resolved by one canonical accepted version **per role/purpose**. The Audit comparison pair is intentional business evidence, not competing singleton truth: exactly one current accepted `conservative` version and one current accepted `maximised` version are required, and monetary uplift is derived by Core. Implementation is subsumed by [[TICK-093]] and [[TICK-098]]; Audit presentation remains deferred to [[TICK-207]]. TICK-205 makes no repository, FRD, Core, persistence, renderer, template, infrastructure, cloud, Worker, or `main` change.
