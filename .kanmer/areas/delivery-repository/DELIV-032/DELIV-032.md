---
id: DELIV-032
type: ticket
title: >-
  Migrations scaffolded after a dev merge re-add columns from
  earlier-timestamped migrations
status: backlog
area: delivery-repository
assignee: ''
profile: fix
labels:
  - migrations
  - ci
  - follow-up
  - ef-core
links:
  - TICK-058
docs_todo: true
archived: false
created: '2026-08-28T19:40:03.260Z'
updated: '2026-08-28T19:40:03.260Z'
---

## What

A migration scaffolded on a task branch after merging `origin/dev` can re-add a
column that a migration merged in from `dev` already creates, producing
`Column names in each table must be unique` on every environment that applies
the chain.

## Why it happens

EF diffs the model against the **last migration's Designer snapshot**, ordered by
migration id (a timestamp). A migration merged in from `dev` often carries an
*earlier* timestamp than the task branch's own migrations, so it sorts before
them while its columns are absent from the branch's latest Designer file. The
diff therefore sees the column as missing and scaffolds an `AddColumn` for it.

Observed on [[TICK-058]]: `20260828110108_CaseEditLeaseHolderKind` arrived by
merge, the branch's last Designer snapshot was `20260828111732_GrantProviderSubmissions`,
and the newly scaffolded `20260828185508_ProviderDeclaredInstruction` re-added
`CaseWorkflows.EditLeaseHolderKind`. Every SQL integration test failed at
migration time. Fixed there by hand, with the reason recorded in the migration.

## Why it matters

The failure is loud in the SQL integration lane but only *after* a full migrate,
and the hand fix depends on someone recognising the shape. Two branches merging
`dev` and scaffolding migrations in the same week will each hit it.

## Approach

- Decide the guard: a script that asserts no migration's `Up()` adds a column an
  earlier migration already added, run in CI alongside `Test-MigrationGrants.ps1`;
  or a documented rule in the runbook that a branch regenerates its own migration
  after merging `dev`.
- The guard is the better answer — the rule relies on memory, and this defect is
  invisible until a database is migrated from scratch.

## Verification

- [ ] A guard reproduces the TICK-058 shape and fails on it.
- [ ] The guard passes on current `dev`.
- [ ] The runbook records what to do when it fires.
