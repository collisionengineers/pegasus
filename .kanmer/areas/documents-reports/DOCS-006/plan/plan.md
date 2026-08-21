# Plan — DOCS-006

Branch `task/docs-006-instruction-images-evidence` from origin/dev, worktree
`../pegasus-worktrees/docs-006`, PR to dev after [[MAIL-006]]'s PR merges
(serial merges; files disjoint).

1. **Selection rule (Core, one owner):** `InstructionEvidenceImages.Select`
   over a receipt's asset records — Kind=`embedded_image` with
   `ContentLength >= 40_000`, plus Kind=`attachment` with an `image/*` media
   type; `inline_image` never; deduped by `ContentHash` keeping the first in
   file-name order. Both custody and the web query call it — the threshold
   lives in exactly one place.
2. **Custody:** `EfQueuedCustodyProcessor` promotes the selected embedded
   images after the attachments pass (attachments already land as files);
   ordinals continue the existing scheme; op key
   `{OperationKey}:embedded:{assetId:N}`; replay verifies via the existing
   default-interface path. Local and Box parity by construction (same
   `ICaseCustody` call).
3. **Asset endpoint:** `DownloadIntakeAsset` (Core) + `/Intake/Asset` page —
   receipt-scoped, image-only inline, nosniff/no-store, hash-verified.
4. **Evidence tab:** `ListEvidenceImagesForCaseAsync` feeds a thumbnail
   gallery on `Cases/Details`; `EvidenceCount` adds the image count.
5. **Tests:** unit facts on the selection rule (threshold boundary, dedupe,
   inline exclusion); custody end-to-end with the corpus images-PDF email
   (skip-if-absent); Box custody expectations; case web test for the gallery
   + count; asset endpoint tests (image served inline, non-image 404,
   foreign receipt 404).
6. Simplification pass over the branch diff before the PR; findings recorded
   here.

Sizing: ~6 source files + tests; no migration, no new grants, no new
top-level anything.

## Simplification pass — 2026-08-21

- Custody processor queried the receipt's assets twice (attachments, then
  attachments + embedded for selection); collapsed to one query split in
  memory. Applied.
- `_ImageGallery` gained a second concrete caller, so it generalized to a
  `GalleryImage(Href, FileName)` view model; all three call sites project
  into it and the partial no longer knows about image-intake records.
  Applied. (Razor lesson re-learned: nested double quotes inside a partial's
  `model` attribute silently mis-parse — projections live in code blocks.)
- The fixed-time hash comparer is shared from `DownloadIntakeSource` rather
  than copied into `DownloadIntakeAsset`. Applied.
- `AcceptAsync` test helper gained an optional expected-version instead of a
  second helper (the corpus email advances the receipt version before
  acceptance where the synthetic fixtures do not). Applied.
- Considered a Core port for the case→receipt-ids join; rejected — the query
  lives once on `EfIntakeReceiptStore` behind `ICaseEvidenceImageQueries`.
