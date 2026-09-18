# Automation & AI

- **Mockup route:** `pegasus_administration_v28.html` (`ad-area=automation`) in [`../../../current/`](../../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Administration/Automation/Index.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s41-admin-automation-1580.png](../../../current/v28-shots/s41-admin-automation-1580.png) · [1440](../../../current/v28-shots/s41-admin-automation-1440.png) · [760](../../../current/v28-shots/s41-admin-automation-760.png)

## Notes

- The strip's "Automation & AI composed" control (`adAutomationComposed`)
  switches between the two composed panels and the live page's own
  `unavailable-panel` message, and also gates the nav item and hub card per
  the absent-not-disabled rule.
- The Granted scopes fixture deliberately omits the live `automation.intake`
  scope (which `OperatorLabels.AutomationAdmin.AutomationScope` renders as
  the single word "Intake"), since the capture's word list keeps "intake" to
  the one permitted "Intake log" tab name. This is a curated fixture choice
  (which scopes to show), not a relabelling of live copy — see [Logs'
  Notes](../logs/) for the two places that word could not be avoided the
  same way.
