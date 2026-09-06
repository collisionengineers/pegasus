# B03 — implementation record

Helper branch `b-work/b03` (7 commits over the WIP checkpoint, base `de69bdcb5`); Opus implementer, 2026-09-06. Integration into `task/pegasus-v1-casework` follows the rebase onto `0ab330a21`, with the Brego/Super CAP source extension deferred (see blocking item).

## Delivered

- `Valuations.cs`: PR 670 port (`ValuationDetails.GuideMonth`, day-1 validation, `RequireManuallyRecordableSource`), `ValuationPolicy.IsManuallyRecordable`; Brego/SuperCap members written but deferred at integration because the A-owned `CK_CaseValuations_Source` constraint is generated from the enum (schema proposal 1).
- New `ValuationCalculations.cs`: `ValuationCalculationSelection(GuideValuationId, CommercialVat, PriorTotalLossPercentage, Additions, ConditionDeduction)`, `ValuationAdditionSelection(PresetId, PresetVersion, Label, Amount)` (Guid.Empty = custom), `IPreviewValuationCalculation`/`PreviewValuationRequest`/`ValuationPreview(GuideValuationId, GuideValuationStampUtc, Calculation)`, `IApplyValuationCalculation`/`ApplyValuationRequest : CaseMutationRequest` (+ `GuideValuationStampUtc`, `CorrectedEngineerValue`), `AppliedValuation(Id, CaseId, CaseVersion, GuideValuationId, GuideValuationStampUtc, Calculation, AcceptedEngineerValue, AcceptedBy, AcceptedAtUtc, Reason, CalculationPolicyVersion)`, `ValuationCalculation` (ordered breakdown), `ValuationAddition`, `IListValuationPresets`, `IListAppliedValuations`, `ValuationPreset`, `ValuationCalculationPolicy` (`CommercialVatRate 0.20`, prior-loss 10%/20%, `MaximumAdditions 20`, `PolicyStamp "case-valuation-calculation/v1"`, `FormatMoney` invariant `£#,##0.00`). Apply refuses an Engineer's Value row as basis, stale guide stamp, disabled/unknown/stale-version presets, negative proposal, non-Engineer actor; a refused apply leaves lease and version untouched.
- `EfValuationStore.cs`: GuideMonth mapping; `IAppliedValuationStore` (`ReadBasisAsync`/`ApplyAsync`/`ListAppliedAsync`) writing the confirmed `assessment.values.engineer` field and an `AppliedValuationSnapshots` row in one serializable transaction (double-apply guard = unique `(CaseId, SnapshotHash)`); `EfValuationPresetStore` (`IValuationPresetStore`, 200-char labels, case-insensitive uniqueness in code).
- New `Pages/Administration/ValuationPresets/Index.cshtml(.cs)`: Administrator-only list/create/edit/enable/disable, PRG, operation-key re-mint, labels in `ValuationPresetLabels` (to fold into `OperatorLabels.Admin` when ownership allows).
- Tests: `ValuationTests.cs` (+ PR 670 hunks), new `ValuationCalculationTests.cs`, `AssessmentPersistenceIntegrationTests.cs` (PR 670 hunks; class made `partial`), new `ValuationPresetPersistenceTests.cs` (partial reusing the Harness), new `ValuationPresetAdministrationWebTests.cs`.

## Verification (agent run)

| Command | Exit | Result |
| --- | --- | --- |
| locked restore / Release build | 0 | 0 warnings |
| Core.Tests `~Valuation` | 0 | 33 passed |
| IntegrationTests `~Valuation` | 0 | 15 passed, 1 skipped (Corpus) — run with a temporary, uncommitted stand-in for the source constraint migration; red on the branch as committed |

## Blocking / schema proposals for A (B edits no migration or mapping)

1. `CK_CaseValuations_Source` is generated from `Enum.GetValues<ValuationSource>()`; adding `Brego`, `SuperCap` needs Drop/AddCheckConstraint in `20260906054658_V1PlatformFoundation` (`[Source] IN ('Glasses','Cazana','EngineersValue','AiMarketResearch','Brego','SuperCap')`) plus designer and snapshot. Deferred at integration until A's G carries it.
2. `ValuationPresets.Label` `HasMaxLength(200)` + unique index (filtered on Active if disabled duplicates should be tolerated).
3. `ValuationPresets.UpdatedBy` `HasMaxLength(200)`.
4. `AppliedValuationSnapshots` index `(CaseId, AcceptedAtUtc)`.
5. Optional `AppliedValuationSnapshots.CaseVersion bigint` if B05 prefers a column over `SnapshotJson`.

## DI patch for A

```csharp
services.AddScoped<IAppliedValuationStore>(p => p.GetRequiredService<EfValuationStore>());
services.AddScoped<EfValuationPresetStore>();
services.AddScoped<IValuationPresetStore>(p => p.GetRequiredService<EfValuationPresetStore>());
services.AddScoped<IListValuationPresets, ListValuationPresets>();
services.AddScoped<ISaveValuationPreset, SaveValuationPreset>();
services.AddScoped<IPreviewValuationCalculation, PreviewValuationCalculation>();
services.AddScoped<IApplyValuationCalculation, ApplyValuationCalculation>();
services.AddScoped<IListAppliedValuations, ListAppliedValuations>();
```
Nav entry (C-owned `_AdminNav.cshtml` + `OperatorLabels.Admin.ValuationPresets = "Valuation presets"`): link to `/Administration/ValuationPresets/Index`, area key `valuation-presets`.

## Deviations / open questions

Enum extension deferred (above); `partial` test class reuse; `ListAppliedAsync` naming; Engineer's Value refused as basis; refused apply keeps the lease (page must not re-claim); `£` renders as `&#xA3;` in Razor; `IsManuallyRecordable` awaits its Case-partial caller.

## Simplification pass (2026-09-06)

Applied: one money rule via `ValuationCalculationPolicy.RequireAmount`; `StampOf` private; usings. Not applied: money-shape rule now duplicated across `ValuationPolicy.Money`, `EstimatePolicy.Money`, `ValuationCalculationPolicy.RequireAmount` (needs `Estimates.cs`; follow-up ticket); helper ordering; `ValuationPresetLabels` second copy pending `OperatorLabels` fold-in; `AutomationComposed` probe convention; `|` model-error idiom.
