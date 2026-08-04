# Pages 4 and 5 — Cases + Search → single Cases page: alteration plan

Sources: `src/Pegasus.Web/Pages/Cases/Index.cshtml` and
`src/Pegasus.Web/Pages/Search/Index.cshtml`. Operator notes: `../page-4-and-page-5.md`.
Screenshots reviewed: `page4.png`, `page5.png`, `local-cases-with-result.png`,
`local-search-with-result.png`. Governing standards: `../../ui-standards-and-review.md`
(§2 vocabulary, §3.2 merge map, §4 presentation rules).

## Review

### Aesthetics

Cases opens with an eyebrow ("Case workspace"), H1, and a lede — *"Search the cases you are
authorised to access. Filters remain in the URL when you move between result pages."* —
browser mechanics narrated as guidance. The filter panel is a fourteen-field grid occupying
the whole first screen: the results, the actual content, start below the fold. Search has its
own page with a different eyebrow ("Case search"), a different lede (*"…The exact query
remains in the URL for paging and safe sharing."*), and inconsistent heading grammar between
the two siblings. The operator: *"Page 4 has the superior design but it needs to be more
compact and located at the top of the page."*

### Practicality

Both pages are searches over the same data — the operator's note (*"page 4 and page 5 blend
the functionality. They are both searches."*) is confirmed in code: both run the identical
Core query, so two nav destinations exist for one job. On Cases, "Principal" is free text
where the set of principals is small and known — it must be a dropdown. "State" is a dropdown
but exposes raw enum values as user-facing text: `NotReady`, `ReportPreparation`,
`PostReportComplete`, `CollisionEngineersRejected`. Fourteen always-visible fields (Case/PO,
Registration, Claimant, Claim number, Principal, State, Engineer ID, Received date,
Instruction date, Received from, Received to, Origin, Any case text, Record type) bury the
three filters people actually use.

### Performance, design and good practice

- **Stage chips fall to neutral grey**: `_StatusChip` is keyed on spaced lowercase phrases
  ("not ready", "post report completion") but the Cases table passes
  `item.State.ToString()` PascalCase compounds (`NotReady`, `ReportPreparation`,
  `PostReportComplete`), which normalise to keys the map misses, so real case stages render
  the neutral fallback. The Search results table doesn't use the chip partial at all — it
  prints the bare enum string.
- The Engineer column prints `item.EngineerId?.ToString("D")` — a GUID where a name belongs
  (standards §4.4).
- Duplicate maintenance: two pages, two ledes, two empty states, two pagers for one query.
- Search's serial empty/error states ("Enter a search query", "Search query not accepted…")
  add copy for states the merged page handles with one designed empty state.

## Changes

1. **Merge Search into Cases.** One nav item, **Cases**. `/Search` retires and redirects to
   `/Cases?query=…` (verified byte-identical backing query, standards §3.2). The Search page,
   its eyebrow, lede, and duplicate result table are deleted.
2. **Remove the eyebrow and lede** on Cases: single H1 **"Cases"**; kicker labels
   ("Case workspace" / "Case search") unified away (standards §4.7).
3. **Compact single-line filter bar anchored at the top** of the page:
   - **Keyword box** (placeholder "Case, PO, registration, claimant or claim number…") —
     absorbs the old Case/PO, Registration, Claimant, Claim number and "Any case text"
     fields for the common path.
   - **Case stage** dropdown (was "State") with human labels only:
     `NotReady` → "Not ready", `Review` → "Review", `Held` → "Held",
     `ReportPreparation` → "Report preparation", `PostReportComplete` → "Report complete",
     `CollisionEngineersRejected` → "Rejected". The label map lives beside the existing
     hand-labelled maps; raw enum `ToString()` never reaches markup.
   - **Principal** dropdown (was free text): "All principals" plus the configured principals.
   - **More filters** disclosure holding the long tail: received date range, instruction
     date, engineer, origin, record type (Cases / Vehicle images).
   - **Search** (primary) and **Clear** actions inline at the end of the bar.
4. **Results table restyle** with proper stage chips: Case/PO · Registration · Claimant ·
   Claim number · Principal · Stage (chip) · Engineer (name, "Unassigned" when none — never a
   GUID) · Received · Origin. Chip tones follow the settled semantics (amber Not ready/Held,
   navy Review/Report preparation, green Report complete, red Rejected).
5. **Fix `_StatusChip` key coverage** so PascalCase lifecycle values map to their labelled
   chips instead of neutral grey (plan-level: the redesign specifies label-map lookup before
   the chip partial, so the partial only ever receives operator labels).
6. **One designed empty state**: "No cases match these filters." with a Clear-filters action;
   the failure state keeps the designed error card. Search's extra empty/error variants
   retire with the page.
7. **Pagination**: one pager pattern ("Previous · Page 2 · Next"); no "bounded view" copy.
8. **Vehicle-images results** (the old "Record type: Images" path) keep working through the
   More-filters record-type control, listed with Image reference and registration — labelled
   "Vehicle images", never "Image intakes".

## Dependencies

- **Principal dropdown source**: a query listing configured principals for the filter
  (exists for Administration; needs exposing to the Cases page model).
- **Stage label map**: a single operator-label map for `CaseLifecycleState` shared by the
  filter dropdown, the results chips, and the case detail surfaces.
- **Engineer names**: resolve engineer IDs to display names in the result projection
  ("Unassigned" when null).
- **Keyword query**: the merged keyword box reuses the existing combined query (already
  proven identical between Cases and Search); the per-field inputs behind More filters keep
  their existing bindings.
- **Redirect**: permanent redirect `/Search` → `/Cases` preserving the `q`/`query` parameter.

## Open questions

- Exact operator label for `PostReportComplete`: mockups use "Report complete"
  (an alternative is "Completed"); needs the operator's settled term before the label map is
  frozen.
- Should "More filters" remember its open/closed state per user? Cosmetic; default closed in
  mockups.
