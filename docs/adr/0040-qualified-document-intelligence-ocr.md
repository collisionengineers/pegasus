---
id: ADR-0040
status: accepted
date: 2026-09-07
supersedes: [ADR-0001]
superseded_by: [ADR-0047]
related_capabilities: [INT-16, EXT-12]
related_frd: [frd-05, frd-07]
tags: [extraction, pdf, ocr]
---
# ADR-0040: Qualified Document Intelligence OCR

## Status

Accepted under the operator's 7 September v1 instruction. This decision
supersedes ADR-0001's scan-only, `prebuilt-read` OCR choice. It does not prove
implementation, provisioning or live acceptance. ADR-0003's PdfPig selection
and ADR-0005's ordinary intake qualification and limits remain accepted.

The source qualification and estimate-import clauses below are superseded by
[ADR-0047](0047-scanned-instruction-ocr-only.md), following the 9 September
operator decision. The provider, model, API, managed identity and custody
decisions remain in force.

## Context

Pegasus already has one page-restricted Azure OCR adapter, a durable operation
record paired with existing external work, and retained page/word/table output.
Scanned instructions need this path activated. A supplied, visually valid
Glass's estimate also has an unusable embedded character map; treating that
failure as an ordinary unreadable estimate leaves required work unsupported.

## Decision

Keep embedded PdfPig extraction first. Use the existing Azure Document
Intelligence `prebuilt-layout` adapter, pinned to GA API `2024-11-30`, for
qualified scan-like pages and positively established unusable-text-map pages
of retained estimate PDFs. Do not implement another OCR engine or glyph
decoder. Mere ambiguity, a failed business parser, encryption or corrupt PDF
structure is not OCR qualification.

The existing Worker and external-work operation perform the charged call.
Extend the existing operation's source context to bind either an intake asset
or a retained Case document version, never both. Read the source through the
existing authorized logical-document reader and retain provider operation,
API/model identity, response hash, coordinates and confidence. Resume known
provider operations and reuse retained output; do not blindly resubmit an
uncertain operation.

Use the Worker's managed identity against the Document Intelligence account's
custom subdomain. Grant only the resource-scoped data-plane role needed to
analyze documents; no application-stored service key or Web data-plane role.
Provision through the existing estate's infrastructure conventions. Ordinary
local/test composition makes no Azure OCR call.

## Consequences

Readable PDFs retain their deterministic, uncharged path. Both supported OCR
input classes share source attribution, external-work recovery and one result
contract. Provider parsers still validate the resulting fields, structure and
totals; confidence alone never accepts an instruction or estimate. OCR output
does not independently allocate a Case/reference or select a Current estimate.

Per-page service charges apply only to selected pages. Record current pricing
and actual activation evidence with operations, not in this durable decision.
The ordinary intake bounds continue to apply; this decision expands neither
upload limits nor supported document formats.

## Links

- [Document behavior](../frd/frd-05-documents-extraction-and-custody.md)
- [Engineering handoff](../frd/frd-07-eva-and-external-engineering-handoff.md)
- [Microsoft layout model](https://learn.microsoft.com/azure/ai-services/document-intelligence/prebuilt/layout?view=doc-intel-4.0.0)
- [Microsoft API and authentication](https://learn.microsoft.com/azure/ai-services/document-intelligence/quickstarts/get-started-sdks-rest-api?view=doc-intel-4.0.0)
- Kanmer TICK-041 (OCR), TICK-085 (Glass's PDF import), PLAT-065 (activation).
