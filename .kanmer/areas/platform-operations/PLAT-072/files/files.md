# Files — PLAT-072

## Where the change lands

This observed scope is mechanical across known consumers. A file with only
an intentional absence assertion need not change. The new migration ID is
reserved by this plan; refresh it before execution if the live migration tail
passes it. Root owns generated snapshot capture and compilation.

| Path | Why |
| --- | --- |
| `src/Pegasus.Core/Cases/CaseContracts.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Core/Cases/CaseDataOperations.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Core/Intake/AcceptIntake.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/IntakeAllocationEntities.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Web/Pages/Cases/Create.cshtml` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Web/Pages/Cases/Create.cshtml.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | Remove dead confirmation fields/call arguments using the existing Core/EF/Razor owner; preserve real completeness and existing state transitions. |
| `tests/Pegasus.Core.Tests/AiWork/AiWorkTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.Core.Tests/Cases/CaseWorkspaceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.Core.Tests/Reports/CaseReportDeliveryPreparationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/AssessmentVehiclePrefillWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/AutomaticVehicleLookupTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/AutomationDocumentIngressTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/AutomationMcpTestSupport.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/Browser/LayoutIntegrityTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/Browser/OperatorJourneyTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseAcceptanceReplayTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseCreateWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseTaskArchivePersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowMigrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/ConcurrencyTokenPersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/CustodyOutboxIntegrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/DueChaserSweepPersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/GlassRepairEstimateCallbackWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/ImageCaseCustodyIntegrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/ImageIntakePersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/ImageIntakeWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/MailWorkspaceWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/OrganizationAdministrationPersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/ProviderApiCaseDataSnapshotPersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/ProviderApiSubmissionTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/ProviderInspectionModeAcceptanceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/QdosAllocationRecoveryTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/QdosTriageIntegrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/RailCountsWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/RetainedMailPersistenceTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/SendToAiIntegrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/TriageQueuesWebTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/UnidentifiedReconciliationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/VehicleLookupBackfillTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `tests/Pegasus.IntegrationTests/VehicleWorkflowTerminalTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | Remove only the four obsolete mapped properties; preserve all other schema. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260907221500_RemoveCaseStaffConfirmation.cs` | One EF removal migration/target model, four obsolete columns only. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/20260907221500_RemoveCaseStaffConfirmation.Designer.cs` | One EF removal migration/target model, four obsolete columns only. |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | Remove obsolete constructor/SQL/form fixture fields only; retain scenario assertions and add focused absence/readiness/migration checks where appropriate. |
| `docs/design/test-ui/pages/case-create--default.html` | Root-owned scoped capture; no artificial bytes or whole-catalogue recapture. |
| `docs/design/test-ui/pages/case-details--default.html` | Root-owned scoped capture; no artificial bytes or whole-catalogue recapture. |
| `docs/design/test-ui/pages/case-details--conflict.html` | Root-owned scoped capture; no artificial bytes or whole-catalogue recapture. |
| `docs/design/test-ui/pages/case-details--unavailable.html` | Root-owned scoped capture; no artificial bytes or whole-catalogue recapture. |
| `docs/design/test-ui/index.html` | Root-owned scoped capture; no artificial bytes or whole-catalogue recapture. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| docs/frd/frd-01-case-identity-and-lifecycle.md | Completeness from persisted facts; handoff is implicit review; other transitions remain. |
| docs/frd/frd-12-operator-experience.md | D44 no-review controls; existing controls and page economy. |
| docs/design/README.md | Existing semantic native forms, labels and no explanatory copy. |
| scripts/Test-MigrationGrants.ps1 | Existing table-grant coverage; drop-only migration does not need fabricated new permissions. |
| tests/Pegasus.IntegrationTests/RepairSpecificationMigrationTests.cs | Seeds an explicitly historical schema; old columns remain in this historical fixture. |
| tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs | Same historical-schema exception, not a current runtime caller. |
| src/Pegasus.Core/Workflow/CaseReadiness.cs | Existing real completeness owner; not replaced by a review flag or handoff rewrite. |
| src/Pegasus.Core/Reports/CaseReportDeliveryPreparation.cs | Report approval/readiness is distinct and unchanged. |

## Ripple effects

Constructor shape, EF materialization, SQL column/value alignment, pending
allocation reconstruction, command fingerprints, current model snapshot and
exact migration lists must agree. Existing receipt/principal/reference, lease,
authorization and replay protections remain. Root's source freezes and claim
clearance govern file access even though these are isolated worktrees.

## Out of scope

Protected operator-notes meaning; historical migrations/designers; standalone
Audit ConfirmedByStaffId; Glass/EVA/handoff mechanics; readiness-policy rewrite;
removal of actual InstructionComplete or ImagesComplete values; new packages,
frameworks, stores, compatibility paths, cloud writes or broad test infrastructure.
