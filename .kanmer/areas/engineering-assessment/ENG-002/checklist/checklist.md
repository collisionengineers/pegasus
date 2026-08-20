# ENG-002 checklist

- [ ] Core `IEstimateDocumentParser` contract + `ParsedEstimate` + `EstimateParseRejectedException`
- [ ] Infrastructure `AudatexEstimatePdfParser` (baseline rows, sections, pairing, checksums, rejection)
- [ ] DI registration
- [ ] Assessment page GET loads current draft + accepted specification
- [ ] `OnPostImportEstimateAsync` (Engineer-first check, parse-first, retain via `IAddCaseDocument`, `StartDraftAsync`, supersession path)
- [ ] `OnPostAcceptSpecificationAsync` (typed basis → `AcceptAsync`)
- [ ] Razor estimate tab: import form + specification panel + accept form (design rules held)
- [ ] Parser fixture tests (happy, checksum mismatch, ambiguity, missing header, unpriced part)
- [ ] Web tests: import + accept + refusals + custody retention
- [ ] Local corpus-sample parse verification (scratchpad only, result recorded abstractly)
- [ ] Release build zero warnings; focused test filters green
- [ ] Simplification pass recorded in plan
- [ ] PR to dev opened; post-implementation-report written; ticket → review
