---
id: ADR-0047
status: accepted
date: 2026-09-09
supersedes: [ADR-0040]
superseded_by: []
related_capabilities: [INT-16]
related_frd: [frd-05]
tags: [extraction, pdf, ocr]
---
# ADR-0047: OCR only for incoming scanned instructions

## Decision

The operator restricts OCR to eligible incoming scans used to identify and
extract instructions. Prior identification as an instruction is not required.
This replaces ADR-0040's estimate/unusable-text-map qualification only; its
Document Intelligence provider, pinned prebuilt-layout API/model, Worker-only
managed identity, custody and durable operation decisions remain accepted.

Bind the operation to the exact retained PDF asset, including an attachment
when the incoming item is an email. Never submit the enclosing email bytes.
Merge selected-page OCR with readable source content; readable pages bypass
OCR. More than one scanned source requires explicit staff review rather than
combining unrelated page identities. Corrupt, encrypted and non-renderable
inputs do not qualify.

Estimate import remains deterministic parsing through its existing Core owner.
Unsupported estimates are refused without OCR or partial import. Retained
source custody and completion of an interrupted ordinary import remain
available; removing OCR does not remove that recovery path.

## Evidence

This decision establishes intended behavior, not deployment or acceptance.
Verification must exercise scanned email-attachment identity, readable-page
retention, provider-result replay and parser-only estimate import. See
[FRD-05](../frd/frd-05-documents-extraction-and-custody.md#qualified-ocr).
