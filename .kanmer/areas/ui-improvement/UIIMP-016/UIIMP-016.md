---
id: UIIMP-016
type: ticket
title: Replace Windows accessibility evidence with Chromium automation
status: implementing
area: ui-improvement
assignee: codex
profile: feature
stageEntered:
  preparing: '2026-09-04T18:12:32.327Z'
taken_at: '2026-09-04T18:14:27.477Z'
branch: UIIMP-016-chromium-accessibility
worktree: .worktrees/uiimp-016
claim_expires_at: '2026-09-04T18:44:27.478Z'
claim_controller: codex
lease_id: e249bc77-023c-45d8-9fc2-4820201cc5c5
lease_revision: 1
lease_workspace: 'worktree:/home/pguser/projects/pegasus/.worktrees/uiimp-016'
lease_phase: implementing
lease_heartbeat_at: '2026-09-04T18:14:27.477Z'
labels:
  - accessibility
  - chromium
  - linux
groups:
  - EPIC-013
links: []
blocks:
  - DELIV-047
refs:
  - docs/prd/pegasus-product.md
archived: false
created: '2026-09-04T11:58:34.790Z'
updated: '2026-09-04T18:14:27.477Z'
---

## What

Make package-pinned Playwright Chromium automation the accessibility release evidence and remove the Edge/Narrator gate.

## Why

The operator selected automation-only evidence to eliminate the Windows accessibility handoff.

## Verification

- [ ] The governing documents and automated browser suite agree, while explicitly recording that screen-reader coverage is no longer claimed.

## Outcome
