Read from the live source on 18 September 2026.

## What this page does not show

- No lede or subtitle sentence — the grid and the two panes name themselves
  (the source comment on line 17 of `Index.cshtml` says this explicitly).
- No "results found" phrasing beyond the plain result count; no highlighting
  of matched terms.
- The Create Case action on this page is receipt-bound: it points at
  `/Upload`, not a blank case-creation form, because `Cases/Create.cshtml.cs`
  returns `NotFound` without a receipt. A source comment on the page (lines
  24-29) records this as outstanding shell work — the Add dialog and Ctrl N
  still point at the same receipt-less 404. The mockup captures the `/Upload`
  link as the *actual* current destination, not the eventual receipt-bound
  form.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Search/Index.cshtml` | Filter form, case-results table, vehicle-images table, empty/failure states |
| `Pages/Search/Index.cshtml.cs` | Query composition, `RefreshFields()`, paging, selection |
| `Pages/Search/_CasePreview.cshtml` | The "Selected Case" detail panel, reused per-row as a client-side swap template |
| `Pages/Shared/_FreshnessBanner.cshtml` | The freshness dot/text + manual Refresh form beside the page actions |
| `Pages/Shared/_StatusChip.cshtml` | The State chip's colour/tone |

## Behaviours

### Two independent result sets, one failure

Both the vehicle-images result set and the case-results set are read inside
one guarded load (`Model.QueryFailed`). Either can fail; when it does, the
page shows one shared "Cases are unavailable" notice rather than answering
either section with an empty or zero-count result — a query failure must
never look like a genuine zero (source comment, lines 111-113).

### Filters

Primary filters (query, registration, claimant, claim reference, principal,
state) render inline; Engineer, Received from/to and Origin sit behind a
native `<details>` "More filters" disclosure (`compact-search__more`). The
State dropdown enumerates every `CaseLifecycleState` through
`OperatorLabels.CaseStage`, so its option text is exactly the same words as
the State column's chip.

### Vehicle images branch

When the record-kind filter resolves to `images` (`Model.RecordKindFilter ==
"images"`), the page shows only the vehicle-images table (or its own empty
line "No vehicle images match these filters.") and skips the two-pane case
layout entirely — it is a genuinely separate result mode, not a mixed list.

### Row selection and Open Case

A case-results row carries `data-select-href`/`data-select-id` and
`aria-selected`; the currently selected row's `_CasePreview` is what the
right-hand pane shows. The mockup keeps this static per the note above.

## Things the FRD does not settle

- Nothing found while reading this page in isolation; the receipt-bound
  Create Case gap noted above is already an acknowledged, in-source open
  item rather than an undocumented one.
