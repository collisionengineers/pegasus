# CASE-006 plan

Branch `task/case-006-image-viewing`, worktree `../pegasus-worktrees/case-006`, **based on `task/intk-014-image-case-box`** (reuses `ResolveGroupMemberReceiptsAsync`; merge order: PR #462 merges first — noted in both PRs). One PR to dev.

## Steps

1. **Core media type honesty.** `DownloadIntakeSource` returns the stored asset media type in `IntakeSourceDownload.ContentType` (reuses the already-loaded `sourceAsset`); the Source page keeps forcing octet-stream, so the pinned retained-HTML security test is untouched.
2. **Core gallery read.** `ImageIntakeImage` record + `IImageIntakeQueries.ListImagesAsync` (default `[]`, matching the interface's existing default-method convention). `EfImageIntakeStore` implements it: ordered receipt ids from `ResolveGroupMemberReceiptsAsync` + origin, filtered to decision `image_intake_registered` and `image/*` source assets, projected to (ReceiptId, FileName, MediaType).
3. **Inline endpoint.** `Pages/Intake/Image.cshtml(.cs)` modelled line-for-line on `Source.cshtml.cs` (same actor factory, same `IDownloadIntakeSource`, same integrity 409) with the differences: media type must parse and be `image/*` else 404; `Content-Disposition: inline; filename=...` (Source's `SafeFileName` reused); nosniff + `private, no-store` (the `Download.cshtml.cs` cache convention).
4. **Gallery partial + CSS.** `Shared/_ImageGallery.cshtml` over `IReadOnlyList<ImageIntakeImage>`: grid of `<a href="/Received/{id}/Image">` wrapping `<img src same, alt = original filename, loading="lazy">` — click expands to full size in place (accessible without script; state never by colour alone). `.image-gallery` grid styles in `site.css` only.
5. **ImageIntake Details.** Page model loads `Images = ListImagesAsync(id)`; view renders an "Images" section with the partial (empty state sentence when none).
6. **Case Details.** Evidence tab: `ImagesByIntake` loaded only when `Tab == "evidence"`; each associated image intake renders a block — reference link + registered time + gallery — replacing the plain table (same data, now visual). Applies identically to image-initiated and instruction cases because `ListForCaseAsync` is case-kind-agnostic.
7. **Tests.** New `ImageViewingWebTests`: (a) registered image receipt → GET `/Received/{id}/Image` = 200, `Content-Type: image/png`, `Content-Disposition: inline`, nosniff, body byte-equal; (b) `.eml` receipt → 404; (c) `X-Test-Anonymous` → 302 to `/Account/SignIn`, `X-Test-Roleless` → 403 (harness pattern from `QdosIntakeWebTests`); (d) `/VehicleImages/{id}` page contains the gallery `<img>`; (e) case Details evidence tab contains it after association. Run browser + a11y suites and the pinned octet-stream test.
8. **Verify.** Release build zero warnings; focused suites + Browser category; simplification pass over the diff before PR.

## Acceptance
- Image-initiated case page and case detail evidence tab show thumbnail previews that click-expand full size, served only by authorised Pegasus endpoints returning true image media types inline.
- Non-image material can never render inline; anonymous access redirects to sign-in.
- Browser + accessibility suites green.

## Simplification pass — 2026-08-20

Lenses: reuse, simplification, efficiency, altitude (`code-simplifier` agent over commit `edceb77e`; applied in `f409cb7b`).

Applied:
- The ordered receipt-id set an Image intake covers now has ONE owner — `EfImageIntakeStore.ResolveOrderedImageReceiptIdsAsync` — shared by the INTK-014 custody payload loader and the new gallery query (removed the second copy of the origin-first-then-ordinal rule).
- `_ImageGallery` owns its empty state (the duplicated operator sentence at both call sites is gone) and builds its one URL once for both the link and the `<img>`.
- `ImageIntakeImage` dropped the `MediaType` component — no production caller consumed it (the endpoint re-derives the type from the stored asset).
- The SQL image filter cites `ImageIntakeLifecycleRules.ImageMediaTypePrefix` (the Core rule owner) instead of restating the string; the endpoint's defence-in-depth check now names that owner in a comment.
- `IntakeSourceDownload.ContentType`'s new meaning documented on the contract record (consumers read the record, not the implementation).
- Dead `.image-gallery li` CSS rule, the redundant `ImageIntakes.Count > 0` guard, a double dictionary lookup (now `TryGetValue` foreach), and a property-declaration reflow.

Not applied, with reasons:
- Evidence-tab N+1 (`ListImagesAsync` per intake, own DbContext each): a batched overload would be a new signature with one caller; recorded as a known cost — revisit if operators routinely fold many registrations into one case.
- Thumbnails are full-resolution fetches with a full integrity re-hash and `no-store` (20-image gallery = 20 reads+hashes per view): the no-derived-thumbnail choice is deliberate scope (CSS-constrained previews); a thumbnail derivation or validator-based caching policy is follow-up work, not a behaviour-preserving edit.
- `SourceModel.SafeFileName` vs Core's `SafeFileName` divergence is pre-existing and NOT behaviour-identical (the Web copy also strips Windows-invalid chars) — noted for a follow-up, no drive-by edit.
- Reviewer note: `rows.ToDictionary` in `ListImagesAsync` throws on a duplicate source asset per receipt — the same condition `DownloadIntakeSource` treats as an integrity fault; failure mode differs (500 vs 409). Flagged for review.
