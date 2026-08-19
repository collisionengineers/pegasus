## Files touched

- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml` — layout restructure (table → compact data table + per-mailbox edit panels) and copy reduction.
- `src/Pegasus.Web/Pages/Administration/Mailboxes.cshtml.cs` — one banned-word ("Intake") fix in the `MissingMailboxIdentity` error string; no handler/model logic changes.
- Any Web/browser/accessibility test asserting the old table-cell-form DOM shape (to be located and updated to match the new honest structure, if any exist).

## Files read for convention

- `docs/design/README.md` — design authority (panel/table/notice conventions, no-lede rule at line 160, banned-word list, colour-never-alone rule).
- `docs/runbook.md` — "Approved mailbox estate" / "Runbook: admitting a new mailbox to the tenant" sections already carry the Exchange-tenant-permission and `mailbox_access_denied` material the UI banner/paragraph currently duplicates.
- `src/Pegasus.Web/Pages/Administration/Accounts/Index.cshtml`, `Accounts/Edit.cshtml`, `Organizations/Index.cshtml`, `Organizations/Edit.cshtml` — sibling Administration pages; establish the `panel`/`form-panel`/`section-label`/`field-hint`/`role-choices`/`button-row` conventions and the compact-table-with-linked-action pattern.
- `src/Pegasus.Web/Pages/Shared/_StatusChip.cshtml` — unchanged, reused as-is.

No other page is touched; the wider banned-word sweep is a separate ticket per the operator's scope note.
