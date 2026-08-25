# Files

## Change files

| File/module | Required change | Risk and reuse |
| --- | --- | --- |
| `src/Pegasus.Core/Cases/CaseDataOperations.cs` | Make instruction and image completeness unconditional in the existing `CaseCompletenessPolicy`; retain the existing staff-review and automatic-intake rules. | Central owner reused by acceptance and later confirmation. Do not introduce another export/readiness policy. |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | Remove the two obsolete completeness-toggle fields from `CaseWorkflowConfiguration`. | Constructor ripple across callers/tests; retain policy identity and staff-review settings. |
| `src/Pegasus.Core/Workflow/WorkflowConfigurationAdministration.cs` | Remove the two obsolete fields from the administrator update command. | Keep authorization, versioning, reason and replay behaviour unchanged. |
| `src/Pegasus.Core/Workflow/DefaultCaseWorkflowConfiguration.cs` | Remove obsolete constructor arguments. | Mechanical contract update only. |
| `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs` | Make assignment readiness require instruction and image completeness unconditionally while retaining configurable staff-review gates. | Reuse the existing validation method; no new abstraction. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyEntities.cs` | Remove obsolete persisted properties. | Schema change must agree with EF configuration and migration. |
| `src/Pegasus.Infrastructure/Persistence/AdministrationPolicyModelConfiguration.cs` | Remove obsolete property configuration/seed values. | Preserve the existing workflow policy row and staff-review defaults. |
| `src/Pegasus.Infrastructure/Persistence/EfWorkflowConfigurationStore.cs` | Stop reading, writing, snapshotting and replay-comparing the removed options. | Preserve exact replay for the remaining supported update shape. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<new migration>.*` and `PegasusDbContextModelSnapshot.cs` | Drop the two obsolete workflow-configuration columns through the normal migration mechanism. | Pre-release roll-forward schema cleanup; no data conversion or compatibility path. |
| `src/Pegasus.Web/Pages/Administration/Configuration.cshtml.cs` | Remove the two bindable properties and update-command arguments. | Prevent a caller from submitting a waiver that Core no longer supports. |
| `src/Pegasus.Web/Pages/Administration/Configuration.cshtml` | Remove the two editable/read-only optionality controls; keep the remaining staff-review configuration. | Avoid displaying a false `Not required` state. No explanatory replacement copy is needed. |
| `tests/Pegasus.Core.Tests/Cases/AutomaticCaseReadinessTests.cs` | Update configuration construction and preserve proof that automatic intake waives staff review only, never missing evidence. | Reuses the existing regression tests from [[CASE-013]]. |
| `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` | Add/adjust the completeness matrix and update constructors. | Direct, fast proof of the Core owner. |
| `tests/Pegasus.Core.Tests/Lifecycle/CaseReviewReadinessTests.cs` and assignment tests | Preserve return/reopen coverage and add configured-assignment coverage if not already present. | Verifies all explicit lifecycle entry/assignment paths agree. |
| `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs` | Prove persistence keeps incomplete cases out of `Review`; update fixed configuration shape. | Covers both stored evaluation and workflow state. |
| `tests/Pegasus.IntegrationTests/AdministrationPolicyPersistenceTests.cs` and administration web tests | Update the supported configuration contract/UI and replay assertions. | Prevent stale admin controls and persistence expectations. |
| Other compile-time `CaseWorkflowConfiguration` / `UpdateWorkflowConfigurationRequest` call sites | Remove the two constructor arguments mechanically. | Compiler supplies the complete caller census; do not refactor unrelated tests. |

## Ripple effects

- `EfCaseAcceptanceStore` and `EfCaseDataStore` should require no new branching: both already consume `CaseCompletenessPolicy.SatisfiesPolicy`.
- EVA Export should require no change: it already gates on `Review`, which is the one readiness owner.
- Existing CASE-013 behaviour must remain: automatic definitive intake can skip staff confirmation only after both completeness facts are true.
- The migration census test may require the new migration id, per the repository runbook.
- FRD-01, FRD-07 and `docs/capabilities.md` already state the intended mandatory behaviour; no governing-document meaning change is required.

## Context files

| File | Why the implementer must read it |
| --- | --- |
| `docs/frd/frd-01-case-identity-and-lifecycle.md:41-59` | Governs mandatory lifecycle gates and forbids policy from removing them. |
| `docs/frd/frd-07-eva-and-external-engineering-handoff.md:8-26` | Establishes `Review` as the single export-readiness decision; prevents duplicate EVA checks. |
| `docs/capabilities.md:139-142` | Confirms CASE-13/14/15 allocation and the distinction between completeness and staff review. |
| `src/Pegasus.Core/Cases/CaseContracts.cs:124-133` | Existing simple readiness predicate and the automatic staff-review exception. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAcceptanceStore.cs:262-264` | Shows how evaluation selects a new case's initial state. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs:97-120` | Shows how confirmation evaluation promotes or demotes an existing case. |
| `src/Pegasus.Infrastructure/Persistence/EfQueuedCustodyProcessor.cs:583-597` | Existing alternate promotion path already requires both facts; avoid regressing it. |
| [[CASE-013]] files/plan | Explains why only staff confirmation is waived for automatic definitive intake. |
| `AGENTS.md` project principles and simplicity rails | Requires one coherent pre-release state and removal of unsupported compatibility/options. |

## Out of scope

- Adding field-by-field EVA export validation.
- Changing which case details count as an instruction-completeness judgement.
- Changing image eligibility rules.
- Removing or redesigning the still-supported staff-review gates.
- Adding compatibility layers, feature flags, fallback policies, or rollback preservation.
- Unrelated workflow, custody, intake, or administration refactoring.
