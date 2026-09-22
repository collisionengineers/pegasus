# Sign in and account

- **Live source:** `src/Pegasus.Web/Pages/Account/SignIn.cshtml`, `src/Pegasus.Web/Pages/Account/SignOut.cshtml`, `src/Pegasus.Web/Pages/Account/PasswordChange.cshtml`, `src/Pegasus.Web/Pages/Shared/_LayoutAuth.cshtml`
- [**How it works**](how-it-works.md)
- [Connector consent](connector-consent/README.md)

Captured from a second local host that uses real sign-in with one throwaway User-role account, so the shell is also seen without the Manage group.

## Captured states

Each state is the running application's own HTML for the route shown, saved with the live CSS and JS. Nothing in it is transcribed.

| State | Live route | Open | Screenshots |
| --- | --- | --- | --- |
| Sign in | `/Account/SignIn` | [frame](../../current/pegasus_account_shell_v28.html#sign-in) · [page](../../current/states/sign-in.html) | [1580](../../current/v28-shots/s63-sign-in-1580.png) · [1440](../../current/v28-shots/s63-sign-in-1440.png) · [760](../../current/v28-shots/s63-sign-in-760.png) |
| Sign in, wrong password | `/Account/SignIn` | [frame](../../current/pegasus_account_shell_v28.html#sign-in-failed) · [page](../../current/states/sign-in-failed.html) | [1580](../../current/v28-shots/s64-sign-in-failed-1580.png) · [1440](../../current/v28-shots/s64-sign-in-failed-1440.png) · [760](../../current/v28-shots/s64-sign-in-failed-760.png) |
| Shell for the User role (no Manage group) | `/` | [frame](../../current/pegasus_account_shell_v28.html#work-centre-user-role) · [page](../../current/states/work-centre-user-role.html) | [1580](../../current/v28-shots/s65-work-centre-user-role-1580.png) · [1440](../../current/v28-shots/s65-work-centre-user-role-1440.png) · [760](../../current/v28-shots/s65-work-centre-user-role-760.png) |
| Signed out | `/Account/SignIn?signedOut=True` | [frame](../../current/pegasus_account_shell_v28.html#signed-out) · [page](../../current/states/signed-out.html) | [1580](../../current/v28-shots/s67-signed-out-1580.png) · [1440](../../current/v28-shots/s67-signed-out-1440.png) · [760](../../current/v28-shots/s67-signed-out-760.png) |
| Change password, voluntary | `/Account/PasswordChange` | [frame](../../current/pegasus_account_shell_v28.html#password-change) · [page](../../current/states/password-change.html) | [1580](../../current/v28-shots/s68-password-change-1580.png) · [1440](../../current/v28-shots/s68-password-change-1440.png) · [760](../../current/v28-shots/s68-password-change-760.png) |

## Not captured

- The forced password change on first sign-in, lockout, and the rate-limited sign-in.
