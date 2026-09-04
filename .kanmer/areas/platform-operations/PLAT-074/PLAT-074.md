---
id: PLAT-074
type: ticket
title: Qualify the Azure SQL Database container for local development
status: done
area: platform-operations
assignee: ''
profile: spike
stageEntered:
  preparing: '2026-09-04T18:10:09.523Z'
  implementing: '2026-09-04T18:12:03.187Z'
  review: '2026-09-04T18:12:03.429Z'
  verifying: '2026-09-04T18:12:03.670Z'
  done: '2026-09-04T18:12:03.903Z'
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
deployment: n/a
archived: false
created: '2026-09-04T11:58:34.782Z'
updated: '2026-09-04T18:12:03.903Z'
---

## What

Evaluate the private-preview Azure SQL Database container against Pegasus migrations, grants, lifecycle and SQL integration tests, adopting it by immutable digest only if every criterion passes.

## Why

The image can improve Azure SQL parity but has preview registry, resource and BACKUP/RESTORE limitations.

## Verification

- [x] Recorded INCONCLUSIVE qualification retains the current SQL Server image without weakened tests.

## Outcome

Microsoft's private preview is the real Azure SQL Database engine in a local Linux container, not Azure SQL Edge and not serverless. This host satisfies the published platform and resource prerequisites, but the preview registry credential is absent and the image pull returned authentication required. Runtime qualification was therefore impossible. The current immutable SQL Server image remains authoritative. A future attempt requires operator-completed preview license/signup and interactive Docker registry login, followed by digest, engine identity, migration, grant and full SQL-lane proof.
