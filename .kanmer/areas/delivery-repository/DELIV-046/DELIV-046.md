---
id: DELIV-046
type: ticket
title: Restore main as an ancestor of dev
status: done
area: delivery-repository
assignee: codex-root
profile: chore
stageEntered:
  preparing: '2026-09-04T11:59:53.345Z'
  review: '2026-09-04T12:23:57.577Z'
  verifying: '2026-09-04T15:08:23.999Z'
  done: '2026-09-04T15:10:04.262Z'
labels:
  - git
  - release
  - urgent
groups:
  - EPIC-013
links: []
blocks:
  - PLAT-073
commits:
  - 2958ef5b68e51fce99b1c677abfa261a3eabbb46
  - 0174adef1a00b4a29729d3a0ffd714838562d2c8
  - c90f2b8915186efd5bf932cec573846ae75ff1fe
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/660'
deployment: n/a
archived: false
created: '2026-09-04T11:58:34.764Z'
updated: '2026-09-04T15:11:02.490Z'
---

## What

Merge the authorised main-only commits into dev through a reviewed task PR while preserving exact ancestry.

## Why

The documented exact-SHA promotion route currently fails because main and dev have diverged.

## Verification

- [ ] origin/main is an ancestor of origin/dev and both main-only test artifacts and dev history remain reachable.

## Outcome

PR #660 merged into dev with merge commit c90f2b8915186efd5bf932cec573846ae75ff1fe. origin/main is now an ancestor of origin/dev; all four authorised test artifacts were preserved byte-for-byte. Follow-up: [[PLAT-073]].
