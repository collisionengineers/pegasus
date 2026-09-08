---
id: DELIV-057
type: ticket
title: Seed the historical vehicle lookup migration schema accurately
status: verifying
area: delivery-repository
order: -10
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:30:20.896Z'
  review: '2026-09-08T14:50:24.397Z'
  verifying: '2026-09-08T15:38:07.533Z'
taken_at: '2026-09-08T13:34:30.046Z'
branch: DELIV-057-seed-historical-vehicle-lookup-schema
worktree: .worktrees/deliv-057
claim_expires_at: '2026-09-08T15:18:31.455Z'
claim_controller: codex-mcp-client
lease_id: 8216975d-1775-4d5b-a9ef-f6a812bb3800
lease_revision: 2
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\deliv-057'
lease_phase: implementing
lease_heartbeat_at: '2026-09-08T14:48:31.455Z'
labels:
  - regression
  - test-fixtures
  - corrective
links: []
refs:
  - docs/engineering.md
commits:
  - 9ab1368bfbb7c93c70cbde4e91fea3bb9408ce2b
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/707'
archived: false
created: '2026-09-08T13:28:14.030Z'
updated: '2026-09-08T15:38:07.533Z'
---

## What

Fix the three VehicleLookupBackfillTests setup failures by seeding the schema that actually exists before the tested migration.

## Why

PR700 run34196369756 fails before exercising migration assertions because its historical schema still requires InstructionConfirmedByStaff. The current production schema removed that field via PLAT-072. Historical test setup must reflect its own starting schema without restoring obsolete production columns.

## Approach

- Change only tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs using its existing database setup.
- Seed required historical columns at the tested migration stage; preserve all transition/backfill/idempotency assertions.
- No production migration/model/permissions change, compatibility mechanism or fabricated domain inputs.

## Verification

- [ ] Sole host verifier runs the existing VehicleLookupBackfillTests selection after a sequential affected build if required.
- [ ] Retain the original nonzero results and exact new exits; independent review and draft PR to dev.

## Outcome
