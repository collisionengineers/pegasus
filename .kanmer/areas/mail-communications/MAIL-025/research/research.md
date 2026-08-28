# Research — MAIL-025

Contract: EPIC-011 `context.md` §1.3 and `docs/design/README.md` §Inbox,
§Component map, §No explanatory copy. Base: origin/dev 5ca2572c (PLAT-029
shell merged).

## Verified by read-only check

- `Pages/Mail/Index.cshtml(.cs)` renders legacy `.page-heading`, mailbox
  `.tabs`, folder `.subtabs`, two `.filterbar` GET forms (queue select with
  `Show view`, search), deleted-items table, a retained-messages `<table>`
  with `data-mail-preview-*` hooks, `<aside id="mail-quick-preview">` fed by
  `OnGetPreviewAsync` JSON, and a `.pager`. Query keys: `mailbox`, `folder`
  (`inbox|sent|deleted`), `queue`, `search`, `pageNumber`.
- `Pages/Mail/Message.cshtml(.cs)` (695 + 1032 lines): `.record` with
  `record__head`, legacy `.tabs` (`?section=`), `.split-main`, `.decision`
  / `.facts` cards, `.prov` provenance spans, queue-list thread, case tab
  with search / Confirm target / Link / Unlink flows, two legacy
  `.reason-dialog-backdrop` dialogs (`correctClassificationDialog`,
  `moveFolderDialog`) plus `_ReasonDialog` for link / unlink. Handlers
  PrepareLinkCase / PrepareUnlinkCase / LinkCase / UnlinkCase /
  CorrectClassification / MoveToRecommendedFolder carry version, lease and
  reason state through TempData — untouched by this port.
- Core `Intake/RetainedMail.cs`: `MailWorkspaceScope(MailboxId, Folder,
  SearchTerm, Destination, DetailedClassification)`; `IRetainedMailQueries`
  has `ListAsync / GetAsync / ListMailboxesAsync / ListPollHealthAsync`, no
  count. `RetainedMailSummary` carries `IsRead`, `AttachmentCount`,
  `Classification`, `OperationalDestination`, `CaseId/CaseReference`,
  `CurrentFolderType`, `Matches` — no attachment names.
  `RetainedMailDetail.Attachments` has names, media type, size, searchable.
- EF `EfRetainedMailboxMessageStore.ListAsync` builds the filter inline
  (folder scope, moved-out exclusion, mailbox, search, classification) then
  counts and pages, ordered `ReceivedAtUtc desc, Id desc`. The filter is
  the one thing a count needs, so it is extracted once.
- Destination policy: `Queries` = PostReportEmails + Billing/billing-query
  ("Case updates"); `Triage` = PreInstructionEmails/triage-request
  ("Pre-instructions"); `ReceivingWork` = NewInstructionReceived;
  `Unidentified` = not-classified outcomes. Sent Items = `folder=sent`.
- site.js (PLAT-029): `[data-row-list]` roving focus over
  `.row-button, .scope-button, tr[data-select-href]`; `[data-sort-toggle]`
  swaps ↓/↑; `[data-select-href]` rows swap their `<template>` into
  `[data-preview-target]`, set `aria-selected`, rewrite `?selected=`; the
  row's own `<a>`/`<button>` clicks navigate normally; `[data-dialog]`
  generalised dialogs (`data-dialog-open/close/dismiss`);
  `form[data-auto-submit]`; `[data-other-toggle]` kept.
- site.css (integrated block, lines < 851): `pane-layout--3`, `pane`,
  `pane-head`, `pane-body`, `pane-scroll`, `scope-list`, `scope-button`
  (`[aria-pressed="true"]`), `scope-visual-icon`, `row-button`
  (`[aria-selected="true"]`), `row-top/title/meta/excerpt/time`,
  `unread`, `unread-indicator`, `table-row-link`, `mail-preview`,
  `mail-header/subject/route/body`, `attachment-chip`, `decision-card/
  head/body/facts/row`, `timeline*`, `tabs/tab/tab-count`, `pagination`,
  `inbox-messages-head`, `sort-toggle`, `fact-grid/fact`,
  `definition-list/definition`, `record/record-head/accent/bar/body`,
  `filter-bar`, `searchbox`, `split`, `notice`, `button-row`. `.tab`
  styles both `aria-selected` and `aria-current`.
- `aria-selected` is valid only on `row/option/tab/gridcell/…`; the
  browser test asserts zero axe violations, so message rows render as
  `role="row"` inside `role="grid"` (the same reason Search uses `tr`).
- `_FreshnessBanner` renders `.freshness` and takes `RefreshFields`;
  `_StatusChip` renders `.status` with tone; `_ReasonDialog` is the
  `[data-dialog]` shape to copy for the two page dialogs.
- Attachment preview routes: `/Received/{receiptId}/Asset/{assetId}` serves
  `image/*` assets inline (`Intake/Asset`); `/Received/{id}/Source` downloads
  the retained source. `IntakeReceipt.AssetRecords` (already loaded on the
  message page as `AssociationReceipt`) carries `Kind == Attachment`,
  `FileName`, `MediaType`, `Id`.
- Tests: `MailWorkspaceWebTests` pins `data-mail-preview-*`,
  `handler=Preview`, `<aside id="mail-quick-preview"`, `section=`,
  `<h2>Decision</h2>`, `<dt>…</dt>` decision rows, `>Unlink</button>`,
  `Page 1 of 2`, one `method="post"` on viewers, sentences for empty /
  unavailable states. `MailWorkspaceBrowserTests` pins the JSON preview
  hooks and `.mail-workspace > .table-wrap` geometry.
- `tests/Pegasus.Core.Tests/Intake/RetainedMailTests.cs` has a private
  `Queries : IRetainedMailQueries` fake — adding an interface member breaks
  the solution build unless it gains the member (outside Owns; reported).
- Icons present in the sprite: inbox, mail, file-text, activity, clock,
  alert-circle, send, paperclip, arrow-up-down, external-link, eye,
  folder, check-circle, alert-triangle, user, info.

## Assumed

- The orchestrator runs tests, snapshots and browser tests; this ticket
  builds only.
- `OperatorLabels.cs` is outside Owns, so scope labels stay page-local in
  `IndexModel` beside the existing `FolderLabel`/`MatchLabel` maps (one list,
  one owner) — reported for a later fold.
