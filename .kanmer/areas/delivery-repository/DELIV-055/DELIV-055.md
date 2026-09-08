---
id: DELIV-055
type: ticket
title: Restore the self-contained release migration host recipe
status: implementing
area: delivery-repository
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:28:18.420Z'
  implementing: '2026-09-08T13:30:29.628Z'
taken_at: '2026-09-08T13:32:27.085Z'
branch: DELIV-055-migration-host-doc
worktree: 'C:\Users\Alex\Documents\GitHub\pegasus\.worktrees\deliv-055'
claim_expires_at: '2026-09-08T14:52:51.369Z'
claim_controller: codex-mcp-client
lease_id: b9dc1816-f141-49a6-a72c-873df695f7f0
lease_revision: 2
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-055'
lease_provider: codex
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T14:22:51.368Z'
labels:
  - release
  - corrective
links: []
refs:
  - docs/adr/0007-direct-terminal-azure-deployment.md
archived: false
created: '2026-09-08T13:25:29.949Z'
updated: '2026-09-08T14:22:51.368Z'
---

## What

Repair the release skill's database-migration reference after documentation PR #702 deleted its required runbook host-environment recipe.

## Why

The current reference points to the absent Release artifacts and bootstrap heading. The bundle constructs the current Production Web host, which requires additional nonblank settings. This is a focused documentation correction supporting [[PLAT-046]], not approval to change deployment order or run migrations.

## Approach

- Document current required process environment in the existing migration reference using approved azd non-secret values and inert host-only placeholders where supported.
- Preserve manifest-bound native bundle invocation, PreMigration and database-bootstrap ordering.
- No source/runtime/schema/permission changes, actual secret material, cloud writes or alternate deployment route.
- Update unmanaged AGENTS.md only as needed to point to the one procedure owner; coordinate after DELIV-053 to avoid overlap.

## Verification

- [ ] Semantic mapping to current Web host required-key/config validation and deployment inputs.
- [ ] No dangling runbook dependency, secrets or change to release ordering.
- [ ] Independent documentation review and relevant serialized documentation checks.

## Outcome
