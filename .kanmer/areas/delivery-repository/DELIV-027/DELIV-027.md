---
id: DELIV-027
type: ticket
title: 'Release 34: live Inbox recovery schedule and release record'
status: done
area: delivery-repository
order: 2260
assignee: claude-fable-5
profile: chore
stageEntered:
  review: '2026-08-27T09:39:30.444Z'
  verifying: '2026-08-27T09:39:35.469Z'
  done: '2026-08-27T09:39:38.508Z'
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
commits:
  - 1ec65dc894f121f4bb5b31ae82c818a401d08beb
deployment: production
archived: false
created: '2026-08-27T08:17:00.253Z'
updated: '2026-09-01T14:44:33.860Z'
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
