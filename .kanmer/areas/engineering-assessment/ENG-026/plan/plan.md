# Plan — ENG-026

Diff estimate: ~1,400 lines added / ~80 changed across Core, Infrastructure,
Web MCP and tests, plus one EF migration and snapshot.

## Steps

1. **Core model** (`RepairSpecifications.cs`, `AssessmentContracts.cs`,
   `AssessmentPolicy.cs`): add `Discarded`, `Json`, `AiDraft`; add
   `EstimateDetails Details`, `IsCurrent`, `AiJobId`, `DiscardReason` to
   `RepairSpecificationVersion`; `PaintWorkUnits`/`Quantity` on line input and
   record (normalised beside WorkUnits/Price); `ValidateSource` requires
   document evidence only for document routes; `ValidateAcceptance` unchanged.
   Reuses: `RequireEngineer`, `NormalizeRepairSpecificationLines`.
2. **Core estimates** (`Estimates.cs`): `EstimateDetails`, `EstimatePolicy`
   (name/rates/VAT/days/notes bounds, actor-route rule), `EstimateOperations`
   (Replace/Repair/RemoveAndRefit/Paint/Other ↔ `EstimateLineCodes.Types`),
   `EstimateTotals.Compute`, requests, `ISaveEstimate` (`SaveEstimate` checks
   the AI job via `IAiJobStore` + `AiJobPolicy.EffectiveState`),
   `IDuplicateEstimate`, `IDiscardEstimate`, `ISetCurrentEstimate`
   (`IConfirmAiJob` when the job is DraftReady), `IListCaseEstimates`; port
   methods on `IRepairSpecificationStore`. Reuses: `CaseMutationRequest`,
   `AiJobPolicy`, `ConfirmAiJob`.
3. **Report** (`AssessmentReportProjection.cs`, `AssessmentReportRendering.cs`
   override only, `EfAssessmentReportProjectionSource.cs`): input gains
   `CurrentEstimate`; `RepairCostRequirement = "Current estimate required"`;
   costs from `EstimateTotals`; lines of type from the Current estimate; the
   source loads the Current estimate through `IRepairSpecificationStore`.
4. **Persistence**: entity columns, model configuration (checks, IsCurrent
   filtered unique replacing the Accepted one), store methods (Serializable
   transaction, replay via `FindReplayAsync`, `Guard`, `AddHistory`), Draft
   query = latest draft, Accepted query = current; workspace source picks
   the latest draft + current. Migration `NamedEstimates` (backfill
   IsCurrent from Accepted, names from version). Append to migration list.
5. **JSON parser**: `JsonEstimateParser : IEstimateDocumentParser` (route
   Json; schema documented on the class; rejects with
   `EstimateParseRejectedException`), registered as a singleton.
6. **MCP**: `pegasus_estimate_save` (AiDraft only, `aiJobId` required, lease +
   expected version like `pegasus_assessment_update`) and
   `pegasus_estimate_list`.
7. **Tests**: Core (totals, operation mapping, policy/actor rules, report
   projection from a Current estimate); integration store (save/duplicate/
   discard/set-current/list, IsCurrent uniqueness, legacy path unchanged);
   MCP ingress (save with job, refusal without job, list); JSON parser.
8. Build, run `Test-MigrationGrants.ps1` and `Test-AzureDeploymentPlan.ps1
   -Mode Local`, merge `origin/dev`, simplification pass, report, PR.

## Acceptance

- One totals owner; report costs and lists come from the Current estimate;
  built-in VAT rule only when no Current estimate.
- Existing import/accept callers compile and keep their behaviour.
- Automation can only create/update AiDraft estimates that cite a Taken
  Estimate job for the case held by the same client.

## Simplification pass — 2026-08-28

Lenses: reuse, simplification, efficiency, altitude over the branch diff
(`git diff origin/dev...HEAD`).

| Finding | Disposition |
| --- | --- |
| `EfRepairSpecificationStore.GetRequiredVersionAsync` duplicated the new `ReplayedAsync` once `AcceptAsync` stamps `LastOperationKey` | Fixed — deleted; `AcceptAsync` replays through `ReplayedAsync` |
| Automation-only-AiDraft rule appears in `ValidateSave` (route on create) and `ValidateEditable` (route of the stored row on update) | Accepted — the two checks guard different inputs; one would let an Automation update rewrite a Manual draft |
| `pegasus_estimate_save` re-reads the workflow to report the new case version | Accepted — `RepairSpecificationVersion` does not carry the case version and the client needs it for the next lease; one indexed read |
| `EstimateOperations.TryParse` has one production caller (the JSON parser) | Accepted — it is the design's operation vocabulary; ENG-028's editor is the second caller |
| Migration `Designer.cs` and `.cs` were generated with a UTF-8 BOM | Fixed — stripped (ENG-022 precedent) |
| Report `Prepare(assessment, costs)` still has the page as a caller passing `costs: null` | Deferred to ENG-028 — the page's readiness panel will name "Current estimate required" until it passes the workspace's Current estimate; the report generator path (`EfAssessmentReportProjectionSource`) is correct now |
