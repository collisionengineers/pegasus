# Files — ENG-029

Preparation against dev 498144b0bb55b68fd53b9a31ffc89ef90622c73a.
This current map replaces af87e92a3c8c7d0a; historical versions remain.
No file is claimed or editable until root approves the whole plan and releases
overlapping active/historical ownership. No new source file is planned.

## Exact intended production and documentation edits

| File | Bounded purpose |
| --- | --- |
| src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | Replace existing Save's ISaveCase call with ISaveCaseWorkspace; bind/route existing canonical editor values and typed fields; preserve unshown accepted facts and posted concurrency intent; load metadata-only readiness and eligible signer choices; extend current-versus-proposed presentation. No Estimate import/Glass handler changes. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml | Retain the one case-edit-form/authority/reason; align hidden accepted values and remove obsolete forced-NotReady save warning. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseSettlement.cshtml | Native form-associated D41 editors and read-only derived figures, current Estimate repair days; no independent form/save. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml | Form-associated comments/fee/sign-off/current content choices/date override; retain existing statement display and generation/preview/delivery/image components without redesign. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml | Add the one Vehicle History control/read-only value in its current design location only; preserve existing vehicle evidence/suggestion callers. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Extend existing bounded proposed-value retention for the same editor fields, explicit clears/false and safe signer display; retain authority exclusions/size signals and existing errors. |
| src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs | Existing presentation owner for missing labels and the single editor-field presentation map shared by binding/retention/rendering where needed. Canonical paths/types remain Core-owned; no generic form framework. |
| src/Pegasus.Core/Assessment/AssessmentContracts.cs | Remove unused SettlementRepairDuration constant and writable definition only; add no vocabulary. |
| src/Pegasus.Core/Assessment/AssessmentPolicy.cs | Include supported PostReport in existing writable-state owner only. Do not broaden any other lifecycle state or finding authority. |
| src/Pegasus.Core/Reports/AssessmentReportProjection.cs | Expose the existing settlement calculation as one pure incomplete-input-safe projection reused by Case display and report generation; no duplicate formula/output record or changed report semantics. |
| docs/frd/frd-06-vehicle-and-engineering-evidence.md | Clarify one workspace editor and Estimate-owned repair days, typed storage/recovery ownership. Preserve TICK-085 canonical import decisions. |
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Clarify current Report/Vehicle field writer and metadata-based named readiness, existing write states and sign-off/content/date rules only. |
| docs/design/README.md | Align affected single-Save/Settlement/Vehicle/Report behavior and remove obsolete forced-demotion warning reference. No independent design or new screen. |

## Exact existing test and generated outputs

| File | Required evidence/change |
| --- | --- |
| tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs | Missing PostReport write acceptance with unchanged other-state boundaries; dead path refusal and existing normalization/finding invariants. |
| tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs | Existing D41 exact values/equity/Estimate repair days reused by both projections, including missing accepted input. |
| tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs | Update existing recording save caller; all new fields/typed members, old hidden facts, authority/antiforgery, refusal retention, partial-load/no-render side effects. Existing fixture only. |
| tests/Pegasus.IntegrationTests/CaseEditModeWebTests.cs | Existing one Save/Discard/form association/current-versus-proposed and keyboard/cross-section intent; no new browser harness. |
| tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs | Actual SQL combined save/replay/rollback, PostReport and preserved Case facts/completeness/current-report invalidation. |
| tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs | Actual page metadata-readiness and saved-field-to-preview input parity with existing renderer seam; no external image read on ordinary GET. |
| docs/design/test-ui/pages/case-details--default.html | Scoped fresh capture/update/verify of changed route. |
| docs/design/test-ui/pages/case-details--conflict.html | Scoped current-versus-proposed/readonly refusal evidence. |
| docs/design/test-ui/pages/case-details--unavailable.html | Only if scoped generator actually changes this existing output; preserve no-data behavior. |
| docs/design/test-ui/index.html | Only if scoped generation changes the catalogue index; no unrelated routes. |

## Reuse/read-only boundaries

CaseWorkspace.cs, EfCaseWorkspaceStore, EfAssessmentReportProjectionSource,
CaseReportGeneration/CaseReportReadiness, CaseSignOffEngineerResolver,
IStaffAccountQueries, AssessmentAccessPolicy, EstimateTotals/ReportRepairCosts,
CaseDataCompletenessPersistenceTests.CaseDataHarness, existing Case page CSS,
site.js and capture scripts are existing reuse points, not a second system.
Use the already registered ISaveCaseWorkspace and ICaseReportSnapshotSource;
no DI change is expected. New engineering form values must be checked using
the stricter Core AssessmentAccessPolicy, preserving ordinary-data save states.

If implementation proves that an existing Core/store or other fixture needs
a change beyond this exact map, stop and amend research/map/plan with root
before editing. In particular, do not alter typed-section contracts to hide
incomplete Web mapping, widen a store's lifecycle permissions, or silently
change report generation behavior.

## Ownership stop

TICK-085 currently overlaps Details.cshtml.cs, CaseWorkspaceLabels.cs,
FRD-06 and generated Case snapshots/index. ENG-034, CASE-040 and CASE-047
retain historical taken records over related Case/report areas. Their
integrated code is evidence, not permission to erase their claims.
Await explicit root sequencing/clearance. ENG-031 image preparation and
ENG-036 damage diagram are excluded. No take/worktree/source write occurred.
