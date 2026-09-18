Read from the live source on 18 September 2026.

## What this page does not show

- No account count subtitle beyond "N accounts" in the panel meta; no
  per-role counts.
- The settings dialog's adverse-action row (Disable/Enable, Force logout,
  Reset password, Delete) is absent for the signed-in operator's own row
  (`!row.IsCurrentOperator`) — an operator cannot disable or delete
  themselves from this page.
- Reset password/Force logout are offered only while the account is enabled
  (`account.IsEnabled`).

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Accounts/Index.cshtml` | Table, Create dialog, per-account settings dialog, Glass's cell, delete confirmation |
| `Pages/Administration/Accounts/Index.cshtml.cs` | `StaffAccountRow.GlassLabel`, role/sign-off posting, `Edit`/`Settings`/`Delete`/`ForceLogout`/`ResetPassword` handlers |
| `Presentation/OperatorLabels.cs` (`StaffAccounts` class) | `SignOffState()`, State chip text, dialog field labels |
| `wwwroot/js/accounts.js` | The Role select's live disabling of the sign-off fields |

## Behaviours

### Columns

Username (`mono`), Role (`Humanise(account.Role)`), Sign-off, Glass's,
State, and a row-level Settings link. Sign-off is one of six states from
`OperatorLabels.StaffAccounts.SignOffState`: "—" (User role — not eligible),
"No", "Signature missing", "Yes · qualifications missing", "Yes · default",
"Yes · not eligible" (default but currently disabled), or plain "Yes".
Glass's is one of "Not set", "Disabled", or the stored username
(`StaffAccountRow.GlassLabel`). State shows the Enabled/Disabled chip plus a
second amber "Password change required" chip when
`account.MustChangePassword` — two chips are legitimate here, one per fact.

### Create staff account

A single dialog: Username and a required Temporary password, nothing else.
The created account's one-time temporary password is then shown in its own
"Temporary password" panel above the table (`Model.ResetTemporaryPassword`),
not inside a dialog, with the sentence "It is shown only for this response
and must be changed at first sign-in." — the one approved consequence
sentence for this flow.

### Settings dialog

Role, Sign-off Engineer (only Administrator/Engineer roles may hold it —
`wwwroot/js/accounts.js` disables the four sign-off-only fields live when
Role is switched to User), Printed name, Qualifications, a "Glass's repair
estimates" section whose "Manage login" button opens the
[Glass's login](../glass/) dialog for this same account, a signature-image
file input, and a Default sign-off Engineer checkbox. Adverse actions
(Disable/Enable, Force logout, Reset password, Delete) live in the dialog's
own second foot row, separate from Save, and Delete opens its own
confirmation dialog rather than acting immediately.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
