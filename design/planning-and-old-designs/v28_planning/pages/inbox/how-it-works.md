Read from the live source on 18 September 2026.

## What this page does not show

- No lede or subtitle on the Inbox list, the Message record or Compose — the
  filter bar, the rows and the freshness element are the explanation
  (`Index.cshtml` line 22's own comment says this explicitly).
- No "Unread" scope on the scope rail: read state is not a queue, unread rows
  are shown bold instead (`IndexModel.cs` comment above `ScopeDefinitions`).
- No highlighting of matched search terms in a row; the row states only
  *which field* matched (`Matched in: Message body` / `Attachment name: …`),
  never the matched text itself.
- No public or external upload link, secure file request, or externally
  reachable upload URL anywhere on these three pages. PR #789
  ("remove-public-upload-links", merged) removed that feature; nothing in
  `Index.cshtml`, `Message.cshtml`, `Compose.cshtml` or their PageModels
  references it. See [`../upload/how-it-works.md`](../upload/how-it-works.md)
  for the same confirmation on the Upload side.
- No explanatory "what happens next" copy anywhere in Reply/Reply all/Forward
  or Compose — a field is a label and a control.

## Source table

| File | Owns |
| --- | --- |
| `Pages/Mail/Index.cshtml` | Scope rail, filter form, message rows, pagination, quick-preview pane |
| `Pages/Mail/Index.cshtml.cs` | List query, scope counts, `SenderLine`/`SubjectLine`/`ForwarderLine`/`MatchLabel`/`FolderLabel` helpers, `AggregateViews`/`DetailedViews` |
| `Pages/Mail/Message.cshtml` | Record head, tabs (Message/Attachments/Thread/Case), decision card, correspondence forms, classification/move dialogs |
| `Pages/Mail/Message.cshtml.cs` | Correspondence handlers (`Reply`/`ReplyAll`/`Forward`/`Reconcile`), classification correction, folder move, link/unlink Case, `AttachmentRows` |
| `Pages/Mail/Compose.cshtml` + `Shared/_ComposeForm.cshtml` | The standalone/dialog composer: case search, send form, attachments |
| `Pages/Mail/Compose.cshtml.cs` | `Send`/`SearchCase`/`SelectCase`/`Reconcile` handlers |
| `Pages/Shared/_StatusChip.cshtml` | Every chip's tone (Case created = green, Unidentified = amber, Unconfirmed = amber, etc.) |
| `Pages/Shared/_FreshnessBanner.cshtml` | The list header's Current/Stale/Unavailable dot and Refresh form |
| `Pages/Shared/_ReasonDialog.cshtml` | Link/Unlink Case's reason-and-consequence dialog |
| `Pages/Shared/_ErrorSummary.cshtml` | The composer's validation summary |
| `Presentation/OperatorLabels.cs` | Every fixed label transcribed here (`Inbox.*`, `StaffMail.State`, `QueryResponseJobs.*`, `AiJobs.*`, `MailOperationalDestinationLabel`, `MailClassification`) |
| `Presentation/MailClassificationSelection.cs` | The classification-correction select's option list and keys |
| `Presentation/UploadOutcome.cs` (shared) | Not used on these three pages; listed in the Upload folder |

## Behaviours

### The scope rail is a set of GET forms, not a client filter

Each scope button (`Index.cshtml` lines 57-72) is its own `<form
method="get">` carrying hidden fields for folder/mailbox/search/queue; a
click is a full navigation to the same page with a different query string.
`Dismissed` is not a folder — it is `Model.Dismissed`, a separate boolean
driven by `folder=dismissed`, and it changes the row action from `Dismiss`
to `Restore` (`IndexModel.cs`, `rowHandler`).

### Deleted Items is read-only and search-only

The Folder select's `Deleted Items` option runs `SearchDeletedMail`, which
only executes once a search term is present; with no term the panel would
show nothing to search. Three failure/limit states are drawn: `Unavailable`
(the read-only search itself failed), a truncation notice ("checked the 100
newest… older items were not scanned"), and the plain zero-match empty
state. Deleted rows have no Dismiss action, no case link, and no
classification chip — they are a distinct row shape (`Index.cshtml` lines
211-235), not the same row component with fields hidden.

### The quick-preview pane is a third column, not a modal

Selecting a row (`data-mail-preview-trigger`) sets `?selected=<id>`, which
server-renders the third pane and switches the section's own class from
`pane-layout--2` to `pane-layout--3` (`Index.cshtml` line 50). There is no
client-side preview fetch; the whole list page re-renders with the preview
attached, and `site.js` layers in an XHR-based progressive enhancement over
this same server contract (out of scope for a static capture, but the
server-rendered baseline is what this mockup transcribes).

### Message record: decision card, not editable fields

The record's own read model — Classification, Destination, Filed to, Folder,
Decided (with a Staff/Automatic provenance icon) — is drawn as a
`decision-card`, never as inline form fields; the only mutations from the
Message page are Correct classification (a dialog, always requiring a
reason), Move to `<recommended folder>` (a dialog, pre-filled with the
policy's own reason) and Check move status (recovers from an uncertain
provider write, `RetainedMailFolderMoveOutcome.Uncertain`).

### Reply/Reply all/Forward is inline, Compose is a separate page

`Message.cshtml`'s own `CorrespondenceMode` (`reply`/`reply-all`/`forward`)
renders a panel *inside* the record, posting to `Reply`/`ReplyAll`/`Forward`
handlers on the same page (lines 237-345). `/Mail/Compose` is a fully
separate PageModel reached from the Inbox list's Compose button
(`data-mail-compose-open`), which `mail-compose.js` opens as an overlay
without a full navigation. Both use the same `Shared/StaffMail` send-status
vocabulary (`Prepared`/`DraftCreating`/`DraftReady`/`Sending`/`Submitted`/
`Sent`/`Failed`/`Cancelled`/`Unknown`) and the same Reconcile recovery for
`Submitted`/`Unknown`.

### Unlink carries the one approved consequence sentence

`Message.cshtml` line 678 supplies `DialogConsequence` only when
`associationReceipt.UnlinkCancelsCase` is true, and the sentence is exactly
`Unlinking this email cancels case <reference>.` — the closed-list sentence
named in `CONTEXT.md`. The mockup's `unlinkCaseDialog` reproduces it
verbatim for the hero Case.

## Things the FRD does not settle

- The exact `MailOperationalDestinationPolicy` partition of `MailCategory`
  values into `DetailedClassification` versus the four other destinations
  was not re-derived while reading these three files in isolation (the
  policy class itself lives outside this page's source); the mockup's
  Category-filter "Categories" optgroup is therefore a representative
  subset, noted in the README.
