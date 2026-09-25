# Sign in: how it works

Read from the live source on 25 September 2026 (`origin/dev` `32dabfc59`).

## What the page does not show

- Which accounts exist, whether an account is disabled, or how many attempts
  remain. A disabled account and a wrong password fail with the same
  sentence; there is no lockout (`lockoutOnFailure: false`).
- Where the person will land: `ReturnUrl` is a hidden field.
- Anything about the application: no navigation, no version, no footer.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [Design authority](../../../../../docs/design/README.md#authenticated-shell) | `_LayoutAuth` is the navless frame; the auth card carries the refined mark at 96px |
| [FRD-12 · Shell and routes](../../../../../docs/frd/frd-12-operator-experience.md#shell-and-routes) | Access denied and the error family render in the same frame |
| [FRD-04](../../../../../docs/frd/frd-04-parties-accounts-and-access.md) | Accounts, temporary passwords and the forced first-sign-in change |

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Shared/_LayoutAuth.cshtml` | The external shell: one card on the dark ground, mark + PEGASUS, no navigation |
| Web | `Pages/Account/SignIn.cshtml`, `.cshtml.cs` | Username, Password, Sign in; the signed-out confirmation; the invalid-credentials sentence |
| Web | `Pages/Account/PasswordChange.cshtml` | Forced change uses the same frame (PR 828: new password and confirmation only) |
| Web | `Pages/Account/AccessDenied.cshtml` | Area eyebrow, "Access denied", one sentence, Return to Work Centre |
| Web | `wwwroot/css/site.css` (`.external-shell`, `.auth-card`, `.auth-brand`) | 440px card, 4px red top border, 96px mark, 23px h1 |
| Core | `Identity` | Roles and the must-change-password flag |

## Behaviours

### The card

`.external-shell` centres one `.auth-card` (440px, white, 4px `--red` top
border, deep shadow) on the `--nav` ground. `.auth-brand` is the 256px
refined mark rendered at 96px beside **PEGASUS** (20px, tracked). Then the
h1, the validation summary (`ModelOnly`), and a form of two `.field`s and a
`btn btn--primary` **Sign in**. The form is a grid with a 12px gap; the
button spans the card.

### Sign in

`POST` with Username and Password. A failure re-renders the page with the
model-only sentence "The username or password is incorrect. If your access
has changed, contact an administrator." and an emptied password. Each field
carries its own `field-error` for the required messages ("Enter your
username.", "Enter your password.").

### Signed out

`?SignedOut=true` renders the same card with a green check and **You are
signed out** as the h1, and the form below it. It is a one-time state, not a
page.

### Forced password change

After a temporary password, `PasswordChange` renders in `_LayoutAuth` with
the h1 **Set a new password before continuing**, one explanatory paragraph,
New password, Confirm new password and **Change password**.

### Access denied

The area as the eyebrow, **Access denied**, one sentence, and **Return to
Work Centre** as the primary button.

## Things the FRD does not settle

- Whether the card's brand row should match the rail lockup (mark and
  "Case management" line) now that v28 P1 refined both marks.
- Whether the forced-change paragraph survives the page-economy rule.
- A show-password control.
