---
id: INTK-018
type: ticket
title: Resolve Unidentified items when their receipt reaches a real destination
status: done
area: intake-processing
order: 1530
assignee: group-lane
profile: fix
stageEntered:
  implementing: '2026-08-20T04:20:13.751Z'
  review: '2026-08-20T04:43:23.616Z'
  verifying: '2026-08-20T05:10:28.317Z'
  done: '2026-08-20T12:45:10.873Z'
labels:
  - defect
  - unidentified
  - production
links:
  - INTK-007
  - INTK-015
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
commits:
  - bbb7b6d4
  - 94472c69
  - 77bb1306
prs:
  - '453'
deployment: production
archived: false
created: '2026-08-20T03:25:25.736Z'
updated: '2026-08-25T01:27:00.569Z'
---

## What

Every Unidentified item ever created in production (U1–U10) is still `Open` with `ResolvedAtUtc` NULL. **U7 is provably stale**: its origin receipt (42ee5893…) was promoted to ImageIntake AU17SEO-01 seconds later, yet U7 stayed open. Also, the Unidentified row was opened *before* routing completed (U7–U10 created at 02:54:39, routing finished 02:55:25) — items are parked in Unidentified while still being processed.

Fix both halves:
- An Unidentified item whose origin receipt subsequently reaches a real destination (image case, formal case, association) is resolved automatically with the destination recorded in its history.
- An item is not shown as Unidentified while its work item is still pending/retrying — Unidentified is a terminal parking state, not a transit lounge.

## Why

[[INTK-007]]'s contract says resolution permanently records where the item went; a queue that only grows is operationally useless and directly caused the operator's "in the interim they were showing in unidentified" report ([[INTK-015]]).

## Verification

- [ ] Concurrency-safe test: receipt promoted after a U-allocation → U row resolved with destination.
- [ ] Pending/retrying group members never appear on the Unidentified surface.
- [ ] Production readback after deploy: stale open rows (U7 at minimum) resolved by the product's own reconciliation, not manual SQL.
