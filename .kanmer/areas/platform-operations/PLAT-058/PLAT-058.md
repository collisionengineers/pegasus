---
id: PLAT-058
type: ticket
title: >-
  Retire or wire MailActivityCounts.ReceivedToday — queried every Work Centre
  load, rendered nowhere
status: backlog
area: platform-operations
assignee: ''
profile: fix
labels:
  - wave-5
  - cleanup
groups:
  - EPIC-011
links:
  - UIIMP-008
archived: false
created: '2026-08-29T08:06:47.668Z'
updated: '2026-08-29T08:06:47.668Z'
---

## What

[[UIIMP-008]] replaced the Dashboard with the Work Centre and removed the
"E-mail activity" section, which was the only surface that rendered
`MailActivityCounts.ReceivedToday`. The value is still queried on every
Work Centre load and rendered nowhere.

- `src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`
  `GetMailActivityCountsAsync` still runs a `CountAsync` over
  `IntakeReceipts` filtered to the mailbox channel (the PLAT-012 rule).
- `src/Pegasus.Core/Operations/DashboardCounts.cs` still declares
  `MailActivityCounts(int ReceivedToday, int NeedsSorting)`; only its
  `Unidentified` member (derived from `NeedsSorting`) reaches the page.
- `src/Pegasus.Web/Pages/Index.cshtml.cs` exposes `MailActivity` but the
  view reads `MailActivity.Unidentified` only.

Decide: give `ReceivedToday` a real surface (none is named in EPIC-011
`context.md` §1.2/§1.3), or delete the property, the query and the
per-load database round trip. AGENTS.md rule 6 — delete what you replace.

UIIMP-008 could not do either: `EfDashboardQueries.cs` and
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`
are outside its wave-2 lane-A allocation. It restored the PLAT-012
regression guard in
`tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs`
(`ReceivedTodayCountsMailboxChannelOnlyNotManualUploads`, re-pointed
from the removed tile onto `IDashboardQueries`) so the rule cannot
regress silently while this is open. If the property is deleted, that
guard goes with it.

## Owns

`src/Pegasus.Core/Operations/DashboardCounts.cs`,
`src/Pegasus.Infrastructure/Persistence/EfDashboardQueries.cs`,
`src/Pegasus.Web/Pages/Index.cshtml.cs`,
`tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs`,
`tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs`.

## Verification

- [ ] No queried-but-unrendered member remains on `MailActivityCounts`.
- [ ] The PLAT-012 channel rule is either guarded or gone with its rule.
