Read from the live source on 18 September 2026.

## What this page does not show

- No Blocked or "Blocked intake" queue — that term is retired; refused
  material is a closed Unidentified item, reachable only through the
  Unidentified scope's Closed items filter, never its own rail entry.
- No cross-scope search; the Principal filter only narrows the open Case
  scope, and there is no free-text search box on this page at all (that is
  `/Search`, and a request carrying search-only parameters is redirected
  there permanently).
- No bulk actions on any row — every action here is either following a row
  to its own record or, on Awaiting instruction only, attaching one image
  item to an existing Case reference typed by hand.
- No indication on the rail itself of a queue's Principal or Missing filter
  being active; only the list pane's own "Clear" link and the filter-bar
  controls show a filter is applied.

## Source table

| File | What it owns |
| --- | --- |
| `Pages/Cases/Index.cshtml.cs` (`IndexModel`) | The nine-tab rail, its counts, every row's columns/cells/chip per scope kind, the Quick detail pane's facts, and the awaiting-instruction Attach handler (inherited from `UploadConfirmationPageModel`). |
| `Pages/Cases/Index.cshtml` | The rendered rail form, filter bars (Principal/Missing/Show), the table's cell-kind switch (`Link`/`Mono`/`Chip`/`Late`/`Text`), and the Quick detail's fact grid, Outstanding requirements list and Work rows. |
| `Presentation/OperatorLabels.cs` | `CaseStage` (the rail/state-chip words), `CaseRequirements` (Outstanding requirement rows), `UnidentifiedReason`/`UnidentifiedMediaKind`, `TriageState`, `SourceChannel`, `ImageCustodyState`, `ImageChaseState`. |
| `Pages/Shared/_StatusChip.cshtml` | The one shared tone table every chip on this page (state, Missing, Triage state) is coloured from, keyed on the chip's own lowercased text. |
| `Presentation/RailCountsPageFilter.cs` | The shell rail's own `Cases` nav badge — a different, wider sum than any one tab's own count (see below). |

## Behaviours found

### The rail is three groups, and "Cases" badge sums only some of them

`Tabs` (`Index.cshtml.cs:89-100`) groups Not ready/Review/With
Engineer/Completed/Query under **Workflow**, Triage/Awaiting instruction
under **Pre-Case work**, and Held/Unidentified under **Exceptions**
(`IsException: true`, giving them the `queue-exception` row style). The
shell's own `Cases` nav badge (`RailCountsPageFilter.cs:23-24,164-170`) is a
different figure: `not_ready + review + with_engineer + query + held +
triage + unidentified`, which deliberately excludes both **Completed** and
**Awaiting instruction** — a Case that is done, or an image item that has no
Case yet, never counts toward "Cases needs attention."

### "With Engineer" merges two Core states and can carry two pages

The `with_engineer` tab reads both `ReportPreparation` and `PostReport`
states as one merged, re-sorted list (`LoadCasesAsync`,
`Index.cshtml.cs:490-511`), so `HasNextPage` is true if *either* underlying
page has more — the visible "page" boundary does not correspond to either
Core state's own paging.

### Not ready is the one tab whose count and rows can disagree

Not ready is read whole (up to 100 rows, `MergedPageSize`) and then the
Missing filter is applied *after* the read (`LoadNotReadyAsync`,
`Index.cshtml.cs:516-542`) — so the rail's Not ready badge is the
unfiltered total, while the table beneath a Missing filter shows fewer rows
than that badge. No other tab's filter (Principal, on any Case scope) works
this way: `LoadCasesAsync` passes `Principal` into the query itself, so a
Principal-filtered Case-scope tab's row count already matches what is
fetched.

### The Query state's chip renders neutral, not a warm tone

`StateChipText` prints the Core state name verbatim through `CaseStage`
(`Query` → "Query"), and `_StatusChip.cshtml`'s tone table has no `"query"`
key — every other workflow word (`not ready`, `review`, `with engineer`,
`held`, `complete`) has an explicit tone, but Query falls through to the
default `neutral` tone alongside genuinely inert states like Cancelled or
Archived. A Case sitting in Query — which is instructed, outstanding
office-side, exactly as "in flight" as Review — reads visually identical to
one that has been closed.

### The Unidentified rail badge never reflects the Closed items filter

`Count(tab)` for `unidentified` is always
`_unidentifiedStore.CountOpenAsync()` (`Index.cshtml.cs:173,185`), so
switching the Show filter to Closed items changes the table and its own
"N items" meta line but never the rail badge next to Unidentified.

### The Editing column names who currently holds the edit lease

Every Case-scope tab's last-but-one or last cell resolves
`item.EditingStaffId` to a display name (`CaseRow`,
`Index.cshtml.cs:740-742`) — added, per the code comment, "so nobody opens a
Case only to find it taken." This is a live lock indicator baked into the
list itself, not just the record page.

## Things the FRD does not settle

- No comment or FRD reference explains why Query — an active, staff-owned
  state — was left out of `_StatusChip`'s explicit tone table while every
  other Workflow-group state has one. It reads as an oversight rather than a
  designed neutral treatment, but nothing in the source confirms either way.
