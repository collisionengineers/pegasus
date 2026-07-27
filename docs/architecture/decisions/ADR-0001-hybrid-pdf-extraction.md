# ADR-0001: Hybrid PDF extraction

- Status: Accepted; embedded engine selected by ADR-0003; scan qualification refined by ADR-0005
- Date: 2026-07-23
- Owners: Alex and the Pegasus `Next`/`unallocated` development team

## Context

Pegasus `Next`/`unallocated` must extract case information from ordinary PDFs and scanned
PDFs received as work instructions. The expected workload is approximately 2,000
cases per month. Original documents remain authoritative and are stored in Box.

The previous application used a custom PDF extraction program. A custom business
extractor is not inherently a problem; the architectural risk comes from trying
to implement the PDF file format ourselves, coupling provider rules to a user
interface, duplicating extraction logic, or silently accepting uncertain values.

Azure Document Intelligence can process every document, but it introduces a
per-page charge and its OCR/layout results are probabilistic. An embedded PDF
engine can decode computer-generated PDFs without a per-page Azure charge, while
Azure Read OCR remains useful for scanned pages. A corrupt, encrypted, or
otherwise unreadable PDF is not valid OCR input merely because embedded text is
unavailable; it must remain a visible terminal or manual-review outcome.

## Decision

Pegasus `Next`/`unallocated` will use a hybrid extraction pipeline:

1. Preserve the original PDF in Box and calculate a content hash.
2. Pass the PDF to a proven, maintained PDF engine; do not implement the PDF file
   format or glyph decoder in Pegasus code.
3. Extract text, page numbers, reading order, and coordinates from PDFs that
   contain usable embedded text.
4. Send only scan-like pages to Azure Document Intelligence `prebuilt-read` OCR:
   the page must have insufficient embedded text and a dominant raster image.
   Low-text pages without that scan evidence require operator review, not OCR.
   Corrupt, encrypted, or structurally unreadable documents are not sent to OCR.
5. Convert the resulting text and coordinates into case fields using one custom,
   deterministic provider-extraction module. Provider-specific rules are isolated
   behind a common contract and versioned independently.
6. Validate extracted registrations, dates, references, mileage, and other typed
   values with explicit business rules.
7. Never silently accept an absent, conflicting, invalid, or uncertain value.
   Route it to the appropriate staff review queue with the source evidence.
8. Retain the extractor version, source locations, OCR confidence where present,
   and staff corrections in permanent action history.

The `0.1.0-alpha.1` will not use Azure custom extraction or generative extraction
models. Our custom provider rules are application code and do not constitute an
Azure custom model.

## Embedded PDF engine selection

PdfPig 0.1.15 was selected for the first local slice by ADR-0003 after the
genuine-QDOS comparison. Licensing was not the deciding constraint.

The compared embedded candidates were PdfPig, iText, and Aspose.PDF; Apryse could
not enter the run without a licence key. Any replacement must still be chosen by
a repeatable benchmark using genuine QDOS documents rather than by feature lists
or developer preference.

The benchmark must cover, where genuine examples are available:

- ordinary computer-generated instructions;
- scanned and mixed text/image PDFs;
- rotations and differing page sizes;
- tables, tick boxes, and multi-column layouts;
- unusual or malformed font encodings;
- revised QDOS layouts;
- password-protected or damaged files that the business genuinely receives.

Selection is based primarily on exact required-field results, silent-error rate,
failure detection, and repeatability. Processing time, deployment complexity,
resource use, vendor support, and per-document cost are secondary measures.

## Cost position

At the expected volume, neither embedded extraction nor Azure Read OCR is likely
to dominate the application bill. Embedded extraction has no Azure per-page fee
but consumes application compute. Azure Document Intelligence is charged per page.

The cost-control rule is therefore to use embedded extraction for readable PDFs
and invoke Azure Read OCR only when required. Accuracy and maintainability take
priority over saving a small monthly OCR charge.

Any implementation proposal must update the cost forecast using the then-current
UK South Azure price and the measured proportion of pages requiring OCR.

## Consequences

### Positive

- Provider field rules remain deterministic, testable, and owned by Pegasus.
- Scanned documents remain supported without maintaining our own OCR engine.
- Ordinary PDFs do not incur an avoidable per-page processing charge.
- The PDF engine and OCR provider can be replaced behind stable application contracts.
- Uncertainty becomes an explicit workflow state rather than hidden bad data.

### Costs and risks

- Two extraction paths must produce one canonical intermediate representation.
- The system needs reliable detection of unusable embedded text.
- Provider templates and rules require regression tests as principals change formats.
- Azure OCR output can change when the service or API version changes, so the API
  version and raw response must be recorded.
- A commercial embedded engine may include native runtime assets that must be
  validated against the chosen App Service operating system.

## Required follow-up

1. Add a representative, access-controlled corpus of genuine QDOS PDFs to the
   approved test-data location.
2. Define the required QDOS fields and manually verified expected values for each
   benchmark document.
3. Build the engine-neutral extraction contract and benchmark harness.
4. Run the shortlisted engines and record the results.
5. Re-evaluate ADR-0003 against the human-reviewed cohort and holdout before
   production acceptance.
