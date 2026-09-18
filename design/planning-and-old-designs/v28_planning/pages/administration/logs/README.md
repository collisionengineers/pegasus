# Logs

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=logs`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Logs.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s38-admin-logs-1580.png](../../../current/v28-shots/s38-admin-logs-1580.png) · [1440](../../../current/v28-shots/s38-admin-logs-1440.png) · [760](../../../current/v28-shots/s38-admin-logs-760.png)

## Notes

- The `adLogsTab` control switches Action logs / Intake log, matching the
  live page's own `?tab=intake` query switch. `adLogsDrawer` opens the
  intake-log detail drawer for the one "Case created" fixture row; the other
  three intake rows' Open links, and every action-log/intake-log filter,
  post the demo/`demo-filter` toast rather than a second fully built drawer.
- Two labels the live page ships unavoidably contain the word "intake"
  beyond the one permitted "Intake log" tab name:
  `OperatorLabels.IntakeLog.FailedIntake` ("Failed intake") and
  `OldestPendingIntake` ("Oldest pending intake") — both fixed figure labels
  on the Intake log tab, not fixture choices this capture can curate around.
  `docs/design/README.md`'s own banned-word rule ("Do not display the
  internal word intake … the Administrator's Intake log tab is the one
  exception") appears to already be violated by this shipped copy; the
  design document itself notes the rule "is a review rule, not an automated
  check". Captured verbatim per "faithfully mirror current live behaviour…
  capture it as-is" rather than silently relabelled. Flagged as sign-off
  item B.
- The Action logs tab's "Recorded counts and processing times" `<details>`
  also ships "Cache bytes" as a literal field label (also on the banned
  list). Captured verbatim for the same reason; part of sign-off item B.
