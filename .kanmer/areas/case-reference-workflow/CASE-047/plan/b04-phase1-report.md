# B04 phase 1 — implementation record

Integrated into `task/pegasus-v1-casework` as `de69bdcb5` (squash of the helper branch `b-work/b04`; base `6dcea9349`). Opus implementer, 2026-09-06.

## Delivered

- `src/Pegasus.Core/Assessment/Estimates.cs`: the one calculator `EstimateTotals.Compute` with `EstimateRawTotals`/`EstimatePrintedTotals`, `RepairerVatStatus { Unknown, Registered, NotRegistered }`, `[Flags] EstimateVatCategories`, `EstimateVatPolicy` (`DefaultFor`, `For`, `BlocksAcceptance`, `Charges`), `EstimateDiscounts`, `EstimateRateSnapshot(RateCardId?, RateCardVersion?, HourlyRate)`, `EstimateLineOrigin`, `EstimateAnomaly`; `EstimateOperation` + Blend/Specialist; `EstimatePolicy.ValidateDiscounts/ValidateVatPolicy/ValidateLineAmounts/BasisFor/ValidateSetCurrent` (Unknown VAT blocks Use-as-Current); `WorkUnitDecimals = 6`, `MaximumLineWorkUnits = 1000`, `DefaultVatPercent = 20`.
- `RepairSpecifications.cs`: `RepairCalculationBasis` carries `VatPolicy` and `Printed`; `ValidateCalculationBasis` asserts printed Gross = printed Net + printed VAT; `PolicyVersion` 3.
- `EstimateImport.cs`: `ImportRawEstimate : IImportRawEstimate` (parser auto-detect over `IEnumerable<IEstimateDocumentParser>`, fail closed on 0/>1, `IReadLogicalDocumentVersion` exact-hash open, one source-labelled Draft "{Provider} {n}", same-hash replay, never touches Current; `MaximumDocumentBytes` 32 MiB); `ParsedEstimate(SourceVersion, Lines, ProviderName, SourceTotals?)`, `EstimateSourceTotals`.
- `AssessmentContracts.cs`: trailing optional members on `EstimateLineInput`/`CaseEstimateLineRecord` (Materials, origin, source-row provenance, AmendedBy/AtUtc, off-pattern flags).
- `AssessmentPolicy.cs`: one hunk — `NormalizeLines` 0.1-step guards replaced by `EstimatePolicy.ValidateLineAmounts` (≤ 6 dp, ≤ 1,000 h, ≥ 0), applied to every estimate line (one rule per concept for the decimal(18,6) column).
- Parsers `JsonEstimateParser` (`DefaultProviderName = "Estimate"`; Blend/Specialist, row materials, provenance) and `AudatexEstimatePdfParser` (`ProviderName = "Audatex"`; printed section totals as `SourceTotals`, never forced; all fail-closed rejections intact).
- Tests: `EstimateTests.cs`, `RepairSpecificationPolicyTests.cs`, `AssessmentPolicyTests.cs` (renamed `EstimateLinesValidateTypePrecisionAndUnpricedRules`), `AssessmentReportProjectionTests.cs` (one assertion: VAT from raw taxable), `JsonEstimateParserTests.cs`, `AudatexEstimatePdfParserTests.cs`.

## Verification

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet restore ./Pegasus.slnx --locked-mode` | 0 | |
| `dotnet build ./Pegasus.slnx --configuration Release --no-restore` | 0 | 0 warnings, 0 errors |
| Core.Tests `--filter "FullyQualifiedName~Estimate\|FullyQualifiedName~RepairSpecification"` | 0 | 70 passed |
| IntegrationTests `--filter "FullyQualifiedName~EstimateParser\|FullyQualifiedName~Audatex"` | 0 | 35 passed |

Vectors asserted: V1 £100.01/£20.00/£100.01/£120.01; V2 £1,142.74 / £1,017.44 / £952.28; V3 printed Net £342.30 ≠ round(raw) £342.29, printed Gross £410.76.

## Compatibility kept until wiring

`EstimateTotals.Parts/Labour/Paint/Other/Subtotal/Vat/Total` are printed-equivalent projections kept so `AssessmentReportProjection.CostsOf`, `AssessmentMcpTools.MapEstimate`, `DetailsModel.EditorTotals`, `_CaseEstimate.cshtml` compile; delete with ENG-039. `EstimateDetails.PaintLabourRate` is validated/persisted but no longer read (one rate prices both hour kinds).

## DI patch for A

```csharp
services.AddSingleton<IEstimateDocumentParser, AudatexEstimatePdfParser>();
services.AddSingleton<IEstimateDocumentParser, JsonEstimateParser>();   // replaces AddSingleton<JsonEstimateParser>()
services.AddScoped<IImportRawEstimate, ImportRawEstimate>();
```
and drop the `JsonEstimateParser` constructor parameter at `Details.cshtml.cs:43` (B wiring). Blocking: `IReadLogicalDocumentVersion` has no implementation in `src/` yet (A04); `IImportRawEstimate` cannot be registered until it exists.

## Deviations / open questions

1. 0.1-step hour rule relaxed for every line (one rule, one column); conflicts expected with B02's `AssessmentPolicy.cs` edits — resolved at integration.
2. D9: `VatPercent` stays per estimate, default 20; VAT = Taxable × VatPercent/100 (A08 docs).
3. D17: one rate prices paint hours (plan over FRD-11/design README `:1063`); test `OneSnapshotRatePricesPanelAndPaintHomesAlike` (A08 docs).
4. `EstimateDetails.VatPolicy` falls back to Registered/NotRegistered from `VatPercent > 0` when no policy is recorded; once the store maps `RepairerVatStatus` (default "Unknown"), existing estimates block Use-as-Current until the operator records status — specified behaviour; data is disposable (D10).
5. Audatex `SourceTotals` carries section totals only (labour/paint work units, parts subtotal, extras); Cost Summary stays `Ignored` to protect the fail-closed geometry.
6. `AssessmentEstimateImportWebTests` compiles but was not run (LocalDB; wiring phase).

## Phase 2 (queued)

Store persistence of the breakdown/VAT/discount/rate/provenance columns in `EfRepairSpecificationStore` + `AssessmentPersistenceIntegrationTests` assertions; ENG-039 Razor totals fix and switch of consumers to `Printed`/`Raw`, delete compatibility members; MCP `pegasus_estimate_import` (A-owned file, A patch); Glass's XML parser/gateway/session store/callback/admin page; consumers for `SourceTotals` and `OffPattern` in the Estimate breakdown.

## Simplification pass (2026-09-06)

Applied (7): named positional nulls in imported `EstimateDetails`; parser-match arms ordered 0/1/many; removed unreachable pluralization; blank line; test builders `Header(...)`/`LineInput(...)`; usings instead of fully-qualified names. Not applied: collapsing the four `Compute` switch arms (mirrors the operation→bucket table); renaming `OffPatternAmount`; the 64-hex SHA-256 shape check duplicated in five places incl. two A-owned Custody files — defer to a follow-up ticket for one owner; `position: 0` sentinel; unifying three private validators; public-API removals (PaintLabourRate, naming of raw totals) → phase 2.
