---
id: DELIV-027
type: ticket
title: 'Release 34: live Inbox recovery schedule and release record'
status: backlog
area: delivery-repository
assignee: ''
profile: chore
labels:
  - release
  - documentation
links:
  - MAIL-015
  - MAIL-016
  - UIIMP-004
  - DELIV-026
refs:
  - docs/current-architecture.md
  - docs/operations.md
deployment: not-deployed
archived: false
created: '2026-08-27T08:17:00.253Z'
updated: '2026-08-27T08:17:00.253Z'
---

## Purpose

Promote `dev` to `main` (exact-SHA fast-forward) after [[MAIL-016]],
[[MAIL-015]], [[UIIMP-004]] and [[DELIV-026]] merge; build immutable
artifacts; provision so the six-field `ApprovedInboxPollSchedule` from
[[MAIL-015]] reaches the live Worker (read-back on 2026-08-27 still showed the
seven-field value); deploy Web and Worker; smoke; record release 34 in
`docs/operations.md` and refresh `docs/current-architecture.md`.

## Verification

Active revision digest equals manifest; `az functionapp config appsettings
list` shows `0 */5 * * * *`; `Invoke-ProductionSmoke.ps1` passes.

## Outcome
