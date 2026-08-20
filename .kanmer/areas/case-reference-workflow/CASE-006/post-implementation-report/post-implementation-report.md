# CASE-006 post-implementation report

PR: https://github.com/collisionengineers/pegasus/pull/464 (task/case-006-image-viewing → dev). Commits: edceb77e (feature), 56a71a38 (FRD-12), f409cb7b (simplification pass), 67e3f63b (merge origin/dev). Branch was based on the INTK-014 branch (for the shared group-member resolver); INTK-014 merged to dev as #462 before this PR opened, so the PR is a clean delta on dev.

## Delivered vs plan

All plan steps delivered. Two refinements from the pass: `ImageIntakeImage` carries only `(ReceiptId, FileName)` (the tentative `MediaType` field had no caller), and the ordered receipt-id rule moved to a single shared owner (`ResolveOrderedImageReceiptIdsAsync`) used by both this query and INTK-014's custody loader.

## Behaviour summary

- `/Received/{id}/Image`: staff-only (actor factory + `PerformCasework` in Core, integrity hash check), serves ONLY a parsed `image/*` stored media type, inline disposition with safe filename, nosniff, `private, no-store`. Non-image, unknown, or foreign material → 404; anonymous → sign-in redirect; roleless staff → 403. The forced-download Source route is byte-for-byte unchanged (its retained-HTML security pin still green).
- Image-initiated Case page: "Images" panel gallery. Case Details Evidence tab: per-record blocks (reference link + registered time + gallery), same for instruction cases with paired image evidence. Thumbnails are the real image CSS-constrained + `loading="lazy"`; click expands full size in a plain navigation (accessible without script; alt = original filename).
- FRD-12 states the behaviour; design constraints respected (no inline styles — CSP invariant test green; no GUID exposure; no colour-only state).

## Evidence (exact counts)

Release build 0 warnings. New ImageViewingWebTests 2/2. Post-merge focused suites 73/73 (ImageViewing, ImageIntakeWeb, MultiFormatIntakeWeb incl. octet-stream pin, CaseDetailsWeb, ImageCaseCustody, ImageIntakePersistence). Browser + accessibility 45/45. Core.Tests 753/753.

## Known bounds (deliberate, recorded)

- Thumbnails fetch the full-resolution image (no derived thumbnail store) and every request re-verifies the content hash with `no-store` caching — correct and safe, but a 20-image gallery re-reads and re-hashes per view; a thumbnail derivation or validator-based caching policy is named follow-up work if galleries grow.
- Evidence tab queries images per associated intake (N+1); acceptable at current volumes, recorded in the plan.
- Image-typed *case documents* keep their existing download row (this slice covers vehicle-image galleries; document thumbnails were not requested).
- Reviewer note: a receipt with duplicate source assets would 500 the gallery query where the download path 409s — same invariant, different failure surface; flagged in the plan for review.
