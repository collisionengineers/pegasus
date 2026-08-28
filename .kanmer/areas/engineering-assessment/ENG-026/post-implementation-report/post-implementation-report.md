# Post-implementation report — ENG-026

Branch `task/eng-026-estimates` (worktree `../pegasus-worktrees/eng-026-estimates`),
merged over `origin/dev` 5ca2572c (PLAT-029). Build green
(`dotnet build ./Pegasus.slnx --configuration Release`);
`scripts/Test-MigrationGrants.ps1` and `scripts/Test-AzureDeploymentPlan.ps1 -Mode Local`
pass. Tests written but not run by the implementer; the orchestrator runs the
wave loop.

## Delivered

- **Migration `20260828112103_NamedEstimates`** — columns on the already
  granted `CaseRepairSpecifications` (Name, RepairDays, LabourRate,
  PaintLabourRate, PaintMaterials, OtherCosts, VatPercent, Notes, IsCurrent,
  AiJobId, DiscardedBy/AtUtc/Reason, LastOperationKey) and `CaseEstimateLines`
  (PaintWorkUnits, Quantity); the Accepted-unique index is replaced by a
  filtered unique index on `IsCurrent = 1`; check constraints for the new
  state/routes, `IsCurrent ⇒ Accepted`, VAT 0–100, Quantity > 0; backfill
  names `Estimate <version>`, VAT 20, and marks the one Accepted row Current.
  No new table, so no grant. Appended to `IntakePersistenceIntegrationTests`.
- **Core** `Estimates.cs`: `EstimateDetails`, `EstimateOperations`
  (Replace/Repair/R&I/Paint/Other ↔ `EstimateLineCodes.Types`),
  `EstimateTotals.Compute` (the one totals owner), `EstimatePolicy`,
  `SaveEstimate`/`DuplicateEstimate`/`DiscardEstimate`/`SetCurrentEstimate`/
  `ListCaseEstimates` with their interfaces; `IRepairSpecificationStore`
  gains the five operations. `RepairSpecificationVersion` carries
  `Details`, `IsCurrent`, `AiJobId`, `DiscardReason`; state `Discarded`;
  routes `Json`, `AiDraft`; `ValidateSource` requires document evidence for
  document routes only; `PolicyVersion` → 2.
- **Report**: `AssessmentReportProjectionInput.CurrentEstimate`; costs and
  the parts/repairs/operations lists come from it through `EstimateTotals`;
  `ReportRepairCosts.VatOverride` carries the estimate's VAT so the built-in
  repairer-VAT rule runs only without a Current estimate; readiness reasons
  `Current estimate required` and `Current estimate labour rate`.
- **Persistence**: store operations (Serializable transaction, replay by
  creation/last operation key, lease/version guards, history);
  `DraftQuery` = latest draft, `AcceptedQuery` = Current; legacy
  `StartDraftAsync`/`AcceptAsync` keep their single-canonical-draft contract
  and existing tests. Workspace source tolerates several drafts.
- **JsonEstimateParser** (`pegasus-estimate/1`, documented on the class),
  registered as a singleton beside the Audatex parser.
- **MCP** `pegasus_estimate_save` (AiDraft only; `aiJobId` must be an
  Estimate job on the case, Taken by the calling client; lease + expected
  version) and `pegasus_estimate_list` under `automation.assessment`.

## What downstream tickets consume

- **ENG-028 (estimate editor UI)**: `ISaveEstimate` (route Manual for typed,
  `Json`/`AudatexPdf` with `RepairSpecificationSource` evidence for the
  import dialog, which selects `JsonEstimateParser` or the Audatex parser by
  the chosen source), `IDuplicateEstimate`, `IDiscardEstimate` (Delete
  estimate), `ISetCurrentEstimate` (Use estimate), `IListCaseEstimates`
  (tabs), `EstimateTotals.Compute` (totals row), `EstimateOperations`
  (Operation column). The page's `Prepare(Assessment, costs: null)` call
  should pass `workspace.AcceptedSpecification` as the Current estimate.
- **PLAT-049 (Operations AI Job List)**: `Review estimate` opens the
  Assessment estimate tab for the job's `ResultReference` (= estimate id);
  `pegasus_estimate_list` exposes `aiJobId` per estimate.

## Files outside Owns

- `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` — `VatOverride`
  on `ReportRepairCosts` (optional, default null).
- `src/Pegasus.Infrastructure/Persistence/EfAssessmentWorkspaceSource.cs` —
  latest-draft / Current selection instead of `SingleOrDefault` over drafts.
- `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` — line
  field mapping and the legacy implicit draft's version number.
- `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs`,
  `DependencyInjection.cs`, `AssessmentContracts.cs` — registrations,
  configuration and the two line fields.
- `tests/Pegasus.IntegrationTests/AssessmentEstimateImportWebTests.cs` —
  fake store implements the new port members.

## Open questions

1. The Audatex parser records paint-section work units in `WorkUnits`
   (labour hours), not `PaintWorkUnits`; an imported Audatex estimate prices
   paint hours at the labour rate until the parser (not owned here) maps
   its Paint section to `PaintWorkUnits`.
2. The rendered report prints the estimate's Paint total (paint labour +
   materials) in the existing "Paint Materials" row; the renderer's row
   label is not owned here.
3. A Current estimate with no labour rate fails report readiness because
   the renderer requires a positive hourly rate; ENG-028 should make the
   rate a required editor field or the renderer should accept zero.
4. The legacy import handler still refuses an import when any Draft exists
   (page-level check, ENG-028 replaces the dialog).
