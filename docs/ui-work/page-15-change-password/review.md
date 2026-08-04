# Page 15 — Change password (`/Account/PasswordChange`)

Screenshot: `change-password.png`. Source: `src/Pegasus.Web/Pages/Account/PasswordChange.cshtml`
and `PasswordChange.cshtml.cs`, plus the forcing middleware in `Program.cs`.

Current facts: a centered card inside the full app layout — h1 **"Change password"**, lede *"Your
new password must contain at least eight characters. No character composition is required."*, three
password fields (**Current password**, **New password**, **Confirm new password**), a hidden
single-use form key, and a red full-width **Change password** button. This page is also the forced
first-sign-in destination: while `MustChangePassword` is set, middleware in `Program.cs` redirects
every request in the application to this page.

## 1. Aesthetics

- The card layout is serviceable and matches sign-in's family, but the requirement text sits in a
  lede above the whole form — two lines of policy prose distanced from the one field they govern
  (§4.1 bans ledes; §4.1 also says guidance sits next to the control it concerns).
- The forced variant is visually identical to the voluntary one. An operator whose every click is
  being redirected here gets no heading, sentence, or state that explains the stop — the single
  most confusing moment in the product's first-run experience is undesigned.
- The full nav renders above a page that (in the forced case) has locked the entire nav: every
  link redirects straight back here. A menu where every item returns you to the current page is
  worse than no menu.

## 2. Practicality

- Failure wording collapses distinct problems into one sentence: *"The password could not be
  changed. Check the current password and the new password requirements."* A wrong current
  password and a too-short new password produce the same message, and `ResetSensitiveInput()`
  clears all three fields — so the operator re-types everything while guessing which part was
  wrong.
- Framework default messages reach the operator raw. `[Compare(nameof(NewPassword))]` renders
  **"'ConfirmPassword' and 'NewPassword' do not match."** and `[MinLength(8)]` renders **"The
  field NewPassword must be a string or array type with a minimum length of '8'."** — quoted
  property names and type-system prose as user copy (§4.3).
- The single-use form key produces two reasonable consequence sentences (*"The form has expired.
  Retry the password change."* / *"This password-change form was already used. Retry from the
  current page."*) — the wording is fine; only presentation needs design.
- Success is silent: the handler signs the user out and back in and lands on the home page with no
  confirmation that the password changed.

## 3. Performance / design / good practice

- Correct security shape throughout: sensitive inputs cleared on failure, single-use key prevents
  replay, session re-issued after the change, `autocomplete="new-password"` set. None of this
  needs to change.
- Server-rendered, no JS — right.
- The requirement statement itself ("at least eight characters, no composition rules") matches the
  `[MinLength(8)]` validation — the content is honest; it is only in the wrong place.
- Gap: policy in Core decides success/failure but the page cannot distinguish *which* rule failed
  (`StaffPasswordChangeError` has no wrong-current-password / weak-password split), which is the
  root cause of the vague combined error above. Fixing the copy properly needs that distinction.
