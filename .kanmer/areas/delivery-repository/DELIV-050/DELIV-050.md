---
id: DELIV-050
type: ticket
title: Record report-input permission migration in the release bootstrap census
status: preparing
area: delivery-repository
assignee: ''
profile: fix
stageEntered:
  preparing: '2026-09-07T23:40:52.619Z'
labels: []
groups:
  - EPIC-014
links:
  - DOCS-020
  - PLAT-065
  - DELIV-048
refs:
  - docs/adr/0007-direct-terminal-azure-deployment.md
archived: false
created: '2026-09-07T23:40:22.715Z'
updated: '2026-09-07T23:40:52.619Z'
---

## What

Name the report-input permission migration beside its existing bootstrap permission rows so the release contract can identify the grant-carrying migration.

## Why

PLAT-065's Local deployment-plan check stopped at the migration census: 20260907210000_ReportInputInvalidationPermissions.cs is not named in Invoke-AzureDatabaseBootstrap.ps1. DOCS-020 already added the five required permission entries. This residual is a missing explicit census annotation, not missing SQL grants.

## Verification

The five bootstrap entries must exactly match the migration; the unmodified Local deployment-plan guard must pass on the corrected integrated source. No schema, permission, Azure resource or application behavior change.

## Outcome
