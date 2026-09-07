# Plan

## Authority and reuse

The current operator instruction and EPIC-014 supersede the historical OCR permission deferral. ADR-0040 selects the already implemented Document Intelligence prebuilt-layout GA 2024-11-30 adapter; no new dependency. Keep the existing IntakeOcr operation, external-work router, logical document reader, metadata query, persistence envelope and retry/result policy. TICK-085 supplies the production retained-estimate caller, so this ticket cannot claim that caller delivered before that integration exists.

## Bounded steps

1. Complete ADR-0040, supersede ADR-0001, and align FRD-05/07 plus INT-16/EXT-12. Scan qualification and existing limits do not broaden. Readable text, corrupt/encrypted documents and mere business-parser ambiguity never trigger paid OCR.
2. Extend the existing request to identify exactly one source: intake receipt+asset OR Case+occurrence+document version, with immutable hash and length. Retain those values in the existing operation envelope, compare all context on replay, and expose deterministic `IntakeOcrOperations.BeginDocumentAsync`. Use the existing metadata query and logical reader to verify the Case source before sending or consuming retained output. Worker completion for Case sources retains the provider output and settles existing work without invoking instruction analysis or saving an estimate. Intake completion keeps its existing durable analysis retry.
3. Qualify genuinely unusable font mappings through PdfPig resource metadata: an actually used Type3 font, absent Unicode mapping and anonymous numeric glyph names. Absence of ToUnicode alone is insufficient (the four normal supplied Glass PDFs also lack it). No glyph decoder, arbitrary low-text fallback or duplicate scan threshold owner. Reuse the same qualification from TICK-085's reader; provider coordinates and confidence remain retained evidence, not independent acceptance criteria.
4. Update known consumers and run focused Core OCR, existing SQL operation-recovery/provider tests and genuine readable/unusable-font fixtures once in the root verification lane. Prove Case-source binding, conflicting replay refusal, retained-result restart without resubmission, inaccessible/changed source refusal and unchanged intake analysis. Keep failed attempts in the report. Independent exact-head review precedes merge; integrated proof does not claim Azure activation. PLAT-065 and TICK-085 complete those separately linked acceptance obligations.

## Scope boundaries

No new OCR engine, dispatcher, table, migration or compatibility path. No automatic estimate save under an expired human lease. No confidence-only rejection/acceptance; deterministic provider parsing still verifies fields and totals. No Azure writes or charged calls in this implementation ticket; operator-authorized provisioning is recorded under PLAT-065. Existing test source documents are immutable.
