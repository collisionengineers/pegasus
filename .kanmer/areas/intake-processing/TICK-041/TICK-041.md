---
id: TICK-041
type: ticket
title: INT-16 — Azure OCR for scan-like and unusable-text-map PDF pages
status: backlog
area: intake-processing
order: 950
assignee: ''
profile: feature
labels:
  - capability
  - INT-16
  - now
  - requires-live-approval
  - evidence-required
  - azure
  - ocr
groups:
  - EPIC-009
  - EPIC-011
links:
  - PLAT-065
  - TICK-085
blocks:
  - INTK-049
  - PLAT-065
  - TICK-085
refs:
  - docs/frd/frd-02-intake-and-source-identity.md
  - docs/frd/frd-05-documents-extraction-and-custody.md
  - docs/frd/frd-07-eva-and-external-engineering-handoff.md
docs_todo: true
archived: false
created: '2026-08-12T15:03:53.610Z'
updated: '2026-09-03T15:15:28.867Z'
---

## What

Activate one provider-neutral Azure Document Intelligence OCR path for persisted scan-like PDF pages and for visually valid PDF estimate pages whose embedded character map is unusable.

## Why

INT-16 was previously allocated only to scan-like instruction pages. The operator has now required all supplied Glass's calculations to import, including YL69YFO: the document renders correctly but its embedded glyph mapping is unusable. Current ADR-0001 excludes that class, so implementation requires a new next-free ADR that supersedes ADR-0001 while preserving ADR-0005's intake limits.

## Approach

- Keep embedded PdfPig extraction first for ordinary readable PDFs.
- Qualify only two OCR input classes: persisted scan-like pages under the existing intake rule, and visually valid post-Case estimate pages whose text-map failure is positively detected.
- Never send corrupt, encrypted, non-renderable or merely ambiguous documents to OCR.
- Define one Core-neutral page/text/coordinate/confidence result consumed by deterministic provider parsers.
- Run the external call in Worker through the existing staged-blob, outbox, external-work, retry and attribution conventions.
- Use Azure Document Intelligence `prebuilt-layout`; pin and record the GA API/model version and response hash.
- Fail closed to staff review on low confidence, missing structure, inconsistent totals or provider outage.
- [[PLAT-065]] owns exact-target Azure provisioning and activation; this ticket does not authorize a cloud write.

## Governing changes

- Write the next-free ADR (currently ADR-0037 on `origin/dev`) and mark ADR-0001 superseded.
- Update FRD-05, FRD-07 and capabilities INT-16/EXT-12 before implementation leaves Backlog.
- Leave ADR-0005 accepted for ordinary intake behavior.

## Verification

- [ ] Embedded-text PDFs never incur an OCR call.
- [ ] A scan-like fixture and the supplied YL69YFO calculation produce versioned coordinate/confidence evidence through the same port.
- [ ] Corrupt/encrypted/non-renderable input is rejected without an OCR call.
- [ ] Timeout, throttling, replay, low confidence and ambiguous results are durable and idempotent.
- [ ] No local/test profile calls Azure.

## Outcome
