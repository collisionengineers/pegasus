# File map — MAIL-025

Lane B owns every file below; no other wave-2 ticket's path is touched.

## Core

- `src/Pegasus.Core/Intake/RetainedMail.cs` — keep dead agent's `UnreadOnly`/
  `OldestFirst` scope members and `CountAsync` on `IRetainedMailQueries` +
  `ListRetainedMail`; remove the caller-less
  `RetainedMailSummary.AttachmentFileNames`/`AttachmentNames` additions.
  Count/sort additions only, per ticket.

## Infrastructure (build fix forced by the owned interface change)

- `src/Pegasus.Infrastructure/Persistence/EfRetainedMailboxMessageStore.cs` —
  extract the ListAsync match filter into one private builder shared with the
  new `CountAsync`; apply `UnreadOnly` (`!IsRead`) and `OldestFirst`
  (ascending order) in `ListAsync`.

## Web

- `src/Pegasus.Web/Pages/Mail/Index.cshtml` — full port: page header
  (Inbox / Retained mail / freshness+Refresh), filter bar (Mailbox, Folder,
  Queue selects, search, Search dark; `data-auto-submit`), three panes
  (scope rail forms, messages with sort toggle + bounded pagination,
  server-rendered preview pane with fact grid and Open full message / Open
  linked Case links). Deleted-Items search results render in the messages
  pane.
- `src/Pegasus.Web/Pages/Mail/Index.cshtml.cs` — add `unread`, `sort`,
  `selected` bound query state; scope option table + per-scope counts via
  `ListRetainedMail.CountAsync`; preview view model via `GetRetainedMail`;
  keep `OnGetPreviewAsync` JSON handler (tested production caller for the
  site.js hover enhancement); refresh fields extended with the new state.
- `src/Pegasus.Web/Pages/Mail/Message.cshtml` — port to record/record-head/
  tabs/decision-card/timeline vocabulary; message tab `.split`, attachments
  table, thread rows, case tab association machinery re-skinned; correction
  and move dialogs moved to `dialog-*` classes with `data-dialog`; record bar
  omitted (wave 4).
- `src/Pegasus.Web/Pages/Mail/Message.cshtml.cs` — no handler changes;
  only presentation helpers touched if needed.

## Tests

- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` — implement
  `CountAsync` on the `Queries` fake; add authorization/normalization tests
  mirroring the List ones.
- `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` — update markup
  assertions to the ported pages (dt/dd decision rows → decision-row
  spans/strongs, mail-view label → Queue select, preview aside link-only
  pane, scope rail, sort/pagination classes); behaviour assertions
  (handlers, antiforgery, versions, reasons, notices) unchanged.
- `tests/Pegasus.IntegrationTests/Browser/MailWorkspaceBrowserTests.cs` —
  layout selectors updated to `pane-layout--3` panes; no-JS subject flow
  updated to select-then-open.

## Explicitly not touched

`wwwroot/css/site.css`, `wwwroot/js/site.js`, `Pages/Shared/*`,
`Presentation/OperatorLabels.cs` (PLAT-029 files); `MailBodyPresentation.cs`
and `MailClassificationSelection.cs` (owned but unchanged — reviewed, no
port-driven change needed).
