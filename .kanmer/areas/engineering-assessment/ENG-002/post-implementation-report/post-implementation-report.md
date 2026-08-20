# ENG-002 post-implementation report — 2026-08-20

PR: #455 (`task/eng-002-estimate-import` → `dev`), 6 commits. Worktree `../pegasus-worktrees/eng-002`.

## What was delivered vs the plan

Every plan step landed as written; no scope was added.

1. **Core port** — `src/Pegasus.Core/Assessment/EstimateImport.cs`: `IEstimateDocumentParser` / `ParsedEstimate` / `EstimateParseRejectedException`. Reuses `EstimateLineInput`, `RepairSpecificationSourceRoute`, `EstimateLineCodes` — no new line vocabulary.
2. **Parser** — `src/Pegasus.Infrastructure/Assessment/AudatexEstimatePdfParser.cs`. One deviation from the plan's sketch, forced by the real document: a guideless description row is ambiguous on arrival (continuation vs a new line whose value prints on the next baseline), so the parser holds it as a deferred "bare row" resolved by the following row. Discovered because the real corpus sample has exactly this shape (a paint "PREPARATION…" line with no guide number); the naive version mis-read it and was fixed before commit. All fail-closed rules held: baseline-pairing ambiguity, unreadable amounts, missing identity, and any section-checksum mismatch reject the whole import with an operator-honest sentence.
3. **Import handler** — `OnPostImportEstimateAsync` in `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`: Engineer-first check, parse-first (rejected parse retains nothing), then document retention via `IAddCaseDocument` (`Other`/`StaffUpload`, identity `estimate-import:{opKey}`) and `StartDraftAsync` with `Source(AudatexPdf, artifactReference, parsed SourceVersion, retained SHA-256)`, two sequential programmatic lease claims (the Operations-page convention). Supersession of an accepted specification requires a typed reason; an existing draft refuses the import.
4. **Accept handler** — `OnPostAcceptSpecificationAsync`: typed five-bucket basis + VAT + VAT-registered answer → `AcceptAsync` with the draft's own source; total is the stated arithmetic (sum + VAT), which `RepairSpecificationPolicy.ValidateCalculationBasis` enforces. No prefill, no derivation.
5. **Razor** — "Repair specification" panel in the estimate tab (accepted + draft states, operator route wording via `OperatorLabels`, shared ordered-lines renderer, accept form, dropzone import form); the record bar's gated "Import assessment" control became the live link.
6. **DI** — parser registered beside `IRepairSpecificationStore`.
7. **Registry** — `docs/capabilities.md` EXT-12 row updated to the delivered slice. No FRD change needed: frd-06 §Canonical repair specifications already specifies exactly this behaviour.

## Test evidence (exact counts)

- Release build: 0 warnings, 0 errors (TreatWarningsAsErrors).
- `AudatexEstimatePdfParserTests`: **12 passed** — synthetic PdfPig-written fixtures reproducing the offset-baseline geometry; exact money per line; rejections for checksum mismatch (parts and labour), unmatched amount, missing identity, non-Audatex PDF, non-PDF bytes.
- `AssessmentEstimateImportWebTests`: **5 passed** — end-to-end through the page with the real parser: provenance (route, artifact identity, source version, SHA-256 of the retained bytes), two-lease sequencing at consecutive versions, nothing retained on a rejected parse, non-Engineer refusal, existing-draft refusal, typed basis on acceptance (total 5207.46 = buckets + VAT).
- Core `RepairSpecification*` + `AssessmentPolicy*`: **25 passed**; ArchitectureTests: **97 passed**; focused integration filter (`RepairSpecificationMigrationTests`, `AutomationAssessmentIngressTests`, `AssessmentReportDraftWebTests`, `QdosIntakeWebTests`, both new suites): **27 passed, 6 skipped** (pre-existing conditional skips in `QdosIntakeWebTests`).
- Local-only (scratchpad, never committed): the real corpus Audatex sample parses to 33 lines with all four section sums equal to the document's own printed totals and `NormalizeRepairSpecificationLines` passing; the corpus Tractable AI estimate rejects as "not recognized as an Audatex estimate report".

## Simplification pass

Recorded in the plan under "Simplification pass — 2026-08-20": 5 applied (parser efficiency ×3, one hoist, labels moved to `OperatorLabels`), 6 left with reasons, and 1 defect the pass surfaced and this branch fixed (`MutationRefusalMessage` would have surfaced `CaseOperationConflictException`'s raw case identifier on a duplicate submit; it now takes the fallback sentence).

## Known limitations / honest edges

- If the draft phase fails after the document was retained (case changed between the two mutations), a retry retains a second occurrence of the same file — content-identical by hash, both visible in custody. Accepted as safe; message tells the operator the document was kept.
- Non-Engineer staff see the import and accept surfaces stated-as-gated rather than hidden, matching the page's own convention.
- The test auth handler gained an additive `X-Test-Roles` header (default identity unchanged: Administrator-only).

## What remains for other tickets

- **EXT-09**: the estimate→report bridge. `ReportRepairCosts` demands labour-hours × hourly-rate and its own VAT rule; the accepted `RepairCalculationBasis` records bucket totals + document VAT, and the two cannot be equated without formula authority (the real sample's printed labour money differs from hours×rate by rounding). "Repair cost figures" readiness still fires. EXT-09 inherits: accepted basis per specification, full imported line data, and the recommendation to render the estimate form's type options from `OperatorLabels.EstimateLineType`.
- **Glass's route**: enum, custody path, and landing all ready; parser parked pending a real export sample (parked open question to the operator).
- **DOCS-001/operations docs**: no deploy happened in this task; current-state docs untouched by design.
