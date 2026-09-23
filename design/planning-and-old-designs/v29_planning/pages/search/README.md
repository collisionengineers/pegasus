# Search

- **Parent:** [v29 pages](../README.md)
- **Mockup route:** [pegasus_unidentified_search_v29.html](../../current/pegasus_unidentified_search_v29.html) (open it from `current/`; presets are in the state picker)
- **Live source:**
  - `src/Pegasus.Web/Pages/Search/Index.cshtml`, `Index.cshtml.cs`, `_CasePreview.cshtml`
  - `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` (`ApplySearchFilters`)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

- Baseline, by registration: [1580](../../current/v29-shots/s10-search-results-1580.png) · [1440](../../current/v29-shots/s10-search-results-1440.png) · [760](../../current/v29-shots/s10-search-results-760.png)
- Baseline, the Triage's registration: [1580](../../current/v29-shots/s11-search-triage-1580.png) · [1440](../../current/v29-shots/s11-search-triage-1440.png) · [760](../../current/v29-shots/s11-search-triage-760.png)
- The Triage Case found (P13): [1580](../../current/v29-shots/p20-search-triage-1580.png) · [1440](../../current/v29-shots/p20-search-triage-1440.png) · [760](../../current/v29-shots/p20-search-triage-760.png)

## Notes

- Scope for v29: what Search matches (Case reference and Audit reference),
  its result columns including Case type and state, and whether Triage
  appears.
