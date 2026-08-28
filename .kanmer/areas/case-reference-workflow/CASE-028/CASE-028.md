---
id: CASE-028
type: ticket
title: >-
  Case timeline with actor IDs, Action Logs query, workflow stage counts and
  rail counts
status: backlog
area: case-reference-workflow
assignee: ''
profile: feature
labels:
  - backend
  - wave-3
  - notes
  - action-logs
  - counts
groups:
  - EPIC-011
links: []
refs:
  - docs/frd/frd-01-case-identity-and-lifecycle.md
  - docs/frd/frd-04-parties-accounts-and-access.md
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T08:35:24.030Z'
updated: '2026-08-28T08:35:24.030Z'
---

## What

Wave 3 of [[EPIC-011]]. (F) `Core/Cases/CaseTimeline.cs` `GetCaseTimeline` merging operator notes, workflow events, chase outcomes and AI job events newest-first with actor IDs (staff username via `ActorDisplayNames`; constants → `SYSTEM`, `AI`). (G) `Core/Identity/ActionLogs.cs` generalising `AutomationActivity` over ActionHistory + SecurityEvents with filters search/area/actor/result/from/to/sort, new right `ReviewActionLogs`, business reference for case aggregates, composite index migration. (E/N) `CaseStageCounts +WithEngineer +Complete` (D3 groupings), `Core/Operations/RailCounts.cs` (Inbox unread via new `CountUnreadAsync`; Cases = stages + triage + unidentified; Operations attention = failed external work + failed AI jobs + expired active links) consumed by `RailCountsPageFilter`.

## Owns

`src/Pegasus.Core/Cases/CaseTimeline.cs`, `Core/Identity/ActionLogs.cs`, `Core/Identity/AutomationActivity.cs`, `Core/Operations/RailCounts.cs`, `Core/Operations/DashboardCounts.cs` (counts additions; coordinate with the Work Centre ticket), `Core/Actors/ActorDisplayNames.cs`, `Core/Intake/RetainedMail.cs` (unread count only), Infrastructure query classes + index migration, `src/Pegasus.Web/Presentation/RailCountsPageFilter.cs` (wiring), Core tests.

## Verification

- [ ] Rail counts are real figures; no invented zero.
