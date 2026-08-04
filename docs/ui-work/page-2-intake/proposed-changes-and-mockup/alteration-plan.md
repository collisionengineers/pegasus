# Pages 2 + 6 — Intake and Email operations → Inbox + Upload: alteration plan

Sources: `src/Pegasus.Web/Pages/Intake/Index.cshtml` and
`src/Pegasus.Web/Pages/Operations/Email.cshtml` (labels in the respective `.cshtml.cs`).
Operator notes: `../page2.md`. Screenshots reviewed: `page2.png`, `intake-queues.png`,
`upload.png`, `local-list-with-items.png`, `../../page-6-operations-email/operations-email.png`.
Governing standards: `../../ui-standards-and-review.md` (§2 vocabulary, §3.1 IA, §4 presentation
rules). Page 6's own review detail is retained at
`../../page-6-operations-email/review.md`; its alteration plan is superseded by this one.

## Why these are one page

Page 2 (Intake) and page 6 (Email operations) answer the same operator question — *what came in,
and did it work?* — on two screens that never reference each other. Page 2 lists received items
and their business state. Page 6 lists mailbox processing outcomes and their technical state.
An item that failed to process is invisible on page 2 and un-actionable anywhere else, so an
operator has to know that a second, undiscoverable screen exists to find out that this morning's
instruction never arrived. Splitting *arrival* from *did arrival work* is the split that made
page 6 an orphan (its only route in is a Dashboard card labelled "Unavailable").

They merge into one **Inbox** with a direction tab and a failure state. Nothing from page 6 is
dropped: mailbox identity, status chips, retry-with-confirmation, human failure sentences,
destination-labelled links, the truncation notice and the Sent direction all survive as parts of
the Inbox rather than as a separate console. **Upload** remains its own surface — that split
(operator: *"It combines manual uploading with what appears to be an inbox viewer. These should
be separated."*) is unaffected by this merge.

## Review

### Aesthetics

Page 2 opens with an eyebrow ("Intake"), an H1 ("Intake queue"), and a lede — *"Receive
instructions and review retained manual or approved-inbox receipts."* — three stacked heading
elements before any content, all in pipeline vocabulary. The dominant element is a full-width
red "Queue instruction" button attached to a bare file input: the page's most visually urgent
control is a manual-upload edge case, while the list — the thing operators actually use — sits
below it. List rows lead with the raw stored filename: today's build shows
`1A0F394ACDE76139C8D3742F250E56FEDCB8F3E2DFDFFE29996E168D8AA2CAF7.eml` in bold as the row
title. No human being can distinguish two rows of hex.

Page 6 stacks its own three-deep heading — "OPERATIONS" / "Email" / *"Bounded approved-mailbox
Received and Sent processing outcomes."* — whose lede is the worst sentence in the product:
"Bounded" and "processing outcomes" are compositional jargon narrated at an operator who wanted
to know whether e-mail worked today. Its default appearance is two empty grey apology panels.
When items exist each is a full-width `article.panel` with an `<h3>` like "Received · Failed" —
the section name repeated into every card heading, then a three-row `<dl>`. Five failures means
five stacked cards of near-identical chrome: no table, no density, no scanning axis. Timestamps
render `ToString("u")` (`2026-08-04 15:24:11Z`), inconsistent with `dd MMM yyyy HH:mm` elsewhere.

### Practicality

The operator's verdict on page 2 is precise: *"Page 2 is a fundamental misread… It combines
manual uploading with what appears to be an inbox viewer. These should be separated."* And on
the filter row: *"We don't have intake queues — intake is automatic. Nothing needs to queue."*
Rows also fail the scanning test: an inbox is scanned by **who sent it and what it is about**,
but sender and subject are absent — only filename, timestamp, and outcome are shown. The filters
sit below the upload panel, so the list's controls are separated from the list.

Page 6 is orphaned: the only route in is a Dashboard workspace card that itself reads
"Unavailable", so the screen cannot be discovered. Its links are labelled by mechanism rather
than destination ("Open Intake receipt", "Open Triage", "Open Intake queue"), with "Owner" as
the `<dt>` for that link — an outcome does not have an "owner" in any sense an operator uses.
Failure detail prints the raw `FailureCode` with no human explanation: a code token is the
entire failure story. "Retry Received processing" posts immediately on click with no
confirmation and no designed result state — after clicking, the button is still there looking
un-clicked. The truncation notice narrates internals and there is no way to reach older items at
all, so the page cannot answer "what failed last week", which is the one question a retry screen
exists to answer.

### Performance, design and good practice

- **The upload button does nothing.** The shipped form renders `action=""` — the
  `asp-page-handler="ReceiveIntake"` URL is never generated — so submitting posts to a handler
  that does not exist and the page silently re-renders. No error, no receipt (verified against
  the live local build; standards §1.2). A dead primary action on a first-line screen is a
  release blocker.
- A 25 MB upload returns a raw browser "HTTP ERROR 400" page, not a designed size-limit message.
- *"Choose an email, document, PDF or image, up to 10 MB. Original bytes are retained before
  durable processing."* narrates storage internals at the operator.
- Success copy narrates plumbing: *"The instruction has been retained and queued for
  processing."*, *"The retained source was queued for policy re-evaluation."*
- Filter labels use internal vocabulary end to end: "Instruction drafts", "Blocked intake",
  "Document text required", "Image intakes"; the empty state is DB-speak ("No intake receipts
  match this view.").
- Page 6's Received and Sent are two nearly identical 70-line copy-paste blocks differing only
  in link fallbacks and one Principal row. Merging them into one table with a direction tab
  halves the markup and stops the two drifting.
- Worth keeping from page 6: `<time datetime="…">` with ISO round-trip values, `role="status"`
  on the status card, `aria-labelledby` on sections, and the per-render `operationKey` +
  `expectedFailureCode`/`expectedDueAtUtc` guard pair on retry. The bones are fine; the
  vocabulary, density and discoverability are not.

## Changes

### A. Structure

1. **Two surfaces, two nav items** (standards §3.1):
   - **Inbox** at the old `/Intake` route — the received-and-sent viewer, absorbing page 6.
   - **Upload** — a dedicated manual-submission page. The upload panel leaves Inbox entirely.
   `/Operations/Email` redirects into Inbox; the "E-mail activity" Dashboard section links to
   the Inbox tab rather than to a separate console.
2. **Inbox heading**: drop the eyebrow and lede; H1 **"Inbox"** only. Page 6's "E-mail
   activity" title and its "Back to Dashboard" link both disappear with the separate page.
3. **Direction tabs at the top of the list**: **Received** · **Sent**. This replaces page 6's
   two stacked tables; the fallback-link differences that justified keeping them apart become a
   per-direction column definition, not a second table.

### B. The Received tab

4. **Filter chips with counts, below the direction tabs**:
   All · **Needs sorting** · **Blocked** (was "Blocked intake") · **Vehicle images** (was
   "Image intakes") · **Failed**. "Document text required" → **Needs text extraction** (or the
   settled operator label — see Open questions).
   **Failed is the chip page 6 existed to provide** — the direct answer to "did anything not
   come in?" — now one click from the list an operator already has open.
   The "Instruction drafts"/`DraftReady` chip is **removed, not relabelled**. Definitive
   authorised intake creates the case directly (`requirements.md:251`) and ambiguous or
   unidentified material is `Needs sorting` (`operator-notes.md:204`), so there is no
   pending-draft queue to filter. An earlier draft of this plan proposed **Ready to review** for
   this chip; withdrawn — `Review` is the Case stage before the report is with an Engineer and
   must never label an intake filter. See `../../defects-and-non-functional.md` §B4.
5. **Row shape**: sender · subject · received time · **mailbox** · state chip · action.
   - Raw stored filenames (hex `.eml` names) never render; where a manual upload has no
     sender/subject, show the original client filename and "Manual upload" as the sender.
   - **Mailbox** carries page 6's identity column. Its fallback is a muted **"Mailbox not
     recorded"** styled as secondary text, never masquerading as a mailbox name.
   - Attachment sizes, where shown, are MB to one decimal — never bytes.
6. **State chips** merge both pages' vocabularies into one scale, always word-plus-colour,
   never colour alone:
   **Case 26001** (green, linked to the case) · **Needs sorting** (amber) · **Blocked** (red) ·
   **Vehicle images** (neutral) · **Needs text extraction** (amber) · **Failed** (red) ·
   **Pending** (amber). "Succeeded" as a standalone state disappears — a succeeded item is
   described by what it became, which is one of the business states above. That is the merge's
   main simplification: page 6's technical status and page 2's business state were two names
   for one row.
7. **Failed rows carry the failure sentence and the retry**, both preserved from page 6:
   - A plain second line under the subject from a failure-label map — "The last message from
     this mailbox could not be processed." Raw `FailureCode` values never reach the page.
   - An inline **Retry** action in the row. First click swaps the action cell to a confirm —
     "Retry processing for this item?" with **Retry** / **Cancel**. After posting, the cell
     shows a green **"Retry scheduled"** chip and the action disappears; an already-scheduled
     replay renders identically. The existing replay-safe handler and its
     `expectedFailureCode`/`expectedDueAtUtc` guards are reused unchanged.
8. **Inbox empty states** in business language, per filter: "No e-mail matches this view."
   (All), "Nothing has failed to arrive." (Failed). Never "No intake receipts match this view."
   or "No Received processing outcomes are recorded."

### C. The Sent tab

9. **Sent keeps page 6's content** as one table: recipient · subject · sent time · mailbox ·
   state chip · where it went. The "Where it went" link is labelled by destination —
   **"Open case 26001"** — and its "Not linked" fallback becomes a muted em dash, not a
   sentence. Empty state: "Nothing has been sent recently."
10. **Sent retry visibility follows role** — see Open questions; the plan does not assume
    operators may retry Sent processing.

### D. Shared list behaviour

11. **Timestamps**: page 6's `u`-format UTC ("2026-08-04 15:24:11Z") → local
    **"04 Aug 2026 16:24"**, keeping the `<time datetime>` ISO attribute on both tabs.
12. **Truncation and depth**: the truncation notice becomes **"Showing the latest N items."**
    once, under the table. Because the merged list is paginated, the cap that stopped page 6
    answering "what failed last week" is lifted — pagination copy stays plain
    ("Previous · Page 2 of 5 · Next"), with no "bounded view" phrasing anywhere.
13. **Links labelled by destination throughout**: "Open Intake receipt" → the row itself is the
    link; "Open Triage" → **"Open queue item"** (Queues is the new surface name);
    "Open Case X" → **"Open case X"**.
14. **Status messages** (post-action TempData) render in the compact corner status pattern, not
    a full-width card, and in business language: "accepted" → "Case 26001 was created from this
    item."; "resolved" → "The decision was recorded."; the retry message keeps its current
    plain wording, which is already human.

### E. The Upload surface

15. **Upload page**: H1 **"Upload"**, a real drop-zone ("Drag a file here, or browse") with
    helper line "E-mail, document, PDF or image — up to 10 MB". Button label **"Upload"**
    (never "Queue instruction"). Kill the copy *"Receive intake"*, *"Queue instruction"*, and
    *"Original bytes are retained before durable processing."* entirely.
16. **The four upload outcomes** as on-page states (no raw browser errors):
    - **Success (definitive)**: "sample-instruction.pdf received — Case 26003 created", linking
      to the case.
    - **Success (not definitive)**: "sample-instruction.pdf received — Needs sorting", linking
      to the item.
    - **Duplicate**: "This file was already received on 3 Aug 2026. No duplicate was created."
      with a link to the existing item.
    - **Too large**: "This file is 24.8 MB. Files must be 10 MB or smaller." rendered inline,
      never a raw HTTP 400 page.
    - **Failure**: "The file could not be processed. Try again, or contact an administrator if
      it keeps failing."
17. **Fix the dead form**: the current build renders `action=""`, so the post never reaches
    `ReceiveIntake`. The redesigned Upload page must emit the correct handler URL (a defect fix
    the redesign depends on, listed under Dependencies).

## Dependencies

- **Sender/subject on list rows**: the list query must expose parsed e-mail sender and subject
  (today rows carry only `SourceFileName`, `ReceivedAtUtc`, outcome, failure reason). Parsed
  envelope data exists at review time; it needs surfacing in the list projection.
- **One merged list projection**: the Inbox query must return both the intake-receipt row data
  (page 2) and the mailbox processing state, mailbox identity, failure code and retry
  eligibility (page 6) in a single paginated projection per direction. Today these are two
  unrelated queries — `IIntakeReceiptQueries.ListAsync` and `GetEmailOperations` — and the merge
  is the substantive backend work in this plan.
- **Sent-direction rows** need the same projection shape for sent items, including the linked
  case where one exists.
- **A failure-code → operator-sentence label map** (Web-side; the pattern already exists for
  state labels in `Email.cshtml.cs:94`).
- **Retry needs no Core change**: the existing `RetryMailboxProcessing` handler, expected
  failure-code/due-time guards and replay behaviour are reused as-is; only its placement and
  confirmation UI change.
- **Upload handler fix**: correct generation of the form's page-handler URL (the shipped markup
  posts to `action=""`); server-side size validation must return the designed inline message
  rather than the framework's 400.
- **Duplicate detection response** must return the existing item's reference and received date
  for the duplicate message.
- **Counts per filter chip** (Needs sorting / Blocked / Vehicle images / Failed) need cheap
  count queries. Whatever replaces them must exclude items that already produced a case — the
  shipped `GetCountsAsync` counts every receipt ever, with no such filter
  (`EfIntakeReceiptStore.cs:152-164`).
- **Pagination over the merged list** replaces page 6's fixed cap; the cap constant and its
  truncation notice retire with it.
- Routes: Inbox keeps the existing route; Upload gets its own page and nav item;
  `/Operations/Email` redirects to Inbox with the Received tab and the Failed filter applied,
  so existing links land on what they were pointing at.

## Open questions

1. Final operator label for the OCR-pending state: standards §2 leaves "Needs text extraction"
   vs a settled operator term open. Mockups use "Needs text extraction".
2. Inbox rows carry the case reference as the state chip once the item has produced a case
   (mockups show "Case 26001", linked). Open: whether a separate trailing column adds anything
   over the chip.
3. Do operators ever need Sent retries, or is Sent retry an administrator action that should not
   render for the User role? (Carried from page 6; unresolved.)
4. Is the mailbox column worth a column on every row, or should it be secondary text under the
   sender for offices running few mailboxes? It matters once all four mailboxes are live
   (INT-05–07) and barely matters today with one.
5. Does the Failed filter need a date range now that pagination replaces the cap, or is
   "page back through Failed" sufficient?
