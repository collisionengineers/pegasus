# File map — DOCS-011

Branch `task/docs-011-evidence-preview` off `origin/dev@a6acc782`.

## Changed

| File | Change | Why |
|---|---|---|
| `src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs` | Add optional `bool inline` parameter. When true **and** the media type is on the inline allowlist, set `Content-Disposition: inline` and return `File(content, mediaType)` with no filename. Otherwise unchanged. | The only route that force-downloads evidence. Additive by construction — the existing branch is the `else`. |
| `src/Pegasus.Web/Presentation/GalleryImage.cs` | `GalleryImage(string Href, string DownloadHref, string FileName, string MediaType)` — was `(Href, FileName)`. | Preview URL and download URL are now different values, and the viewer must choose `<img>` vs `<iframe>` vs fall-through. |
| `src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml` | Tile stays a real `<a href>` (no-script fallback) and gains `data-evidence-item`, `data-download-href`, `data-media-type`, `data-file-name`. `<ul>` gains `data-evidence-set`. **Remove the `empty-state` paragraph**; render nothing when the list is empty. | The trigger and its paging set. Empty-state panel is forbidden by `design/README.md:422`. |
| `src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml` | **New.** One overlay per page, `.reason-dialog-backdrop` + `data-evidence-viewer`, containing name, position, Download, Close, Previous, Next, an `<img>` and an `<iframe>`. | The viewer. Reuses the existing overlay class and contract. |
| `src/Pegasus.Web/wwwroot/js/site.js` | One new `[data-evidence-viewer]` block modelled on the `[data-reason-dialog]` block at 694-771: same focusable/open/close/Escape/Tab-wrap/focus-return, plus Previous/Next, ArrowLeft/ArrowRight, and swapping the source. | CSP forbids inline script; `site.js` is the only JS file. |
| `src/Pegasus.Web/wwwroot/css/site.css` | `.evidence-viewer*` rules beside the existing `.image-gallery` block. Reuses `--scrim`, `--panel`, `--border`, `--radius`, `--shadow-modal` and the existing `pegasus-spin` keyframes. | No new design tokens. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | Both galleries supply the new `GalleryImage` fields; evidence gallery adds `inline=true` to the preview URL for a case document. Render `_EvidenceViewer` once. | Two of the three call sites. |
| `src/Pegasus.Web/Pages/ImageIntake/Details.cshtml` | Supply the new fields; render `_EvidenceViewer` once; guard the Images section on `Count > 0` now that the partial no longer prints an empty state. | Third call site. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml` | **One line only** (the filename anchor, line 57): point it at `inline=true`, add the four `data-` attributes; `<tbody>` gains `data-evidence-set`. | "This is the same for documents." Kept to one anchor because DOCS-012/PR #532 rewrites this file. |

## Tests changed

| File | Change |
|---|---|
| `tests/Pegasus.IntegrationTests/QdosCustodialWebTests.cs` | **Strengthen** `CanonicalDownloadOwnerCallsCoreAndReturnsVerifiedSafeMetadata` to assert `DispositionType == "attachment"`, then add a case for `?inline=true` returning `inline`, and one proving a non-allowlisted media type stays `attachment` even with the flag. |
| `tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs` | Extend `ImageCasePageAndCaseEvidenceTabRenderTheGallery` to assert the trigger attributes and that the viewer partial renders. |

### Correction to the ticket brief, verified

The brief said `QdosCustodialWebTests` "asserts `Content-Disposition` attachment
on the plain route, so flipping the default breaks it". **Both halves are
wrong.** The request at line 116 already carries `?versionId={…}` — it is not
the plain route — and line 123 asserts only

```csharp
Assert.Contains("engineer-report.pdf", response.Content.Headers.ContentDisposition?.ToString(), StringComparison.Ordinal);
```

which never mentions `attachment`. The disposition **type** is currently
unpinned by any test in the suite. So the safety net the brief relied on does
not exist; an accidental default flip would have shipped green. This ticket
adds the missing assertion, which is what makes "additive" a checked claim
rather than an argued one.

## Read, not changed

`Pages/Intake/Image.cshtml.cs`, `Pages/Intake/Asset.cshtml.cs` (the inline
convention being copied), `Pages/Shared/_ReasonDialog.cshtml`,
`Pegasus.Core/Documents/DocumentContracts.cs`,
`Pegasus.Core/Intake/InstructionEvidenceImages.cs`, `Program.cs` (CSP),
`docs/design/README.md`.

## Deliberately not changed

- **Core, Infrastructure, any query or projection.** Media type is already on
  `CaseEvidenceImage` and `DocumentVersion`. This is presentation only — no
  migration, no port, no new Core owner.
- **`Pages/Intake/Image.cshtml.cs` / `Asset.cshtml.cs`.** Already inline.
- **`Pages/Cases/Details.cshtml:191`'s own `No vehicle images yet.`
  empty-state.** Same rule arguably condemns it, but it is outside this
  ticket's surface and sits in the region DOCS-012 is rewriting. Recorded in the
  simplification pass as a named non-application.
- **The dead native-`<dialog>` helper at `site.js:98`.** Deleting it is a
  tempting drive-by but is unrelated behaviour-changing scope; left alone.
