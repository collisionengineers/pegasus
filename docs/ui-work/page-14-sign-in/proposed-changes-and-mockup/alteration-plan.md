# Page 14 — Sign in — alteration plan

## Review summary

The sign-in card is close to right, but it renders inside the full application chrome (the nav is
unconditional in the layout), carries a policy lede, leaks the `UserName` property name in
validation copy, and has one entirely undesigned failure: rate-limited requests return a raw
HTTP 429 with no body. The redesign gives the page a navless centered auth shell, a proper mark,
and designed invalid-credentials and rate-limited states.

## Changes

1. **Shell**: full app layout with unconditional nav → shared unauthenticated auth shell: centered
   card on paper background, no navigation, no user menu. (Same shell serves the signed-out
   confirmation, page 16.)
2. **Mark**: eyebrow `Collision Engineers` → `COLLISION ENGINEERS` text mark at the top of the
   card, styled as the brand mark rather than a section kicker.
3. **Lede removed**: *"Use the staff account issued to you. Contact an administrator if your
   access has changed."* → gone. H1 + form is the explanation (§4.1). The administrator pointer
   moves into the invalid-credentials state, where it is actually needed.
4. **Heading**: `Sign in to Pegasus` → unchanged.
5. **Field validation copy**: "The UserName field is required." / "The Password field is
   required." → "Enter your username." / "Enter your password." (explicit `ErrorMessage` /
   `[Display]` on the bind properties).
6. **Invalid credentials state**: bare validation-summary text → designed inline alert above the
   form. Sentence unchanged: *"The username or password is incorrect."* plus the relocated second
   line: *"If your access has changed, contact an administrator."* Fields retained (username kept,
   password cleared), focus returned to password.
7. **Rate-limited state**: raw bodyless HTTP 429 → styled page in the same card family:
   h1 **"Too many sign-in attempts"**, one sentence — *"Wait a minute, then try again."* — derived
   from the existing `Retry-After: 60`. No support reference (nothing is broken).
8. **Primary action**: **Sign in**, red, full-width — unchanged.

## Dependencies

- A navless unauthenticated layout (shared with page 16's signed-out confirmation).
- The 429 rejection currently sets status + headers only; rendering the designed page needs the
  `OnRejected` callback (or status-code-pages middleware) to write the styled body.
- `ErrorMessage`/`[Display]` attributes on `SignInModel.UserName` / `Password`.
- No Core changes; the failure sentence and rate-limit policy are kept as-is.

## Open questions

- Should the rate-limited page show a live countdown, or is the static sentence enough? (Static
  recommended: no JS on auth pages.)
- Is a password-reveal toggle wanted for a staff application, or deliberately omitted?
