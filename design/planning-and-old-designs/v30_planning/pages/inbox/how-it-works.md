# Inbox: how it works

Read from the live source on 25 September 2026 (`origin/dev` `32dabfc59`).

## What the page does not show

- Mail received before retention began ("Nothing here yet…" explains it in
  the empty state only).
- Sent Items and Deleted Items are not kept; those folders show a sentence,
  or a read-only search of the 100 newest Deleted Items.
- Which mailbox a row came from, except in the preview and the Deleted
  Items rows.

## Governing documentation

| Document | What it settles for this page |
| --- | --- |
| [FRD-20](../../../../../docs/frd/frd-20-mailbox-workspace.md) | Scopes, filters, the preview, Dismiss and Restore |
| [FRD-12 · Shell and routes](../../../../../docs/frd/frd-12-operator-experience.md#shell-and-routes) | `/Inbox` and `/Inbox/{id}`; the rail's Inbox count; date above time in a row |
| [Design authority](../../../../../docs/design/README.md#component-map) | `pane-layout`, `scope-list`, `row-button`, `mail-preview`, `status`, filters as dropdowns |

## Source by layer

| Layer | File | Owns |
| --- | --- | --- |
| Web | `Pages/Mail/Index.cshtml` | The three panes, the filter form, the rows, the preview, pagination |
| Web | `Pages/Mail/Index.cshtml.cs` | `ScopeDefinitions` (All incoming, Receiving work, Case updates, Pre-instructions, Unidentified, Sent Items, Dismissed), `AggregateViews`, `DetailedViews`, `SenderLine`, `SubjectLine`, `ForwarderLine`, Dismiss and Restore handlers |
| Web | `Pages/Mail/Message.cshtml.cs` | `OutcomeLabel` (Case created, Creating case, Case not created, Triage, …), `DecisionLabel`, `AssociationLabel` ("No case") |
| Web | `wwwroot/css/inbox.css` | 220px scope pane, 470px preview pane, the filter grid, the row's absolute Dismiss button |
| Web | `Shared/_FreshnessBanner`, `Shared/_RefreshButton` | "Current · HH:MM" and the one Refresh |
| Core | `Intake` (retained mail, classification, operational destinations) | What a row means |

## Behaviours

### Header

Eyebrow **Emails**, h1 **Inbox**. Actions: **Compose** (when staff mail is
available) and the freshness banner with Refresh first
(`FreshnessActionsFirst`), so the status text sits apart from the two
buttons.

### Scopes (left pane, 220px)

Seven `scope-button`s, each a GET form: icon well, label, count. The pressed
scope has the red left border and red icon well.

### Filter form

Mailbox, Folder (absent in the Dismissed scope), Category (disabled in
Deleted Items; two optgroups: Destinations and Categories), Search, and a
`btn btn--dark` **Search**. Auto-submits on change.

### Messages

Pane head: **Messages**, "N messages", and the sort toggle **Received ↓/↑**
(a text link toggling `sort=oldest`). Rows are `row-button inbox-row`, bold
when unread with the red `unread-indicator`: sender line with the date and
time stacked at the right; the subject as the link that selects the row
(`aria-current`); one-line excerpt; then the outcome chip, the
classification (and destination when there is no Case), the Case reference
link and "N attachments"; "Forwarded by …" and "Matched in: …" when they
apply; **No longer polled** when the mailbox is not polled. Each Inbox row
has an absolute icon button at its right: Dismiss (×) or Restore (undo).

### Preview (right pane, 470px, only with a selection)

**Message preview**: subject, "From … · date time · mailbox", the outcome
chip; the excerpt; the attachment names; a four-cell fact grid
(Classification, Case association, Folder, Search match); **Open full
message** (dark) and **Open linked Case**.

### Empty and unavailable

Five empty sentences depending on search, Dismissed, folder and retention
history. Deleted Items search unavailable and truncated states are
`notice`s.

### Pagination

"Page N of M" with Previous and Next, only when there is more than one page.

## Things the FRD does not settle

- The sort toggle draws a Unicode arrow, which the design authority's icon
  rule excludes.
- Where the Search button belongs when the search field is the wide one.
- Whether the row's Dismiss control should read as quiet as the row's meta.
