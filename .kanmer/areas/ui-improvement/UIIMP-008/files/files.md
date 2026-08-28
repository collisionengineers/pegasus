# File map — UIIMP-008

Lane A (wave 2). Whole files this ticket owns or extends:

| File | Action |
| --- | --- |
| `src/Pegasus.Core/Operations/DashboardCounts.cs` | Repair the pushed `NeedsAttentionKind`/`NeedsAttentionPriority`/`NeedsAttentionItem` additions (audit findings only; no rewrite) |
| `src/Pegasus.Core/Operations/OperationsSnapshot.cs` | Repair the pushed needs-attention composition (field semantics below) |
| `src/Pegasus.Web/Pages/Index.cshtml` | Full port to the Work Centre contract |
| `src/Pegasus.Web/Pages/Index.cshtml.cs` | Full port: selected-item binding, view mapping, labels |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | Extend (shared, PLAT-029-delivered, merged): kind/priority label lists + string-overload label helpers following the `CaseStage(string?)` precedent |
| `tests/Pegasus.IntegrationTests/DashboardCountersWebTests.cs` | Rewrite assertions for the new metric strip markup |
| `tests/Pegasus.Core.Tests/Operations/DashboardBoundaryTests.cs` | Repair construction (4 new dependencies); add needs-attention composition coverage in the same file |

Neighbour files deliberately untouched: `site.css`, `site.js` (PLAT-029),
`Pages/Cases/**`, `Pages/Triage/**`, `Pages/Unidentified/**`,
`Pages/Operations/**`, `RailCountsPageFilter.cs`.

## Projection field semantics (audit outcome)

Kept from the pushed commit: the five kinds, the priority enum and its
declaration order, the bound (`MaximumNeedsAttention` = 50) applied to every
source query and to the composed list, the sort (priority, then due, then
reference), `OfficeBoundaries` untouched, ExternalWork filtered to
`CanRetry`, held/owner name resolution through `ActorDisplayNames`.

Repairs:

1. **Case rows duplicated the reference as the title** (row-meta already
   carries `kind · ref`). Title becomes the recorded missing-material reason;
   Detail becomes the recorded most-recent outcome; Reason (the notice value)
   becomes the recorded chase state, so the page can label it with the
   existing `ChaseState` vocabulary instead of repeating the title.
2. **Case rows set `Source = MostRecentChannel`** — a chase channel is not
   where work came from. Case rows carry no origin fact; `Source` is null.
3. Held/Mail/Triage/ExternalWork mappings are correct as pushed (title =
   claimant / handle / registration / external kind; reason = Held /
   reason code / state / failure reason).

## Page structure (final render only)

- `page-header`: eyebrow "Office-wide work", h1 "Work Centre", page-actions =
  `_FreshnessBanner` (freshness + Refresh) + Create Case primary link
  (`/Cases/Create`).
- `section.metric-section.work-centre-metrics` + `section-label`
  "Work requiring attention" + `metric-strip metric-strip--5`: five `metric`
  links (Not ready/Review/Held/Unidentified/Blocked) with prototype
  `data-value` keys and icons, each to `/Cases?tab=…` (Blocked →
  `tab=unidentified`, D14). No meta lines (hidden by CSS and explanatory).
- `section.pane-layout.integrated-home.integrated-home--expanded`:
  - left `pane`: `pane-head` h2 "Needs attention" (no Filter button, no
    ordering sentence); `pane-body pane-scroll` + `data-row-list` of
    `a.work-item` rows → `/?selected=<Id>`; row = `work-item-head`
    (`row-meta` "Kind · ref", h3 title, priority `_StatusChip`),
    `p` detail, `work-item-foot` (owner, due). Empty list renders no rows and
    no empty-state prose.
  - right `pane`: `pane-head` (`h2.today-pane-title` "Today" + muted
    "Selected work" + "Open full record" small link); `detail-canvas` with
    `article.work-detail`: cluster (eyebrow kind · ref, h2 title,
    `work-detail-lead`, priority chip), notice "Why this needs attention"
    (label + Core-derived value), `fact-grid` (Source, Owner, Last recorded
    outcome, Due), `panel` "Next permitted action" (dark action link per kind
    + Copy reference via `data-copy-target`, rendered `hidden`).
