---
id: DELIV-013
type: ticket
title: >-
  Release 14: verify merged dev, deploy to production, refresh current-state
  docs, promote to main
status: review
area: delivery-repository
assignee: claude-code
profile: chore
stageEntered:
  review: '2026-08-20T12:51:27.291Z'
taken_at: '2026-08-20T11:08:34.207Z'
branch: task/deliv-013-release-14
worktree: ../pegasus-worktrees/release-14
labels:
  - release
  - deployment
  - requires-live-approval
links: []
deployment: not-deployed
archived: false
created: '2026-08-20T11:07:36.848Z'
updated: '2026-08-20T12:51:27.291Z'
---

## Why

Origin/dev at a3c88a7b carries PRs #437–#467 (36 tickets now in Verifying) fixing every operator-reported production issue from 2026-08-20 plus the expanded roster (MCP, EXT-01/02, SIMPLI-013/INT-14/15, INT-32, PLAT set). Production still serves release 13 (2325ed4a). Operator directive: verify each merged ticket against its requirements, ensure repository rules (especially docs/design/README.md copy rules) are met, deploy release 14 via the full runbook route, refresh current-state docs on BOTH dev and main, promote dev→main (MERGE AUTH GRANTED 2026-08-20), close out all verifying tickets, and restore git hygiene without touching in-flight work (TICK-064/PR-013, AUTO-004/005, PLAT-014, TICK-053, PLAT-005 — codex-mcp-client lanes).

## How to verify

Production serves the promoted SHA (Invoke-ProductionSmoke green), docs/operations.md release-14 row + docs/current-architecture.md match reality on dev AND main, all release-scope tickets Done with proof, git hygiene clean except in-flight lanes.

## Outcome
