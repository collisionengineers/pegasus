# Research — MAIL-025 (Inbox list + message port)

Read-only checks run 2026-08-28 on `task/mail-025-inbox-port` after merging
`origin/dev` (clean merge, no conflicts, `Migrations/*` untouched by the merge).

## Verified premises

- **PLAT-029 vocabulary is on dev**: `src/Pegasus.Web/wwwroot/css/site.css`
  carries `pane-layout--3`, `pane/pane-head/pane-body/pane-scroll`, `scope-list`
  /`scope-button` (pressed style keyed on `[aria-pressed="true"]` only — no
  `.is-active` variant), `row-button/row-top/row-title/row-excerpt/row-time`,
  `unread`/`unread-indicator`, `inbox-messages-head .sort-toggle`, `pagination`,
  `mail-preview/mail-header/mail-subject/mail-route/mail-body`,
  `attachment-chip`, `fact-grid/fact`, `decision-card/decision-head/
  decision-body/decision-facts/decision-row`, `timeline/timeline-item`,
  `record/record-head/record-accent/record-bar/record-body/record-identity`,
  `tabs/tab/tab-count`, `dialog-backdrop/dialog/dialog-head/dialog-body/
  dialog-foot`, `_StatusChip`, `_ReasonDialog` (already restyled,
  `data-dialog` + legacy `data-reason-dialog` alias).
- **site.js contracts (PLAT-029 file, not editable)**: mail preview enhancement
  needs one `[data-mail-preview-workspace]` ancestor with rows
  `[data-mail-preview-row]`, a trigger descendant
  `[data-mail-preview-trigger][data-mail-preview-url]`, panel
  `[data-mail-preview]`, `[data-mail-preview-status]`, `[data-mail-preview-facts]`
  with fields `-sender|-subject|-received|-excerpt|-classification|-association|
  -attachments` (attachments filled as bare `<li>` into a `<ul>`).
  `form[data-auto-submit]` submits on change (INTK-022). Dialogs bind on
  `[data-dialog]`/`[data-reason-dialog]` with `[data-dialog-open]` invokers.
- **Scope → domain mapping** (`MailOperationalDestinationPolicy.Map` +
  `Query`): All incoming = Inbox folder, no queue; Unread = Inbox folder +
  `UnreadOnly`; Receiving work = `ReceivingWork`; Case updates = `Queries`
  (post-report + billing query families); Pre-instructions = `Triage`
  (`PreInstructionEmails`/triage-request); Unidentified = `Unidentified`;
  Sent Items = `MailFolderScope.Sent`. Scope labels are Inbox-scope concepts,
  not destination labels, so they live in the Mail page model beside the
  existing `AggregateViews`/`FolderLabel` precedent; destination labels in
  `OperatorLabels` are unchanged and keep their callers.
- **Prototype defects to avoid** (context §1.15): unbounded "Next" (real pager
  is bounded by TotalPages — keep); fixture prose ("Quick preview · evidence
  only", "Newest first · individual messages", empty-state explanations,
  `style=""` attributes) is not ported.
- **Record bar Reply/Forward/Compose/Flag/Delete**: ticket body says NOT
  rendered in this ticket (wave 4, MAIL-026); no named handler exists on
  `MessageModel` today → the whole record bar is omitted (an empty bar is
  worse than none). Same rule removes the attachments-table Preview column
  (no handler, not a D7 seam). Noted for report.
- **axe/ARIA**: `aria-pressed` is only valid on buttons (one existing use is a
  `<button>`); the scope rail therefore renders one small GET form per scope
  with a real `<button type="submit" class="scope-button">` (+ hidden
  mailbox/search carry-over), not links — this keeps the pressed styling and
  passes `aria-allowed-attr`.

## Dead-agent commit audit (`4a272967`, Core only)

- `MailWorkspaceScope`: **keep** `UnreadOnly`, `OldestFirst` (both get real
  callers: Unread scope + sort toggle).
- `IRetainedMailQueries.CountAsync` + `ListRetainedMail.CountAsync`: **keep**
  (scope-rail counts; reuses `Normalize` + `StaffAuthorization`), but the commit
  **breaks the build**: `EfRetainedMailboxMessageStore` does not implement
  `CountAsync`, and the Core test fake `RetainedMailTests.Queries` does not
  either. Both must be added.
- `RetainedMailSummary.AttachmentFileNames`/`AttachmentNames`: **remove** — no
  caller survives; the server-rendered preview pane and the JSON `Preview`
  handler both read attachment names from `RetainedMailDetail.Attachments`
  (already projected). A member with no caller is a smell, not scope.

## Interaction decisions (recorded for review)

- Rows: subject link = `?selected=` (server-rendered preview, the prototype's
  select-on-click); the pane's "Open full message" is the single full-detail
  entry. This replaces the old subject→message-page link; browser test
  `SubjectRemainsTheFullDetailLinkWithoutJavaScript` is updated to the new
  flow (select server-side, then open). The trigger keeps
  `data-mail-preview-url` so hover preview is unchanged.
- Preview pane: server-rendered for `?selected=` (auto-select first row when
  absent, prototype behaviour); links only (`<a class="btn">`) — the web test
  pins "no form/button inside the preview aside".
- Refresh: the `_FreshnessBanner` Refresh (page actions) is the one Refresh;
  the prototype's filter-bar duplicate is not rendered (one list per concept).
- Scope counts: one rule — each count is `CountAsync` of that scope with the
  page's current mailbox + search term applied (the count is what clicking the
  scope shows).

## Assumptions

- 7 count queries per list render is acceptable (the list already issues
  several; SQL COUNT over the same filter pipeline).
- Existing empty-state sentences (e.g. "No mail has been received.") are
  factual one-line values pinned by tests, not how-it-works copy — kept.
