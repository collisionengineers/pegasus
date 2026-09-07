---
id: DELIV-050
type: ticket
title: Record report-input permission migration in the release bootstrap census
status: verifying
area: delivery-repository
assignee: codex-v1-remediation-root
profile: fix
stageEntered:
  preparing: '2026-09-07T23:40:52.619Z'
  review: '2026-09-07T23:43:38.044Z'
  verifying: '2026-09-07T23:47:21.174Z'
taken_at: '2026-09-07T23:41:24.189Z'
branch: DELIV-050-bootstrap-census
worktree: .worktrees/deliv-050
claim_expires_at: '2026-09-08T00:20:20.956Z'
claim_controller: codex-v1-remediation-root
lease_id: 82c1a50c-868f-4287-90cb-1fba8b1ca3f0
lease_revision: 2
lease_controller_run: 20260907T234022Z-bootstrap-census
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-050'
lease_phase: verifying
lease_heartbeat_at: '2026-09-07T23:50:20.956Z'
labels: []
groups:
  - EPIC-014
links:
  - DOCS-020
  - PLAT-065
  - DELIV-048
refs:
  - docs/adr/0007-direct-terminal-azure-deployment.md
commits:
  - 4b7a2af44342c8c4153bb7c7df87716edb103e49
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/687'
archived: false
created: '2026-09-07T23:40:22.715Z'
updated: '2026-09-07T23:50:20.956Z'
---

## What

Name the report-input permission migration beside its existing bootstrap permission rows so the release contract can identify the grant-carrying migration.

## Why

PLAT-065's Local deployment-plan check stopped at the migration census: 20260907210000_ReportInputInvalidationPermissions.cs is not named in Invoke-AzureDatabaseBootstrap.ps1. DOCS-020 already added the five required permission entries. This residual is a missing explicit census annotation, not missing SQL grants.

## Verification

The five bootstrap entries must exactly match the migration; the unmodified Local deployment-plan guard must pass on the corrected integrated source. No schema, permission, Azure resource or application behavior change.

## Outcome
