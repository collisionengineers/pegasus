# Proof — CASE-006

Type: visual + command-log. Released in **release 14** (`d91fd7d7…`, PR #464), production smoke passed 2026-08-20; promoted to `main` (`39bb118a`).

- Live (signed-in browser pass): `/VehicleImages/AU17SEO-01` renders the thumbnail gallery with the real photograph; clicking it serves the full-resolution image (1536×2048) inline from `/Received/{id}/Image` — staff-only, `image/*`-only, `nosniff`, `no-store`.
- Verification lane at the cut: gallery on the case Evidence tab and the image-initiated page; shared ordered-receipt owner with the custody loader; integrity failure → 409.
- Disclosed bound: "progressive loading" is lazy-loaded full-resolution thumbnails; server-side thumbnail derivation is a named follow-up.
- Full transcript: DELIV-013 scratch.
