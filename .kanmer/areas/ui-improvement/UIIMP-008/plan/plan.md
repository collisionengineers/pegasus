# Plan — UIIMP-008 Work Centre port

Branch `task/uiimp-008-work-centre` (recovered; one pushed Core commit
`78005070`), worktree `../pegasus-worktrees/uiimp-008-work-centre`.
PR targets `dev`. Build only (no tests/snapshots — orchestrator runs the
wave loop). Small slices, each `feat(...): ... (UIIMP-008)`.

## Steps

1. **Core projection repair** (`OperationsSnapshot.cs`, `DashboardCounts.cs`
   — the pushed diff). Reuse: every source query and helper the pushed code
   already uses (`IIntakeReceiptQueries`, `IListTriage`,
   `ICaseDueWorkQueries`, `IDashboardQueries`, `ISearchCases`,
   `IUnidentifiedStore`, `GetRequestOperations`, `IStaffAccountQueries`,
   `ActorDisplayNames.ResolveStaffNamesAsync`). Changes, per the audit:
   Case rows: Title = `MissingMaterialReason`, Detail =
   `MostRecentOutcome`, Reason = `State` (chase-state name), Source = null;
   doc comments updated to match. Triage read repaired (see the
   simplification pass). Nothing else of the pushed projection changes.
2. **Labels** (`OperatorLabels.cs`). Reuse the `CaseStage(string?)`
   string-overload precedent: `ChaseState(string?)`,
   `UnidentifiedReason(string?)`, `UnidentifiedMediaKind(string?)` overloads;
   add the two one-list maps this page needs — `NeedsAttentionKind(kind)`
   and `NeedsAttentionPriority(priority)` + its tone (red/red/amber/
   neutral, matching `_StatusChip` tones). No second spelling of any
   existing label.
3. **Page model** (`Index.cshtml.cs`). Keep `IGetOperationsSnapshot` as the
   one query. Replace the old tile/due-list properties with:
   `NeedsAttention` items, the five metric figures (Not ready/Review/Held
   from `CaseStages`; Unidentified from `MailActivity.Unidentified`; Blocked
   from `Counts.BlockedIntake`), `LoadedAtUtc`; `OnGetAsync(string? selected)`
   resolves the selected item server-side (first item when absent/unknown).
   A private view mapping computes per item: kind label, priority label +
   tone, chip/reason/source labels, route page + id, action label
   (Open Case / Open Case / Review source / Open Triage / Open Operations),
   due text. Reuse `_FreshnessBanner` (freshness + Refresh in one partial).
4. **Markup** (`Index.cshtml`). Structure/classes exactly per the file map
   (final render layer only): header, five-metric strip, two panes, no new
   CSS/JS, no inline styles, no explanatory copy, no Filter button.
5. **Tests**.
   - `DashboardBoundaryTests`: repair the 4 new constructor dependencies with
     file-local stubs (the file's existing convention); add composition
     coverage: the five kinds appear; ExternalWork limited to `CanRetry`;
     ordering priority → due → reference; bound 50; open Triage behind
     fifty settled records (regression).
   - `DashboardCountersWebTests`: rewrite against the new strip (five metric
     labels, `/Cases?tab=` hrefs, queried values). Not run locally.
6. **Build** `dotnet build ./Pegasus.slnx --configuration Release` for
   compiler feedback only — green.
7. **Simplification pass** (below, dated) then PR.

## Acceptance

- Every metric and work-item row is a real link; every figure a queried
  count; Blocked = `IntakeQueueCounts.BlockedIntake` (D14 route).
- No new CSS file, no inline styles, no new JS; classes only from the
  component map vocabulary.
- Labels live in `OperatorLabels` (one list per concept).
- No prototype prose; empty needs-attention list renders no empty-state
  panel.
- Build green; review by a second agent before merge (separate step).

## Verification commands

- `dotnet restore ./Pegasus.slnx --locked-mode` — green
- `dotnet build ./Pegasus.slnx --configuration Release --no-restore` — green
- (tests/snapshots deferred to the orchestrator's wave loop by EPIC rule)

## Simplification pass — 2026-08-28

Lenses: reuse / simplification / efficiency / altitude, over
`git diff origin/dev...HEAD` (7 files, +923/−172 at pass time).

Findings and dispositions:

1. **FIXED — Triage burial (correctness, found by the efficiency lens).**
   The pushed projection read one unfiltered Triage page (newest-first
   across every state); fifty settled records would silently bury an open
   one. Repaired by querying the two no-finding states directly through the
   same bounded query; regression test added; the client-side state filter
   in the composition removed as redundant. `TriageCount` (no consumers)
   now carries the without-finding total.
2. **FIXED — Case row duplication (simplification).** The pushed rows titled
   each Case with its reference (already in the row-meta) and repeated the
   missing-material reason as both detail and notice value. Title is now the
   recorded reason, Detail null, Reason the chase state; `Source` no longer
   misreports the chase channel.
3. **KEPT — explicit priority label arms.** `Overdue/High/Today/Normal`
   equal their enum names, so `Humanise` would produce the same words; the
   explicit arms stay because the one-list rule wants the settled vocabulary
   in `OperatorLabels`, not spelled at the call site.
4. **KEPT — `Guid.Empty` sentinels in staff-name resolution** (pushed code).
   `ActorDisplayNames.ResolveStaffNamesAsync` already filters empty ids;
   the sentinel is harmless and removing it is churn on working code.
5. **OUT OF LANE — `ListQueueAsync` is unbounded.** The composition caps at
   50 after composing, but the store query itself returns every open
   Unidentified row. The interface belongs to the Cases/Unidentified lanes
   (C1/C2, wave 2); reported in the PR, not changed here.
6. **n/a — no merge of origin/dev.** Dev drifted 38 files (5 migrations,
   PLAT-048) since the merge base with zero overlap with this lane's seven
   files; the PR merges cleanly without pulling the migrations in, so no
   merge was performed.

## Addendum — origin/dev merged 2026-08-28

Simplification-pass finding 6 above ("n/a — no merge of `origin/dev`";
"zero overlap with this lane's seven files") is **superseded and was
wrong by the time it mattered**. Dev moved on: CASE-025 (`c56b5d5b`,
`5c685460`, `95f69958`) edited two of this lane's files —
`src/Pegasus.Core/Operations/DashboardCounts.cs` and
`tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` — and the PR
went CONFLICTING at 64 behind.

`origin/dev` is now merged (merge commit `11f1f7de`; a merge, not a
rebase, so `c07b4488`, `f524b343` and `5a9ff906` stay reachable). The one
conflict was hand-resolved and the three auto-merged files were verified
by inspection; the full account, the evidence table and the reported
file-ownership breach are in `post-implementation-report/`.

Finding 5 (`ListQueueAsync` unbounded) is unchanged and still out of
lane.
