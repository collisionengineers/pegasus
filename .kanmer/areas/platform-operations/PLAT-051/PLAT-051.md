---
id: PLAT-051
type: ticket
title: 'Administration: Action Logs, Reports and Service health areas'
status: backlog
area: platform-operations
assignee: ''
profile: feature
labels:
  - ui
  - wave-4
  - administration
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
archived: false
created: '2026-08-28T08:35:24.123Z'
updated: '2026-08-28T08:35:24.123Z'
---

## What

Wave 4 of [[EPIC-011]]. New admin areas per `context.md` §1.12: Action Logs (filters Search/Area/Actor/Result/From/To, sort toggle, Clear; table Time/Actor/Area/Action/Reference/Result; keeps the `correlationId` parameter for existing links; replaces `Automation/Activity` and `Access`), Reports (From/To/Engineer; Generate/Preview/Export CSV; Engineer Report table), Service health (same table as Operations); `_AdminNav` rows added.

## Owns

`src/Pegasus.Web/Pages/Administration/ActionLogs/**`, `Reports/**`, `ServiceHealth/**` (new), `Pages/Administration/Shared/_AdminNav.cshtml` (rows), tests.

## Blocked by

[[PLAT-029]], the timeline/action-logs ticket, the service-health/report ticket.
