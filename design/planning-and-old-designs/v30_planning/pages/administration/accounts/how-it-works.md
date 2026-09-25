# Staff accounts: how it works

Read from the live source on 25 September 2026 (`origin/dev` `32dabfc59`).

## What the page does not show

- Why the Role select is disabled on the signed-in Administrator's own
  account (it is: an Administrator cannot demote themselves).
- Whether a signature is on file: the file input's label changes between
  "Signature image" and "Replace signature", which is the only cue.
- Which accounts are Sign-off eligible until the Sign-off column is read.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-17 · Accounts](../../../../../../docs/frd/frd-17-administration-workspace.md#accounts) | Reset password and the once-revealed temporary password; sign-off flags, qualifications and signature for any role |
| [FRD-04 · Staff accounts](../../../../../../docs/frd/frd-04-parties-accounts-and-access.md#staff-accounts) | Roles, enable/disable, delete, forced first-sign-in change |
| [Design authority](../../../../../../docs/design/README.md#reasons-confirmations-and-dialogs) | Administration actions post on the click with no confirmation, except Delete account |
| [UI guardrails](../../../../../../.agents/skills/pegasus-ui-guardrails/SKILL.md) | No mystery-disabled control; read and edit look the same; one fact, one home |

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Administration/Accounts/Index.cshtml` | The list, the Create dialog, the settings dialog, the Delete confirmation, the temporary-password panel |
| Web | `Pages/Administration/Accounts/Index.cshtml.cs` | Rows, `IsCurrentOperator`, the Settings/Create/Disable/Enable/ForceLogout/ResetPassword/Delete handlers, `SettingsPost*` values |
| Web | `Administration/Shared/_AdminNav.cshtml` | The three-group area nav |
| Web | `Presentation/OperatorLabels.StaffAccounts` | Enabled, Disabled, Password change required, Sign-off Engineer, Printed name, Qualifications, Signature image, On file, Not on file, Replace signature, Default sign-off Engineer, the validation sentences |
| Web | `wwwroot/js/accounts.js` | Reset on Cancel; the unsaved-changes confirm before Manage login |
| Core | `Identity` (`StaffAccountAdministrationPolicy`, `SignOffSignaturePolicy`) | Lengths, the PNG signature limit, roles |

## Behaviours

### The list

Header eyebrow **Administration**, h1 **Staff accounts & roles**. The
`admin-layout` puts the `admin-nav` panel (People and access, Configuration,
Operations and oversight) beside a panel **Staff accounts** · "N accounts"
with **Create staff account** (primary). Table: Username (mono), Role,
Sign-off ("Yes · default", "Yes · qualifications missing", "No", …),
Glass's (the login in mono or muted "Not configured", then **Manage
login**), State (Enabled/Disabled chip, plus **Password change required**),
and a **Settings** button per row.

### Create staff account dialog

Username and Temporary password; Cancel, **Create staff account**.

### Account settings dialog (`dialog--wide account-settings-dialog`)

Head: the username, Close. Body: a two-cell `fact-grid` (Username, State);
`account-signoff-fields` (a two-column grid): Role (a select, disabled for
the signed-in operator with a hidden input carrying the value), Sign-off
Engineer (No/Yes), Printed name, Qualifications (spanning both columns);
an `account-settings-section` **Glass's repair estimates** · "Manage this
account's login." · **Manage login**; the signature file input ("Signature
image" or "Replace signature"); the **Default sign-off Engineer** checkbox.
Foot: Cancel, **Save settings**. For another account, a second foot: left
Disable (danger) or Enable, **Delete** (danger); right Force logout and
Reset password (Enabled accounts only). Every action posts on the click;
Delete confirms first in a native `dialog`.

### Temporary password

After Reset password the page redisplays with a panel **Temporary
password** below the list: one sentence and the password in mono.

## Things the FRD does not settle

- How the dialog says the Role cannot be changed on one's own account.
- Whether Username and State belong in the body when the head already
  carries the name.
- How the signature's on-file state is shown, and where the temporary
  password appears.
