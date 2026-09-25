# Sign in

- **Mockup route:** `pegasus_signin_v30.html` in [`../../current/`](../../current/README.md); `?state=default|error|signed-out|forced-password|access-denied`, `?layer=baseline|proposal`, `?opt=brand:compact,title:short,reveal:on`
- **Live source:** `src/Pegasus.Web/Pages/Account/SignIn.cshtml`, `Shared/_LayoutAuth.cshtml`, `Account/PasswordChange.cshtml` (forced), `Account/AccessDenied.cshtml`
- **Dialogs:** none · **States:** [`states/`](states/README.md) · **Panels:** [`panels/`](panels/README.md)

- [**How it works**](how-it-works.md) · [**How it should work**](how-it-should-work.md)

## Screenshots

Not captured in this pass (operator: prepare the mockups for examination
first). `shoot.ps1` names the shots `signin-<state>-<width>.png` when run.

## Notes

The navless frame is shared by the whole error family; a change here is a
shared-layout change and needs the 760px capture when it is shot.
