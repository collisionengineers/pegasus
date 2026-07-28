# P20 — PDF extraction

## Scope

Implement the complete declared [PDF 1.0–2.0 extraction surface](../../formats/pdf.md). The payload is ordered text and recoverable embedded/inline images only. Extraction records other observed features, extensions and profile claims as control evidence. Pixel rendering, OCR, writing, decryption, arbitrary attachment emission and active-feature execution are excluded.

## Owned units

- `EXT-PDF-001` standards, versions, extensions, profiles and detection.
- `EXT-PDF-002` bounded lexical/COS object model and exact spans.
- `EXT-PDF-003` core filter and predictor pipeline.
- `EXT-PDF-004` cross-reference/object resolution, revisions and linearisation.
- `EXT-PDF-005` media filters and encryption classification.
- `EXT-PDF-006` Catalog, pages, trees, resources and content programs.
- `EXT-PDF-007` fonts, CMaps, Unicode and positioned text.
- `EXT-PDF-008` Information/XMP, IDs and profile claims.
- `EXT-PDF-009` images, embedded/associated files and collections.
- `EXT-PDF-010` navigation, annotations, AcroForm and passive XFA.
- `EXT-PDF-011` tagged/logical/geometric order and optional content.
- `EXT-PDF-012` passive actions, JavaScript, multimedia and 3D.
- `EXT-PDF-013` signatures, byte ranges and revision forensics.
- `EXT-PDF-014` projection, recovery and all PDF acceptance evidence.

## Required outputs

- Validated classic and stream-based cross-reference traversal without unbounded recovery scans.
- Bounded filter pipelines and explicit unsupported-filter or malformed-stream issues.
- Deterministic page/content order with font/encoding uncertainty represented visibly.
- Discrete images plus text/image-only nested extraction; attachments and interactive/active features remain passive inventory without byte emission.
- Explicit encrypted, corrupt, partial, unsupported and resource-limit outcomes.

## Exit evidence

Specification-derived fixtures cover every declared version, syntax/filter/font route, structural profile and passive evidence surface. Independent semantic comparison, fuzz/security, decompression, incremental-update, cancellation and performance gates pass for the declared subset. Profile recognition is not profile conformance validation; no rendering result is required or claimed.
