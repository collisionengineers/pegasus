---
id: PLAT-053
type: ticket
title: >-
  External-work state vocabulary has three copies in Infrastructure — one
  internal owner
status: done
area: platform-operations
order: 2530
assignee: claude-code
profile: fix
stageEntered:
  implementing: '2026-08-28T21:37:08.893Z'
  review: '2026-08-28T21:38:14.763Z'
  verifying: '2026-08-29T09:19:31.480Z'
  done: '2026-08-29T09:59:39.943Z'
labels:
  - backend
  - simplification
groups:
  - EPIC-011
links:
  - PLAT-048
  - PLAT-056
  - PLAT-057
refs:
  - docs/frd/frd-12-operator-experience.md
commits:
  - 8a358ad4
  - 99483f55
prs:
  - 'https://github.com/collisionengineers/pegasus/pull/613'
archived: false
created: '2026-08-28T10:58:39.368Z'
updated: '2026-09-01T14:44:34.110Z'
---

## What

The persisted `ExternalWorkItems.State` words (`pending`, `dispatching`, `queued`, `processing`, `completed`, `failed`) are spelled as string literals in three Infrastructure classes: `Persistence/EfExternalWorkStore.cs`, `Persistence/EfEvaSubmissionWorkStore.cs`, and (since [[PLAT-048]]) `Persistence/EfEvaSubmissionQueries.GetActivityAsync`. Give them one internal owner (an `internal static class ExternalWorkStates` or `EfExternalWorkStore.ToCode/ParseState`, matching `EfIntakeWorkStore`) and make the three callers read it. Behaviour-preserving; no schema change.

## Owns

`src/Pegasus.Infrastructure/Persistence/EfExternalWorkStore.cs`, `EfEvaSubmissionWorkStore.cs`, `EfEvaSubmissionQueries.cs`.

Raised in the [[PLAT-048]] review (2026-08-28).
