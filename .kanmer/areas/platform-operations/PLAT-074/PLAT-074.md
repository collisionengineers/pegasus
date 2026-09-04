---
id: PLAT-074
type: ticket
title: Qualify the Azure SQL Database container for local development
status: preparing
area: platform-operations
assignee: ''
profile: spike
stageEntered:
  preparing: '2026-09-04T18:10:09.523Z'
labels:
  - sql
  - docker
  - azure-sql
  - preview
groups:
  - EPIC-013
links: []
blocks:
  - DELIV-047
refs:
  - docs/adr/0014-local-to-production-deployment.md
archived: false
created: '2026-09-04T11:58:34.782Z'
updated: '2026-09-04T18:10:09.523Z'
---

## What

Evaluate the private-preview Azure SQL Database container against Pegasus migrations, grants, lifecycle and SQL integration tests, adopting it by immutable digest only if every criterion passes.

## Why

The image can improve Azure SQL parity but has preview registry, resource and BACKUP/RESTORE limitations.

## Verification

- [ ] A recorded PASS supports canonical adoption, or a recorded non-PASS retains the current SQL Server image without weakened tests.

## Outcome
