# B04 phase 2a — implementation record

Integrated into `task/pegasus-v1-casework` as `8795c1581` (squash of helper branch `b-work/b04p2`: b7f224f83, 0c96434af, 13ee110a4, d51ff3331; base `3a4d0902b`). Opus implementer under claude-fable-b orchestration, 2026-09-06.

## Delivered

- `src/Pegasus.Infrastructure/Persistence/EfRepairSpecificationStore.cs`: `ApplyDetails`/`ReadDetails` are the one write/read pair for the canonical header — rate snapshot (`RateCardId`/`RateCardVersion` beside the one `LabourRate` column), the four `*DiscountPercent` columns (percent, lossless for the 4-dp fractions Core validates), `RepairerVatStatus` (enum name) with the four `*VatApplicable` flags written only for an explicit override (their presence is `CategoriesOverridden`), `CalculationBreakdownJson` = internal `EstimateCalculationBreakdown(CalculationPolicyVersion, VatPercent, Raw, Printed)` written by `RecordBreakdown` on save, duplicate and accept from one `EstimateTotals.Compute`; `ApplyDetails` nulls it so no path leaves stale figures; `Map`/`MapPageItem` hydrate `RepairCalculationBasis.Printed` and `.VatPolicy` from it. Lines (`EstimateLineWriter.NewLine`): `Materials`, `Operation` (derived via `EstimateOperations.FromLineType`), `OriginalValuesJson` (the `EstimateLineOrigin`), `SourceDocumentIdentity/VersionId/Sha256`, `SourceRowIdentity`, `AmendedBy/AmendedAtUtc`; per-line off-pattern anomalies in that line's `CurrentValuesJson`. `CloneLine(retainProvenance)`: correction keeps provenance, duplicate drops it and keeps the whole header. One-Current invariant, versioning, replay by operation key, accept/discard and SQL constraints untouched.
- `src/Pegasus.Core/Assessment/Estimates.cs`: `EstimatePolicy.BasisFor(EstimateTotals)` overload so accept computes once.
- ENG-039 (`_CaseEstimate.cshtml`): both `RenderEstimateTotals(totals);` calls were markup text; now `@{ … }` statements (proved from the emitted Razor source). The block renders `Printed` — Parts, Panel labour, Paint labour, Materials, Specialist, Net, VAT, Gross — `£` + `N2`; labels in B-owned `CaseWorkspaceLabels.EstimateTotals`.
- `Details.cshtml.cs`: save and add/remove-row redraw carry forward the estimate's `Discounts`/`Vat`, its rate card (kept only while the posted rate equals the card's) and each line's `Materials`/`Origin`/provenance/amendment attribution, so a Web save no longer erases what the store persists.
- Tests: `AssessmentPersistenceIntegrationTests` +3 (`AnEstimatesCanonicalHeaderLinesAndBreakdownRoundTrip`, `AnAcceptedEstimatesBreakdownAndPolicyVersionSurviveALaterRevision`, `AnUnknownRepairerVatStatusBlocksUseAsCurrentUntilItOrTheCategoriesAreRecorded`); `AssessmentReportProjectionTests.TheReportsCostsAreThePrintedBreakdownRowForRow`; `EstimateTests` adjusted.

## Verification (Windows, PowerShell 7, Release)

| Command | Exit | Result |
| --- | --- | --- |
| helper: `dotnet build ./Pegasus.slnx` | 0 | 0 warnings / 0 errors |
| helper: Core `~Estimate\|~RepairSpecification\|~AssessmentReportProjection` | 0 | 103 passed |
| helper: integration `~AssessmentPersistenceIntegrationTests\|~EstimateParser\|~Audatex` | 0 | 65 passed |
| helper: Architecture | 0 | 100 passed |
| squashed tree 8795c1581: solution build | 0 | 0 / 0 |
| squashed tree: full Core | 0 | 1470 / 1470 |
| squashed tree: Architecture | 0 | 100 / 100 |
| squashed tree: integration `~AssessmentPersistenceIntegrationTests\|~EstimateParser\|~Audatex\|~CaseWorkspacePersistence\|~CaseReportGenerationPersistenceTests` | 0 | 86 / 86 |
| combined tree (A head + B): Estimate-section / import / engineer-section / report-draft web tests | see scratch | run in progress at record time |

## Deviations and dispositions

1. **VAT default on save.** A save whose `Details.Vat` is null persists the effective policy (`Registered`/`NotRegistered` derived from `VatPercent`), not `Unknown`: writing `Unknown` would block Use-as-Current for every save the current Web editor makes, which has no VAT-status control yet. Rows written before this change read back `Unknown` and block, as specified. Follow-up inside B08: the Estimate section's VAT status / category / discount controls, after which the derivation is removed and only an explicit status unblocks.
2. **Compatibility members kept.** `EstimateTotals.Parts/Labour/Paint/Other/Subtotal/Vat/Total` stay because A-owned `AssessmentMcpTools.MapEstimate` still reads them; the hunk A needs (`EstimateTotalsToolItem` on the printed breakdown, or the wire-shape-preserving alternative) is published on PR 672. Delete on A's consumption.
3. `AssessmentReportProjection.CostsOf` does not exist; the report's cost mapping is `ReportRepairCosts.For` in `AssessmentReportRendering.cs`, already on `Printed` since phase 1 — no change, proved by the new projection test.
4. `VatOverrideReason` left unwritten: no Core member carries an override reason (contract addition needed first). `Operation` and `CurrentValuesJson` are write-only: `CaseEstimateLineRecord` has no reader for them; adding one with no production reader would be speculative.
5. A rate snapshot with no card id/version reads back `Rate = null`; the hourly rate is preserved in `LabourRate`. `PaintLabourRate` retained (removal touches A's MCP file).
6. Now-unused labels in C's `OperatorLabels.cs` (`EngineerSections.Labour/.Paint/.Subtotal/.Total`): reported, not edited.
7. No web-side rendered-totals regression standalone (host blocked on A04 adapters); verified in the combined tree instead. Test UI snapshots unaffected: `case-details--default` renders the empty-estimate branch and no snapshot contains the old literal.

## Simplification pass (2026-09-06)

Applied in the helper's own commits (`d51ff3331`: read the header once per map; assert the edited draft's breakdown). Not applied: none outstanding beyond the deferred public-API removals above.

## Remaining B04 (phase 2b)

Glass's: `GlassEstimateXmlParser`, `GlassRepairEstimateGateway`, `EfGlassRepairEstimateSessionStore`, `Integrations/Glass/Callback` page, `Administration/Glass` page, launch/resume handlers and tests; needs A's `Program.cs` callback rate-limit/page-convention patch, the external-credential `StaffAccessRight`, and the Accounts link. MCP `pegasus_estimate_import` and `MapEstimate` hunks are A's.
