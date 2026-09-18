# Action logs (legacy route)

- **Mockup route:** none — folded into `pegasus_administration_v28.html` (`ad-area=logs`, `adLogsTab=action`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/ActionLogs.cshtml`, `ActionLogs.cshtml.cs`

- [**How it works**](how-it-works.md)

## Screenshots

None taken separately — see [`../logs/README.md`](../logs/README.md)'s screenshots (`ad-area=logs`), which is where this legacy route's content is captured, tabbed alongside the Intake log.

## Notes

This route has no capture of its own. `ActionLogsModel.OnGet()` is
`RedirectPermanent("/Administration/Logs" + Request.QueryString.Value)` — the
page carries no markup and no state. The content it once served now lives
entirely at [Logs' "Action logs" tab](../logs/), which this mockup captures.
