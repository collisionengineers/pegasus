# B02 core/persistence — implementation record

Helper branch `b-work/b02` (11 commits over `6dcea9349`, rebased cleanly onto `0c00c74a7`); Opus implementer, 2026-09-06. Integration into `task/pegasus-v1-casework` follows the rebased build/test run.

## Delivered

- New `src/Pegasus.Core/Cases/CaseWorkspace.cs`: `SaveCaseWorkspaceRequest : CaseMutationRequest` with nullable init sections Overview, Inspection, Vehicle, Damage, Valuation (draft inputs), Estimate, Settlement, Report, Completeness (null section = untouched; submitted section replaces every member it owns; empty payload refused; `assessment.values.engineer` refused everywhere), section records (`CaseWorkspaceOverview` incl. `RepairerAddress` and `CaseWorkspaceClaimSource`; `CaseWorkspaceInspection` with `CaseReportAddressTreatment`, `CaseLocationProvenance`, `CaseWorkspaceStorageBusiness`, seven inspection values, storage/recovery amounts; `CaseWorkspaceVehicle` with `CaseWorkspaceOdometer`; `CaseWorkspaceDamage` typed impacts; `CaseWorkspaceReport` with sign-off id and report date), `CaseOdometer` (1 mile = 1.609344 km, display conversion never reconverts, zero present), `SaveCaseWorkspaceResult(Data, Assessment, Estimate, WasReplay)` + Version/Completeness/Readiness, `ICaseWorkspaceStore`, `ISaveCaseWorkspace`/`SaveCaseWorkspace(ICaseWorkspaceStore, IStaffAccountQueries)`, `CaseWorkspacePolicy` (`PolicyKey = "case-workspace-save"`, v1; `ValidateAndNormalize`, `AssessmentFields`, `Overlay`), `CaseDataPolicy.ResolveInspection(treatment, address)`; `CaseDataProjection.Workspace` (`CaseWorkspaceData`) appended; `CaseInspectionData.RepairerAddress` now populated.
- New `EfCaseWorkspaceStore(IDbContextFactory<PegasusDbContext>, TimeProvider, ICaseWorkflowConfiguration, IEnumerable<IProviderCaseMatchPolicy>?)`: one serializable transaction, replay by operation key + request hash, version/lease/archived/terminal guards, writable-state rule `AssessmentPolicy.IsWritableState` (deliberate widening; auto state move and due-work reschedule only pre-assignment), merged case-data write, assessment write set with derived impact values, single open Draft estimate (supplied `EstimateId` must match), factual completeness flags, readiness re-evaluated from persisted facts (no forced demotion), one version bump, one history triple.
- Helpers extracted inside existing B-owned files: `CaseDataFieldWriter`/`CaseDueWorkScheduler` (EfCaseDataStore.cs), `CaseOperationReplay`/`CaseMutationHistory` (CaseMutationGuard.cs; all three stores now fixed-time compare), `AssessmentWriteSet` (AssessmentFieldWriter.cs), `EstimateLineWriter` + promoted `ApplyDetails` (EfRepairSpecificationStore.cs).
- 27 new `CaseDataFieldNames` (claim source, storage business, odometer display unit, address treatment, location provenance, inspection values, repairer address) — additive edit to A-owned `CaseDataEntities.cs` (deviation raised with A).
- `AssessmentContracts.cs`: 23 detailed damage zones with parent map beside the broad zones; `impact_location` derivation; new v3 paths; `MaximumFieldsPerSave` 80 → 120. `AssessmentPolicy.cs`: AUTO-015 gate (`assessment.values.engineer` never written or cleared by a generic save), retired D18 items removed from post-review readiness, `NormalizeWritableField`, `SerializeImpacts`. `CaseSignOffEngineerResolver.RequireEligible` shared.
- CASE-046: `CaseLifecycleRules.ValidateReviewReadiness`/`ValidateReadiness` deleted; Review-gated transitions in `EfCaseWorkflowStore` read persisted `InstructionComplete/ImagesComplete`; `CaseReadinessEvidence` kept only because `Pages/**` still constructs it (now optional/inert; web wave removes it). Staff-confirmation flags no longer written.
- Tests: new `Cases/CaseWorkspaceTests.cs`, `CaseWorkspacePersistenceTests.cs` (10); extended `AssessmentPolicyTests`, `CaseDataOperationsTests`, `CaseDataCompletenessPersistenceTests` (harness made `internal`, gained `Factory/WorkflowStore/WorkspaceStore`), `CaseWorkflowPersistenceTests` (Review gate from persisted facts).

## Verification (agent run, base 6dcea9349)

| Command | Exit | Result |
| --- | --- | --- |
| locked restore / Release build | 0 | 0 warnings, 0 errors |
| Core.Tests `CaseWorkspace\|CaseDataOperations\|AssessmentPolicy` | 0 | 87 passed |
| IntegrationTests `CaseWorkspacePersistence\|CaseDataCompleteness\|CaseWorkflowPersistence` | 0 | 55 passed, 2 failed |
| IntegrationTests `CaseWorkspacePersistence` | 0 | 10 passed |
| IntegrationTests `CaseWorkflowPersistence` | 0 | 42 passed |

The 2 failures are pre-existing fixture drift from the F seed, not B02: `CaseDataCompletenessPersistenceTests.AcceptanceSnapshotsTypedSourceProvenanceWithAutoAddedValues` and `.CorrectedInspectionAddressRetainsExtractedValueAndRecordsStaffCorrectionSource` expect an extracted physical address, but F seeds QDOS with `InspectionMode = 'image_based_assessment'` (D08/S05: QDOS defaults to IBA) and `AcceptIntake` applies it. Disposition: the seed is correct by decision; the two fixtures are re-based on a non-IBA principal in the web/cleanup wave (B-owned test file). Not weakened.

## DI patch for A

```csharp
services.AddScoped<ICaseWorkspaceStore, EfCaseWorkspaceStore>();
services.AddScoped<ISaveCaseWorkspace, SaveCaseWorkspace>();
```

## Deviations / open questions

1. Additive edit to A-owned `CaseDataEntities.cs` (`CaseDataFieldNames`) to avoid a second name list — ownership assignment requested from A. 2. `CaseReadinessEvidence` retained until the web wave. 3. `EfCaseDataStore.SaveAsync` still forces `InstructionComplete = false` (legacy path; retired when the page moves to the workspace save). 4. Enum-valued facts persist as enum names in `text` rows (`CK_CaseDataFields_ValueType` still closed to text|integer|date|inspection_mode) — acceptable; A may add a `flag` type later. 5. `CaseDataFieldNames.All` now has no callers — candidate for deletion with the file owner. 6. Workspace estimate write keys on the single open Draft — revisit with named estimates in B04 phase 2. 7. `MaximumFieldsPerSave` 120.

## Simplification pass (2026-09-06)

Fixed: three replay probes and history triples collapsed; two estimate-line writers unified; assessment write set extracted; case-data field writer/due-work scheduler extracted; one `NormalizeWritableField` for both save routes; one `SerializeImpacts` wire shape; one `RequireEligible` for sign-off. Deferred: dead `CaseDataFieldNames.All` (A-owned file); inert `*ConfirmedByStaff` members on `CaseCompleteness` (A-owned columns, PLAT-072). Accepted: `ValidateWorkflowConfiguration` remains in `ValidateAssignment`.
