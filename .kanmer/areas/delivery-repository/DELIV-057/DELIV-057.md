---
id: DELIV-057
type: ticket
title: Seed the historical vehicle lookup migration schema accurately
status: done
area: delivery-repository
order: -10
assignee: codex-mcp-client
profile: fix
stageEntered:
  preparing: '2026-09-08T13:30:20.896Z'
  review: '2026-09-08T14:50:24.397Z'
  verifying: '2026-09-08T15:38:07.533Z'
  done: '2026-09-08T15:50:04.524Z'
labels:
  - regression
  - test-fixtures
  - corrective
links: []
refs:
  - docs/engineering.md
commits:
  - 3370e58f40de205986fb9642e62b329f06f91a4f
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/707'
deployment: n/a
delivery_state: integrated
delivery_branch: dev
delivery_sha: 3370e58f40de205986fb9642e62b329f06f91a4f
delivery_recorded_at: '2026-09-08T15:51:18.374Z'
archived: false
created: '2026-09-08T13:28:14.030Z'
updated: '2026-09-08T15:51:59.167Z'
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

- [x] Sole host verifier ran the existing VehicleLookupBackfillTests selection after a sequential affected build.
- [x] Original nonzero results and exact new exits are retained; independent review and exact-merge verification passed.

## Outcome

PR #707 squash-merged into `dev` as
`3370e58f40de205986fb9642e62b329f06f91a4f`. The author commit
`9ab1368bfbb7c93c70cbde4e91fea3bb9408ce2b` remains provenance; the merged
SHA is the reachable integration record. Schema-2 proof records three
exact-merge PASS attempts: locked restore, affected Release build with zero
warnings/errors, and three focused VehicleLookupBackfillTests passed with none
failed or skipped.

The prior PR700 NULL `InstructionConfirmedByStaff` setup failures and
subsequent optional pre-merge CI history remain in the proof-linked record;
they were not relabelled or erased. This is a non-deployable test-fixture
correction (`n/a`): no deployment, release-candidate promotion, or main update
occurred.
