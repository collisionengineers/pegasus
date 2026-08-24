# Plan — DOCS-011

## Governing docs

- **`docs/design/README.md:1129` — Contracts § Evidence image preview.** This
  ticket **implements an existing specification**; it adds none. The row
  requires four things, met as follows:

  | Required by the contract | How this change meets it |
  |---|---|
  | "Loading … states are explicit" | The stage carries `aria-busy="true"` and an `is-loading` class from the moment a source is set until `load` (or `error`) fires; a CSS spinner reuses the existing `pegasus-spin` keyframes. Delivered **visually**, reusing the `aria-busy` + class convention already at `site.js:33-40`. No prose. |
  | "source-preserving enlarged-image states" | The enlarged view points at the *same authorised route* as the thumbnail. There is no derived, resized or re-encoded copy anywhere in the path. |
  | "opening or closing a preview preserves Case context" | The viewer is an overlay on the case page. No navigation, no history entry, no query-string change; the tab, scroll position and every unsaved edit survive. Closing returns focus to the tile that opened it. |
  | "does not alter source, category, advisory, or report-image selection" | The change is read-only: one additional `GET` disposition and client-side display. No command, no mutation, no form. |

- **`docs/design/README.md:422` — No explanatory copy and page economy.** Copy
  is `Previous`, `Next`, `Close`, `Download`, the filename and a bare `n / m`.
  Nothing else. The partial's existing empty-state paragraph is removed under
  the "a section with nothing recorded … is absent, not an empty-state panel"
  rule.
- **`docs/design/README.md:400` — closed necessary-copy list.** Not extended. No
  operator question arises: the one place a sentence was tempting (a
  "can't preview this type" notice) is answered by falling through to the
  existing download.
- **`docs/design/README.md:412` — banned words.** No new operator-facing string;
  in particular nothing renders the word `intake`.
- **`docs/design/README.md:1315` — Reason dialog contract** (initial focus,
  focus containment, Escape where safe, focus return). Inherited by copying the
  live `[data-reason-dialog]` block rather than writing a second contract.
- **`docs/frd/frd-05-documents-extraction-and-custody.md`** — custody content is
  reached only through the authorised case-document use case. The inline flag
  changes the *disposition*, never the authorisation: same `[Authorize]` roles,
  same `TryGetActor`, same `IDownloadCaseDocument` call, same
  `TryValidateResponse`.

## Steps

Each step names the existing code it reuses.

### 1 — Inline disposition on the case-document route *(additive)*

`Pages/Cases/Documents/Download.cshtml.cs`. Add `bool inline = false` to
`OnGetAsync`. After the existing `TryValidateResponse` and header block, branch
**only** at the return:

```csharp
if (inline && IsInlineSafe(mediaType))
{
    Response.Headers.ContentDisposition =
        new ContentDispositionHeaderValue("inline") { FileName = fileName }.ToString();
    return File(download.Content, mediaType);
}
return File(download.Content, mediaType, fileName);
```

*Reuses:* the disposition idiom from `Pages/Intake/Asset.cshtml.cs`
(`ContentDispositionHeaderValue("inline")` + `File(…)` with **no** filename
argument, so ASP.NET does not overwrite the header). Everything above the
branch — authorisation, actor, use case, validation, `nosniff`,
`X-Content-SHA256`, `ContentLength` — is untouched.

`IsInlineSafe` allowlists **`image/*` and `application/pdf` only**, parsed with
`MediaTypeHeaderValue.TryParse`. This is not caution for its own sake: a case
document is arbitrary operator-supplied content, and serving a retained
`text/html` file inline from the app origin would be a stored-XSS route. Both
receipt routes already gate the same way and say so in comments; this restates
the existing convention rather than inventing one. Anything not on the list
falls to the unchanged attachment return.

### 2 — `GalleryImage` carries preview, download and type

`Presentation/GalleryImage.cs` becomes
`(string Href, string DownloadHref, string FileName, string MediaType)`.
`Href` previews, `DownloadHref` downloads. They genuinely differ for a case
document (`?inline=true` vs the plain route) and coincide for the two receipt
routes, which are already inline — there the `download` attribute on the anchor
does the work.

### 3 — Gallery tiles become viewer triggers, keeping the no-script fallback

`Pages/Shared/_ImageGallery.cshtml`. The tile **stays an `<a href>`**, because
`site.js`'s own header states the house rule: *"Progressive enhancement only:
every behaviour here is a convenience on top of markup that already works
without it."* With no script the anchor still navigates to the inline preview,
which is what happens today. The script calls `preventDefault()` and opens the
overlay instead.

```html
<ul class="image-gallery" data-evidence-set>
  <li>
    <a href="@image.Href" data-evidence-item
       data-download-href="@image.DownloadHref"
       data-media-type="@image.MediaType"
       data-file-name="@image.FileName">
      <img src="@image.Href" alt="@image.FileName" loading="lazy" />
      <span class="image-gallery__name">@image.FileName</span>
    </a>
  </li>
</ul>
```

The preview URL is the anchor's own `href` — not duplicated into a `data-`
attribute. `alt="@image.FileName"` and `loading="lazy"` are preserved verbatim;
`ImageViewingWebTests.cs:131-132` assert on exactly those two literals.

Remove the `empty-state` paragraph; when the list is empty the partial renders
nothing.

### 4 — The document filename link becomes the same trigger

`Pages/Cases/Shared/_CaseDocuments.cshtml`, the anchor at line 57 plus
`data-evidence-set` on the `<tbody>`. **One anchor and one attribute — nothing
else in this file** because DOCS-012 (PR #532, open) rewrites it.
`asp-route-inline="true"` on the link; `data-download-href` is the plain route.
`version.MediaType` supplies the type. Paging then walks the document rows,
which is what "the same for documents" asks for.

### 5 — The viewer partial

`Pages/Shared/_EvidenceViewer.cshtml`, one instance per page, reusing
`class="reason-dialog-backdrop"` so it inherits the scrim, centring and
z-index with no new positioning CSS. Contains: the filename as the labelled
heading, a bare `n / m` position, a `Download` anchor (carrying the `download`
attribute), a `Close` button, `Previous`/`Next` buttons, and a stage holding one
`<img>` and one `<iframe>` (never both visible). `<iframe>` not `<embed>`/
`<object>` because CSP sets `object-src 'none'`.

### 6 — One `site.js` block

Appended beside the `[data-reason-dialog]` block and modelled on it line for
line: the `dataset.…Bound` idempotence guard, `focusable()`, `open(source)`
remembering the invoker, `close()` returning focus, and `onKeydown` with
`Escape` and Tab/Shift-Tab wrap. Added on top: `Previous`/`Next`,
`ArrowLeft`/`ArrowRight`, and `show(index)` which swaps the source.

The set is `trigger.closest('[data-evidence-set]')` → its
`[data-evidence-item]` elements, in document order. **The list is read from the
trigger elements themselves**, so no server-rendered duplicate data list and no
second source of truth about what the gallery contains.

Type routing, with **no regex** — this repo shipped a ReDoS one release ago and
a second was caught in review this week, so string operations here are
deliberately index-based:

```js
var type = (raw || '').split(';')[0].trim().toLowerCase();
if (type.indexOf('image/') === 0) { /* <img> */ }
else if (type === 'application/pdf') { /* <iframe> */ }
else { return; }   // not previewable: let the anchor navigate as it does today
```

`Previous`/`Next` are `disabled` at the ends rather than wrapping, so the
position value and the controls agree without a sentence explaining them.

### 7 — CSS

`.evidence-viewer*` rules beside `.image-gallery` in `site.css`, reusing
`--scrim`, `--panel`, `--border`, `--radius`, `--shadow-modal` and the existing
`pegasus-spin` keyframes. No new tokens, no new colours.

### 8 — Tests

- `QdosCustodialWebTests`: **strengthen** the existing assertion to pin
  `DispositionType == "attachment"` (it currently only checks the filename
  substring — see the file map), then add `?inline=true` → `inline`, and a
  non-allowlisted media type with the flag still → `attachment`.
- `ImageViewingWebTests`: assert the trigger `data-` attributes and that the
  viewer partial renders on the evidence tab.

## Acceptance

1. Clicking an evidence photograph opens the overlay in place; the case page,
   its tab and its scroll position are still behind it.
2. `Previous`/`Next` and ArrowLeft/ArrowRight page the set; position reads `n / m`;
   the controls disable at the ends.
3. `Download` saves the file under its own name.
4. `Escape`, `Close` and a scrim click all close it and return focus to the tile.
5. Clicking a PDF in the document table previews it inline in the overlay.
6. A non-previewable type still downloads — no error, no new copy.
7. The plain download route still answers `attachment`, proven by test.
8. With JavaScript disabled every link still resolves as it does today.

## Out of scope

Report-image selection (a future Engineers-screen surface per
`design/README.md`), any change to retention, EVA eligibility or custody, any
derived-thumbnail store, and any scripted PDF renderer.

## Simplification pass

*To be completed before the PR, under a dated heading.*

---

## Simplification pass — 2026-08-24

Run over this branch's own diff with the `code-simplifier` agent across the four
lenses (reuse, simplification, efficiency, altitude). Build re-run after every
edit: **succeeded, 0 warnings**.

### Applied — behaviour-preserving

1. **Dead CSS removed.** The new
   `@media (prefers-reduced-motion: reduce) { .evidence-viewer__stage.is-loading::after { animation-duration: 3s; } }`
   never applied: `site.css:1066` already carries a site-wide
   `*, *::before, *::after { animation-duration: .01ms !important; }` under the
   same query, and `!important` wins regardless of order. Under reduced motion
   the spinner is already frozen by the global rule and `aria-busy` carries the
   state — the existing convention.
2. **Four `WebApplicationFactory` instances collapsed to one** in the new
   custody test. The first draft built a factory per request (four LocalDB
   restores and four hosts for four GETs on one route); every other test in that
   file builds one. `RecordingDocumentHandlers` moved from constructor
   parameters to settable `MediaType`/`FileName` with the original defaults, so
   the pre-existing test is untouched. Real saving against a ~28-minute suite.
3. **Double ternary flattened** in `Cases/Details.cshtml`. The evidence
   projection allocated a shared `documentRoute` anonymous object (even for
   retained-asset images that never use it), then branched on `IsCaseDocument`
   twice and re-projected its three members into a fresh anonymous type just to
   add `inline = true`. Now one `if/else`, each arm assigning `preview` and
   `download`.
4. **`rows.ToDictionary(row => row.IntakeReceiptId, row => row)` →
   `rows.ToDictionary(row => row.IntakeReceiptId)`** — the single-argument
   overload is the identity case.

### Defects the pass found in this branch's own new code — all fixed

The pass was asked to report bugs rather than fix them. Four came back; all four
are mine and all four are now fixed, with the security one covered by a test.

- **Paging could land on a non-previewable row.** `data-evidence-item` is on
  *every* document-version link, and `open()` collected the whole
  `[data-evidence-set]`. The click guard was correct, but **Next/Previous could
  reach a `.docx`**, and `show()`'s `else` arm then set the hidden iframe's
  `src` — the server correctly refused to disposition it inline, so the browser
  **started a download the operator never asked for**, and the spinner never
  stopped because no `load` fired. *Fixed:* the paging set is now filtered to
  previewable items, so `show()` can only receive a kind it can render. This
  also makes the `n / m` count honest.
- **`image/svg+xml` passed the inline allowlist.** SVG is `image/*` but executes
  script when *navigated to*. DOCS-011 is what newly points the operator-facing
  document link at `?inline=true`, so with JavaScript off — or a middle-click —
  an operator would navigate to a same-origin inline SVG. For arbitrary
  operator-supplied case documents that is the same stored-XSS class the
  `text/html` exclusion already guards. *Fixed:* excluded on both sides
  (`IsInlineSafe` and `previewKind`, kept as one rule), with a regression test
  asserting an SVG stays `attachment` even with the flag.
- **No `error` listener on the iframe.** A PDF that failed to load left
  `aria-busy="true"` and the spinner running forever. *Fixed.*
- **Divergent focus-trap selector.** The viewer's `focusable()` omitted
  `input, select, textarea` while claiming to share the reason-dialog contract.
  Equivalent today (the viewer has no form controls) but a silent trap for
  whoever adds one. *Fixed:* identical selector string.

### Findings deliberately not applied

- **Extracting `focusablesIn` + `containTab` from the two overlay blocks.** A
  clean extraction genuinely exists — two pure functions, no options object, no
  callback hook, two concrete callers. **Not applied:** the repo's threshold is
  "a third copy is a stop condition"; two is where an abstraction becomes
  *permitted*, not required. Applying it also edits the shipped
  `[data-reason-dialog]` block — the focus trap behind every destructive-action
  confirmation — whose only regression proof is the ~28-minute suite, which is
  not proportionate for ~20 lines in a quality pass. **Recorded for the next
  person: the third overlay is the trigger; extract these two first.**
- **The inline-safe media-type rule now exists in two layers** (`IsInlineSafe`
  in C#, `previewKind` in JS). The rails call a second copy of a taxonomy
  duplication "even when it is just strings", and it is avoidable: the server
  could emit a computed `data-previewable` and the JS would read a boolean.
  **Not applied:** it restructures the design rather than tidying it, and moves
  a security-critical allowlist — not a job for a behaviour-preserving pass. The
  two agree today (I could construct no input where they differ) and both were
  changed together for the SVG fix. **The risk is drift, not a present defect**
  — worth a reviewer's opinion, and a follow-up ticket if they want it collapsed.
- **Duplicated retained-image projection** across `Cases/Details.cshtml` and
  `ImageIntake/Details.cshtml` — duplicated before this change too, but this
  change doubles its mass (3 lines each → 6). **Not applied:** removing it needs
  an `IUrlHelper`-consuming helper, and no such pattern exists anywhere under
  `Presentation/`. "The existing convention wins" points away from inventing one
  for a two-copy duplication.
- **`bool inline = false` sits after `CancellationToken`.** Against convention,
  but C# forces it — the token has no default. Fixing it means changing an
  existing parameter for no behavioural gain. **Not applied.**
- **Two defensive dead branches in `open()`** (`set ? … : [trigger]` and
  `start < 0 ? 0 : start`). Neither can fire today. **Not applied** — cheap
  insurance against a future partial that forgets `data-evidence-set`.
- **Per-trigger click listeners** rather than one delegated listener.
  **Not applied** — the reason-dialog block binds per-element the same way, N is
  small, and changing it is a convention change, not a simplification.
- **Two `Url.Page` calls per document row.** Deriving one from the other means
  string surgery on URLs. **Not applied**, deliberately.

### Checked, clean

**No regex in the new JS** — grepped for `RegExp`, `.match(`, `.test(`,
`.replace(` and regex literals; only comment-slash hits. `previewKind` is
index-based (`split(';')[0]`, `indexOf('image/') === 0`). No unbounded loop.
Given the ReDoS shipped one release ago and the second caught in review this
week, this was checked rather than assumed.

### Behaviour changes a reviewer should look at directly

- **The document filename link no longer downloads without JavaScript.** With JS
  off, clicking an image or PDF filename now previews in the tab where it
  previously saved. The *route* flag is additive; the *link* is not. This is
  what the operator asked for ("clicking should preview the document"), and with
  SVG excluded the inline set is inert — but it is a real change and is called
  out in the PR body rather than buried.
- **`data-evidence-set` on `<tbody>` includes historical and logically-removed
  versions**, so paging walks superseded revisions. Judged correct: the viewer
  pages through exactly what the table shows. Flagged for confirmation.
- **An image-initiated record with zero images now renders its heading and
  registration line with nothing beneath**, because the partial's empty-state
  paragraph is gone. Intentional under the no-empty-state rule; the heading and
  registration are themselves recorded content.
