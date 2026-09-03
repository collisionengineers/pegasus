---
id: DELIV-039
type: ticket
title: >-
  Release 38 — promote, build, provision, deploy, smoke, and record the EPIC-011
  programme
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - release
  - requires-live-approval
  - work-pack
groups:
  - EPIC-011
links:
  - DELIV-030
  - DELIV-037
  - PLAT-067
  - 'https://github.com/collisionengineers/pegasus/pull/645'
refs:
  - docs/runbook.md
  - docs/engineering.md
  - docs/operations.md
deployment: not-deployed
archived: true
created: '2026-09-01T21:54:35.713Z'
updated: '2026-09-03T14:12:18.406Z'
---

## What

The single production release after every programme PR has merged: preflight, the operator's literal `MERGE AUTH GRANTED`, the atomic lease promotion of one exact `origin/dev` SHA, immutable artifacts built in a detached worktree at that SHA, `oras` upload with digest equality, migration if the manifest's `migrationIdentity` changed, `PreProvision` then `azd provision` (including the approved Document Intelligence resource from [[PLAT-065]]), Worker `config-zip`, `Invoke-ProductionSmoke.ps1` plus per-change canaries, the current-state docs PR to `dev` ([[DELIV-030]] and the release row), and the second promotion-only pass.

## Why

AGENTS.md rule 4 (the ticket precedes the branch) and the [[DELIV-037]] precedent for release 37. The pack runbook `pegasus-work-pack/orchestration/claude/orchestration-plan.md` Phase 6 is the procedure; `.agents/skills/pegasus-release/SKILL.md` and `docs/engineering.md` § Branches and delivery bind.

## Prerequisites

- `.azure/pegasus-prod` present on the release workstation (`azd env get-values -e pegasus-prod` names `rg-pegasus-prod` and `pegasusprodkv252ow37g`); absent on this machine on 2026-09-01.
- The release-37 artifact folder for Worker rollback, or Worker rollback recorded as unavailable (the Web image survives in ACR by digest).

## Verification

- [ ] `docs/operations.md` release-38 row names the source SHA, image digest, revision and migrations.
- [ ] Both remote heads equal the promotion-only docs SHA after the second pass.
- [ ] Smoke passes with the exact source revision and Worker activation; canaries recorded.
- [ ] Every programme ticket's proof gains its Part 2 release evidence.

## Outcome

Superseded and wholly delivered by [[PLAT-067]] through PR #645. Release 38 was promoted, deployed, smoke-tested, and production-verified; the current-state documents record the resulting source, artifact, migration, and runtime evidence.
