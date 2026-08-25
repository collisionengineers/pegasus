---
id: DELIV-022
type: ticket
title: 'Release 31: deploy mailbox Image Intake and interrupted-work recovery'
status: preparing
area: delivery-repository
assignee: ''
profile: chore
stageEntered:
  preparing: '2026-08-25T17:22:42.586Z'
labels:
  - release
  - deployment
  - requires-live-approval
  - quality-review
links:
  - INTK-040
  - PLAT-036
  - INTK-003
refs:
  - docs/runbook.md
  - docs/engineering.md
  - docs/operations.md
deployment: not-deployed
archived: false
created: '2026-08-25T17:22:39.944Z'
updated: '2026-08-25T17:22:42.586Z'
---

## What

Promote the audited `dev` revision `7dbb7c3952fba74cab2d65a2971ee30b9bc8d273`, deploy it as production release 31, verify the mailbox Image Intake, interrupted-work recovery and telemetry-volume changes, and record exact release evidence.

## Why

Production remains on release 30 (`eaabf311`). The audited release candidate contains the merged changes from PRs #546–#551, including one additive migration and updated Worker permissions.

## Boundaries

- Pin release scope to `7dbb7c39`; stop and re-audit if `origin/dev` changes.
- Do not include or claim [[INTK-042]] immediate publication or [[DELIV-021]] latency/cost proof.
- Apply the migration and database-role bootstrap before activating Web or Worker packages.
- Every Azure write requires exact-target approval. Updating `main` requires the literal `MERGE AUTH GRANTED` immediately before the atomic push.

## Acceptance

- All local, CI, immutable-artifact and deployment-plan gates pass for the exact SHA.
- `main` and `dev` are atomically fast-forwarded without rewriting history.
- Migration, permissions, Web digest, Worker package and nine-function activation read back exactly.
- Production smoke and approved live intake evidence pass; unsupported failure injection is recorded as not proved.
- `docs/current-architecture.md` and `docs/operations.md` record release 31 before closeout.

## Outcome
