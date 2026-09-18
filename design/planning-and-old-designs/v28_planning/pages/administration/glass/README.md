# Glass's login

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=accounts`, `glass-credential-dialog`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Glass/Index.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s44-admin-glass-1580.png](../../../current/v28-shots/s44-admin-glass-1580.png) · [1440](../../../current/v28-shots/s44-admin-glass-1440.png) · [760](../../../current/v28-shots/s44-admin-glass-760.png)

## Notes

- Live, this is its own route (`/Administration/Glass/{staffId:guid}`) that
  sets `ViewData["AdminArea"] = "accounts"` and renders as a full-page
  dialog reached only from an Accounts row's or the account settings
  dialog's "Manage login" link — it has no rail entry of its own. The
  capture keeps it as the same dialog, opened from either place, inside the
  `accounts` area rather than inventing an `ad-area=glass` value the live
  nav has no equivalent for.
- The strip's `adGlass` control (Not set / Set / Disabled) drives the
  dialog's own not-configured/configured detail rows, matching the three
  states `StaffAccountRow.GlassLabel` distinguishes on the Accounts table.
- `adGlassEditing` toggles the dialog between its read-only summary (Edit
  credential / Back to account) and its editable username/password fields
  (Save credential / Cancel) — the live page's own `Model.IsEditing` switch.
  The lease heartbeat/beacon and "Take over" mechanics are not modelled, as
  elsewhere in this lane.
