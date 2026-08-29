---
id: AUTO-014
type: ticket
title: >-
  Production callers for the AI job by-subject query and staff QueryResponse
  jobs
status: preparing
area: automation-integrations
assignee: ''
profile: feature
stageEntered:
  preparing: '2026-08-29T17:20:55.532Z'
labels:
  - backend
  - ai
  - rule-14
  - wiring
groups:
  - EPIC-011
links:
  - AUTO-011
blocks:
  - AUTO-011
refs:
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
  - docs/frd/frd-10-mcp-automation-and-actor-boundary.md
archived: false
created: '2026-08-29T13:04:32.304Z'
updated: '2026-08-29T17:20:55.532Z'
---

## What

Supply the two missing production callers that [[AUTO-011]] named but no board
ticket delivers, so AUTO-011 can satisfy the strict rule 14 settled in D20
(`.kanmer/groups/EPIC-011/decisions-2026-08-29-done-rule.md`):

1. **`IAiJobQueries.ListForSubjectAsync`** — a real reader of the by-subject AI
   job list. The natural home is the per-record surface that shows the jobs
   raised against one Case, Triage record or Unidentified item.
2. **Staff creation of `AiJobKind.QueryResponse`** — a Web caller that raises a
   query-response job through `ICreateAiJob` with the staff `PerformCasework`
   permission, per FRD-11's AI Job List.

Either wire each one to a real, reachable surface, or take the operator
decision to drop it from AUTO-011's delivered contract and remove the code.

## Why

AUTO-011's own `## What` names both: "`IAiJobQueries` (open, by subject,
recent, counts)" and the kind list including `QueryResponse` under "commands
create (staff `PerformCasework` …)". Neither has a production caller on merged
`dev` at `b92cb9a7`, and the GPT-5.6 adjudication of 2026-08-29 found no board
ticket that supplies one — so AUTO-011 was reversed out of Done and cannot
return without this.

Evidence recorded by the audit:

- `ListForSubjectAsync` census is `src/Pegasus.Core/AiWork/AiJobs.cs:196`,
  `src/Pegasus.Infrastructure/Persistence/EfAiJobStore.cs:239`, and two test
  fakes (`ServiceHealthTests.cs:406`, `OperationsWebTests.cs:477`). Test-only
  code is explicitly not Done under rule 14.
- [[PLAT-049]] does **not** supply it: its `plan/plan.md:39` loads
  `ListOpenAsync()` unioned with `ListRecentAsync(200)`, never
  `ListForSubjectAsync`. AUTO-011's `proof/proof.md:249` assigning it to
  PLAT-049 is false.
- `AiJobKind.QueryResponse` appears only in Core mapping, validation and
  construction (`AiJobOperations.cs:32,43,275,326,338`), the migration check
  constraint, and one MCP parameter description string. No Web caller.
- [[TICK-101]] (AI-08) is backlog, plan-and-research only, and blocked pending
  its activation decision — it is not a supplier. [[MAIL-026]] only prefills
  the composer from an existing draft and names no job-creation caller.

## Approach

- Search before building: reuse `ICreateAiJob` / `IAiJobQueries` exactly as
  [[AUTO-011]] shipped them; add no second query or command.
- For the by-subject list, prefer an existing record surface over a new page —
  the Case, Triage or Unidentified detail page that already loads the subject.
  Name the chosen surface in the plan.
- For `QueryResponse`, establish first whether a staff-initiated query-response
  job is in scope for the alpha at all. If TICK-101's activation gate means it
  is not, the correct disposition is removal of the kind and its Core
  construction path, not a disabled control (D21).
- No new feature gate and no permanently inert control: D21 makes both
  incapable of satisfying rule 14.

## Verification

- [ ] `git grep ListForSubjectAsync -- src/` shows a non-test production
      consumer that is itself reachable from a route or an open-gated tool.
- [ ] A staff action reachable in the deployed estate creates an
      `AiJobKind.QueryResponse` job, or the kind and its construction path are
      deleted with the operator decision recorded.
- [ ] No control shipped disabled and no closed composition gate is used to
      satisfy either item.
