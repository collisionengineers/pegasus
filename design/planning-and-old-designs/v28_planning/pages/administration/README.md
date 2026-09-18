# Administration

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=hub`) in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Index.cshtml`, `Shared/_AdminNav.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s36-admin-hub-1580.png](../../current/v28-shots/s36-admin-hub-1580.png) · [1440](../../current/v28-shots/s36-admin-hub-1440.png) · [760](../../current/v28-shots/s36-admin-hub-760.png)
- [s39-admin-denied-1580.png](../../current/v28-shots/s39-admin-denied-1580.png) · [1440](../../current/v28-shots/s39-admin-denied-1440.png) · [760](../../current/v28-shots/s39-admin-denied-760.png) (non-Administrator role)

## Notes

- This one file covers the hub and all thirteen live Administration routes as
  one `ad-area` switcher, per the capture brief: `ActionLogs.cshtml` is a
  permanent redirect folded into [Logs](logs/README.md)'s "Action logs" tab
  (see [action-logs/](action-logs/README.md)); `Glass/Index.cshtml` is a
  per-account credential dialog reached from [Accounts](accounts/README.md)
  and shares its `accounts` nav area live (`ViewData["AdminArea"] =
  "accounts"`), so it is documented at [glass/](glass/README.md) but has
  no separate nav entry or
  `ad-area` value here.
- The live hub's cards use Lucide glyphs (`icon-user`, `icon-shield`,
  `icon-settings`, `icon-mail`, `icon-loader`, `icon-scroll-text`,
  `icon-file-text`, `icon-sparkles`), not the `accounts.png` /
  `configuration.png` / `mailboxes.png` / `automation.png` marks
  `docs/design/README.md` describes for these panel heads — `git grep
  "images/marks"` under `Pages/` finds only the rail brand lockup. The
  capture follows the live `.cshtml` (Lucide icons, no marks) rather than the
  design document's stated intent, per "faithfully mirror current live
  behaviour" over any other source. Flagged as sign-off item A.
- The hub's own icon choice is itself inconsistent between the hub card and
  the rail nav for the same area (Health: `icon-loader` on the card vs
  `icon-heart-pulse` in `_AdminNav`; Reports: `icon-file-text` vs
  `icon-bar-chart`; Automation & AI: `icon-sparkles` vs `icon-bot`).
  Captured as-is; not a mockup error.
- State-key prefix: every control in this lane's strip/body/js uses an `ad`
  prefix (`ad-area`, `adLogsTab`, `adGlass`, `adGlassEditing`,
  `adContactsView`, `adContactEdit`, `adConfigEdit`, `adPresetEdit`,
  `adLogsDrawer`, `adAutomationComposed`) so its keys never collide with
  another lane's strip.
