Read from the live source on 18 September 2026.

## What this page does not show

- Sign-out has no screen of its own — `SignOut.cshtml` carries no markup
  (its own comment: "this route is a handler, not a screen"). The rail-foot
  "Sign out" form POSTs directly and redirects to the "You are signed out"
  state of `SignIn.cshtml` (`Model.SignedOut`); a GET on the same route
  redirects without ending the session.
- `AccessDenied.cshtml` states only the fact, deliberately with no alarm
  chip: a source comment says a red "Denied" pill beside an "Access denied"
  heading would say the same thing twice and treat a routine permission
  boundary as an incident.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Account/SignIn.cshtml` | Sign-in form and the one-time signed-out confirmation state |
| `Pages/Account/SignOut.cshtml` | POST/GET handlers only, no markup |
| `Pages/Account/AccessDenied.cshtml` | The access-denied statement, with an optional named-area eyebrow |
| `Pages/Account/PasswordChange.cshtml` | Both the forced and voluntary password-change forms, switching `Layout` on `Model.Forced` |
| `Pages/Shared/_LayoutAuth.cshtml` | The navless card frame: logo, product name, no navigation |
| `Presentation/OperatorLabels.Shell.AccessDenied`, `.AccessDeniedSentence`, `.AdministrationDenied` | The exact denial wording |

## Behaviours

### Signed-out is a one-time state, not a bookmarkable page

`Model.SignedOut` renders the green "You are signed out" heading only
immediately after the redirect; reloading the same URL later shows the plain
sign-in form, so an old bookmark can never assert a stale "you are signed
out" fact (source comment on `SignIn.cshtml`).

### Password change is forced or voluntary, and switches its whole frame

`Model.Forced` selects `Layout = "Shared/_LayoutAuth"` (no navigation — a
comment explains the middleware has already locked every other destination,
so a menu would offer nowhere the operator can go) versus the ordinary
`_Layout` app shell when the operator chose this voluntarily from inside the
application. Both branches share the same three-field form; only the frame,
heading and page-header/panel wrapping differ.

## Things the FRD does not settle

**A — Access denied's actual frame does not match documented design intent.**
`AccessDenied.cshtml` carries no `Layout` override, so ASP.NET's default
(`_ViewStart.cshtml`'s `Layout = "_Layout"`, the full app shell with rail and
utility bar) applies. `docs/design/README.md`'s "Case record frame" section
states: "`_LayoutAuth` remains the navless frame: sign-in, the signed-out
confirmation, access denied and the error family are not places in the
application" — but the live source shows Access denied is NOT navless; it
renders inside the ordinary authenticated shell (rail, utility bar, working
set) like any other page. `Error.cshtml`, `StatusCode.cshtml` and
`Connect/Authorize.cshtml` DO carry the `Layout = "Shared/_LayoutAuth"` (or
`"_LayoutAuth"`) override and are genuinely navless. This is a live/documented
mismatch for Access denied specifically, not a design opinion. This mockup
captures Access denied's content inside the navless card only because that is
where this round's build groups the "no rail" family — the live rendering
actually keeps the full rail and utility bar around it. **Confirm, or**: is
this an intentional exception the design doc should record, or a defect
where `AccessDenied.cshtml` is missing its `Layout = "Shared/_LayoutAuth"`
line?
