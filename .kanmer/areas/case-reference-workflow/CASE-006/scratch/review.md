## Independent review — PR #464 (orchestrator, 2026-08-20)

Verdict: **pass**, merge on green.

- The serving boundary is exactly right: a staff-only inline endpoint that renders ONLY parsed `image/*` media (everything else stays on the forced-download route, whose octet-stream pin is untouched), with `inline` disposition + `nosniff` + `private, no-store`. Tests pin content type, disposition, auth redirect, and the non-image refusal.
- One `_ImageGallery` partial serves both case kinds (Image-initiated page + case Details evidence tab): CSS-constrained lazy thumbnails, link-to-full-size (an accessible no-JS expansion), alt from original filename. Core `ListImagesAsync` shares INTK-014's group-member-resolution owner rather than duplicating it.
- Simplification pass recorded honest known costs (evidence-tab N+1; full-res thumbnails under no-store) and one open reviewer note (duplicate-source-asset 500 vs 409) — acceptable as recorded quality debts, not blockers.
- FRD-12 updated. Focused 73/73, browser+a11y 45/45, Core 753.
