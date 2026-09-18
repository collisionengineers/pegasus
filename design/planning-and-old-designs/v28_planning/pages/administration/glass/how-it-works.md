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
| `Pages/Administration/Glass/Index.cshtml.cs` | `Status`, `Edit`/`Save`/`Clear`/`CancelEdit` handlers |
| `Presentation/CaseWorkspaceLabels.cs` (`GlassCredential` class) | Every label on this dialog |

## Behaviours

### Read-only summary

Account (the staff username), and once configured: Username, Generation,
Version, Updated. State chip in the dialog head
(`Model.StateName`). Foot: "Edit credential" (or "Take over" when another
window holds the edit) and "Back to account", which returns to the Accounts
row this credential belongs to.

### Editing

A required Username and a required Password (`type="password"`, no
autofill of the old value). Foot: Clear credential (danger, left-aligned,
only when configured), Cancel, Save credential.

## Things the FRD does not settle

- Nothing found while reading this page in isolation.
