---
id: ADR-0081
status: accepted
date: 2026-10-22
supersedes: []
superseded_by: []
related_capabilities: [INT-16, AI-04]
related_frd: [frd-05, frd-19]
tags: [extraction, pdf, renderer, pdfium]
---

# ADR-0081: Render a refused full-page raster's page with PDFtoImage over PDFium

## Status

Accepted (operator rulings R194 and R209, 22 October 2026). This record
refines ADR-0061 (a full-page raster is a document or a photograph by its
colour) and ADR-0025 ("one live PDF implementation, not two"; R209 accepts
the second native PDF engine), and sits beside ADR-0040 and ADR-0003 (PdfPig
reads PDF).

## Context

The PDF reader hands an image over only as JPEG or PNG. Issue #989's first
fix also recovers a JPEG behind other filters and samples that run past the
declared size. JPEG 2000, JBIG2 and CCITT streams stay unread, so a photo
PDF in one of them lost its photographs and each page went to OCR as a
scanned document page. PdfPig has no page renderer.

## Decision

Render the page to an image for photo PDFs only: when a stream is not JPEG
or PNG, that full page is rendered to a raster image and treated as the
photograph; scans and text PDFs are untouched (R194).

The page renderer is one small internal rasteriser in
`Pegasus.Infrastructure`: PDFtoImage 5.2.1 (MIT) over PDFium 147.0.7690
(Apache-2.0, about 3 MB native per platform), called only when the dominant
raster is refused (R209). A small render goes through the unchanged
scan-page classifier, and only a photograph verdict renders the page at full
size, kept as JPEG at the existing report-photo quality 85 within the
40,000,000-pixel bound. A render that draws nothing, or any failure, keeps
the document-page and OCR path. No receipt line is added: the existing
scan-page sentence stands, and `ReaderVersion` names the engine and its
version (R209).

PDFtoImage over PDFium is the second native PDF engine on Web and Worker,
beside QuestPDF (ADR-0050). PDFtoImage 5.2.1 needs SkiaSharp 3.119.2, so
SkiaSharp moves from 3.116.1 to 3.119.2.

## Consequences

- PdfPig remains the PDF reader. PDFium draws a page only around a dominant
  raster the reader refuses, on a page with almost no text.
- The publish output carries PDFium for its runtime: `pdfium.dll` (5.8 MB)
  for `win-x64` and `libpdfium.so` (6.0 MB) for `linux-x64`.
- PDFium runs in-process on received PDFs in both hosts.

## Evidence

The evaluation (licences, assets, time and peak memory, hostile inputs, and
the 103 JPEG 2000 full-page rasters of the reference textbook) is recorded
in the pull request that resolves issue #989. See
[FRD-05](../frd/frd-05-documents-extraction-and-custody.md#supported-source-boundary)
and [ADR-0061](0061-ocr-every-scanned-document-page.md).
