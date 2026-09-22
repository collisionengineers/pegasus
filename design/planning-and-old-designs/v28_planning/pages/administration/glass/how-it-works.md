Read from the live source on 18 September 2026.

## What this page does not show

- The submitted password is never redisplayed: "No tag helper and no value
  attribute: the submitted secret is never written back into this field."
  (source comment). Only whether a credential is configured, its generation
  and version numbers, and when it last changed are shown.
- Generation, Version and Updated rows only appear once a credential is
  configured (`status.Configured`); Username only appears once
  `status.Username` is non-empty.
- Clear credential only appears once a credential is configured.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Glass/Index.cshtml` | The whole dialog: summary, edit form, foot actions |
| `Pages/Administration/Glass/Index.cshtml.cs` | `Status`, `Save`/`Clear` handlers |
| `Presentation/CaseWorkspaceLabels.cs` (`GlassCredential` class) | Every label on this dialog |

## Behaviours

### Summary and form together

There is no read-versus-edit switch: a summary `<dl>` — Account (the staff
username), and once configured Username, Generation, Version, Updated — sits
above an always-open save form, both shown at once. State chip in the
dialog head (`Model.StateName`).

### Save form

A required Username (pre-filled with the stored value once loaded) and a
required Password (`type="password"`, never pre-filled with the old value).
Foot: Clear credential (danger, left-aligned, only when configured), "Back
to account" (a GET back to `/Administration/Accounts/Index` with
`?editStaffId=<id>&expectedVersion=<n>`, reopening that account's settings
dialog), Save credential.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
