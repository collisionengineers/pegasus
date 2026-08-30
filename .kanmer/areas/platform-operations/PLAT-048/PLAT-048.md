---
id: PLAT-048
type: ticket
title: Service health snapshot and Engineer activity report queries
status: verifying
area: platform-operations
assignee: claude-code
profile: feature
stageEntered:
  preparing: '2026-08-28T10:30:32.496Z'
  review: '2026-08-28T10:49:18.208Z'
  verifying: '2026-08-28T12:42:03.989Z'
taken_at: '2026-08-28T10:38:15.898Z'
branch: task/plat-048-service-health-report
worktree: ../pegasus-worktrees/plat-048-service-health-report
labels:
  - backend
  - wave-3
  - health
  - reports
groups:
  - EPIC-011
links:
  - PLAT-053
  - PLAT-054
blocks: []
refs:
  - docs/frd/frd-12-operator-experience.md
  - docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md
commits:
  - fc0537a1
  - 40f6d043
  - 2818fc26
  - 11ad83b2
prs:
  - '591'
deployment: production
archived: false
created: '2026-08-28T08:35:24.047Z'
updated: '2026-08-30T20:14:23.820Z'
---

## What

Wave 3 of [[EPIC-011]]. (K) `Core/Operations/ServiceHealth.cs` `GetServiceHealth` composing existing sources: approved mailbox poll status, sent-evidence poll, intake dispatch, failed external work (with retry target), EVA submissions (new `IEvaSubmissionQueries.GetRecentFailuresAsync` + pending work counts), AI jobs counts + kill-switch status; rows for uncomposed services absent. (H) `Core/Reports/EngineerActivityReport.cs` `IEngineerActivityQueries.GetAsync(from, to, engineerId?)` → reports sent (case-linked Sent evidence by assigned Engineer) and queries received (retained messages classified post-report-emails associated with the Engineer's cases — D12); right `ViewOperationalReports`; CSV export shape.

## Owns

`src/Pegasus.Core/Operations/ServiceHealth.cs`, `src/Pegasus.Core/Reports/EngineerActivityReport.cs`, `src/Pegasus.Core/Eva/EvaApiContracts.cs` (query addition), `Core/Identity/StaffAuthorization.cs` (right), Infrastructure adapters, Core tests.

## Verification

- [ ] Every health row names its evidence time; no probe is invented.
