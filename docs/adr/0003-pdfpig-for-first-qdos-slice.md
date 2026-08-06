# ADR-0003: PdfPig for the first QDOS embedded-text slice

- Status: Accepted for the first local QDOS slice
- Date: 2026-07-23
- Owners: Pegasus development team

## Context

ADR-0001 requires a genuine-QDOS comparison before an embedded PDF engine enters
the production code path. The local corpus contains direct PDFs and PDF
attachments from genuine QDOS emails. Corpus contents are immutable and were not
uploaded or copied into the repository.

The comparison covered 74 unique PDFs representing 567 pages: 15 occurred as
direct corpus PDFs and 68 occurred as email attachments, with overlap between
those origins. PdfPig 0.1.15, iText 9.7.0, and Aspose.PDF 26.7.0 were run against
the same bytes. Apryse 12.0.0 could not enter the run because the installed SDK
required a licence key at initialization.

PdfPig and iText both opened every document, identified the same 12 documents as
having insufficient embedded text, and produced identical document-level
coverage for the business markers measured by the harness. Aspose opened every
document but found fewer claim and mileage markers and ran under its evaluation
restriction. Full results are in
`docs/evaluation/qdos-pdf-engine-benchmark.md`.

## Decision

Use PdfPig 0.1.15 as the embedded-text adapter for the first local QDOS intake
slice.

The choice is deliberately narrow:

- the package stays in `Pegasus.Infrastructure`;
- Core receives an engine-neutral page/text result and never references PdfPig;
- insufficient embedded text becomes `OCR required` only when the page also has
  a dominant raster image; other low-text pages stay in manual review rather
  than producing guessed content;
- raw documents remain authoritative;
- the adapter records its engine and version;
- a later engine can replace it without changing QDOS field rules.

That replacement may be a separately developed in-house extractor or another
external engine. It is not part of the current slice. Any candidate must replace
the Infrastructure adapter through the existing engine-neutral contract after
contract-parity, frozen-cohort, holdout, security, licence/maintenance and real-
caller evidence; do not introduce it first as a parallel reader, external
checkout dependency or dormant feature flag.

PdfPig was preferred over iText because observed marker coverage and unreadable
detection were equal while PdfPig has the smaller operational and licensing
surface. iText was faster in this run, but performance was secondary and both
processed the cohort in seconds. Aspose did not demonstrate better field-marker
coverage in the constrained evaluation run.

## Limits

This decision proves a suitable embedded-text adapter for one local vertical
slice. It does not prove every extracted field against a human-approved expected
value, scanned-document OCR accuracy, future QDOS layouts, Linux publication, or
production acceptance.

The manual upload path is development-only and now retains the original and
extracted assets in ignored local content-addressed storage. That is not
production Blob staging, Box custody, backup, or retention. The route must not be
enabled in a deployed environment until authenticated intake and approved
durable source custody are implemented. Local SQLite concurrency evidence also
does not establish the Azure SQL locking behaviour required for production
reference allocation.

Before production rollout, rerun the same adapter contract against a frozen,
human-reviewed field-expectation cohort and an untouched holdout. Reopen this ADR
if another engine materially reduces silent field errors or unreadable outcomes.

## Consequences

- The first actual Web caller can process genuine embedded-text QDOS PDFs now.
- Twelve observed documents had insufficient embedded text in the original
  benchmark. ADR-0005 now requires page-level dominant-raster evidence before
  any of them becomes an OCR candidate; no cloud OCR call is introduced here.
- The application adds one PDF dependency rather than carrying the losing
  benchmark candidates.
- Engine replacement remains an Infrastructure concern rather than a second
  business parser.
