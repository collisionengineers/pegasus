# CASE-049 files

## Where the change lands

| Path | Why |
| --- | --- |
| src/Pegasus.Core/Assessment/AssessmentWorkspace.cs | Remove export tuple and duplicate report access policy; retain one lifecycle/read-only rule. |
| src/Pegasus.Core/Reports/AssessmentReportProjection.cs | Use the single access owner. |
| src/Pegasus.Infrastructure/Persistence/EfAssessmentAccessSource.cs | Query lifecycle state without EVA/history subqueries. |
| src/Pegasus.Infrastructure/Persistence/EfAssessmentWorkspaceSource.cs | Same native projection; no export dependency. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs | Assignment becomes atomic existing ReportPreparation transition with assignment/sign-off and readiness guard. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | Single native section guard and known tuple consumers. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml | Native handoff action/dialog in existing action bar. |
| src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs | Current handoff completion label; no redundant StartWork UI handler. |
| src/Pegasus.Web/Pages/Cases/Shared/_EvaHandoff.cshtml | Remove native assignment from optional EVA dialog; retain actual EVA/sign-off controls. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml | Remove mandatory second Start report preparation control/dialog. |
| src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs | Existing action labels if required by caller search. |
| tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs | Native access no-export policy cases. |
| tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs | Known assignment contract coverage. |
| tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs | Update access-state constructor fixture. |
| tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs | Update known tuple fixture and native section coverage. |
| tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs | Real native workspace without export, reopen/read-only evidence. |
| tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs | Atomic handoff, replay/readiness/lease/version and report-history acceptance. |
| tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs | Actual assignment POST and handoff label. |
| tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs | Replace obsolete StartWork handler/capture expectations. |
| tests/Pegasus.IntegrationTests/CaseSignOffEngineerWebTests.cs | Existing optional EVA/assignment dialog assertions if present; resolve actual path before editing. |
| docs/frd/frd-01-case-identity-and-lifecycle.md | Native handoff replaces mandatory EVA progression. |
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Native estimate/report access agrees. |
| docs/frd/frd-12-operator-experience.md | One human handoff action. |
| docs/design/README.md | Action/dialog mapping reflects user-authorized native handoff. |
| docs/design/test-ui/*case-details* | Scoped generated captures only. |

## Context files

| Path | What it tells the implementer |
| --- | --- |
| src/Pegasus.Core/Lifecycle/CaseLifecycle.cs | Assignment eligibility/sign-off resolution and legitimate existing headless StartCaseWork caller. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs | Existing mutation/replay/lease and report-evidence temporal guards share the authoritative state event. |
| docs/operator-notes.md | Protected historical statements; current 7 September direction makes EVA optional. Do not rewrite history. |
| docs/index.md | Canonical document ownership. |
| docs/engineering.md | One Core business owner and focused evidence tiers. |

## Ripple effects

Constructor/compiler consumers and UI snapshots must agree. No migration, runtime permissions, packages or cloud action. Exact files found by rg override no guessed absent path: record any concrete missing expected file refinement before implementation.

## Out of scope

PLAT-072 schema/confirmation removal, TICK-035 principal classification, TICK-085 estimate parser, ENG-041 provider recovery, optional EVA transport and historical foreign workspaces/claims.
