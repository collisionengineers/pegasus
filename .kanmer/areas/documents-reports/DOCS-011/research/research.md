# Research — DOCS-011

Worktree `../pegasus-worktrees/docs-011-evidence-preview`, branch
`task/docs-011-evidence-preview`, from `origin/dev` at `a6acc782`.

Every premise below was **verified by reading the file on this branch** unless
it is marked *assumed*.

## 1. The ticket's original file path was wrong — corrected

*Verified.* The gallery is `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml`,
a global shared partial, not `Pages/Cases/Shared/_ImageGallery.cshtml` (which
does not exist). Three callers:

| Caller | Gallery | Preview URL today |
|---|---|---|
| `Pages/Cases/Details.cshtml:188` | Instruction photographs | `/Cases/{id}/Documents/{occ}/Download` when `IsCaseDocument`, else `/Received/{id}/Asset/{assetId}` |
| `Pages/Cases/Details.cshtml:223` | Vehicle images, per image-initiated record | `/Received/{id}/Image` |
| `Pages/ImageIntake/Details.cshtml:64` | Images | `/Received/{id}/Image` |

The ticket body has been corrected in place.

## 2. The ticket's claim about new tabs was wrong — corrected

*Verified.* `grep` for `target="_blank"` across `src/Pegasus.Web/Pages` finds
exactly one hit — `_CaseDocuments.cshtml:17`, the external **Box folder** link.
No evidence image or document link opens a new tab. What actually happens:

- `Pages/Intake/Image.cshtml.cs` and `Pages/Intake/Asset.cshtml.cs` both set
  `Response.Headers.ContentDisposition = new ContentDispositionHeaderValue("inline")`
  and return `File(bytes, contentType)` — so a click **navigates the same tab**
  to a bare image, losing the case page.
- `Pages/Cases/Documents/Download.cshtml.cs:56` returns
  `File(download.Content, mediaType, fileName)`. Passing a filename makes
  ASP.NET emit `Content-Disposition: attachment`, so that route **downloads**.

Since DOCS-007, an instruction photograph registered as a case document takes
the third path. **So the operator's "it should not just download" is the
download route, and it bites photographs as well as documents.** Both halves of
the complaint are real; the diagnosis in the ticket was not.

## 3. There is exactly one live overlay convention — reuse it

*Verified.* `Pages/Shared/_ReasonDialog.cshtml` is a `div`-backdrop overlay
(`class="reason-dialog-backdrop"`, `role="dialog"`, `aria-modal="true"`,
`data-reason-dialog`, `hidden`) driven entirely from
`wwwroot/js/site.js:694-771`. That block already implements, with no inline
script:

- open from any `[data-dialog-open="<id>"]` control, remembering the invoker;
- close on `[data-dialog-dismiss]`, on a backdrop click, and on `Escape`;
- focus containment with Tab/Shift-Tab wrap over a computed `focusable()` set;
- focus return to the invoking control on close;
- an idempotence guard (`dialog.dataset.dialogBound`).

CSS exists: `.reason-dialog-backdrop` (fixed, `inset: 0`, `background: var(--scrim)`,
`z-index: 90`, `display:grid; place-items:center`) and `.reason-dialog` at
`site.css:981-1000`.

*Verified:* a second, older native-`<dialog>` helper sits at `site.js:98-99`
(`[data-dialog-open]` → `getElementById` → `showModal`) and has **no markup
callers** — no `.cshtml` renders a `<dialog>` element. It is dead and must not
be extended. The `[data-reason-dialog]` block is the live contract.

**Decision: the viewer is a third `.reason-dialog-backdrop`-class overlay with
its own `data-evidence-viewer` block modelled line-for-line on that code.** It
is not a generalised "dialog framework" — see the plan's simplification note.

## 4. No existing preview surface

*Verified.* No `<iframe>`, `<embed>` or `<object>` in any `.cshtml` under
`src/Pegasus.Web`. There is no lightbox, carousel or viewer to reuse. This is
genuinely new markup.

## 5. CSP is binding, and it constrains the design

*Verified* at `src/Pegasus.Web/Program.cs:754-765` — applied when the
environment is **not** Development:

```
default-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'
```

plus `X-Content-Type-Options: nosniff`. Consequences, each of which the plan
obeys:

- **No inline `<script>`** — no nonce, no hash allowance. `site.js` says so in
  its own header comment. All behaviour goes in `site.js`, driven by `data-`
  attributes.
- **No `blob:`** — `default-src 'self'` excludes it, so a
  fetch → `URL.createObjectURL` preview is blocked. `img.src` and `iframe.src`
  must point at the same-origin authorised route directly.
- **`object-src 'none'`** kills `<embed>` and `<object>`. PDF preview must be an
  `<iframe>`.
- `frame-src` is **not** declared, so it falls back to `default-src 'self'` — a
  same-origin `<iframe src="/Cases/…?inline=true">` is permitted. *Verified by
  reading the header string; the fallback is the CSP spec's documented
  behaviour.*
- `frame-ancestors 'none'` restricts who may frame **us**, not what we frame. It
  does not block this.

## 6. Serving custody content inline is a security decision, not a formatting one

*Verified* that both receipt routes already gate on media type before serving
inline, each with an explicit comment calling it defence-in-depth:

> *"Only a true `image/*` media type is ever rendered inline — everything else
> stays off this route, so retained HTML or scripts can never execute from this
> origin."*

A case document is arbitrary operator-supplied content. Serving it inline from
the app origin with no type gate would be a stored-XSS route (a retained
`text/html` document rendering as same-origin script). **The inline flag must
therefore carry the same allowlist**: `image/*` and `application/pdf` only,
everything else keeps the existing attachment disposition. `nosniff` is already
set on the route. This mirrors the existing convention rather than inventing a
rule.

## 7. Media type is already available at both call sites

*Verified.*

- `Pegasus.Core.Intake.CaseEvidenceImage` (`InstructionEvidenceImages.cs:99`)
  carries `string MediaType`, plus `OccurrenceId`/`VersionId` and the derived
  `IsCaseDocument`.
- `Pegasus.Core.Documents.DocumentVersion` (`DocumentContracts.cs:32`) carries
  `string MediaType`.

So no query, projection or Core change is needed to decide `<img>` vs
`<iframe>` vs fall-through. **This was the main risk to the design and it is
clear.**

## 8. `Presentation/GalleryImage.cs` is too small for the job

*Verified.* It is `sealed record GalleryImage(string Href, string FileName)` —
one URL. Preview URL and download URL are now different things (a case document
needs `?inline=true` for one and the plain route for the other), and the viewer
needs the media type. The record gains `DownloadHref` and `MediaType`.

## 9. The design authority already specifies this surface

*Verified* — `docs/design/README.md:1129`, in the **Contracts** table:

> | Evidence image preview | Loading and source-preserving enlarged-image states are explicit; opening or closing a preview preserves Case context and does not alter source, category, advisory, or report-image selection. |

**This ticket implements an existing specification; it does not add one.** How
each clause is met is in the plan's governing-docs section.

Also verified and binding:

- `docs/design/README.md:422` **No explanatory copy and page economy**, operator
  direction 2026-08-20, "review rules with the same force as the banned-words
  list — a change violating one does not merge". Including: *"In read-only view,
  a section with nothing recorded and no available action is absent — not an
  empty-state panel."*
- `docs/design/README.md:400` the **closed** necessary-copy list — four
  sentences, none of them about previews.
- `docs/design/README.md:412` the banned-word list, which includes **`intake`**.
  New operator-facing copy must not use it. (Existing headings say "Vehicle
  images", which is already compliant.)
- `docs/design/README.md:1315` **Reason dialog** contract — initial focus, focus
  containment, Escape where safe, focus return. The viewer inherits all of it by
  reusing the block.

`_ImageGallery.cshtml` currently renders
`<p class="empty-state">No images are available to display for this record.</p>`,
which is exactly the empty-state panel that rule forbids. It goes.

## 10. Copy budget

Everything the viewer needs is already safe: `Previous`, `Next`, `Close`,
`Download`, the filename, and a bare position value (`3 / 12`). **No new
sentence is required and none is written.** The design's "loading … states are
explicit" is delivered *visually*, reusing the `aria-busy` + `is-…`-class
convention `site.js` already uses for manual refresh feedback
(`site.js:33-40`), not as prose. Non-previewable types fall through to the
existing download rather than saying so — which is both the simplest behaviour
and the only one that needs no unapproved copy.

**No operator question arises from this ticket.** That is a finding, not an
omission: the one place a sentence was tempting (an unsupported-file-type
notice) is answered by falling through to the download instead.

## 11. Collision with DOCS-012

*Verified* by `gh pr view 532`: DOCS-012 is **open, not merged**, on
`task/docs-012-evidence-files`, and rewrites
`Pages/Cases/Shared/_CaseDocuments.cshtml` and removes `DocumentSemanticRoles`
from `Pages/Cases/Details.cshtml.cs`. This branch is off `dev` and does not see
those changes. The document filename link (`_CaseDocuments.cshtml:57`) is the
one line both touch, so this ticket's edit to that file is deliberately kept to
that single anchor. A rebase after #532 merges is expected and is called out in
the PR body.

## Premises assumed, not verified

- That a browser honours the `download` attribute on a same-origin anchor. This
  is standard behaviour and same-origin is *verified*; the attribute's effect
  itself is not machine-checked by the test suite.
- Native PDF rendering inside an `<iframe>` depends on the operator's browser
  having a built-in PDF viewer. Chrome and Edge do. There is no scripted PDF
  renderer in this repo and adding one is out of scope.
- Visual/keyboard inspection at 1280+, constrained desktop and 200% zoom is
  recorded by the design authority as required acceptance evidence for a UI
  capability. This branch does not carry that evidence; it is a verification
  step, noted in the checklist.
