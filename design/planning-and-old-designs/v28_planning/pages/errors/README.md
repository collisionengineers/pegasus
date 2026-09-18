# Error and status-code family

- **Mockup route:** `pegasus_account_shell_v28.html` (`acc-page=error|status404|status413|status429|status403`) in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Error.cshtml`, `src/Pegasus.Web/Pages/StatusCode.cshtml`

- [**How it works**](how-it-works.md)

## Screenshots

- [s11-error-1580.png](../../current/v28-shots/s11-error-1580.png) · [1440](../../current/v28-shots/s11-error-1440.png) · [760](../../current/v28-shots/s11-error-760.png)
- [s12-status404-1580.png](../../current/v28-shots/s12-status404-1580.png) · [1440](../../current/v28-shots/s12-status404-1440.png) · [760](../../current/v28-shots/s12-status404-760.png)

## Notes

Notifications.cshtml (`src/Pegasus.Web/Pages/Notifications.cshtml`) is not
given its own screen here: its own source comment says it is "the bell's two
handlers, not a screen" — every notification row and the Mark-all-read
action are already captured on the shell's Notifications dialog, present on
every mockup file in this round.
