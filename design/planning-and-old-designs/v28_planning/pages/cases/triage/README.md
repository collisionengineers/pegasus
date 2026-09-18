# Triage

- **Mockup route:** `pegasus_triage_unidentified_v28.html` (`tu-area=triage`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Index.cshtml` (tab `triage`, the list), `src/Pegasus.Web/Pages/Triage/Details.cshtml` (the record). `src/Pegasus.Web/Pages/Triage/Index.cshtml` is a permanent redirect to `/Cases?tab=triage` and renders nothing of its own.

- [**How it works**](how-it-works.md)

## Screenshots

- [s25-triage-list-1580.png](../../../current/v28-shots/s25-triage-list-1580.png) · [1440](../../../current/v28-shots/s25-triage-list-1440.png) · [760](../../../current/v28-shots/s25-triage-list-760.png)
- [s26-triage-detail-1580.png](../../../current/v28-shots/s26-triage-detail-1580.png) · [1440](../../../current/v28-shots/s26-triage-detail-1440.png) · [760](../../../current/v28-shots/s26-triage-detail-760.png)

## Notes

- `Pages/Triage/Index.cshtml` never renders — `IndexModel.OnGet` always
  `RedirectPermanent`s to `/Cases` (with `?tab=triage` when a `queue` was
  carried). The list this mockup calls "Triage — LIST" is therefore
  transcribed from the `triage` tab of the unified Cases queue page
  (`Pages/Cases/Index.cshtml`), not from a page of its own. The Workflow
  rail's other tabs (Not ready, Review, With Engineer, Complete, Query,
  Held) are shown as inert chrome for orientation only — their content is
  the `cases_index` lane's scope, not duplicated here. The "Awaiting
  instruction" tab is the `image_intake` lane's scope; clicking it in this
  mockup shows a demo toast rather than navigating, since the two lanes are
  separate offline files.
- The mockup keeps the record's "Response evidence candidates" as one
  illustrative option rather than the live multi-select-from-a-poll list —
  a faithful simplification of representative data, not a different control.
- No bulk or merge action exists on the live Triage list or record; none is
  shown here.
- `triage.css` and `unidentified.css` are this lane's only page-specific
  stylesheets per `build.py`'s `PAGES` dict. The `triage`/`unidentified`
  tabs' own list-page refinements live in `cases-index.css`, which is not
  inlined into this build (it belongs to the `cases_index` lane's page
  name), so the list view's table/pane spacing uses `site.css`'s base
  `.pane-layout`/`.table`/`.fact-grid` rules rather than the Cases page's
  own tightened variants. The real class names (`cases-queue`,
  `cases-table`, `cases-detail`, `cases-facts`, `cases-empty`) are used
  as-is from the live markup; only their extra CSS rules are absent here.
