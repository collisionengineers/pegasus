---
id: DELIV-013
type: ticket
title: >-
  Release 14: verify merged dev, deploy to production, refresh current-state
  docs, promote to main
status: done
area: delivery-repository
order: 1020
assignee: claude-code
profile: chore
stageEntered:
  review: '2026-08-20T12:51:27.291Z'
  verifying: '2026-08-20T12:51:31.578Z'
  done: '2026-08-20T12:51:44.660Z'
labels:
  - release
  - deployment
  - requires-live-approval
links: []
commits:
  - d91fd7d7835af116c0c769b75fd4ccae56ca377b
  - 39bb118ab5cfd72f6e970ce5b08e85b11c3d56d9
prs:
  - '472'
  - '475'
deployment: production
archived: false
created: '2026-08-20T11:07:36.848Z'
updated: '2026-09-03T09:06:47.465Z'
---

## Why

Origin/dev at a3c88a7b carried PRs #437–#467 (36 tickets in Verifying) fixing every operator-reported production issue from 2026-08-20 plus the expanded roster. Production served release 13 (2325ed4a). Operator directive: verify each merged ticket, enforce repository rules (especially design-authority copy rules), deploy release 14 via the full runbook route, refresh current-state docs on BOTH dev and main, promote dev→main (MERGE AUTH GRANTED 2026-08-20), close out all verifying tickets, and restore git hygiene without touching in-flight work.

## How to verify

Production serves the promoted SHA (Invoke-ProductionSmoke green), docs/operations.md release-14 row + docs/current-architecture.md match reality on dev AND main, all release-scope tickets Done with proof, git hygiene clean except in-flight lanes.

## Outcome

Release 14 shipped 2026-08-20. Cut d91fd7d7 (after the pre-cut verification fan-out and copy-fix PR #472); full runbook route executed gate-by-gate; production serves the SHA with smoke passed; four migrations applied; post-deploy live verification confirmed every browser/SQL-checkable operator issue fixed (badge=rows, mailbox-only counter, U7 swept, identifier-free mailbox admin, single readiness chip, image galleries, .doc/.msg intake, worker abort silence, clean polls). Docs refreshed (PR #475, incl. the OPS-14 previous-artifact rollback procedure) and dev→main promoted at 39bb118a — docs correct on both branches. 34 tickets closed with proofs; TICK-102/TICK-104 held on the closed Features:SendToAi gate; PLAT-015 filed for structural copy debt; artifacts retained at artifacts/releases/release-14-d91fd7d7. Full transcript in scratch.
