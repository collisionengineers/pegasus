# Post-implementation report — UIIMP-008

Branch `task/uiimp-008-work-centre`, worktree
`../pegasus-worktrees/uiimp-008-work-centre`, PR #610 → `dev`.
12 commits ahead of `origin/dev`, 0 behind (was 10 ahead / 64 behind and
CONFLICTING before this round).

## What shipped

The Work Centre port is unchanged from the reviewed round (`78005070`
through `5a9ff906`, approved on #598; findings and dispositions are in
`plan/plan.md` and `scratch/notes.md`):

- `src/Pegasus.Core/Operations/OperationsSnapshot.cs` and
  `DashboardCounts.cs` — the needs-attention projection composed from the
  existing queries, plus `NeedsAttentionKind`, `NeedsAttentionPriority`
  and `NeedsAttentionItem`. No new table, no new store.
- `src/Pegasus.Web/Pages/Index.cshtml(.cs)` — header, the five-metric
  strip (`data-value="…"` + `.metric-value`, Blocked routed to
  `?tab=unidentified` per D14), the two-pane
  `integrated-home--expanded` layout and server-resolved selection.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — the three
  string-overload label helpers and the two one-list maps
  (`NeedsAttentionKind`, `NeedsAttentionPriority` + tone), appended in
  the lane's own region.
- Tests: `Operations/DashboardBoundaryTests.cs`,
  `DashboardCountersWebTests.cs`, `HealthEndpointTests.cs`,
  `TriageQueuesWebTests.cs`, `Browser/AccessibilityTests.cs`,
  `Browser/OperatorJourneyTests.cs`.

## This round — merging origin/dev

The branch had never merged `dev` and had gone 64 behind. It was merged
(not rebased, so the SHAs recorded in `scratch/notes.md` — `c07b4488`,
`f524b343`, `5a9ff906` — stay reachable), producing merge commit
`11f1f7de`.

### The one conflict, and why it had to be resolved by hand

`tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`. Dev's CASE-025
commits (`c56b5d5b`, `5c685460`) rewrote the file: it renamed
`NotReadyBadgeCountMatchesRowsAcrossBothOrigins` to
`NotReadyRailCountMatchesRowsAcrossBothOrigins`, swapped the badge
assertion for a `.scope-button` rail-count regex, and replaced
`NotReadyOriginFilterReturnsOnlyTheMatchingOriginsRows` with
`NotReadyMissingFilterReturnsOnlyTheMatchingRows`. This lane had edited
the same lines under the old name.

Neither side could be taken wholesale:

- **Taking dev's side** merges cleanly and then fails at runtime — dev's
  surviving test still scrapes `/` with the old markup
  `data-state="not-ready"…metric__value">(\d+)</strong>`, which this
  lane replaces on `Pages/Index.cshtml`.
- **Taking this branch's side** silently reverts CASE-025's rail
  rewrite.

Resolution: dev's complete file is the base, with only this lane's three
edits re-applied inside dev's renamed test — the `/` scrape becomes
`data-value="not_ready"[\s\S]*?metric-value">(\d+)</span>`, the assert
message becomes "Work Centre Not ready metric markup not found.", and
the XML summary and inline comment say "the Work Centre's Not ready
metric". Dev's test name, `.scope-button` rail assertion,
`Assert.Equal(2, Regex.Count(notReadyHtml, "class=\"row-button\""))` and
`railCount` variable are all kept; this branch's `badgeCount` naming is
discarded entirely. Diffed against `origin/dev`, the resolved file
differs by exactly those three hunks and nothing else.

That test is the proof the resolution is right: it scrapes CASE-025's
rail markup and this lane's new metric markup in the same run, so it can
only pass if both survived the merge.

### Auto-merges verified by inspection

- `src/Pegasus.Core/Operations/DashboardCounts.cs` — carries dev's
  `CaseStageCounts(…, int Complete = 0)` and this lane's
  `NeedsAttentionKind` / `NeedsAttentionPriority` / `NeedsAttentionItem`.
  Diffed against `origin/dev`: additions only.
- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — carries dev's
  `TriageState`, `CaseRequirement`/`CaseRequirements`,
  `RequestOperationState` and the four `ServiceHealth*` helpers plus this
  lane's `UnidentifiedReason`, `UnidentifiedMediaKind`, `ChaseState`,
  `NeedsAttentionKind`, `NeedsAttentionPriority`,
  `NeedsAttentionPriorityTone`. Diffed against `origin/dev`: additions
  only, nothing reordered.
- `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` —
  dev's version with this lane's Work Centre retargets ("Dashboard" →
  "Work Centre", the read order, `.metric__value` → `.metric-value`).

### One post-merge defect, fixed

The merge kept `using Pegasus.Core.Operations;` from both sides, so
`OperatorLabels.cs` declared it twice — `CS0105`, a hard build failure.
Dev's sorted entry stays and the trailing duplicate was deleted
(`b8c0cf77`). No member was reordered; four lanes share that file this
wave.

## Evidence

| Command | Result |
| --- | --- |
| `dotnet build ./Pegasus.slnx --configuration Release` | exit 0 — 0 warnings, 0 errors |
| `dotnet test … --filter "FullyQualifiedName~TriageQueuesWebTests"` | exit 0 — Failed 0, Passed 8, Skipped 0, Total 8 (2 m 16 s) |
| `dotnet test … --filter "FullyQualifiedName~DashboardBoundaryTests"` | exit 0 — Failed 0, Passed 8, Skipped 0, Total 8 |
| `dotnet test … --filter "FullyQualifiedName~DashboardCountersWebTests"` | exit 0 — Failed 0, Passed 2, Skipped 0, Total 2 (37 s) |
| `dotnet test … --filter "FullyQualifiedName~HealthEndpointTests"` | exit 0 — Failed 0, Passed 3, Skipped 0, Total 3 |

The first build attempt after the merge failed on `CS0105`; the table
records the build after the fix. No assertion was weakened, skipped or
deleted to reach these results.

CI: the branch head had never scheduled a run — `gh pr checks 610`
reported "no checks reported", and two empty `ci: retrigger` commits
produced zero runs, because an empty commit does not schedule one here.
The content-bearing merge push scheduled run
[33212916874](https://github.com/collisionengineers/pegasus/actions/runs/33212916874),
the first real CI this head has had. The full suite, the Browser
category and the snapshot/catalogue scripts stay with the orchestrator's
wave loop.

## Deviations from the plan

`plan/plan.md`'s simplification-pass finding 6 recorded "n/a — no merge
of `origin/dev`" on the grounds that dev had drifted with zero overlap
with this lane's files. That is now superseded: dev did overlap, on two
of this lane's files, and the merge was performed. See the plan's dated
addendum.

## Reported, not fixed

- **File-ownership breach (for the orchestrator).**
  `waves.md` allocates `src/Pegasus.Core/Operations/DashboardCounts.cs`
  to wave-2 lane A (this ticket), but CASE-025 (lane C1) edited it
  anyway in `95f69958`, and also edited
  `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs`. That is what
  produced this conflict. Two lanes in one wave sharing a path is
  exactly what the wave plan's "a ticket owns whole files" rule exists to
  prevent.
- **`ListQueueAsync` is unbounded** (carried forward from the plan's
  finding 5). The composition caps at 50 after composing, but the store
  query returns every open Unidentified row. The interface belongs to
  the Cases/Unidentified lanes (C1/C2); not changed here.
