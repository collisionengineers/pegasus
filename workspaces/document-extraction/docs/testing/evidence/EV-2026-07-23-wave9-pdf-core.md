# EV-2026-07-23 — Wave 9 PDF core subset

Scope: custom BCL-only PDF grammar, structural-resolution, stream-filter, page-tree and text-extraction subset. Independent review rejected the initial implementation; correction added authoritative xref state, strict stream/filter boundaries, operator/inline-image handling, Form cumulative budgets/cycles, encodings/CMaps and provenance honesty. This remains partial synthetic evidence, not complete ISO 32000 support or Wave 9 acceptance.

Implemented behaviour includes bounded COS lexical values and exact spans; direct/indirect objects and streams; classic/xref-stream/hybrid structural checks, bounded `/Prev` chains and object streams; ASCIIHex, ASCII85, LZW, Flate and RunLength with TIFF/PNG predictors and expansion limits; header/Catalog versions; trailer/root/page trees; common PDF text/position operators; basic single-byte encoding and ToUnicode bfchar/bfrange; deterministic approximate geometric runs; encryption/media classification; cancellation/limits and visible bounded recovery.

```powershell
dotnet test --project tests\unit\CollisionDocNet.Pdf.Tests\CollisionDocNet.Pdf.Tests.csproj --configuration Release --no-restore
```

After correction, the primary agent repeated the focused PDF suite: exit `0`, 46 succeeded, 0 failed, 0 skipped. Production build and formatting passed. Inputs were owned synthetic PDFs only.

Secondary source review showed that its relevant path delegates significant PDF parsing and rendering to external engines, so no code was ported from that path and no external engine is a production dependency.

Remaining gates include authoritative revision state, complete hybrid/linearisation, indirect length without recovery, XObjects/inline images, inherited resources, Type0/CID/CMaps/metrics, CTM/rotation/columns/bidi, tagged/ActualText/optional content, decoder-loop cancellation, passive native media assets, conformance, differential, fuzz, corpus and performance acceptance.
