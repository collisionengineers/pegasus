# Workflow configuration

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=configuration`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Configuration.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s42-admin-configuration-1580.png](../../../current/v28-shots/s42-admin-configuration-1580.png) · [1440](../../../current/v28-shots/s42-admin-configuration-1440.png) · [760](../../../current/v28-shots/s42-admin-configuration-760.png)

## Notes

- The strip's `adConfigEdit` toggle switches the whole page (both panels)
  between its read-only and editing states, matching the live page's single
  `Model.IsEditing` switch.
- The concurrent-edit "Take over" variant and the lease heartbeat/beacon
  forms are not modelled, as with every other editor in this lane (see
  [Accounts' Notes](../accounts/)).
- No eyebrow above "Workflow configuration" — `Configuration.cshtml` never
  sets `ViewData["Eyebrow"]`, unlike every other sub-area page. Captured
  as-is.
