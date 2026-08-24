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
