Read from the live source on 18 September 2026.

## What this page does not show

- No lede under any of the three panel heads.
- The mailbox settings dialog's Address field is read-only once the
  mailbox's identity is bound and it is not Disabled
  (`mailbox.IdentityIsBound && mailbox.State != Disabled`) — an approved,
  active mailbox's address cannot be edited out from under its identity.
- Refresh (re-resolve logical folders) only appears once the mailbox's
  identity is bound.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Administration/Mailboxes.cshtml` | Default sender panel, Approved mailboxes table, Mail categories table, all four dialogs |
| `Pages/Administration/Mailboxes.cshtml.cs` | `Mailboxes`, `Categories`, `PollStatusFor()`, `Update`/`SetDefault`/`SaveCategory`/`EditMailbox`/`EditCategory` handlers |
| `Presentation/OperatorLabels.cs` (`MailSettings` class) | Every label, `PollStatus()`, `SubscriptionStatus()`, `MailboxState()`, `CategoryState()` |
| `Presentation/OperatorLabels.cs` `RouteScope()` | The three route-scope sentences |
| `Core/Intake/Classification/MailLogicalFolderPolicy.cs` | The fourteen logical folder names |

## Behaviours

### Default sender

One line — the current default staff-send mailbox's address, or "Not
configured" — and a Change button that opens a dialog offering every
eligible staff-send mailbox in a select.

### Approved mailboxes

Mailbox, Used for (its route scopes, joined — "New instructions and Triage
mail (Inbox)", "Exact report and Triage evidence (Sent Items)", "Staff
send"), Last checked (a full sentence from `PollStatus()`: last completed
time, next due time, and any last-failure reason in plain words), State
chip, Settings. Add mailbox and the Settings dialog share the same fields:
Approved address, Route scope checkboxes, Verified encoded-message size
limit, State. The Settings dialog additionally shows Access and polling
(Activation, Subscription, Last success, Freshness, Last error) and Logical
folders — a definition list of all fourteen logical folder names, each
"Configured"/"Not configured".

### Mail categories

Category, State chip, and a Review `<details>` disclosure whose summary is
always visible and whose body is an editable Display name and State plus a
Save button (disabled until this row is the one currently being edited) —
Edit category / Cancel sit below the form itself, not inside its foot.

## Things the FRD does not settle

- The "Verified encoded-message size limit (bytes)" field label carries the
  banned word "bytes" in shipped copy — see this area's README Notes.
