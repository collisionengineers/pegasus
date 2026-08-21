---
id: CASE-010
type: ticket
title: >-
  Grant the Worker INSERT on VehicleLookupRequests so the automatic lookup sweep
  enqueues
status: done
area: case-reference-workflow
assignee: ''
profile: fix
stageEntered:
  done: '2026-08-21T15:05:31.862Z'
labels:
  - regression
  - least-privilege
  - worker
links: []
commits:
  - 1af1f203828ec73d1753d2b6354bb937fb756096
prs:
  - '493'
deployment: production
archived: false
created: '2026-08-21T10:45:00.010Z'
updated: '2026-08-21T15:05:31.862Z'
---

## What

CASE-008's automatic vehicle-lookup sweep runs in the Worker, whose
least-privilege SQL role held only SELECT on `VehicleLookupRequests` — on the
deployed estate every request-row INSERT was denied, and `EnqueueDueAsync`'s
`catch (DbUpdateException)` counted the denial as "already recorded". The
reconciliation timer reported success every minute while enqueuing nothing;
`VehicleLookupRequests` and `VehicleLookupObservations` were empty despite
three live cases carrying Fact registrations (operator report 2026-08-21,
issue 2).

Delivered by PR #493 (merged to dev, squash `1af1f203`):
grant-only migration `20260821095500_GrantWorkerVehicleLookupRequests`
(INSERT for `pegasus_worker_runtime_role`, DELETE stays denied); the sweep
now swallows only genuine duplicate-key failures (2601/2627) and any other
database failure fails the function visibly; both migration censuses updated;
`LatestMigrationGrantsWorkerAutomaticVehicleLookupInsert` pins the
post-migration role matrix.

## Verify

Takes production effect at the next release's efbundle run. Proof: live
`sys.database_permissions` shows the INSERT grant, and a fresh instruction
email produces a `VehicleLookupRequests` row within one reconcile tick.

Deviation note: subagents barred by operator directive — self-reviewed;
board write-up backfilled (Kanmer MCP was disconnected when the work ran).
