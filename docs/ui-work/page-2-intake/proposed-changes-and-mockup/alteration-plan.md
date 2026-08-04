# Page 2 — Intake → Inbox + Upload: alteration plan

Source: `src/Pegasus.Web/Pages/Intake/Index.cshtml`. Operator notes: `../page2.md`.
Screenshots reviewed: `page2.png`, `intake-queues.png`, `upload.png`,
`local-list-with-items.png`. Governing standards: `../../ui-standards-and-review.md`
(§2 vocabulary, §3.1 IA, §4 presentation rules).

## Review

### Aesthetics

The page opens with an eyebrow ("Intake"), an H1 ("Intake queue"), and a lede — *"Receive
instructions and review retained manual or approved-inbox receipts."* — three stacked heading
elements before any content, all in pipeline vocabulary. The dominant element is a full-width
red "Queue instruction" button attached to a bare file input: the page's most visually urgent
control is a manual-upload edge case, while the list — the thing operators actually use —
sits below it. List rows lead with the raw stored filename: today's build shows
`1A0F394ACDE76139C8D3742F250E56FEDCB8F3E2DFDFFE29996E168D8AA2CAF7.eml` in bold as the row
title. No human being can distinguish two rows of hex.

### Practicality

The operator's verdict is precise: *"Page 2 is a fundamental misread… It combines manual
uploading with what appears to be an inbox viewer. These should be separated."* And on the
filter row: *"We don't have intake queues — intake is automatic. Nothing needs to queue."*
Rows also fail the scanning test: an inbox is scanned by **who sent it and what it is about**,
but sender and subject are absent — only filename, timestamp, and outcome are shown. The
filters sit below the upload panel, so the list's controls are separated from the list.

### Performance, design and good practice

- **The upload button does nothing.** The shipped form renders `action=""` — the
  `asp-page-handler="ReceiveIntake"` URL is never generated — so submitting posts to a handler
  that does not exist and the page silently re-renders. No error, no receipt (verified against
  the live local build; standards §1.2). A dead primary action on a first-line screen is a
  release blocker.
- A 25 MB upload returns a raw browser "HTTP ERROR 400" page, not a designed
  size-limit message.
- *"Choose an email, document, PDF or image, up to 10 MB. Original bytes are retained before
  durable processing."* narrates storage internals at the operator.
- Success copy narrates plumbing: *"The instruction has been retained and queued for
  processing."*, *"The retained source was queued for policy re-evaluation."*
- Filter labels use internal vocabulary end to end: "Instruction drafts", "Blocked intake",
  "Document text required", "Image intakes"; the empty state is DB-speak ("No intake receipts
  match this view.").

## Changes

1. **Split into two surfaces, two nav items** (standards §3.1):
   - **Inbox** at the old route — the received-items viewer only.
   - **Upload** — a dedicated manual-submission page. The upload panel leaves Inbox entirely.
2. **Inbox heading**: drop the eyebrow and lede; H1 **"Inbox"** only.
3. **Inbox filters move to the top of the list** as chips with counts:
   All · **Ready to review** (was "Instruction drafts"/DraftReady) · **Needs sorting** ·
   **Blocked** (was "Blocked intake") · **Vehicle images** (was "Image intakes").
   "Document text required" → **Needs text extraction** (or the settled operator label — see
   Open questions).
4. **Inbox rows restructure**: sender · subject · received time · state chip. Raw stored
   filenames (hex `.eml` names) never render; where a manual upload has no sender/subject, show
   the original client filename and "Manual upload" as the sender. Attachment sizes, where
   shown, are MB to one decimal — never bytes.
5. **Inbox empty state** in business language: "No e-mail matches this view." (never "No
   intake receipts match this view.").
6. **Upload page**: H1 **"Upload"**, a real drop-zone ("Drag a file here, or browse") with
   helper line "E-mail, document, PDF or image — up to 10 MB". Button label **"Upload"**
   (never "Queue instruction"). Kill the copy *"Receive intake"*, *"Queue instruction"*, and
   *"Original bytes are retained before durable processing."* entirely.
7. **Design the four upload outcomes** as on-page states (no raw browser errors):
   - **Success**: "sample-instruction.pdf received — Ready to review", linking to the item.
   - **Duplicate**: "This file was already received on 3 Aug 2026. No duplicate was created."
     with a link to the existing item.
   - **Too large**: "This file is 24.8 MB. Files must be 10 MB or smaller." rendered inline,
     never a raw HTTP 400 page.
   - **Failure**: "The file could not be processed. Try again, or contact an administrator if
     it keeps failing."
8. **Fix the dead form**: the current build renders `action=""`, so the post never reaches
   `ReceiveIntake`. The redesigned Upload page must emit the correct handler URL (this is a
   defect fix the redesign depends on, listed under Dependencies).
9. **Status messages rewritten in business language**, e.g. "accepted" → "Case 26001 was
   created from this item."; "duplicate" → the duplicate copy in change 7; "resolved" →
   "The decision was recorded."
10. **Pagination copy** stays plain ("Previous · Page 2 of 5 · Next"); no "bounded view"
    phrasing anywhere.

## Dependencies

- **Sender/subject on list rows**: the list query must expose parsed e-mail sender and
  subject (today rows carry only `SourceFileName`, `ReceivedAtUtc`, outcome, failure reason).
  Parsed envelope data exists at review time; it needs surfacing in the list projection of the
  backing query.
- **Upload handler fix**: correct generation of the form's page-handler URL (the shipped
  markup posts to `action=""`); server-side size validation must return the designed inline
  message rather than the framework's 400.
- **Duplicate detection response** must return the existing item's reference and received
  date for the duplicate message.
- **Counts per filter chip** (Ready to review / Needs sorting / Blocked / Vehicle images)
  need cheap count queries if chips carry counts.
- Route: Inbox keeps the existing route with the old path redirecting if the slug changes;
  Upload gets its own page and nav item.

## Open questions

- Final operator label for the OCR-pending state: standards §2 leaves "Needs text extraction"
  vs a settled operator term open. Mockups use "Needs text extraction".
- Should Inbox rows show the destination case reference once an item is accepted, as a
  trailing column? (Mockups omit it; additive.)
