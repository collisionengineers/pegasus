# QDOS embedded PDF benchmark

- Run: `2026-07-23T07:06:15Z`
- Source: immutable local `corpus/qdos-email-corpus/`
- External calls: 0
- External processing cost: 0
- Unique PDFs: 74
- Pages reported by each engine: 567
- Direct-PDF origins: 15
- Email-attachment origins: 68

No filenames, document text, claimant details, registrations, claim references,
addresses, or source hashes are recorded here. The disposable harness and any
detailed output remain under ignored `artifacts/`.

## Aggregate result

| Engine | Opened | Insufficient embedded text | Extracted characters | Time | Result |
|---|---:|---:|---:|---:|---|
| PdfPig 0.1.15 | 74/74 | 12 | 303,030 | 6.50 s | Selected for first slice |
| iText 9.7.0 | 74/74 | 12 | 296,294 | 5.15 s | Equivalent measured marker coverage |
| Aspose.PDF 26.7.0 evaluation | 74/74 | 12 | 308,991 | 11.29 s | Lower claim/mileage marker coverage; evaluation-limited |
| Apryse 12.0.0 | 0/74 | Not run | Not run | Not run | SDK required a valid key at initialization |

PdfPig and iText had identical document counts for all ten redacted business
markers measured by the harness: QDOS, claim, claimant, registration, vehicle,
mileage, accident, instruction, inspection, and address. Aspose matched most of
those counts but detected `claim` in 49 documents rather than 55 and `mileage` in
22 rather than 34.

## What this establishes

- PdfPig can decode the embedded-text cohort without an exception.
- Its insufficient-text decision agrees with iText and Aspose for this cohort.
- It does not lose measured business-marker coverage relative to the other
  runnable candidates.
- The embedded adapter can remain entirely local and incur no per-page service
  charge.

## What remains unproved

- Literal field-value accuracy against operator-approved expectations.
- OCR accuracy for the 12 insufficient-text documents.
- Encrypted, damaged, revised-layout, and future-QDOS behavior beyond the sampled
  corpus.
- Linux App Service native/runtime behavior and production throughput.
- Operator acceptance.

Those limits are acceptance work for the slice and later production hardening;
they must not be hidden by aggregate success counts.
