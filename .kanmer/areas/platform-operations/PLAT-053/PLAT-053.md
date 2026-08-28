---
id: PLAT-053
type: ticket
title: >-
  External-work state vocabulary has three copies in Infrastructure — one
  internal owner
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - backend
  - simplification
groups:
  - EPIC-011
links:
  - PLAT-048
refs:
  - docs/frd/frd-12-operator-experience.md
archived: false
created: '2026-08-28T10:58:39.368Z'
updated: '2026-08-28T10:58:39.368Z'
---

## What

The persisted `ExternalWorkItems.State` words (`pending`, `dispatching`, `queued`, `processing`, `completed`, `failed`) are spelled as string literals in three Infrastructure classes: `Persistence/EfExternalWorkStore.cs`, `Persistence/EfEvaSubmissionWorkStore.cs`, and (since [[PLAT-048]]) `Persistence/EfEvaSubmissionQueries.GetActivityAsync`. Give them one internal owner (an `internal static class ExternalWorkStates` or `EfExternalWorkStore.ToCode/ParseState`, matching `EfIntakeWorkStore`) and make the three callers read it. Behaviour-preserving; no schema change.

## Owns

`src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs`, `EfEvaSubmissionWorkStore.cs`, `EfEvaSubmissionQueries.cs`.

Raised in the [[PLAT-048]] review (2026-08-28).
