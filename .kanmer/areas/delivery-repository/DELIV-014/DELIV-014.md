---
id: DELIV-014
type: ticket
title: >-
  Release 15: deploy the feedback-round-2 fixes, verify every issue live,
  promote to main
status: done
area: delivery-repository
order: 1230
assignee: claude-code
profile: chore
stageEntered:
  preparing: '2026-08-20T20:02:47.675Z'
  review: '2026-08-20T20:53:55.997Z'
  verifying: '2026-08-20T20:54:00.869Z'
  done: '2026-08-20T20:54:05.685Z'
labels:
  - release
  - deployment
links: []
refs:
  - docs/runbook.md
  - docs/engineering.md
archived: false
created: '2026-08-20T20:02:19.215Z'
updated: '2026-08-25T06:38:30.002Z'
---

## Why

Operator feedback round 2 (2026-08-20, 15 issues) is fixed across PLAT-016, INTK-020, INTK-021, CASE-005, CASE-007, CASE-008, INTK-022, ENG-006, MAIL-005, DOCS-005, with the PLAT-017 test-data wipe executed. Release 15 ships it all and closes the loop live.

## What

- Merge the remaining green PRs to dev; full runbook release route from the post-merge dev head (azd preview byte-compare vs `artifacts/releases/release-14-d91fd7d7/azd-preview.txt` allowing only the expected code/migration deltas; migration census already carries dev's two new migrations).
- DOCS-005 deployment step: delete the legacy `pegasus-*-binding.json` files from the live Box case folders (approved Box write; exact folders listed at execution).
- Post-deploy verification of every round-2 issue (browser + read-only SQL/Box), including the PLAT-017 live check (empty queues/inbox, login works).
- Docs refresh in the release task before merge: `docs/operations.md` release-15 row + runtime state; `docs/current-architecture.md` (extraction facts, automatic vehicle lookup sweep, Box custody without bindings + attachment files, mail case resolution).
- dev → main exact-SHA non-force promotion under the operator's standing MERGE AUTH; tickets walked to done with proofs; git hygiene sparing codex's in-flight lanes (#470, #473 and their worktrees).

## How to verify

`proof.md`: deployment output, byte-compare result, per-issue live verification, promotion SHAs.

## Outcome
