## Review fixes — PR #598 review (2026-08-28, REQUEST CHANGES → fixed)

Findings and dispositions (blocking 1–2 and minor 3–4 fixed on the branch;
5–7 accepted/deferred by the reviewer, untouched):

1. FIXED (blocking) — `NeedsAttentionSkipsTriageWithAFinding…` was red as
   shipped: it fed an Open Triage record while asserting no Triage row,
   contradicting the repaired state queries. Now feeds only the
   FindingRecorded record (the row that must never surface).
2. FIXED (blocking) — `…StillListsOpenTriageBehindFiftySettledRecords`
   guarded nothing (stub ignored Page/PageSize). `StubListTriage` now pages
   the way `ListTriage` does (Skip/Take window, TotalCount = full match
   count, insertion order), so a revert to one unfiltered page truncates
   the Open record and the test genuinely fails.
   Evidence: `dotnet test tests/Pegasus.Core.Tests --filter
   "FullyQualifiedName~DashboardBoundaryTests"` → Failed 0 / Passed 8 /
   Skipped 0 / Total 8 (the one permitted focused run).
3. FIXED (minor) — Refresh/F5 dropped the selection; `Index.cshtml` now
   sets `ViewData["RefreshFields"]` with `["selected"]` per the
   Cases/Index convention.
4. FIXED (minor) — false coverage claim corrected in `research/research`
   (coverage deleted with the tile; channel split unpinned; wave 3
   CASE-028 owns `DashboardCounts.cs`) and in the PR body.

Commits: c07b4488 (test guards), f524b343 (refresh keeps selection);
pushed; Release build green.

## CI fixes — PR #598 run 33183320100 (2026-08-28)

Five cross-page failures (old landing pins), retargeted to the Work Centre
surface in commit 5a9ff906; build green; the Connection-Timeout shard-1
failures were runner flakes per the reviewer and were not touched.

| Test | Old pin → New pin | Strength |
| --- | --- | --- |
| TriageQueues NotReadyBadgeCount… | `data-state="not-ready"…metric__value` tile → `data-value="not_ready"…metric-value` metric | kept: badge == figure across both origins |
| HealthEndpoint LandingPage… | "Active cases"/"E-mail activity" labels → Work Centre heading + `/Cases/Create` href + not_ready/unidentified metric hrefs + "Unidentified" | strengthened: pins the workspace's entry points, not section labels |
| OperatorJourney Unimplemented… (:222) | `.metric .metric__value` (matched nothing) → `.metric .metric-value` | kept: every shipped metric renders a digit |
| OperatorJourney OperationsFirstJourney | h1 "Dashboard" → "Work Centre"; read order "active cases/e-mail activity/today and this week" → "work requiring attention/needs attention/selected work"; Review metric click-through unchanged | kept |
| Accessibility constrained viewport (:123) | h1 "Dashboard" → "Work Centre"; metric locator tightened from dual `.metric-value, .metric__value` to `.metric-value` only (its own comment deferred the tightening to this port) | strengthened |

No gaps: every retired surface has a Work Centre equivalent still pinned.
UploadDropzoneBrowserTests' `.page-heading` use targets /Upload (lane G),
not this page.
