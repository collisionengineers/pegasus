# Sign in

- **Mockup route:** `pegasus_account_shell_v28.html` (`acc-page=signin|signedout|accessdenied|passwordchange-forced`) in [`../../current/`](../../current/README.md)
- **Live source:** `src/Pegasus.Web/Pages/Account/SignIn.cshtml`, `SignOut.cshtml`, `AccessDenied.cshtml`, `PasswordChange.cshtml`

- [**How it works**](how-it-works.md)
  - [Connector consent](connector-consent/README.md)

## Screenshots

- [s06-signin-1580.png](../../current/v28-shots/s06-signin-1580.png) · [1440](../../current/v28-shots/s06-signin-1440.png) · [760](../../current/v28-shots/s06-signin-760.png)
- [s07-signedout-1580.png](../../current/v28-shots/s07-signedout-1580.png) · [1440](../../current/v28-shots/s07-signedout-1440.png) · [760](../../current/v28-shots/s07-signedout-760.png)
- [s08-accessdenied-1580.png](../../current/v28-shots/s08-accessdenied-1580.png) · [1440](../../current/v28-shots/s08-accessdenied-1440.png) · [760](../../current/v28-shots/s08-accessdenied-760.png)
- [s09-passwordchange-forced-1580.png](../../current/v28-shots/s09-passwordchange-forced-1580.png) · [1440](../../current/v28-shots/s09-passwordchange-forced-1440.png) · [760](../../current/v28-shots/s09-passwordchange-forced-760.png)

## Notes

`PasswordChange.cshtml` is dual-layout: forced (`Model.Forced`) uses the
navless `_LayoutAuth` frame captured here; voluntary (reached from the
Account dialog while signed in) uses the ordinary `_Layout` app shell with
the same form fields. Only the forced state is captured as its own screen —
the voluntary state's form content is identical, and the app-shell chrome
around it is already captured on every other page in this round, so
re-showing it was judged non-additive for this pass. See
[how-it-works.md](how-it-works.md) for the Access denied layout finding
(sign-off item A in [`../../current/v28-notes.md`](../../current/v28-notes.md)).
