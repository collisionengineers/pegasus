# Work Centre: panels

## Three-design pass — 25 September 2026

A uses queue / selected-work panes with New cases and AI jobs below. B uses
one tabbed section area, with inline details in its attention table. C uses
three due-group lists, a Selected work drawer and the two supporting feeds.
Every alternative retains the five office queue totals.

See the [comparison](../../../current/pegasus_work_centre_designs_v30.html)
and [proposals](../../../current/work-centre-design-proposals.md).

| Panel | Live class | Holds |
| --- | --- | --- |
| Metrics | `metric-section` · `metric-strip metric-strip--5` | Not ready, Review, Held, Unidentified, Triages |
| Needs attention | `pane` (`data-wc-attention`) | Office/Mine, kind chips, grouped rows, paging |
| Today | `pane` (`data-wc-today`) | The selected item's facts and actions |
| New cases | `panel wc-section` (`#wc-new-cases`) | Last 7 days, arrival chips, the divider |
| AI jobs | `panel wc-section` (`#wc-ai-jobs`) | The jobs table |
