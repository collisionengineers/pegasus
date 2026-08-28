# Plan — MAIL-025

Diff estimate: ~1,500 lines (two page rewrites ~900, models ~250, Core +
EF ~120, tests ~200, catalogue ~10). Build only (`dotnet build
./Pegasus.slnx --configuration Release`).

## 1. Core count/sort/unread (reuses `MailWorkspaceScope`, `ListRetainedMail` validation)

- `MailWorkspaceScope` + `UnreadOnly = false`, `OldestFirst = false`
  (trailing optional positionals — existing callers unchanged).
- `RetainedMailSummary` + `AttachmentFileNames` (optional; `AttachmentNames`
  accessor) so the row preview template can draw the attachment chips the
  contract and the browser test name. Reviewed deviation from "count/sort
  only": one record parameter, one EF projection.
- `IRetainedMailQueries.CountAsync(scope, ct)`; `ListRetainedMail.CountAsync`
  sharing the existing scope validation (extracted `Normalize(scope)`).
- EF: `ApplyScope(context, scope)` extracted from `ListAsync` (adds
  `UnreadOnly`), `CountAsync` = `ApplyScope().CountAsync`, ordering by
  `OldestFirst`, `SummaryRow.AttachmentFileNames` projected.
- Core test fake gains `CountAsync` (compile only).

## 2. Inbox list (reuses `_FreshnessBanner`, `_StatusChip`, `[data-select-href]`, `[data-row-list]`, `[data-sort-toggle]`, `form[data-auto-submit]`)

- Query: existing `mailbox/folder/queue/search/pageNumber` + `unread=1`,
  `sort=received_asc|received_desc`, `selected=<id>`. `RefreshFields`
  carries unread and sort.
- Scope list (page-local `Scopes` table in `IndexModel`, beside
  `FolderLabel`): All incoming (`/Inbox`), Unread (`unread=1`), Receiving
  work (`queue=receiving-work`), Case updates (`queue=queries`),
  Pre-instructions (`queue=triage`), Unidentified (`queue=unidentified`),
  Sent Items (`folder=sent`). Each carries the mailbox filter; counts come
  from seven `CountAsync` calls run with `Task.WhenAll` (one context each).
  Active scope → `aria-pressed="true"` (links carry `role="button"`).
- Filter bar: three `form[data-auto-submit]` in one `.filter-bar` —
  Mailbox (carries folder, queue, search), Folder (carries mailbox, search;
  drops queue because `folder=deleted&queue=…` is 404 by pinned contract),
  Queue + search + Search (dark) (carries mailbox, folder). Reason recorded:
  one form cannot drop the queue on the folder switch. No labels beyond the
  `<label>` word; no hints.
- Panes: `pane-layout--3` → Scope | Messages (`pane-head.inbox-messages-head`
  with `<h2>Messages</h2>` + sort toggle link `Received ↓/↑`; body
  `role="grid"` `[data-row-list]` of `role="row"` `.row-button`
  `[data-select-href]` rows: unread dot + sender + date/time, subject link
  (`table-row-link`, the no-script navigation), excerpt, outcome chip,
  case reference or queue label · n attachments, `<template>` with the
  preview; `.pagination` with `Page x of y` + Previous/Next bounded by
  `TotalPages`) | Preview (`pane-body mail-preview [data-preview-target]`
  rendering `_Preview` for the selected row, defaulting to the first).
- `_Preview` partial (model `MailPreviewView`): `mail-header` (subject,
  route), chip, excerpt, attachment chips, `fact-grid` Classification /
  Case association / Folder / Search match, `button-row` Open full message
  (dark) + Open linked Case. Built from `RetainedMailSummary` only, so the
  template and the server render are the same.
- Deleted-items search keeps its results table in the Messages pane
  (`pane-layout--2`, no preview). Empty / unavailable / validation states
  render as a `.notice` inside the Messages pane with the pinned sentences;
  the decorative mark and the retention paragraph go.
- `OnGetPreviewAsync` deleted (replaced by `?selected=`).

## 3. Message page (reuses existing handlers, `_ReasonDialog` shape, `_StatusChip`, `[data-dialog]`, `[data-other-toggle]`)

- `?section=` → `?tab=` everywhere (redirects, links, `ActiveTab`).
- Page header (eyebrow "Inbox message", h1 subject, Back to Inbox in
  `.page-actions`); `.record` > `.record-head` (sender, mailbox, received;
  end: Classified / outcome chip) > `.record-accent` > `.tabs[role=tablist]`
  (Message, Attachments `tab-count`, Thread, Case) > `.record-body`.
- Message tab: `.split` — left `mail-header` (subject, route) + `mail-body`
  (existing `MailBodyPresentation`); right `decision-card` (head
  "Decision"; `decision-row`s Classification, Destination, Filed to,
  Folder, Decided · provenance icon; `button-row` Correct classification /
  Move to X / Check move status) + `decision-card` "Corrections" with
  `.timeline`.
- Attachments tab: table File, Type, Size, Search content, Custody,
  Preview — Preview links `image/*` attachments to `/Received/{receipt}/
  Asset/{asset}` (matched by file name in `AssociationReceipt.AssetRecords`
  where `Kind == Attachment`), other attachments to the retained source
  download; no receipt → no link.
- Thread tab: `.row-button` links (`[data-row-list]`), current entry
  `aria-current="page"`.
- Case tab: linked → `definition-list` (Case/PO, Principal, Registration,
  Claimant, Claim number, State) + Open Case + Change association (existing
  PrepareUnlink post → Confirm unlink `_ReasonDialog`); unlinked → the
  existing search / results / Confirm target / Link flow on the new
  vocabulary.
- Dialogs `correctClassificationDialog` and `moveFolderDialog` become
  `.dialog-backdrop[data-dialog]` with `dialog-head/body/foot`.
- `OutsideListScope` and TempData notices render as `.notice`.

## 4. Tests and catalogue

- Web tests: preview pins → `?selected=` render (`[data-preview-target]`,
  same no-mutation and 403 checks, unknown id ignored); `section=` → `tab=`;
  decision pins → `<span>Classification</span>` / `<strong>…</strong>`;
  `>Unlink</button>` → `>Change association</button>`; `<h2>Decision</h2>` →
  `>Decision</h2>`; deleted-search and freshness pins unchanged.
- Browser test: row focus / click / Enter selects; `[data-preview-target]
  .mail-subject`, attachment chip, association fact; `aria-selected`;
  panes side-by-side at 1280, stacked at 640; no-script subject link.
- `catalogue.json` branch text; `pwsh ./scripts/Test-UiCatalogue.ps1`.

## Not in scope

Reply / Forward / Compose / Flag / Delete (MAIL-026), rail Inbox count
(wave 3), `OperatorLabels` fold of the scope labels, site.css / site.js.
