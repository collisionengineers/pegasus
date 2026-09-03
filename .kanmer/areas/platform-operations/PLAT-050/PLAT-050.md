---
id: PLAT-050
type: ticket
title: 'Principal settings dialog: EVA API toggles and the Provider API credential'
status: backlog
area: platform-operations
order: 750
assignee: ''
profile: feature
labels:
  - ui
  - wave-4
  - principals
  - credentials
groups:
  - EPIC-011
  - EPIC-009
links:
  - PLAT-028
  - TICK-058
  - TICK-061
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-09-provider-and-intermediary-routes.md
archived: false
created: '2026-08-28T08:35:24.106Z'
updated: '2026-09-03T15:15:28.410Z'
---

## What

Wave 4 of [[EPIC-011]]. Second pass on `Pages/Administration/Principals/**` after [[PLAT-028]]: the Settings dialog with read-only route addresses (FRD-09), the two ADR-0034 EVA API submission toggles (fold `EvaSubmission.cshtml` in), and the Provider API credential controls (generate/show once, reset, revoke, pause, resume with reason) backed by [[TICK-061]] and delivered together with the [[TICK-058]] submission endpoint (D8).

## Owns

`src/Pegasus.Web/Pages/Administration/Principals/**`, tests.

## Blocked by

[[PLAT-028]], [[TICK-061]], [[TICK-058]].
