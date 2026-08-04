# Page 13 — Public upload (the requested-documents link) — alteration plan

## Review summary

The product's only anonymous screen currently renders the staff navigation bar to claimants and
third parties, wears an eyebrow and a lede about where files are stored, offers a native
unstyled file button for one file at a time with no client-side validation, reports success as a
grey strip above an unchanged form, and answers every expired or revoked link with a raw browser
404. The security model underneath it is sound — token digests, rate limiting, idempotent replay,
and no leakage of case or principal information — and the redesign keeps all of it. What changes is
that this becomes a designed, self-contained, mobile-first page with the Collision Engineers mark,
no application navigation, plain claimant-facing wording, a real drop zone with a visible file
list, and worded success, failure, and expired-link states.

## Changes

1. **Remove the application navigation, footer and product name from this route.** Add
   `Pages/Uploads/_ViewStart.cshtml` selecting a new `_PublicLayout.cshtml`: the Collision Engineers
   mark only (not a link to `/Index`), no nav, no "Sign in", no "Pegasus" wordmark, and a one-line
   footer. Old `<title>` "Upload requested documents · Pegasus" → new **"Upload your documents ·
   Collision Engineers"**.
2. **Centred card on plain paper.** The full-width staff `app-shell` → a single ~560px card
   vertically centred on the page background, sized and spaced for a phone first. This is the whole
   layout; there is no second column and no panel stack.
3. **Heading and lede.** Old eyebrow *"Secure document request"* → **deleted** (rule 7). Old h1
   *"Upload requested documents"* → **"Upload the documents requested for your claim"**. Old lede
   *"Documents submitted here are retained directly in the requesting case. Do not upload material
   unrelated to the request you received."* → **"Collision Engineers asked for these documents by
   e-mail. Add them below and they will be sent straight to the person handling your claim."**
   No case reference, no principal name, no internal identifier of any kind appears on this page —
   that constraint is inherited from the current build and is deliberately preserved.
4. **State the rules before they are broken.** A single quiet line under the drop zone, populated
   from the configured limits: **"PDF, JPG, PNG or Word documents · up to 10.0 MB each · up to 5
   documents"**. `MaximumFileBytes` is rendered in MB to one decimal, always (the `KB` branch of
   `FormatBytes` is removed); `MaximumFileCount` is rendered as a number. The `accept` attribute
   keeps the media-type list; the human sentence is derived from a media-type → label map.
5. **A real drop zone with a file list.** Old single `<input type="file">` with the section label
   *"Choose a document"* → a labelled drop area — **"Drag your documents here, or choose files"** —
   wrapping a `multiple` file input, with a keyboard-reachable "Choose files" button. Chosen files
   appear as rows: name, size in MB, a per-file chip (**Ready** / **Too large** / **Not an accepted
   type** / **Uploaded**), and a Remove control. Files that fail a check are marked in the list
   before anything is sent.
6. **Client-side checks before the round trip.** Type, per-file size, and count are checked in the
   browser and shown against the offending row; the server checks stay exactly as they are and
   remain authoritative. Old button *"Upload document"* → **"Upload documents"**; while posting it
   reads **"Uploading…"** and is disabled.
7. **A designed success state, not a status strip.** Old *"Your document was received and retained
   securely."* rendered above the same empty form → a **success card**: green tick, **"Thank you —
   your documents have been received"**, the list of what arrived with its size and the time, and
   the line **"You can add more documents using the same link if you were asked for anything
   else."** with a secondary **"Add more documents"** button returning to the upload state. The
   running count ("2 of 5 documents received") comes from the request's recorded file count.
8. **A designed expired/revoked state replacing the raw 404.** All four dead-link outcomes (expired,
   revoked, exhausted, superseded limits version) resolve to one page, not `NotFound()`:
   **"This upload link is no longer active"** / **"Upload links stay open for a limited time.
   Reply to the e-mail that sent you this link and we will send a new one."** No distinction is
   drawn between the four causes — telling a stranger which one applies is an information leak with
   no benefit to them. Genuinely unknown tokens resolve to the same page for the same reason.
9. **Rewrite every error message in claimant language.**
   - *"The upload operation is invalid. Reload the link and try again."* → **"Something went wrong
     with this page. Reload it and try again."**
   - *"This upload operation was already used for different content. Reload the link and try
     again."* → **"These documents were already sent. Reload the page to add anything else."**
   - *"The document could not be retained. Try again using the same upload operation."* → **"We
     could not save your documents. Please try again."**
   - *"This file type cannot be accepted. Choose one of the permitted document types."* → **"We can
     accept PDF, JPG, PNG and Word documents."**
   - *"This request has reached its document or size limit."* → split into **"You have already sent
     the 5 documents we asked for."** and **"This document is larger than the 10.0 MB limit."**
   - *"Too many upload attempts were made. Wait before trying again."* → **"Too many attempts. Wait
     5 minutes and try again."** — the wait is printed from the configured window.
   - *"The selected document is empty."* → **"This file is empty."** shown against the file's row.
10. **Failure state, worded.** A save failure renders the card with a red-bordered message and the
    files still listed, so the visitor can retry without re-selecting anything.
11. **Reassurance instead of assertion.** The eyebrow's claim of security is replaced by one factual
    footer line: **"This link was created for you and is not shared. Collision Engineers ·
    collisionengineers.co.uk"**, plus **"Not expecting this? Reply to the e-mail you received."**
12. **Mobile and camera.** The drop zone doubles as a large touch target; on touch devices the file
    input offers the camera. Minimum 44px targets throughout; the card is full-bleed under 480px.
13. **Link hygiene at the page level.** `Referrer-Policy: no-referrer` and
    `<meta name="robots" content="noindex, nofollow">` on this route; the existing no-store response
    cache stays.

## Dependencies

Backend/plumbing work required before this page can ship as designed — plan only, none of it is
done here:

- **A composed local path for upload links.** Today the local build cannot issue a token at all
  (`DocumentRequests` is absent from both `appsettings` files), so no state of this page can be
  seen, tested, or reviewed. A development configuration that issues a token is a prerequisite for
  any visual QA of this work.
- **Return a page instead of `NotFound()` for dead links** (`Request.cshtml.cs:44-46, 55-57`) —
  a new `Expired` view state on the model, with the four causes collapsed to one message.
- **`MaximumFileCount`, `MaximumRequestBytes`, `RateLimitWindow` and the accepted media types
  exposed on `RequestUploadPublicView`** — it currently carries only `AllowedMediaTypes` and
  `MaximumFileBytes` (`RequestUploadPolicy.cs:306-308`), which is not enough to write any of the
  new copy.
- **A files-already-received count on the public view** for the "2 of 5 received" line and the
  success list. This must be scoped to the token and must not expose anything about the case.
- **Multi-file POST handling**: the model binds one `IFormFile`; accepting a list means a per-file
  decision result and a partial-success outcome ("3 received, 1 rejected") that the current
  single-decision return type cannot express.
- **A media-type → human label map** ("application/pdf" → "PDF") shared with the staff Upload page.
- **An MB formatting helper** (one decimal, never KB) shared app-wide — the same dependency page 2
  and page 12 raise.
- **A public layout** (`_PublicLayout.cshtml` + `Pages/Uploads/_ViewStart.cshtml`) and a small
  public-surface CSS scope; this is the first externally-facing layout in the product.
- **Client-side validation assets** on this route — currently no scripts are rendered at all.
- **Streaming upload** to replace the double in-memory buffer if the per-file limit is ever raised.

## Open questions

- **What is the real configured limit?** The mockups print 10.0 MB per file, 5 documents, and a
  7-day link lifetime because nothing in the repository configures `DocumentRequests` outside test
  fixtures. The operator must state the intended production values before this copy is final.
- **Should the page name the requested documents?** Listing "Repair invoice, photographs of the
  damage" would remove the biggest practical gap — but the request text is authored by staff and
  would be the first case-derived content ever shown to an external user. Proposal: allow a short
  staff-authored request note, reviewed at creation, and nothing else.
- **Should the recipient's name appear?** ("Documents requested from Sample Claimant".) It confirms
  the link reached the right person, but it puts a name on a page reachable by anyone holding the
  URL. Proposal: no.
- **Multi-file acceptance semantics**: is partial success acceptable (some files stored, some
  rejected), or must the batch be all-or-nothing? This is a policy decision, not a UI one.
- **Rate-limit wording**: printing the exact wait tells an attacker the window. Proposal: print it —
  the window is not secret and the honest message is worth more.
- **Does an expired link deserve a self-service route** ("Ask for a new link"), or must the visitor
  reply to the original e-mail? A self-service button implies an anonymous endpoint that triggers
  staff work; the plan currently assumes the e-mail reply.
