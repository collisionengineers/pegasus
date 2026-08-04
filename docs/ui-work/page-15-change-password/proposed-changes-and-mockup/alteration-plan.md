# Page 15 — Change password — alteration plan

## Review summary

The form works and its security shape is right, but the password requirement sits in a banned lede
away from the field it governs, framework default messages ("'ConfirmPassword' and 'NewPassword'
do not match.") reach the operator raw, failure wording cannot distinguish a wrong current
password from a weak new one, and the forced first-sign-in stop is visually identical to a
voluntary change. The redesign moves the page onto the auth-card family, states the requirement
once beside the New password field, and gives the forced variant its own heading and consequence
sentence.

## Changes

1. **Card family**: same panel as today → auth card consistent with sign-in (page 14): mark,
   single h1, fields, full-width primary action.
2. **Requirement placement**: lede *"Your new password must contain at least eight characters. No
   character composition is required."* → one hint line directly under the **New password** label:
   *"At least 8 characters. Any characters are allowed."* Stated once; nowhere else.
3. **Forced first-sign-in variant**: identical page → distinct heading context. H1 **"Set a new
   password before continuing"** with one consequence sentence: *"You cannot use Pegasus until the
   password issued to you is replaced."* (consequence copy, allowed by §4.1). Rendered navless —
   the middleware has already locked every destination, so the menu is honest by being absent.
   The voluntary variant keeps h1 **"Change password"** and may keep the app nav.
4. **Mismatch message**: "'ConfirmPassword' and 'NewPassword' do not match." → *"The passwords do
   not match."*, shown at the Confirm field.
5. **Length message**: "The field NewPassword must be a string or array type with a minimum length
   of '8'." → *"The new password must be at least 8 characters."*, shown at the New password
   field.
6. **Wrong current password**: combined sentence → *"The current password is incorrect."*, shown
   at the Current password field. Requires the error split in Dependencies; until then the
   combined sentence is kept but restyled as the designed alert.
7. **Expired / already-used form states**: keep the existing sentences, presented in the designed
   alert style rather than the bare validation summary.
8. **Success confirmation**: silent redirect → one-time notice on the destination page: *"Your
   password has been changed."* (green, confirmed-completion role, dismisses on navigation).
9. **Empty-field messages**: framework defaults → "Enter your current password." / "Enter a new
   password." / "Confirm the new password."

## Dependencies

- `StaffPasswordChangeError` (Core) needs distinct wrong-current-password vs policy-failure
  values for change 6; page copy for both outcomes is specified above so the Core change is
  mechanical.
- Forced-variant flag: the page must know it was reached under `MustChangePassword` to swap
  heading, sentence, and shell (the middleware already knows; pass it through).
- One-time success notice mechanism (TempData or equivalent) for change 8.
- Navless auth shell shared with pages 14 and 16.

## Open questions

- Telling an authenticated user "the current password is incorrect" is not an enumeration risk
  (the session already proves identity), but confirm no policy objection before splitting the
  error.
- Should the voluntary variant also drop the nav for family consistency, or keep it since the
  operator arrived from inside the app? (Plan assumes: keep nav when voluntary.)
