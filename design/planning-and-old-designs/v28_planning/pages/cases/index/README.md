# Cases

- **Live source:** `src/Pegasus.Web/Pages/Cases/Index.cshtml`, `src/Pegasus.Web/Pages/Cases/Index.cshtml.cs`, `src/Pegasus.Web/wwwroot/js/cases-index.js`
- [**How it works**](how-it-works.md)
- [Case record](../case-record/README.md)
- [Create Case](../create-case/README.md)
- [Triage](../triage/README.md)
- [Unidentified](../unidentified/README.md)

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Not ready | `/Cases` | [frame](../../../current/pegasus_cases_index_v28.html#cases-not-ready) · [page](../../../current/states/cases-not-ready.html) | [1580](../../../current/v28-shots/s08-cases-not-ready-1580.png) · [1440](../../../current/v28-shots/s08-cases-not-ready-1440.png) · [760](../../../current/v28-shots/s08-cases-not-ready-760.png) |
| Review | `/Cases?tab=review` | [frame](../../../current/pegasus_cases_index_v28.html#cases-review) · [page](../../../current/states/cases-review.html) | [1580](../../../current/v28-shots/s09-cases-review-1580.png) · [1440](../../../current/v28-shots/s09-cases-review-1440.png) · [760](../../../current/v28-shots/s09-cases-review-760.png) |
| With Engineer | `/Cases?tab=with_engineer` | [frame](../../../current/pegasus_cases_index_v28.html#cases-with-engineer) · [page](../../../current/states/cases-with-engineer.html) | [1580](../../../current/v28-shots/s10-cases-with-engineer-1580.png) · [1440](../../../current/v28-shots/s10-cases-with-engineer-1440.png) · [760](../../../current/v28-shots/s10-cases-with-engineer-760.png) |
| With Engineer, row selected | `/Cases?tab=with_engineer&selected=f3dbeb86-4ab8-4d5b-ade3-dea35c995b1f` | [frame](../../../current/pegasus_cases_index_v28.html#cases-with-engineer-selected) · [page](../../../current/states/cases-with-engineer-selected.html) | [1580](../../../current/v28-shots/s11-cases-with-engineer-selected-1580.png) · [1440](../../../current/v28-shots/s11-cases-with-engineer-selected-1440.png) · [760](../../../current/v28-shots/s11-cases-with-engineer-selected-760.png) |
| Completed | `/Cases?tab=complete` | [frame](../../../current/pegasus_cases_index_v28.html#cases-complete) · [page](../../../current/states/cases-complete.html) | [1580](../../../current/v28-shots/s12-cases-complete-1580.png) · [1440](../../../current/v28-shots/s12-cases-complete-1440.png) · [760](../../../current/v28-shots/s12-cases-complete-760.png) |
| Query | `/Cases?tab=query` | [frame](../../../current/pegasus_cases_index_v28.html#cases-query) · [page](../../../current/states/cases-query.html) | [1580](../../../current/v28-shots/s13-cases-query-1580.png) · [1440](../../../current/v28-shots/s13-cases-query-1440.png) · [760](../../../current/v28-shots/s13-cases-query-760.png) |
| Held | `/Cases?tab=held` | [frame](../../../current/pegasus_cases_index_v28.html#cases-held) · [page](../../../current/states/cases-held.html) | [1580](../../../current/v28-shots/s14-cases-held-1580.png) · [1440](../../../current/v28-shots/s14-cases-held-1440.png) · [760](../../../current/v28-shots/s14-cases-held-760.png) |

## Not captured

- Populated Not ready, Review, Completed, Query and Held queues. The fixture's one Case is With Engineer; the other queues show their real empty state.
- The Principal and Missing filters applied, and a second page of results.
