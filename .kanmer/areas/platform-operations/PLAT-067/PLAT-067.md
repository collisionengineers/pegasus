---
id: PLAT-067
type: ticket
title: Sterilize intake data and deploy release 38
status: implementing
area: platform-operations
assignee: codex-root
profile: chore
taken_at: '2026-09-02T12:06:39.290Z'
branch: task/plat-067-wipe-release-38
worktree: 'C:/Users/Alex/Documents/GitHub/pegasus-worktrees/plat-067-wipe-release-38'
claim_expires_at: '2026-09-02T14:55:44.439Z'
claim_controller: codex-root
lease_id: e74166af-f738-4d01-844d-9f13461f8311
lease_revision: 7
lease_controller_run: 20260902T120412Z-codex-root
lease_worker_run: 20260902T120412Z-codex-root-execute-1
lease_workspace: >-
  worktree:c:\users\alex\documents\github\pegasus-worktrees\plat-067-wipe-release-38
lease_provider: codex
lease_phase: running-command
lease_heartbeat_at: '2026-09-02T12:55:44.439Z'
labels:
  - production
  - release
  - intake-wipe
  - requires-live-approval
groups:
  - HZN-004
links: []
commits:
  - 0f0e90ae44ffda7339ca2a460310deeb98121afa
  - 70bb9653b598f7944b46c6be0deae36fa09064ba
  - 1b705bd01d88109b21affddd014fbaa06c82b1ce
prs:
  - '645'
deployment: production
archived: false
created: '2026-09-02T11:55:45.210Z'
updated: '2026-09-02T13:36:22.327Z'
---

## What

Sterilize the production intake-generated Blob and SQL data, then promote and deploy the reviewed release-38 candidate and record the observed production state.

## Why

A clean estate was required before the next intake test round, and the `dev` candidate contained deployable mail, Web, Infrastructure, and Core changes that required the exact-SHA authorised release route.

## Approach

- Inventory, approve, execute, and verify the bounded intake-data wipe.
- Promote and release the frozen exact SHA through immutable artifacts and exact-target approvals.
- Record wipe and release evidence in the canonical current-state documents and promote that documentation without redeploying unchanged code.

## Verification

- [x] Blob and SQL wipe post-checks passed and the operator confirmed the wiped data was absent from the authenticated Web UI.
- [x] Exact release SHA, artifact digest, Web revision, Worker deployment, smoke, and focused checks passed.
- [x] `docs/operations.md` and `docs/current-architecture.md` match the observed state on `main`.

## Outcome

Release 38 is production-verified. The sparse Graph entry no longer blocks the cursor, queued emails are arriving, and the release evidence is promoted to `main` at `1b705bd01d88109b21affddd014fbaa06c82b1ce`.
