---
id: PLAT-073
type: ticket
title: Provision and document the Linux-native WSL toolchain
status: done
area: platform-operations
assignee: codex-root
profile: chore
stageEntered:
  preparing: '2026-09-04T15:11:36.637Z'
  review: '2026-09-04T16:57:17.882Z'
  verifying: '2026-09-04T18:03:12.916Z'
  done: '2026-09-04T18:08:23.229Z'
taken_at: '2026-09-04T15:15:10.556Z'
branch: PLAT-073-wsl-toolchain
worktree: .worktrees/plat-073
claim_expires_at: '2026-09-04T18:24:31.629Z'
claim_controller: codex-root
lease_id: b367fe67-fd5d-4e8e-9918-740ddd0d9974
lease_revision: 4
lease_workspace: 'worktree:/home/pguser/projects/pegasus/.worktrees/plat-073'
lease_provider: codex
lease_phase: review
lease_heartbeat_at: '2026-09-04T17:54:31.629Z'
labels:
  - wsl
  - linux
  - tooling
groups:
  - EPIC-013
links: []
blocks:
  - PLAT-074
  - UIIMP-016
commits:
  - a33896724339591d07862bd5223f9d689a355aa7
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/661'
deployment: n/a
delivery_state: integrated
delivery_branch: dev
delivery_sha: a33896724339591d07862bd5223f9d689a355aa7
delivery_recorded_at: '2026-09-04T18:09:02.548Z'
archived: false
created: '2026-09-04T11:58:34.774Z'
updated: '2026-09-04T18:09:02.548Z'
---

## What

Install the pinned offline and cloud development tools under Linux, remove Windows PATH dependencies, reconcile Kanmer v0.4.1, and align Doctor/runbook repair guidance.

## Why

The WSL checkout is native but previously resolved Windows tools and lacked most Pegasus prerequisites.

## Verification

- [x] Both Doctor profiles and the locked restore/build/test lanes pass using Linux-native executables.

## Outcome

Merged through https://github.com/collisionengineers/pegasus/pull/661 at `a33896724339591d07862bd5223f9d689a355aa7`. The WSL host is provisioned and the repository contains only the cross-platform Doctor/diagnostic compatibility changes plus Kanmer v0.4.1 managed reconciliation. WSL restart remains the operator handoff. Follow-ups: [[PLAT-074]], [[UIIMP-016]], [[DELIV-047]], and [[DELIV-048]].
