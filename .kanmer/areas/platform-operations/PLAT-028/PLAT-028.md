---
id: PLAT-028
type: ticket
title: Redesign Organizations and Principals with provider API controls
status: done
area: platform-operations
order: 80
assignee: principal_delivery_audit
profile: feature
stageEntered:
  preparing: '2026-08-21T14:23:40.633Z'
  review: '2026-09-07T21:19:29.994Z'
  implementing: '2026-09-07T21:30:12.602Z'
  verifying: '2026-09-07T21:50:51.106Z'
  done: '2026-09-07T22:15:12.755Z'
review_round: 1
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
  - 987988e0b984ad63c1de4be3ad189f3afeb2928c
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/680'
deployment: not-deployed
delivery_state: integrated
delivery_branch: dev
delivery_sha: 987988e0b984ad63c1de4be3ad189f3afeb2928c
delivery_recorded_at: '2026-09-07T22:15:40.073Z'
archived: false
created: '2026-08-21T13:19:14.403Z'
updated: '2026-09-07T22:18:03.532Z'
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

- [x] The current one-customer redesign replaces the historical owner-Organization workflow with flat Principal creation, settings and replacement; the real repairer/location directory stays separate.
- [x] An Administrator can generate, reset, revoke, pause, and resume a Principal's provider access with the required confirmations and history.
- [x] Non-administrators and provider clients cannot access the Administration surface.

## Outcome

PR https://github.com/collisionengineers/pegasus/pull/680 merged into dev on 2026-09-07T21:50:36Z as 987988e0b984ad63c1de4be3ad189f3afeb2928c. Root-approved exact-merge proof 321095455570a06d is PASS: locked restore, Release build (zero warnings/errors), 19 Core and 28 integration/browser checks. Delivery is integrated/dev, not deployed. Review F-001 was fixed and independently re-reviewed; original finding and failed-attempt history remain in review/report. Manual viewport/zoom inspection was unavailable and is not claimed.

Existing [[PLAT-050]] settings acceptance is folded into this one customer workflow; generic principal-domain route activation is separately owned by [[TICK-035]]. No new live principal or credential was created. Root owns final integrated release CI/packaging and deployment under [[EPIC-014]].

## Current operator correction and handoff — 7 September 2026

The explicit one-customer requirement in [[EPIC-014]] supersedes the historical owner-organisation premise above. Implementation 539aa4684d6dba1964c8fa594d2d2a0e3e3489b6 replaces that hierarchy with the flat Principal workflow, preserves the real repairer/location directory, and folds [[PLAT-050]] existing settings acceptance. PR #680 is now merged into dev and the independent review and exact-merge proof are complete, as recorded in Outcome above. The original implementation SHA is historical author evidence, not the structured integrated commit. The visual-inspection limitation remains explicit.
