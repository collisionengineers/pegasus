# Inbox

- **Mockup route:** `pegasus_inbox_v30.html` in [`../../current/`](../../current/README.md); `?state=default|list|empty|stale|dismissed|deleted|search`, `?layer=baseline|proposal`
- **Live source:** `src/Pegasus.Web/Pages/Mail/Index.cshtml`, `.cshtml.cs`, `wwwroot/css/inbox.css`, `Shared/_FreshnessBanner.cshtml`, `Shared/_RefreshButton.cshtml`
- **Dialogs:** [`dialogs/`](dialogs/README.md) · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Not captured in this pass. `shoot.ps1` names them `inbox-<state>-<width>.png`.

## Notes

The message record (`/Inbox/{id}`) and the Compose dialog are not in this
round; the preview pane's **Open full message** and the header's
**Compose** are drawn as the links they are.
