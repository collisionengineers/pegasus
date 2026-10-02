---
id: ADR-0061
status: accepted
date: 2026-10-01
supersedes: [ADR-0047]
superseded_by: []
related_capabilities: [INT-16, AI-04]
related_frd: [frd-05, frd-16, frd-19, frd-04]
tags: [extraction, pdf, ocr, audit]
---
# ADR-0061: OCR every scanned document page; a full-page raster is a document or a photograph by its colour

## Decision

A PDF page with fewer than 80 embedded text characters and one raster covering
at least 80 percent of the visible page is a full-page raster. The reader
decodes that raster at a reduced size and measures its near-white share. At or
above the calibrated threshold the page is a scanned document page; below it,
a photograph that fills the page. A raster that does not decode is a document
page.

A scanned document page produces no image asset. Its raster is the page, so it
stays inside the retained PDF and never reaches a gallery, Box, plate
recognition, image completeness, a report or EVA. This is the one exception to
ADR-0005 decision 5. A photograph page is an ordinary embedded image and is not
sent to OCR. Photographs laid out with margins or several to a page never meet
the full-page rule and are selected as before.

Every scanned document page on a Mailbox, Manual upload or Principal API
receipt goes to the approved Azure Document Intelligence `prebuilt-layout`
boundary, one operation per retained PDF asset, whether or not the Principal
was already established from the message. The third-party report reader reads
the OCR text for every asset. The instruction reader reads it only when the
receipt still needs a Principal. An Audit Case whose original report arrived as
a scan is filled from the OCR text through the same recognition that fills it
from a readable report.

ADR-0047's restriction of OCR to instruction identification is replaced. Its
surviving clauses are carried here: the operation binds to the exact retained
PDF asset, never the enclosing e-mail; readable pages are neither sent nor
replaced; a blank OCR page is a page, not a failure; and an instruction with
scanned pages from more than one retained source still needs staff review for
the instruction decision, while each asset's report reading stands on its own.
ADR-0040's provider, pinned model and API version, Worker-only identity,
custody and durable-operation clauses remain accepted.

Known limits, stated rather than hidden: a photographed document on a dark or
grey background reads as a photograph and is never OCR'd, so staff mark the
report and fill its cells by hand; a near-white photograph filling a page reads
as a document, keeps no image and is OCR'd. Both verdicts are recorded on the
receipt with the measured share.

## Evidence

The threshold is calibrated on the local corpus; the measurement of every
full-page raster it holds lives in `artifacts/0110-scan-page-classifier/`
(local, not committed). Verification must exercise a paper-white full-page
raster (no image, one OCR operation), a photograph-sized paper-white raster
(still no image), a full-page photograph (one image, no OCR), a photograph on
a readable page (one image), the OCR-fed report reading and the Audit fill.
See [FRD-05](../frd/frd-05-documents-extraction-and-custody.md#qualified-ocr),
[FRD-16](../frd/frd-16-case-record-workspace.md#original-report) and
[FRD-19](../frd/frd-19-image-led-intake-and-pairing.md).
