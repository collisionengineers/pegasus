# Checklist — DOCS-011

## Implementation

- [ ] `Download.cshtml.cs`: additive `inline` flag + `image/*`/`application/pdf` allowlist
- [ ] `GalleryImage`: `Href`, `DownloadHref`, `FileName`, `MediaType`
- [ ] `_ImageGallery.cshtml`: trigger attributes, `data-evidence-set`, empty-state removed
- [ ] `_EvidenceViewer.cshtml`: new partial on the `.reason-dialog-backdrop` convention
- [ ] `site.js`: `[data-evidence-viewer]` block modelled on `[data-reason-dialog]`
- [ ] `site.css`: `.evidence-viewer*` reusing existing tokens and `pegasus-spin`
- [ ] `Cases/Details.cshtml`: both galleries updated, viewer rendered once
- [ ] `ImageIntake/Details.cshtml`: gallery updated, viewer rendered, section guarded
- [ ] `_CaseDocuments.cshtml`: one anchor + `data-evidence-set`, nothing else

## Boundaries held

- [ ] No new operator-facing sentence; copy limited to Previous / Next / Close / Download / filename / `n / m`
- [ ] No banned word (`intake`, `artifact`, `aggregate`, …) in rendered copy
- [ ] No inline `<script>`, no `blob:`, no `<embed>`/`<object>`
- [ ] No regex and no unbounded loop in the new JS
- [ ] Inline disposition allowlisted — a retained `text/html` document can never render inline
- [ ] No-script fallback: every trigger is still a real `href`
- [ ] Core/Infrastructure untouched; no migration, no new port

## Verification

- [ ] `dotnet build --configuration Release`
- [ ] `dotnet test tests/Pegasus.Core.Tests`
- [ ] `dotnet test tests/Pegasus.ArchitectureTests`
- [ ] `QdosCustodialWebTests` green, including the new attachment/inline assertions
- [ ] `ImageViewingWebTests` green
- [ ] Simplification pass run and recorded with dispositions in `plan`
- [ ] CI green on the pushed SHA (three shards — the authority)

## Deferred to verification, not claimed here

- [ ] Keyboard, screen-reader, forced-colours, reduced-motion, 1280+, constrained-desktop and 200%-zoom inspection, which `design/README.md` § Accessibility and acceptance requires for a UI capability. This branch does **not** carry that evidence.
- [ ] Rebase after DOCS-012 / PR #532 merges (`_CaseDocuments.cshtml` overlap).
