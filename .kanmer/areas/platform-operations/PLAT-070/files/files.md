# Files — PLAT-070

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | change | Remove retired configuration and readiness-review members. | `CaseReadinessEvidence`, `CaseWorkflowConfiguration` |
| `src/Pegasus.Core/Workflow/DefaultCaseWorkflowConfiguration.cs` | change | Remove now-obsolete default policy flags. | Default configuration implementation |
| `src/Pegasus.Core/Workflow/WorkflowConfigurationAdministration.cs` | change | Remove review-toggle update request members. | Existing authorization/versioning command |
| `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` | change | Make assignment readiness completeness-only. | `ValidateReviewReadiness` |
| `src/Pegasus.Core/Cases/CaseDataOperations.cs` | change | Remove configuration-dependent staff-review completeness evaluation. | `CaseCompletenessPolicy.Evaluate` |
| `src/Pegasus.Core/Cases/CaseContracts.cs` | change | Remove staff-confirmation state (`InstructionConfirmedByStaff`/`ImagesConfirmedByStaff`) per D44's full wording. | `CaseCompleteness` |
| `src/Pegasus.Core/Intake/IntakeAllocation.cs` | change | Stop constructing retired confirmation fields. | Existing completeness construction |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs` | change | Remove persisted workflow-review columns. | `WorkflowConfigurationEntity` |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs` | change | Remove review seed/model members. | Existing model configuration |
| `src/Pegasus.Infrastructure/Persistence/EfWorkflowConfigurationStore.cs` | change | Remove review persistence, replay, mapping, and audit snapshot members. | Existing store/replay handling |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | change | Remove persisted case confirmation fields (full D44 deletion). | Existing entity mapping |
| `src/Pegasus.Infrastructure/Persistence/IntakeAllocationEntities.cs` | change | Remove retired confirmation storage. | Existing case entity |
| `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` | change | Stop reading/writing/resetting confirmation fields. | `CaseCompletenessPolicy` |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs` | change | Stop persisting confirmation state on accepted cases. | Existing acceptance snapshot |
| `src/Pegasus.Infrastructure/Persistence/EfIntakeAllocationStore.cs` | change | Stop storing/mapping confirmation state. | Existing allocation mapping |
| `src/Pegasus.Infrastructure/Persistence/EfLinkedCaseReplacementStore.cs` | change | Stop copying retired confirmation state. | Existing replacement mapping |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs` | change | Stop using the former confirmation-based readiness rule. | `CaseCompletenessPolicy` |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_RemoveStaffReviewRequirements.cs` | create | Drop retired workflow and case-confirmation columns. | EF migration convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_RemoveStaffReviewRequirements.Designer.cs` | create | EF migration metadata. | EF scaffold output |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | Match the post-migration model. | EF snapshot |
| `src/Pegasus.Web/Pages/Administration/Configuration.cshtml` | change | Remove the staff review requirements panel. | Existing configuration form |
| `src/Pegasus.Web/Pages/Administration/Configuration.cshtml.cs` | change | Remove review-checkbox binding and update command inputs. | Existing page model |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change | Delete retired review-panel labels. | Central label owner |
| `src/Pegasus.Web/Pages/Cases/Shared/_ReadinessHiddenFields.cshtml` | change | Remove review hidden inputs. | Shared completeness inputs |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | change | Remove review-dependent field/rendering handling. | Shared workflow partial |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | change | Remove staff-review field UI only (narrow, shared-lock path). | Case detail form |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | change | Remove corresponding bound handling. | Existing page model |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | change | Reduce the shared readiness factory and retained fields. | `Readiness` helper |
| `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs` | change | Remove review-post parameters (review field handling only). | `Readiness` helper |
| `src/Pegasus.Web/Pages/Cases/Closure.cshtml.cs` | change | Remove review-post parameters on reopen. | `Readiness` helper |
| `tests/Pegasus.Core.Tests/Lifecycle/CaseReviewReadinessTests.cs` | change | Prove complete instructions/images reach Review without review flags. | Lifecycle test fixture |
| `tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs` | change | Update reduced readiness/configuration constructors. | Assignment tests |
| `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs` | change | Replace retired staff-review scenarios with completeness-only assertions. | `CaseCompletenessPolicy` |
| `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` | change | Update configuration constructors and policy tests. | Case-data tests |
| `tests/Pegasus.Core.Tests/Cases/ImmediateExternalPublicationTests.cs` | change | Update configuration constructor. | Existing fixture |
| `tests/Pegasus.Core.Tests/Identity/AdministrationPolicyTests.cs` | change | Update workflow configuration administration tests. | Existing command tests |
| `tests/Pegasus.IntegrationTests/WorkflowConfigurationWebTests.cs` | change | Assert no review panel/checkboxes and preserve access behavior. | Administration route test |
| `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs` | change | Replace retired configuration update/replay assertions. | EF store test |
| `tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs` | change | Remove posted review values and expected evidence values. | Case workflow harness |
| `tests/Pegasus.IntegrationTests/CaseClosureWebTests.cs` | change | Remove posted review values and expected evidence values. | Closure harness |
| `tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs` | change | Update readiness/configuration construction. | Existing workflow harness |
| `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs` | change | Update fixed configuration and completeness persistence. | Existing fixture |
| `tests/Pegasus.IntegrationTests/CaseMatchIntegrationTests.cs` | change | Update fixed configuration. | Existing fixture |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | change | Update fixed configuration. | Existing fixture |
| `tests/Pegasus.IntegrationTests/ProviderInspectionModeAcceptanceTests.cs` | change | Update fixed configuration. | Existing fixture |
| `tests/Pegasus.IntegrationTests/ProviderApiCaseDataSnapshotPersistenceTests.cs` | change | Update fixed configuration. | Existing fixture |
| `docs/design/test-ui/pages/administration-configuration--default.html` | change | Regenerated snapshot without review panel. | `Update-TestUiSnapshots.ps1` |
| `docs/frd/frd-01-case-identity-and-lifecycle.md` | change | Replace staff-review gate language with D44 completeness-only rule. | D44 |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | change | Amend D39 damage zone from severity/type/note to severity/note. | D45 |
| `docs/frd/frd-12-operator-experience.md` | change | Record D44 workflow configuration/readiness behaviour. | D44 |
| `docs/design/README.md` | change | Remove Review panel from configuration list and Type from damage-diagram row. | D44/D45 |
| `.kanmer/groups/EPIC-012/context.md` | change | Confirm/cross-reference D44/D45 in the governing group context (D44/D45 already appear in this document — verify no further edit needed beyond what already exists at plan time). | Kanmer group document |
| `.kanmer/groups/EPIC-011/context.md` | change | Record inherited D44/D45 amendment if EPIC-011 needs an explicit cross-reference (EPIC-012 already states D44-D46 supersede EPIC-011 D1-D28 where they differ; confirm at plan time whether an edit here is actually required). | Kanmer group document |

## Must not touch

- `src/Pegasus.Web/Pages/Cases/Details.cshtml` outside the narrowly-owned
  review-field deletion; CASE-038 owns its frame redesign and is blocked by
  this ticket (serial, shared lock).
- Any Damage model, diagram UI, report projection, labels, or tests owned by
  ENG-035/ENG-036; PLAT-070 records D45 in governing docs only, not
  implementation.
- `C:/Users/PC/Downloads/Pegasus_UI_v2_src/**` and `Pegasus_UI_v2_notes.md`;
  they are read-only mockup sources.
- Historical migrations and external EVA schema/reference documents.
- `Pages/Shared/*`, `Pages/Administration/Shared/*`, `site.css`, and
  `site.js`; none is required for this deletion.
