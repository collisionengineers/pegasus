# Research — ENG-026 named estimates

## Premises verified by read-only checks (worktree at origin/dev 1f2cf4a6)

- `Core/Assessment/RepairSpecifications.cs`: `RepairSpecificationVersion` is a
  positional record (State Draft/Accepted/Superseded; routes LegacyUnresolved,
  Manual, Glasses, AudatexPdf, ApprovedAiProposal). `RepairSpecificationPolicy`
  has `RequireEngineer`, `ValidateSource` (SHA-256 required for every
  non-legacy route), `ValidateCalculationBasis`, `ValidateAcceptance`
  (Draft only, all lines confirmed, basis required), `DisplaySection`.
- `IRepairSpecificationStore` = StartDraft / Accept / GetVersion /
  GetCurrentAccepted / GetCurrentDraft. `EfRepairSpecificationStore.DraftQuery`
  and `AcceptedQuery` are shared with `EfCaseAssessmentStore` (legacy implicit
  draft on `pegasus_assessment_update` lines) and callers use
  `SingleOrDefaultAsync` on them — multiple drafts per case would throw.
  `EfAssessmentWorkspaceSource` does the same in memory (`SingleOrDefault`
  over Draft / Accepted rows).
- DB: `CaseRepairSpecifications` has a filtered unique index on
  `CaseId WHERE State='Accepted'`; check constraints on State, SourceRoute,
  Acceptance. `CaseEstimateLines` has LineType/Status/EvidenceLabel/Position/
  Unpriced checks. Both tables already granted (VersionedRepairSpecifications
  + follow-ups) — new columns need no grant; `Test-MigrationGrants.ps1` only
  inspects `CreateTable(` in `Up()`.
- Existing callers: `Pages/Cases/Assessment/Index.cshtml.cs`
  `OnPostImportEstimateAsync` (StartDraftAsync with parsed lines; refuses
  when a draft exists; requires a supersession reason when an accepted one
  exists) and `OnPostAcceptSpecificationAsync` (AcceptAsync with typed basis).
  Integration test `RepairSpecificationAcceptanceCorrectionAndExactVersionPersist`
  asserts StartDraft refuses a competing draft while an accepted spec exists
  without `SupersedesSpecificationId` — that legacy path is kept intact.
- Report: `AssessmentReportProjectionInput.Costs` (nullable) →
  `Prepare` adds `RepairCostRequirement` ("Repair cost figures") when null;
  tests assert the constant and that WhyOutstanding contains "EXT-09".
  `ReportRepairCosts` (AssessmentReportRendering.cs, **not in Owns**) carries
  the built-in VAT rule; rendering rejects `HourlyRate <= 0`.
  `EfAssessmentReportProjectionSource` passes `Costs: null` today.
- AI jobs (AUTO-011 merged): `AiJobKind.Estimate`, `AiJobResultKind.Estimate`,
  `IAiJobStore.GetAsync`, `AiJobPolicy.EffectiveState`, `IConfirmAiJob`
  (DraftReady → Completed, staff only). Job `TakenBy` = MCP client id =
  `ActionActor.Automation(clientId).SubjectId`.
- MCP: `AssessmentMcpTools` pattern (resolver.RequireAsync(AssessmentScope),
  auditor.RecordAsync, AutomationMcpErrors.ExecuteAsync/RequireOperationKey/
  RequireId). `AutomationMcpTestSupport.AllScopes` excludes `automation.jobs`,
  so tests seed jobs through `IAiJobStore` directly.
- `dotnet ef` 10.0.10 is available; migrations live in
  `Infrastructure/Persistence/Migrations`; `IntakePersistenceIntegrationTests`
  lists applied migration names.

## Assumed (not checked)

- TICK-061 / PLAT-048 merge before this PR; their migration timestamps are
  earlier than the one generated here (re-checked at merge time).

## Design decisions

1. **Legacy paths stay intact**: `StartDraftAsync`/`AcceptAsync` keep their
   single-canonical-draft + supersession contract (existing import/accept
   callers and tests). Named-estimate behaviour is the new
   `SaveEstimate`/`Duplicate`/`Discard`/`SetCurrent`/`List` path.
2. **"Current"** = `IsCurrent` (filtered unique per case). `AcceptedQuery`
   becomes "Accepted and current"; the Accepted-unique index is replaced by
   the IsCurrent one so several Accepted estimates can coexist and the
   Engineer switches with `Use estimate`. `DraftQuery` = latest draft
   (`Take(1)`) so every `SingleOrDefault` caller keeps working.
3. **One totals owner** `EstimateTotals.Compute`; the report's
   `ReportRepairCosts` receives the estimate's VAT figure as an override and
   never recomputes it; the built-in repairer-VAT rule applies only when no
   Current estimate exists (callers that pass `Costs` directly).
4. Lines gain `PaintWorkUnits` and `Quantity` (the design's Qty column; the
   Parts formula in FRD-11 is price × quantity).
5. `ValidateSource` requires artifact evidence for document routes only;
   Manual/AiDraft estimates have no document.
6. JSON parser: Pegasus-owned schema, registered as a concrete singleton
   beside the Audatex parser; the import dialog (ENG-028) selects by source
   route. The page's single `IEstimateDocumentParser` injection is untouched.
