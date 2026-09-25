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

## Three designs — 25 September 2026

Open the [comparison page](../../current/pegasus_signin_designs_v30.html),
[A — Quiet focus](../../current/pegasus_signin_a_v30.html),
[B — Brand split](../../current/pegasus_signin_b_v30.html), or
[C — Charcoal frame](../../current/pegasus_signin_c_v30.html).
See the [proposals](../../current/signin-design-proposals.md) for tradeoffs
and open decisions H2, H3, I and J. The initial-login alternatives support
`default`, `validation`, `error` and `signed-out` presets.

The shots below are from this pass. The earlier “not captured” note above
describes the previous five-surface round.

| Shot | Design / state | 1580×1000 | 1440×900 | 760×1000 |
| --- | --- | --- | --- | --- |
| 01 | A / Initial | [View](../../current/v30-signin-shots/01-a-default-1580.png) | [View](../../current/v30-signin-shots/01-a-default-1440.png) | [View](../../current/v30-signin-shots/01-a-default-760.png) |
| 02 | B / Initial | [View](../../current/v30-signin-shots/02-b-default-1580.png) | [View](../../current/v30-signin-shots/02-b-default-1440.png) | [View](../../current/v30-signin-shots/02-b-default-760.png) |
| 03 | C / Initial | [View](../../current/v30-signin-shots/03-c-default-1580.png) | [View](../../current/v30-signin-shots/03-c-default-1440.png) | [View](../../current/v30-signin-shots/03-c-default-760.png) |
| 04 | A / Required fields | [View](../../current/v30-signin-shots/04-a-validation-1580.png) | [View](../../current/v30-signin-shots/04-a-validation-1440.png) | [View](../../current/v30-signin-shots/04-a-validation-760.png) |
| 05 | B / Required fields | [View](../../current/v30-signin-shots/05-b-validation-1580.png) | [View](../../current/v30-signin-shots/05-b-validation-1440.png) | [View](../../current/v30-signin-shots/05-b-validation-760.png) |
| 06 | C / Required fields | [View](../../current/v30-signin-shots/06-c-validation-1580.png) | [View](../../current/v30-signin-shots/06-c-validation-1440.png) | [View](../../current/v30-signin-shots/06-c-validation-760.png) |
| 07 | A / Incorrect credentials | [View](../../current/v30-signin-shots/07-a-error-1580.png) | [View](../../current/v30-signin-shots/07-a-error-1440.png) | [View](../../current/v30-signin-shots/07-a-error-760.png) |
| 08 | B / Incorrect credentials | [View](../../current/v30-signin-shots/08-b-error-1580.png) | [View](../../current/v30-signin-shots/08-b-error-1440.png) | [View](../../current/v30-signin-shots/08-b-error-760.png) |
| 09 | C / Incorrect credentials | [View](../../current/v30-signin-shots/09-c-error-1580.png) | [View](../../current/v30-signin-shots/09-c-error-1440.png) | [View](../../current/v30-signin-shots/09-c-error-760.png) |
| 10 | A / Signed out | [View](../../current/v30-signin-shots/10-a-signed-out-1580.png) | [View](../../current/v30-signin-shots/10-a-signed-out-1440.png) | [View](../../current/v30-signin-shots/10-a-signed-out-760.png) |
| 11 | B / Signed out | [View](../../current/v30-signin-shots/11-b-signed-out-1580.png) | [View](../../current/v30-signin-shots/11-b-signed-out-1440.png) | [View](../../current/v30-signin-shots/11-b-signed-out-760.png) |
| 12 | C / Signed out | [View](../../current/v30-signin-shots/12-c-signed-out-1580.png) | [View](../../current/v30-signin-shots/12-c-signed-out-1440.png) | [View](../../current/v30-signin-shots/12-c-signed-out-760.png) |

The [self-check](../../current/v30-signin-selfcheck.html) passed 564 checks
on 25 September 2026; the
[evidence record](../../current/v30-signin-shots/verification.json) includes
keyboard checks and confirms no console errors or external requests.
