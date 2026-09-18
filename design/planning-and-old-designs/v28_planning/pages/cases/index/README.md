# Cases index

- **Mockup route:** `pegasus_cases_index_v28.html` in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Index.cshtml` (model `Index.cshtml.cs`)

- [**How it works**](how-it-works.md)

## Screenshots

- [s16-cases-index-list-1580.png](../../../current/v28-shots/s16-cases-index-list-1580.png) · [1440](../../../current/v28-shots/s16-cases-index-list-1440.png) · [760](../../../current/v28-shots/s16-cases-index-list-760.png)
- [s18-cases-unidentified-closed-1580.png](../../../current/v28-shots/s18-cases-unidentified-closed-1580.png) · [1440](../../../current/v28-shots/s18-cases-unidentified-closed-1440.png) · [760](../../../current/v28-shots/s18-cases-unidentified-closed-760.png)

## Notes

- All nine workflow-rail scopes (Not ready, Review, With Engineer, Completed,
  Query, Triage, Awaiting instruction, Held, Unidentified) are captured with
  their exact columns and one or two representative rows each, switchable
  from the mockup strip's "Workflow tab" control (state key `citab`) or by
  clicking the rail itself. Only one row's Quick detail is shown per tab
  (the aria-selected row); the live page's per-row reselection inside an
  already-open tab is not reproduced, since this is a static capture and the
  fixture's one or two rows per tab already demonstrate the record kind's own
  detail shape.
- The rail's own badge counts (6/3/4/12/2/2/3/2/2) are this page's fixture
  counts, independent of the shared shell chrome's fixed `Cases` nav badge
  (14) baked into `shell-chrome.html`, which this lane does not own or edit.
- The Unidentified rail badge (2) stays fixed when the Show filter is
  switched to Closed items, matching the live page: `Count(tab)` for
  `unidentified` always reads `UnidentifiedStore.CountOpenAsync`, never the
  closed list's own size.
- The Principal filter's options are a fixed representative set (Wingrove
  Insurance, Fenwick Mutual, Marlowe Assurance) rather than the live page's
  read of the principals actually present on the loaded rows
  (`PrincipalOptions`); the control and its label are exact, the option list
  is illustrative.
- The Awaiting instruction Quick detail shows the "Add to an existing case"
  form (the `RequiresGroupConfirmation` false path). The alternate
  "Continue with this submission" path, shown only for a multi-image manual
  upload still awaiting group confirmation, is not captured — no fixture
  submission group exists for it and it is a narrow sub-state of one row
  kind, not a distinct page.
- `QDOS26090` (Completed tab) is an invented reference in the fixture sheet's
  numbering style; every other reference (`QDOS26205`, `QDOS26214`,
  `QDOS26150`, `QDOS26177`, `QDOS26198`, `T-2601`, `T-2599`, `U-1140`,
  `U-1138`) is from the shared fixture sheet. `U-1120` (closed Unidentified)
  and the Triage/Awaiting registrations are invented in the same style since
  the fixture sheet names no closed Unidentified item or Awaiting-instruction
  image reference.
