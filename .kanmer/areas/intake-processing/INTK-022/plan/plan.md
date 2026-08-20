# Plan — INTK-022

Branch `task/intk-022-queues-one-table` from origin/dev, worktree `../pegasus-worktrees/intk-022`.

1. **Core order** (reuse: existing `SearchCasesQuery` shape and validation style): `CaseSearchOrder` enum — `ReceivedDesc` (default), `ReceivedAsc`, `ReferenceAsc/Desc`, `RegistrationAsc/Desc`, `ClaimantAsc/Desc`, `PrincipalAsc/Desc` — on `SearchCasesQuery`; `EfCaseQueryStore.SearchAsync` switches its OrderBy accordingly (stable tiebreak Reference, CaseId as today). `CaseSearchItem` gains trailing `DateTimeOffset? NextChaseAtUtc = null`, populated from the workflow's DueWork left join.
2. **Merged Not-ready table** (reuse: existing `_searchCases` + `_imageIntakeQueries` loads): page model maps both sources to one `QueueRow` view record (Reference, Url, Registration, Claimant, Principal, StatusLabel, ReceivedAtUtc, NextChaseAtUtc, IsImageInitiated); merge, sort by the requested order in memory (both sources fully loaded for the queue: cases fetched at page size 100), render one table with "—" blanks. Review/Held pass the order to the query and keep the pager.
3. **Dropdown filters** (reuse: GET form + existing query params): a filter row form with Origin select (All / Awaiting images = instruction-initiated / Awaiting instructions = image-initiated; values `instruction`/`image` unchanged) and Principal select (distinct principals present in the loaded rows); `data-auto-submit` change handler in `site.js`, no-script Apply button. Principal filters the merged rows (and is passed to the case search filter).
4. **Sort headers**: each column header on the case tables is a link toggling `?sort=` asc/desc, preserving queue/origin/principal params; current sort marked `aria-sort`.
5. **Tests**: update `TriageQueuesWebTests` origin-filter test to the dropdown params (unchanged values — assertions on rendered references stand); add merged-table test (both origins in one table, newest-first order, "—" blanks) and a sort-toggle test; suites: TriageQueuesWebTests + RailCountsWebTests + Release build 0/0.

Dispositions noted: Triage tab already lists newest-first; Unidentified stays oldest-first deliberately (exception queue — oldest is the work); top-level queue tabs remain tabs (they are pages, not filters — the operator's dropdown ask targeted the filter pills).

Deviation: subagents barred — self-review recorded.

## Simplification pass — 2026-08-20 (self, subagents barred)

Lenses over `origin/dev...HEAD` (7 files, +385/−77):

- **Reuse** — sort rides the existing `SearchCasesQuery` (new enum, default preserves today's order); the merged table reuses the two existing loads and the TICK-065 `ImageIntakeChaseSchedule` chip; dropdowns are a plain GET form on the existing query params. No new queries, stores, or migrations. ✔.
- **Simplification** — applied mid-implementation: my first table markup dropped TICK-065's image chase chip (caught by the diff against dev, restored into the merged Chase cell); nested double-quoted Razor helper attributes converted to single-quoted (they silently mis-render). Principal dropdown options come from the loaded queue rows rather than a new principal query — an empty-queue principal is not selectable, which is the correct trade for zero new query surface.
- **Efficiency** — Not ready fetches one 100-row page + the bounded image list (both already fetched today at 25+all); in-memory merge/sort of a bounded queue. ✔.
- **Altitude** — ordering lives in the store switch; labels in the view; the page model maps, never owns policy. ✔.

No BOM drift vs dev. All findings applied; none deferred.
