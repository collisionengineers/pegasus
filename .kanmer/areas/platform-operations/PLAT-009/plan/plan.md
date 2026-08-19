## Plan

### 1. Layout restructure (original brief)

- `Mailboxes.cshtml` "Current policies" table: drop the `Update` column and the per-row `<form>` from the last `<td>`. The table becomes Address / Route scope / State (`_StatusChip`, unchanged) / Polling / Version only — normal 32px-class row height, no per-row form.
- Add a new section below the table, `Update policies` (rendered only when `Model.Mailboxes.Count > 0`), containing one `<div class="panel form-panel">` per mailbox. Each panel carries an `<h3>` naming the mailbox address and the panel is `aria-labelledby` that heading's id (`mailbox-update-heading-{id}`), giving one clearly associated, always-visible edit surface per mailbox — no `<details>`/JS toggle: `git grep` found no `data-toggle`/JS-driven show-hide precedent in `Pages`, and native `<details>` isn't warranted for what is normally a one-or-few-row estate, so a plain always-visible panel (the ticket's own suggested fallback) is simplest and consistent with `docs/design/README.md`.
- The form inside each panel is **byte-identical in field names, handler, hidden fields (`MailboxId`, `ExpectedVersion`, `OperationKey`), and validation** to the current per-row form — only its container moves from `<td>` to `<div class="panel form-panel">`, and the `operationKey` computation (`mailbox.Id == Model.MailboxId && Model.ExpectedVersion > 0 ? Model.OperationKey : Guid.NewGuid()...`) is preserved verbatim.
- Reuse the existing `panel`, `form-panel`, `role-choices role-choices--stacked`, `button-row`, `primary-action`, `field-hint` CSS classes already defined in `site.css` — no new classes, no inline styles.

### 2. Scope extension — strip narration (operator, mid-flight, citing `docs/design/README.md` line 160)

- Delete the `<aside class="notice">` lede banner under the page header. Its content ("no Exchange access... no mailbox browsing, message sending, credentials, rules or folder controls") duplicates `docs/runbook.md`'s "Approved mailbox estate" / "Runbook: admitting a new mailbox to the tenant" sections (verified present: "Approving a mailbox in Pegasus grants no Exchange access, and Pegasus cannot request or grant it", `Mail.Read` scope, `mailbox_access_denied` failure mode already documented there) — nothing is lost, so no runbook edit is needed.
- Collapse the Add panel's two `field-hint` paragraphs to **one sentence**, placed immediately before the identity fields it governs: "Identities cannot be changed once saved: disable this row and add a new one to move a mailbox." The Exchange-tenant-permission paragraph is deleted outright (covered by the runbook as above).
- Banned-word sweep (`docs/design/README.md` line ~404 list: intake, bounded, projection, lease, opaque, ingress, composed, artifact, durable, aggregate, queue, caller, correlation identifier, bytes) — this page only:
  - `Mailboxes.cshtml`: both `<span>Inbound Intake (Inbox)</span>` checkbox labels (row-edit panel and add panel) rename to `<span>New instructions and Triage mail (Inbox)</span>` — mirrors the existing parallel `SentEvidence` label ("Exact report and Triage evidence (Sent Items)"); the `name="SelectedRouteScopes" value="InboundIntake"` code value is untouched (a code identifier, not operator copy).
  - `Mailboxes.cshtml.cs`: `MissingMailboxIdentity` error text "...Inbox folder identity for inbound Intake and..." → "...Inbox folder identity for new instructions and...". No other banned words found in either file (`ApprovedMailboxRouteScope.InboundIntake` at line 206 is a C# enum reference, not rendered operator copy — excluded per the design doc's own carve-out).

### 3. Tests

- `dotnet build ./Pegasus.slnx -c Release`
- `dotnet test tests/Pegasus.IntegrationTests -c Release --filter "FullyQualifiedName~Mailbox|FullyQualifiedName~Administration"`
- `dotnet test tests/Pegasus.IntegrationTests -c Release --filter "Category=Browser"` (install Playwright chromium first if needed)
- Any test asserting the old table-cell-form DOM or the deleted banner/paragraph text gets its assertion updated to the new honest structure — logic/handler assertions stay as-is.

### 4. Visual proof

- Local run (`ASPNETCORE_ENVIRONMENT=Development`, DevelopmentOffline, local `PegasusPlat009` LocalDB) and screenshot `/Administration/Mailboxes` at 1920 and 1366, or an honest note if impractical plus Browser-suite screenshots as substitute.

### Reuse note

No new component, class, or pattern is introduced. The edit-panel-per-record shape reuses the exact `panel`/`form-panel`/`section-label` classes and `aria-labelledby` convention already used throughout Administration; the compact-table-with-separate-action convention seen in `Accounts/Index.cshtml`/`Organizations/Index.cshtml` (table row → external `Edit.cshtml`) was considered and **not** used, because splitting into a second routed page would exceed "layout only" (new route, duplicated `LoadAsync`/TempData plumbing) — the ticket's own same-page panel-per-mailbox suggestion stays truer to the stated constraint.

### Simplification pass

Recorded after implementation, dated, in this document.
