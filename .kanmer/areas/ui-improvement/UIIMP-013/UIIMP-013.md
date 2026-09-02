---
id: UIIMP-013
type: ticket
title: Reduce the Test UI snapshot gate critical path without weakening coverage
status: implementing
area: ui-improvement
assignee: codex-root
profile: chore
stageEntered:
  preparing: '2026-09-02T03:09:00.895Z'
taken_at: '2026-09-02T03:21:55.240Z'
branch: task/uiimp-013-test-ui-cost
worktree: ../pegasus-worktrees/uiimp-013-test-ui-cost
claim_expires_at: '2026-09-02T15:17:08.819Z'
claim_controller: codex-root
lease_id: 1ae99c27-83ef-4d11-81a3-d6f25bc61fa4
lease_revision: 6
lease_workspace: >-
  worktree:c:\users\pguser\documents\github\pegasus-worktrees\uiimp-013-test-ui-cost
lease_provider: codex
lease_phase: running-command
lease_heartbeat_at: '2026-09-02T13:17:08.819Z'
lease_reclaimed_from: claude-code/20260901T215000Z-claude-controller/implementer-a1
labels:
  - ci
  - performance
groups:
  - EPIC-011
links:
  - UIIMP-005
refs:
  - docs/engineering.md
commits:
  - fa7d82ed95c7dc8a0b90f9d22db74118603def75
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/644'
archived: false
created: '2026-08-30T12:51:09.415Z'
updated: '2026-09-02T13:17:08.819Z'
---

## What

Reduce the Test UI snapshot gate's pull-request critical path without weakening
its complete capture, stale-file, orphan, or offline-render guarantees.

## Current evidence

- Historical run `33310451221` spent 40m23s in capture before cancellation.
- Recent successful build-relevant runs complete capture and verify in
  approximately 24–27 minutes, so the original 50-minute headline is stale.
- The script currently pins the whole capture selection to two threads even
  though only browser tests require that cap; non-browser integration tests use
  the repository's proven four-thread default elsewhere.
- Verify is one fact over the retained capture, not a second run of all capture
  tests.

## Decision

Keep the full gate on every build-affecting pull request. Split the exact
capture selection into browser and non-browser phases, retain the browser cap,
let non-browser capture inherit the project cap, reuse the build and capture
directory, and make incomplete-run output distinct from an explicit stale
snapshot assertion.

Do not reuse broader CI lane captures or introduce UI-only scheduling: both
weaken the curated snapshot input or detection boundary.

## Verification

- [ ] The same 415 tests run across a disjoint two-filter partition.
- [ ] Fresh verify, stale-file injection, orphan injection, and catalogue checks
      retain their outcomes.
- [ ] Three runs of one PR SHA all pass with median snapshot time at most
      22 minutes and no run above 25 minutes.

## Outcome
