# CASE-006 research — viewable case images

## Verified facts (read-only checks, 2026-08-20)

- **The only image-bytes endpoint forces download**: `src/Pegasus.Web/Pages/Intake/Source.cshtml.cs` (route `/Received/{id}/Source`) serves every retained source as `application/octet-stream` attachment with nosniff. `tests/Pegasus.IntegrationTests/MultiFormatIntakeWebTests.cs:334` pins that behaviour deliberately (retained HTML must never render) — so inline viewing needs a NEW endpoint gated to `image/*`, not a change to Source.
- **`Cases/Documents/Download.cshtml.cs` already honours the stored media type** (validated via `MediaTypeHeaderValue.TryParse`, nosniff, `private, no-store`) — the convention the new endpoint follows.
- **No page renders `<img>` for case material.** `Pages/ImageIntake/Details.cshtml` is metadata-only; `Pages/Cases/Details.cshtml` evidence tab lists associated image intakes as a 3-column table (`ListForCaseAsync` already loaded in the page model — the case↔images join exists).
- **Bytes live in blob custody**: `IntakeAssets` (Kind=source) → `IIntakeArtifactStore` (pegcustody container in prod). `DownloadIntakeSource` (Core) authorises (`PerformCasework`), integrity-checks hash/length, and returns content — but hardcodes `ContentType = "application/octet-stream"`; the record's only consumers are the Source page (which ignores it and forces octet-stream anyway) and Core itself, so the record can carry the real stored media type without behaviour change.
- **Group images**: since INTK-015 the submission group is the registration unit; an image intake's images = origin receipt + registered group member receipts. INTK-014 (this lane's open PR #462) added the single owner of that resolution, `EfImageIntakeStore.ResolveGroupMemberReceiptsAsync` — this branch is therefore **based on `task/intk-014-image-case-box`** (merge order: PR #462 first; noted in both PRs).
- **CSP**: production `default-src 'self'`, no style-src — no inline styles (mechanically enforced by `AccessibilityTests`); all styling in `site.css`. Serving images from Pegasus's own authorised endpoint satisfies `img-src 'self'`.
- **Auth test harness**: `useIntegrationTestAuthentication: true` + `X-Test-Anonymous` header → challenge redirect to `/Account/SignIn`; `X-Test-Roleless` → 403 (pattern: `QdosIntakeWebTests.cs:105`).
- **Browser/a11y suites**: `tests/Pegasus.IntegrationTests/Browser/AccessibilityTests.cs` (axe + no-inline-style + landmark invariants over fixed routes) and the other Browser-category suites; record-id pages are covered by dedicated browser tests, not the fixed-route list.
- Box is the custody home, not the serving path (INTK-014); display must not proxy Box.

## Assumptions

- "Instruction cases with image evidence show the same gallery" is satisfied by the associated image intakes rendered on the case Details evidence tab — the same mechanism for both case kinds (`ListForCaseAsync` returns them for any case). Image-typed *case documents* keep their existing download row (out of this slice; noted in the report).
- Click-to-expand: the design-README-compatible, no-script, accessible form is a full-size open-in-place — each thumbnail is a link to the image endpoint itself (keyboard focusable, alt text from the original filename); no JS lightbox library is introduced.

## Design decisions

1. `IntakeSourceDownload.ContentType` carries the stored asset media type (was a hardcoded octet-stream literal). The Source page continues to force octet-stream — pinned test unchanged.
2. New page `Pages/Intake/Image.cshtml(.cs)`, route `/Received/{id:guid}/Image`: staff-authorised via the same `StaffActorFactory` + `DownloadIntakeSource` path as Source; serves ONLY `image/*` (anything else 404s), inline disposition, nosniff, `private, no-store`.
3. New Core read `IImageIntakeQueries.ListImagesAsync(imageIntakeId)` → ordered `(ReceiptId, FileName, MediaType)` for the intake's registered image receipts (origin + group members via `ResolveGroupMemberReceiptsAsync`), filtered to `image/*`. Interface default returns `[]` (same default-method convention the store interface already uses) so existing fakes keep compiling.
4. One shared gallery partial `Pages/Shared/_ImageGallery.cshtml` + `.image-gallery` CSS (grid of CSS-constrained, lazy-loaded thumbnails, each an `<a>` to the full-size endpoint) used by ImageIntake Details and the case Details evidence tab.
