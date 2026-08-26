---
id: DELIV-008
type: ticket
title: >-
  Release 9: promote dev to main, deploy to production, refresh current-state
  docs
status: done
area: delivery-repository
order: 430
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-18T10:49:29.692Z'
  review: '2026-08-18T12:18:11.524Z'
  verifying: '2026-08-18T12:22:01.445Z'
  done: '2026-08-18T12:22:21.868Z'
labels:
  - release
  - requires-live-approval
links:
  - DELIV-002
  - DELIV-003
commits:
  - f1e116c6eb939f901f32e5f89d58d1d8a4701851
  - 898ad3f0
  - c172543f
  - de94c1d0
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/400'
  - 'https://github.com/collisionengineers/pegasus/pull/404'
deployment: production
archived: false
created: '2026-08-18T10:49:05.877Z'
updated: '2026-08-26T14:34:43.301Z'
---

## What

Carry the first exact-SHA fast-forward release under [[DELIV-002]] policy from
`dev` to `main`, then execute production release 9 by the runbook route
(immutable artifacts → validation modes → image push → migrations + DB
bootstrap → `azd provision` web revision → Worker package → smoke) and refresh
`docs/operations.md` / `docs/current-architecture.md` to the shipped state.

## Why

Production served `aecad247` (14 Aug). `main` (`2b0df78c`, PR #394) was never
deployed and `dev` carried ~30 further reviewed commits (design UI, Case Details
split, MAIL-21/22, SIMPLI-*, BUG-001, delivery policy, AUTO-001). Two migrations
(`20260814092852_AddWorkerCaseCreationGrants`, `20260814094632_DropBoxFileRequests`)
were pending on the production database. Operations still recorded an
"un-numbered post-release-8 deployment" pending a recovered manifest.

## Approvals recorded 2026-08-18 (operator)

- `MERGE AUTH GRANTED` for the reviewed `origin/dev` SHA recorded at preflight.
- Azure writes: image push to `pegasusprodacr252ow37gij`; migrations + database
  bootstrap on `pegasus-prod-sql-252ow37gij/pegasus`; `azd provision` producing
  the new digest-pinned revision on `pegasus-prod-web-252ow37gij` (preview
  reviewed and approved); package deploy to `pegasus-prod-worker-252ow37gij`.

## Verification

- Both remote heads equal the promoted SHA; main-push `repository-check` green
  including the contained-in-dev guard.
- `Invoke-ProductionSmoke.ps1` passes: health 200, `/diagnostics/version`
  `sourceSha` == promoted SHA, anonymous `/Cases` → https sign-in, nine Worker
  settings enabled; `__EFMigrationsHistory` head `20260814094632_DropBoxFileRequests`.
- Release-9 row and manifest evidence merged into `docs/operations.md`.

## Outcome

Release 9 shipped 2026-08-18: `main` = `dev` = `f1e116c6` (PR #400 auto-marked merged by the push); web revision `pegasus-prod-web-252ow37gij--f1e116c6eb93` (image `sha256:63e86324…`, `Features__AutomationMcp=true`); both migrations applied and the runtime-role matrix verified; Worker `f1e116c6` deployed via `config-zip` and polling; smoke passed; docs refresh merged as PR #404 (`de94c1d0`, rides the next release). Findings recorded in operations/runbook: the local azd env carried the retired adopted vaults and the Worker's six Key Vault references were unresolved in production (now `Resolved`); `azd deploy worker --from-package` is not usable on this estate; the Log Analytics daily cap was exhausted at 11:52 UTC by the transient host crash-loop; `efbundle.exe` needs the Web host environment. Follow-ups: consider raising the 0.1 GB/day cap; rotate `automation-mcp-client-secret` if its value was surfaced outside Key Vault. Closed out 2026-08-18.
