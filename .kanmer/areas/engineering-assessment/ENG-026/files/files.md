# Files — ENG-026

## Owned (change)

- `src/Pegasus.Core/Assessment/Estimates.cs` (new): EstimateDetails,
  EstimateTotals, EstimateOperations, EstimatePolicy, requests, use cases.
- `src/Pegasus.Core/Assessment/RepairSpecifications.cs`: states/routes,
  version record fields, policy adjustments, store port extension.
- `src/Pegasus.Core/Assessment/AssessmentContracts.cs`: line PaintWorkUnits +
  Quantity (EstimateLineInput, CaseEstimateLineRecord).
- `src/Pegasus.Core/Assessment/AssessmentPolicy.cs`: line normalisation for
  the new fields.
- `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`: Current estimate
  input, "Current estimate required", costs from EstimateTotals.
- `src/Pegasus.Infrastructure/Assessment/JsonEstimateParser.cs` (new).
- `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs`,
  `AssessmentModelConfiguration.cs`, `EfRepairSpecificationStore.cs`,
  `EfAssessmentReportProjectionSource.cs`, `EfCaseAssessmentStore.cs` (line
  mapping only), migration `NamedEstimates` + snapshot.
- `src/Pegasus.Infrastructure/DependencyInjection.cs`: registrations.
- `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs`: `pegasus_estimate_save`,
  `pegasus_estimate_list`.
- Tests: `tests/Pegasus.Core.Tests/Assessment/EstimateTests.cs` (new),
  `RepairSpecificationPolicyTests.cs` (constructor arg),
  `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs`,
  `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs`,
  `AutomationAssessmentIngressTests.cs`, `JsonEstimateParserTests.cs` (new),
  `IntakePersistenceIntegrationTests.cs` (migration name).

## Touched outside Owns (reported)

- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs`: `ReportRepairCosts`
  gains an optional VAT override (the only way the Current estimate's VAT can
  replace the built-in rule without a second totals owner).
- `src/Pegasus.Infrastructure/Persistence/EfAssessmentWorkspaceSource.cs`:
  Draft/Accepted selection must tolerate several drafts per case.

## Read only

`Pages/Cases/Assessment/Index.cshtml.cs`, `AiJobs.cs`, `AiJobOperations.cs`,
`EfAiJobStore.cs`, `AutomationMcpErrors.cs`, `AiJobMcpTools.cs`.
