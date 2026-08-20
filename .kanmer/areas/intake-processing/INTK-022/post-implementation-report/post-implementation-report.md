# Post-implementation report — INTK-022

Branch task/intk-022-queues-one-table (15f167c6). Delivered every limb:

- **One table**: Not ready merges instruction and image-initiated rows into one table (Reference · Registration · Claimant · Principal · Status · Received · Chase), dash cells where a field doesn't apply, each reference linking to its own details page; the image rows keep TICK-065's derived chase chip in the shared Chase column.
- **Dropdowns**: origin pills → "Waiting for" select (All / Awaiting images / Awaiting instructions — unchanged query values `instruction`/`image`) + Principal select (principals present in the queue); `data-auto-submit` change handler in site.js, no-script Apply button; subtabs gone from the tab.
- **Sort**: `CaseSearchOrder` on `SearchCasesQuery` (default ReceivedDesc — today's order), store OrderBy switch, sortable header links toggling `?sort=` with `aria-sort` and a direction glyph; Review/Held pass the order to the query, Not ready applies it to the merged rows. `CaseSearchItem` gains optional `NextChaseAtUtc` from the DueWork left join.
- Triage and Unidentified orderings untouched (already deliberate); badge counts untouched.

Tests: TriageQueuesWebTests 7/7 (2 new: merged-table + sort-toggle; existing origin-filter and TICK-065 chase tests green on the new surface); RailCounts/DashboardCounters/CasesIndex 4/4; Release build 0/0. Simplification pass in the plan.

Deviation: subagents barred — self-reviewed.

## Verification hand-off
Post-deploy: /Triage Not ready shows one table, both origins, newest first; dropdowns filter; header links flip direction.
