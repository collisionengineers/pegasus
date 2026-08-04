# Page 13 — Public upload (the requested-documents link) — review

**Capture note.** The only screenshot in this folder, `public-upload-404-local.png`, is a raw
Chrome error page: *"This localhost page can't be found — No web page was found for the web
address: http://localhost:5233/Uploads/example-token-not-issued — HTTP ERROR 404"*. That is not a
capture of the screen; it is the screen refusing to exist. The upload-link services are
composition-gated: `Program.cs:183-222` only builds `RequestUploadLimits` when
`Features:LocalDocumentCustody` (or the production profile) is set **and**
`DocumentRequests:AcceptedLimitsVersion` is present. Neither `appsettings.json` nor
`appsettings.Development.json` defines a `DocumentRequests` section, so the local DevelopmentOffline
build resolves `IGetRequestUpload` to `UnavailableDocumentRequestStore`
(`src/Pegasus.Infrastructure/Persistence/UnavailableDocumentRequestStore.cs:40-43`), which returns
`null` for every token. `OnGetAsync` then returns `NotFound()`
(`Request.cshtml.cs:44-46`) and the visitor gets the browser's own 404. **No token can be issued
locally and no state of this page can be photographed.** The review below is therefore written from
`src/Pegasus.Web/Pages/Uploads/Request.cshtml`, `Request.cshtml.cs`,
`src/Pegasus.Core/Documents/RequestUploadPolicy.cs` and `Pages/Shared/_Layout.cshtml`.

This matters more than for any other page in the set. This is the **only anonymous, externally
reachable screen in the product** (`[AllowAnonymous]`, `Request.cshtml.cs:8`). A claimant or third
party will see this screen and nothing else. It is currently the least designed screen in the
application.

## 1. Aesthetics

- **The page wears the internal application's chrome.** There is no `_ViewStart` override in
  `Pages/Uploads/`, so the anonymous visitor gets `_Layout.cshtml` in full: the primary nav renders
  "Operations", a disabled greyed-out "…unavailable" span, "Triage", "Cases", "Search" and a
  "Sign in" link (`_Layout.cshtml:35-65`). A claimant sent a link to hand over their own photographs
  is shown a staff navigation bar advertising internal work queues, one of which is visibly broken.
  The brand logo is wrapped in `<a asp-page="/Index" aria-label="Pegasus Operations">`
  (`_Layout.cshtml:31`) — clicking the logo, the most natural "go to the homepage" gesture, sends
  the external visitor to the staff dashboard and then to a sign-in wall.
- **The browser tab says `Upload requested documents · Pegasus`** (`_Layout.cshtml:23` +
  `Request.cshtml:4`) and the footer says *"Pegasus · Collision Engineers case management"*
  (`_Layout.cshtml:77`). The external audience is told the internal product name twice and the
  business name once, incidentally.
- **The whole screen is three stacked elements with no composition**: an eyebrow, an `<h1>`, a lede,
  then a panel containing a bare `<input type="file">` and a button. There is no card, no centring,
  no visual containment — the page inherits the full-width staff `app-shell` and leaves an
  ~800px-wide expanse of white beside a native file-picker button. It looks like a form scaffold
  someone forgot to design.
- **The file control is the operating system's default.** `<input asp-for="Upload" type="file"
  accept="@Model.AcceptedMediaTypes" />` (`Request.cshtml:30`) renders as "Choose File / No file
  chosen" in the platform's own styling. On the single screen where the business's competence is
  judged by a stranger, the most prominent control is unstyled browser furniture.
- **An eyebrow the standards ban.** `<p class="eyebrow">Secure document request</p>`
  (`Request.cshtml:9`) is exactly the kicker rule 7 removes, and it asserts security rather than
  demonstrating it.

## 2. Practicality

- **The lede talks about the system's storage, not the visitor's task.** *"Documents submitted here
  are retained directly in the requesting case. Do not upload material unrelated to the request you
  received."* (`Request.cshtml:11`). "The requesting case" leaks the internal record concept to
  someone who has never heard the word; "retained directly in" is storage narration; and the whole
  second sentence is a warning aimed at a person who has been given no list of what *was* requested.
- **The visitor is never told what to upload.** Nothing on the page names the requested documents.
  The heading is *"Upload requested documents"* and the section label is *"Choose a document"* —
  neither says which. The person holding the link must go back to their e-mail to find out.
- **The accepted file types are invisible until they are violated.** `AcceptedMediaTypes`
  (`Request.cshtml.cs:145-147`) is a comma-joined list of raw media types poured straight into the
  `accept` attribute. It is never rendered as human text. A visitor whose file dialog lets them pick
  anything gets, after uploading, *"This file type cannot be accepted. Choose one of the permitted
  document types."* (`Request.cshtml.cs:119`) — a message that declines to name a single permitted
  type.
- **The size limit can print in kilobytes.** `FormatBytes` (`Request.cshtml.cs:153-155`) renders
  `"{bytes / 1024} KB"` whenever the configured limit is not a whole number of megabytes, so
  *"Maximum file size: 1024 KB"* is a reachable string on the most public screen in the product.
  Rule 5 requires MB with one decimal.
- **One file at a time, with a full round trip each.** The model binds a single `IFormFile`
  (`Request.cshtml.cs:20`); there is no `multiple`, no drop zone, no chosen-file list, and no
  progress indication. A claimant sending six photographs performs six pick-submit-wait-read cycles
  on what is very likely a phone.
- **No client-side validation at all.** `Request.cshtml` renders no `Scripts` section and no
  validation-scripts partial, so `asp-validation-for` and the validation summary only ever appear
  after a completed POST. Every mistake — empty file, wrong type, too large — costs a full upload
  and a page reload.
- **"Success" is a grey strip above an unchanged form.** The accepted path sets TempData to *"Your
  document was received and retained securely."* and redirects (`Request.cshtml.cs:112-113`); the
  page then renders that string in a `status-card` above the same empty form
  (`Request.cshtml:15-18`). There is no confirmation screen, no name or size of what arrived, no
  time, no list of what has been received so far, and no statement of whether anything else is
  outstanding. The one moment where an anxious external user needs reassurance is a sentence in a
  grey box.
- **Every terminal state of the link is a raw browser 404.** Expired, revoked, exhausted, or issued
  under a superseded limits version — `EfDocumentRequestStore.cs:385-393` returns `null` for all of
  them, and the page returns `NotFound()`. There is no styled page and no wording. The person is
  told, by Chrome, that Collision Engineers' web address does not exist. This is the single worst
  failure on the screen; rule 6 forbids it outright.
- **Error copy exposes internal mechanics to strangers.** Verbatim: *"The upload operation is
  invalid. Reload the link and try again."* (`:62`), *"This upload operation was already used for
  different content. Reload the link and try again."* (`:125`), *"The document could not be
  retained. Try again using the same upload operation."* (`:140`). "Upload operation" is the
  idempotency key by another name; the last message instructs the visitor to reuse something they
  cannot see or control.
- **Rate-limit copy gives no waiting time.** *"Too many upload attempts were made. Wait before
  trying again."* (`:86`, `:116`) — the window is configured as
  `DocumentRequests:RateLimitWindowMinutes` and is perfectly printable, but the visitor is left
  guessing between one minute and one day.
- **The count and total limits are never surfaced.** `RequestUploadLimits` carries
  `MaximumFileCount` and `MaximumRequestBytes` (`RequestUploadPolicy.cs:85-89`), and hitting either
  produces the merged, unactionable *"This request has reached its document or size limit."*
  (`:122`). The visitor is never shown "3 of 5 documents received", so the limit only ever appears
  as a refusal.

## 3. Performance / Design / Good practice

- **Rule violations, by the book**: rule 1 (eyebrow + lede), rule 5 (a KB branch), rule 6 (no
  designed expired/failure state; raw 404), rule 7 (kicker heading stack), rule 9 (the internal nav
  and a disabled "unavailable" nav span rendered to an anonymous audience).
- **The whole file is buffered in memory before anything is validated for content.**
  `new MemoryStream((int)Upload!.Length)` then `content.ToArray()` (`Request.cshtml.cs:90-104`)
  holds two copies of the file in managed memory per request. It is capped by the configured
  per-file limit, so it is not a live risk — but on an anonymous endpoint with a per-token rate
  limit only, it is the wrong default; the stream should go to custody without the second copy.
- **The rate limiter is per-process and in-memory.** `RequestUploadAttemptLimiter`
  (`Request.cshtml.cs:165-224`) keeps a `Dictionary` behind a `lock`, pruned only once it exceeds
  1024 entries. Any second instance, restart, or scale-out multiplies the effective allowance. For
  the product's only anonymous write endpoint that is a thin defence, and it is not stated anywhere
  as a known limitation.
- **The link is the credential and the page treats it casually.** The token is a path segment
  (`@page "/Uploads/{token}"`), so it lands in browser history, in any `Referer` sent by a resource
  on the page, and in shoulder-view of a shared phone. No `Referrer-Policy` and no
  `<meta name="robots">` are set on this page. `ResponseCache(NoStore)` is set, which is right, and
  the token is stored only as a digest, which is also right — the presentation layer is the weak
  part, not the storage.
- **Accessibility is thin rather than wrong.** The label is associated and the status card carries
  `role="status"`, both correct. But the size limit sits in a loose `<p>` with no
  `aria-describedby` to the input, the permitted types are announced to nobody, and the visitor must
  tab through six irrelevant staff nav links before reaching the only control on the page.
- **No mobile consideration whatsoever.** This link is overwhelmingly opened on a phone, from an
  e-mail. The page is inherited desktop staff layout with a native file button; there is no camera
  affordance, no large touch target, and the nav bar consumes the first screenful.
- **Kept, because it is right**: anonymous access with no account requirement; the token digest
  model; per-attempt rate limiting; idempotent replay returning the same outcome as acceptance
  (`:110-113`); refusing unknown tokens rather than confirming or denying anything about them; and
  never showing the visitor any case, principal, or staff information. That last one is the page's
  best property and the redesign must not spend it.
