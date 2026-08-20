# ENG-002 file map

## New

- `src/Pegasus.Core/Assessment/EstimateImport.cs` — Core port `IEstimateDocumentParser` (external-boundary abstraction: PDF text extraction lives behind it), `ParsedEstimate` result (SourceVersion + normalized `EstimateLineInput` list), `EstimateParseRejectedException` with the honest operator-facing reason.
- `src/Pegasus.Infrastructure/Assessment/AudatexEstimatePdfParser.cs` — deterministic PdfPig parser for the Audatex full-report shape: baseline row grouping, section state machine (LABOUR / PAINT WORK / PARTS / Extras), value-to-row pairing with ambiguity rejection, document-checksum verification (work-unit sums, parts sub-total, extras total), whole-import rejection on any failure.
- `tests/Pegasus.IntegrationTests/AudatexEstimatePdfParserTests.cs` — synthetic Audatex-shaped PDF fixtures built with PdfPig's `PdfDocumentBuilder` (no corpus content): happy path, checksum-mismatch rejection, ambiguous-row rejection, missing-header rejection, unpriced part.
- `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` — page-handler tests for import + accept (modelled on CaseCustodyWebTests / CaseCapabilityPagesTestSupport).

## Modified

- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` — inject `IRepairSpecificationStore`, `IAddCaseDocument`, `IAcquireCaseEditLease`, `IEstimateDocumentParser`; load current draft/accepted specification on GET; `OnPostImportEstimateAsync` (Engineer check → parse → retain document → start draft, two sequential leases); `OnPostAcceptSpecificationAsync` (basis form → `AcceptAsync`).
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` — estimate tab: import form (file picker + drag-drop reusing the existing dropzone convention), current-specification panel (lines, money, source route label, version, state) with the Engineer accept form.
- `src/Pegasus.Infrastructure/DependencyInjection.cs` — register the parser beside the other assessment services (near line 269 `IRepairSpecificationStore`).

## Reused, unchanged

- `IRepairSpecificationStore.StartDraftAsync` / `AcceptAsync` / `GetCurrentDraftAsync` / `GetCurrentAcceptedAsync` (TICK-093).
- `IAddCaseDocument` + `DocumentSource.StaffUpload` custody path (10 MiB cap convention from `Custody.cshtml.cs`).
- `IAcquireCaseEditLease` programmatic-claim convention (`Operations/Index.cshtml.cs`).
- `AssessmentPolicy.NormalizeRepairSpecificationLines`, `EstimateLineCodes` vocabulary.
- MCP route `pegasus_assessment_update` + `AutomationAssessmentIngressTests` (cited, untouched).
