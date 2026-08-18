---
id: DELIV-008
type: ticket
title: >-
  Release 9: promote dev to main, deploy to production, refresh current-state
  docs
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - release
  - requires-live-approval
links:
  - DELIV-002
  - DELIV-003
deployment: not-deployed
archived: false
created: '2026-08-18T10:49:05.877Z'
updated: '2026-08-18T10:49:05.877Z'
---

## What

Carry the first exact-SHA fast-forward release under [[DELIV-002]] policy from
`dev` to `main`, then execute production release 9 by the runbook route
(immutable artifacts → validation modes → image push → migrations + DB
bootstrap → `azd provision` web revision → Worker package → smoke) and refresh
`docs/operations.md` / `docs/current-architecture.md` to the shipped state.

## Why

Production serves `aecad247` (14 Aug). `main` (`2b0df78c`, PR #394) was never
deployed and `dev` carries ~30 further reviewed commits (design UI, Case Details
split, MAIL-21/22, SIMPLI-*, BUG-001, delivery policy). Two migrations
(`20260814092852_AddWorkerCaseCreationGrants`, `20260814094632_DropBoxFileRequests`)
are pending on the production database. Operations still records an
"un-numbered post-release-8 deployment" pending a recovered manifest.

## Approvals recorded 2026-08-18 (operator)

- `MERGE AUTH GRANTED` for the reviewed `origin/dev` SHA recorded at preflight.
- Azure writes: image push to `pegasusprodacr252ow37gij`; migrations + database
  bootstrap on `pegasus-prod-sql-252ow37gij/pegasus`; `azd provision` producing
  the new digest-pinned revision on `pegasus-prod-web-252ow37gij` plus the single
  `webIntakeQueueSender` role-assignment removal (stop if the preview shows
  anything else); package deploy to `pegasus-prod-worker-252ow37gij`.

## Verification

- Both remote heads equal the promoted SHA; main-push `repository-check` green
  including the contained-in-dev guard.
- `Invoke-ProductionSmoke.ps1` passes: health 200, `/diagnostics/version`
  `sourceSha` == promoted SHA, anonymous `/Cases` → https sign-in, nine Worker
  settings enabled; `__EFMigrationsHistory` head `20260814094632_DropBoxFileRequests`.
- Release-9 row and manifest evidence merged into `docs/operations.md`.

## Outcome
