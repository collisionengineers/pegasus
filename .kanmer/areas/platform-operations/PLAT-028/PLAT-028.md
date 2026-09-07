---
id: PLAT-028
type: ticket
title: Redesign Organizations and Principals with provider API controls
status: implementing
area: platform-operations
order: 80
assignee: principal_delivery_audit
profile: feature
stageEntered:
  preparing: '2026-08-21T14:23:40.633Z'
  review: '2026-09-07T21:19:29.994Z'
  implementing: '2026-09-07T21:30:12.602Z'
taken_at: '2026-09-07T20:10:46.013Z'
branch: PLAT-028-principal-customer
worktree: .worktrees/plat-028
claim_expires_at: '2026-09-07T22:07:16.224Z'
claim_controller: principal_delivery_audit
review_round: 1
lease_id: 898e963f-db89-4b9c-830b-c8faf286e861
lease_revision: 9
lease_controller_run: 20260907T200500Z-v1-remediation
lease_workspace: 'worktree:c:\users\alex\documents\github\pegasus\.worktrees\plat-028'
lease_phase: implementing
lease_heartbeat_at: '2026-09-07T21:37:16.223Z'
labels:
  - ui
  - administration
  - organizations
  - principals
  - provider-api
  - credentials
  - operator-requested
groups:
  - EPIC-008
  - HZN-002
  - EPIC-009
  - EPIC-011
  - EPIC-014
links:
  - TICK-058
  - TICK-061
  - PLAT-024
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-09-provider-and-intermediary-routes.md
commits:
  - 539aa4684d6dba1964c8fa594d2d2a0e3e3489b6
  - d2bf633ec8ddc5b08b4052554d1d4e79f3930682
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/680'
archived: false
created: '2026-08-21T13:19:14.403Z'
updated: '2026-09-07T21:46:19.733Z'
---

## What

Redesign Organizations and Principals as one consolidated Administration experience and add the administrator controls required for the principal-scoped provider API.

## Why

The existing Organizations/Principals surface needs a deliberate redesign. Provider submission access belongs to a stable Principal, so credential generation and lifecycle controls must sit with that Principal rather than in a separate settings area.

## Approach

- Research and redesign the existing organization list, organization detail, principal creation, and principal replacement workflows.
- Add principal-scoped provider credential generation, reset/rotation, revocation, pause, and resume controls.
- Show a generated or reset secret once; retain only its hash and never display it later.
- Pause blocks new submissions while authenticated reads of the Principal's prior receipts/results remain available; revocation invalidates the credential.
- Reuse the existing Administrator authorization and permanent administration history conventions.
- Coordinate the API contract through [[TICK-058]] and credential lifecycle through [[TICK-061]].

## Verification

- [ ] The approved redesign supports existing Organization and Principal workflows without explanatory copy or page duplication.
- [ ] An Administrator can generate, reset, revoke, pause, and resume a Principal's provider access with the required confirmations and history.
- [ ] Non-administrators and provider clients cannot access the Administration surface.

## Outcome

## Current operator correction and handoff — 7 September 2026

The explicit one-customer requirement in [[EPIC-014]] supersedes the historical owner-organisation premise above. Implementation 539aa4684d6dba1964c8fa594d2d2a0e3e3489b6 replaces that hierarchy with the flat Principal workflow, preserves the real repairer/location directory, and folds [[PLAT-050]] existing settings acceptance. PR #680 targets dev. Root supplied the focused passing checks recorded in post-implementation-report; independent visual/exact-head review and post-merge proof remain outstanding.
