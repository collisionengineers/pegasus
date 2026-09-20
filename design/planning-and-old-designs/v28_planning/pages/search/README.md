# Search

- **Live source:** `src/Pegasus.Web/Pages/Search/Index.cshtml`, `src/Pegasus.Web/Pages/Search/_CasePreview.cshtml`
- [**How it works**](how-it-works.md)

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Search, no filters | `/Search?selected=47f25921-e2ae-4c2c-8be3-405a12746f5f` | [frame](../../current/pegasus_search_operations_v28.html#search) · [page](../../current/states/search.html) | [1580](../../current/v28-shots/s42-search-1580.png) · [1440](../../current/v28-shots/s42-search-1440.png) · [760](../../current/v28-shots/s42-search-760.png) |
| Search by registration | `/Search?Registration=AB12CDE&selected=47f25921-e2ae-4c2c-8be3-405a12746f5f` | [frame](../../current/pegasus_search_operations_v28.html#search-results) · [page](../../current/states/search-results.html) | [1580](../../current/v28-shots/s43-search-results-1580.png) · [1440](../../current/v28-shots/s43-search-results-1440.png) · [760](../../current/v28-shots/s43-search-results-760.png) |
| Search, Case selected | `/Search?page=1&selected=47f25921-e2ae-4c2c-8be3-405a12746f5f` | [frame](../../current/pegasus_search_operations_v28.html#search-selected) · [page](../../current/states/search-selected.html) | [1580](../../current/v28-shots/s44-search-selected-1580.png) · [1440](../../current/v28-shots/s44-search-selected-1440.png) · [760](../../current/v28-shots/s44-search-selected-760.png) |
| Search, nothing matches | `/Search?Registration=ZZ99ZZZ` | [frame](../../current/pegasus_search_operations_v28.html#search-no-match) · [page](../../current/states/search-no-match.html) | [1580](../../current/v28-shots/s45-search-no-match-1580.png) · [1440](../../current/v28-shots/s45-search-no-match-1440.png) · [760](../../current/v28-shots/s45-search-no-match-760.png) |

## Not captured

- More filters applied, a second page, and the invalid-filter validation summary.
