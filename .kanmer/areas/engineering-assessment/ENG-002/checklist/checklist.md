# ENG-002 checklist

- [x] Core `IEstimateDocumentParser` contract + `ParsedEstimate` + `EstimateParseRejectedException`
- [x] Infrastructure `AudatexEstimatePdfParser` (baseline rows, sections, pairing, checksums, rejection)
- [x] DI registration
- [x] Assessment page GET loads current draft + accepted specification
- [x] `OnPostImportEstimateAsync` (Engineer-first check, parse-first, retain via `IAddCaseDocument`, `StartDraftAsync`, supersession path)
- [x] `OnPostAcceptSpecificationAsync` (typed basis → `AcceptAsync`)
- [x] Razor estimate tab: import form + specification panel + accept form (design rules held)
- [x] Parser fixture tests (happy, checksum mismatch, ambiguity, missing header, unpriced part) — 12 passed
- [x] Web tests: import + accept + refusals + custody retention — 5 passed
- [x] Local corpus-sample parse verification (scratchpad only): the real Audatex sample parses to 33 lines with all four section checksums matching the document's own printed totals and normalization passing; the Tractable AI line-level estimate is rejected as "not recognized as an Audatex estimate report"
- [x] Release build zero warnings; focused test filters green (Core repair-spec/assessment 25, architecture 97, focused integration 27 passed / 6 pre-existing conditional skips)
- [ ] Simplification pass recorded in plan
- [ ] PR to dev opened; post-implementation-report written; ticket → review
