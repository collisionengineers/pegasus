# Unidentified

- **Mockup route:** `pegasus_triage_unidentified_v28.html` (`tu-area=unidentified`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Cases/Index.cshtml` (tab `unidentified`, the list), `src/Pegasus.Web/Pages/Unidentified/Details.cshtml` (the item). `src/Pegasus.Web/Pages/Unidentified/Index.cshtml` is a permanent redirect to `/Cases?tab=unidentified` and renders nothing of its own.

- [**How it works**](how-it-works.md)

## Screenshots

- [s27-unidentified-detail-1580.png](../../../current/v28-shots/s27-unidentified-detail-1580.png) · [1440](../../../current/v28-shots/s27-unidentified-detail-1440.png) · [760](../../../current/v28-shots/s27-unidentified-detail-760.png)
- [s28-unidentified-closed-1580.png](../../../current/v28-shots/s28-unidentified-closed-1580.png) · [1440](../../../current/v28-shots/s28-unidentified-closed-1440.png) · [760](../../../current/v28-shots/s28-unidentified-closed-760.png)

## Notes

- `Pages/Unidentified/Index.cshtml` never renders — `IndexModel.OnGet`
  always `RedirectPermanent`s to `/Cases?tab=unidentified`. The list this
  mockup calls "Unidentified — LIST" is transcribed from the `unidentified`
  tab of the unified Cases queue page, with its `Show` filter (`open` /
  `closed`) reproduced exactly, including the distinct column sets
  (`Reason` on open rows, `Outcome` on closed rows) and the distinct empty
  strings ("Nothing Unidentified" vs "No closed items").
- The "closed with a reason" / reopen flow (CONTEXT.md: "Readable material
  that must not become a Case is closed with a reason, and a closed item
  can be reopened") is captured as its own record state (`tu-unident-state:
  closed`) with the Resolution panel's Outcome fact reading `Closed ·
  <reason>` and a Reopen control that opens the same reason-required
  dialog shape as Close.
- "Could not be read" is captured as one of the six reason values
  (`tu-unident-reason: could_not_be_read`), which additionally shows the
  `Could not be read · <file kind>` warning banner that the other five
  reasons never show, per `DetailsModel.CouldNotBeRead`.
- The Image Intake Reference / association-to-Case flow (`Link to Case`
  dialog with case search and a per-candidate `Link` action, and `Register
  images` for the image-only path) is captured on this page as the
  Unidentified item's own resolve actions; the Image intake record itself
  — what an Image Intake Reference looks like once registered, and its own
  association reversal — is the `image_intake` lane's scope, not
  duplicated here.
- No bulk or merge action exists on the live Unidentified list or record;
  none is shown here.
- Registration readings and History are read-only lists on the live page;
  the mockup keeps one illustrative reading (pending, dismissible) and one
  illustrative history entry rather than a long scroll, per the fixture
  sheet's "representative fixture data" instruction.
- Per the note on the Triage README, `cases-index.css`'s list-page
  refinements are not inlined into this build; the list view's table/pane
  spacing uses `site.css`'s base rules while the real Cases-page class
  names are kept in the markup.
