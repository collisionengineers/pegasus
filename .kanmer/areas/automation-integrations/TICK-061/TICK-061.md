---
id: TICK-061
type: ticket
title: 'API-04 — Issue, reset, revoke, pause, and resume provider credentials'
status: done
area: automation-integrations
order: 340
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-21T14:20:04.602Z'
  review: '2026-08-28T10:49:38.535Z'
  verifying: '2026-08-28T12:42:03.965Z'
  done: '2026-09-02T17:37:46.398Z'
labels:
  - capability
  - API-04
  - next
  - requires-live-approval
groups:
  - HZN-002
  - EPIC-009
links:
  - TICK-058
blocks:
  - TICK-058
  - PLAT-028
refs:
  - docs/frd/frd-09-provider-and-intermediary-routes.md
commits:
  - 41a17163b31a76c6e28307c7767cdceff3602950
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/592'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 41a17163b31a76c6e28307c7767cdceff3602950
delivery_recorded_at: '2026-09-02T16:09:59.631Z'
archived: false
created: '2026-08-12T15:05:19.485Z'
updated: '2026-09-02T17:39:23.871Z'
---

## What

Deliver and plan **API-04**: Principal-scoped provider credential issue, reset/rotation, revocation, pause, resume, and authentication.

## Why

API-01 and API-03 need a real Principal-scoped machine identity. Administrators also need safe lifecycle controls without storing or redisplaying clear secrets.

## Approach

- Add one credential per Principal with one-time secret display and hash-only storage.
- Reset immediately invalidates the previous secret; revocation invalidates authentication.
- Pause blocks new submissions while authenticated reads of prior receipts/results remain available.
- Reuse existing Core Administrator authorization, expected-version, reason, operation-key, and permanent-history conventions.
- Supply the backend contracts consumed by [[PLAT-028]], then unblock [[TICK-058]].

## Verification

- [x] Core, persistence, authentication, migration, and architecture plans/tests cover the complete lifecycle and fail-closed isolation.
- [x] No live credential was issued; live issuance remains subject to separate exact-target approval.

## Notes

- Source: `docs/capabilities.md` — API-04.
- Blocks API-01 and PLAT-028 through structured dependencies.

## Outcome

- PR [#592](https://github.com/collisionengineers/pegasus/pull/592) merged to `dev`
  at `41a17163b31a76c6e28307c7767cdceff3602950` on
  2026-08-28T12:41:28Z.
- Exact-SHA verification PASS on 2026-09-02. The retained first-run
  ActivitySource failure was discharged as transient by a green unchanged-SHA
  rerun, untouched-file census, and the documented process-wide listener race.
- No live credential was issued or deployed.
- Follow-on consumers remain [[TICK-058]] and [[PLAT-028]].
