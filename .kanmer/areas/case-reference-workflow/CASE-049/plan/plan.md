# Plan — CASE-049 native Engineer handoff

## Objective

One human handoff moves a ready Case to With Engineer and enables native estimating without EVA.

## Starting state

Evidence: research/research.md@3c0b558c3e2f9cb5; files/files.md@4689efb462748ddf. ENG-041 merged baafa29e0f7002b8235aa43bf333f5d9bb172828. PLAT-072 now owns overlapping constructor/fixture cleanup: wait for its merge before taking this ticket, then use fresh origin/dev. No declared research sources. Preserve old CASE-040/ENG-034/CASE-047 claims/workspaces; their integrated predecessor work is not this residual correction.

## Governing docs

Meets FRD-11/D30: Engineer sections always viewable, writable only With Engineer under lease/role/version, and reports do not require EVA. Explicit current user instruction authorizes modifying FRD-01/12 and the existing design action contract: native handoff is the review action, EVA optional. Preserve protected historical operator-notes verbatim; current September directive controls. No new ADR, package, schema or runtime.

## Required changes

- AssignCaseEngineer supplies ReportPreparation to its existing assignment persistence method; extend that port with the explicit destination argument. The adapter uses the existing state_<destination> event and transaction/discriminator, assigns Engineer/sign-off, checks persisted readiness and advances once. Existing lease/version/replay/history remain. No separate transaction or double transition.
- Remove latest-review/export fields and both corresponding SQL subqueries from assessment access/projection. Keep one Core native action-state rule (ReportPreparation/PostReport/PostReportComplete), with read-only true outside the two writable states. Remove duplicate CanOpenReports and its delegate parameter/helper where now redundant. Read-only workspace projection loads retained sections in all states rather than using an action gate to hide content; mutation/report-preview eligibility remains separate and unchanged except no EVA prerequisite.
- Add Hand to Engineer to the existing Case action bar in Review/edit context. Its ordinary dialog chooses an eligible Engineer using the already loaded list; post existing AssignEngineer envelope with fixed attributable handoff reason, no reviewed checkbox or extra reason input. Move assignment out of _EvaHandoff, retaining optional actual EVA/sign-off actions. Remove Start report preparation UI/dialog/POST; headless Core StartCaseWork remains a supported handoff to an already-assigned Engineer through TransitionCase (including return/review cases), not a second mandatory UI step.
- Align canonical behavior/UI statements and current constructor/caller fixtures. Existing labels own repeated handoff wording. No explanatory UI copy.

## Expected files

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Modify | src/Pegasus.Core/Lifecycle/CaseLifecycle.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Core/Assessment/AssessmentWorkspace.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Core/Reports/AssessmentReportProjection.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Infrastructure/Persistence/EfAssessmentAccessSource.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Infrastructure/Persistence/EfAssessmentWorkspaceSource.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Web/Pages/Cases/Details.cshtml | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Web/Pages/Cases/Shared/_EvaHandoff.cshtml | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml | Bounded owner/caller/test/document change from files map. |
| Modify | src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.Core.Tests/Lifecycle/AssignCaseEngineerTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.IntegrationTests/CaseWorkflowPersistenceTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.IntegrationTests/CaseWorkflowWebTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs | Bounded owner/caller/test/document change from files map. |
| Modify | docs/frd/frd-01-case-identity-and-lifecycle.md | Bounded owner/caller/test/document change from files map. |
| Modify | docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Bounded owner/caller/test/document change from files map. |
| Modify | docs/frd/frd-12-operator-experience.md | Bounded owner/caller/test/document change from files map. |
| Modify | docs/design/README.md | Bounded owner/caller/test/document change from files map. |
| Modify | docs/design/test-ui/pages/case-details--default.html | Bounded owner/caller/test/document change from files map. |
| Modify | docs/design/test-ui/pages/case-details--conflict.html | Bounded owner/caller/test/document change from files map. |
| Modify | docs/design/test-ui/pages/case-details--unavailable.html | Bounded owner/caller/test/document change from files map. |
| Modify | docs/design/test-ui/index.html | Bounded owner/caller/test/document change from files map. |

## Do not modify

- docs/operator-notes.md
- infra/**
- src/Pegasus.Infrastructure/Persistence/Migrations/**
- corpus/**

## Constraints

Reuse existing Core assignment, persistence mutation/lease/replay and native dialogs. No required external service or synthetic domain email. Actual state-transition history must continue to admit later valid report-Sent evidence. No invented external delivery/first-export evidence. Original errors remain evidence; no assertion weakening.

## Ordered steps

1. Replace obsolete export access tuple/queries and duplicate report gate; update constructor consumers and state/access tests. Prove visible retained workspace versus mutation eligibility across Review, With Engineer and terminal states.
2. Wire atomic Core-selected handoff in existing store and actual Case action/dialog. Update known direct-store/POST callers, preserve readonly/lease behavior, and test report-history timing plus exact replay/incomplete/unauthorized/stale refusals.
3. Align FRD-01/11/12/design action text; regenerate only case-details snapshot scope and verify. Record focused command results and all failures; submit PR to dev for independent review.

## Acceptance checks

Actual caller /Cases/{id} → Workflow AssignEngineer → Core AssignCaseEngineer → EfCaseWorkflowStore. Ready handoff changes Engineer/state/sign-off in one version/history event, no EVA proxy. Native workspace and Glass/import/report controls are available appropriately without export. Existing tests prove state/lease/version refusal, exact replay and Sent-evidence acceptance after handoff. No separate reviewed action.

## Commands

Root owns native PowerShell7 verification in this ticket's worktree. Locked restore and one Release solution build; focused Core filter FullyQualifiedName~AssessmentPolicyTests|FullyQualifiedName~AssignCaseEngineerTests. Integration filter FullyQualifiedName~CaseWorkflowPersistenceTests|FullyQualifiedName~AssessmentPersistenceIntegrationTests.AssessmentAccess|FullyQualifiedName~CaseDetailsWebTests.Workflow|FullyQualifiedName~CaseDetailsWebTests.Lifecycle|FullyQualifiedName~CaseEngineerSectionsWebTests plus the new named native-handoff and retained-workspace cases. Scope exact capture using scripts/Update-TestUiSnapshots.ps1 -Scope case-details -CaptureFilter FullyQualifiedName~CaseDetailsWebTests; then -Verify -SkipCapture -Scope case-details and scripts/Test-UiCatalogue.ps1. Prefer a narrower existing capture cohort once exact test names are pinned before execution. Final integrated CI remains controller-owned; no repeated full test rails or stress runs.

## Failure and deviation rules

Stop on failing checks, unresolved actual caller, unplanned file/dependency/schema need or role/readiness change. Report and amend bounded plan before implementing an expansion. Preserve original failures; rerun only changed failures plus affected regression cases.

## Stop condition

After focused evidence and scoped snapshot checks, publish one bounded PR and stop for independent review. No author merge, next ticket, cloud write or deployment. Controller later owns merge/exact integrated proof and authorized release.

## Current execution readiness — 8 September 2026

PLAT-072 is Done and closed at d442366787d452da22d36719272d4eb79dc1afde.
ENG-041's original implementation is integrated; its post-merge correction
planf0aa4318d6dca111/files1d4c177e23c8d8b9 now reserves only custody/report
source-census owners, three separate tests and FRD-06. Root read both full
maps: there is no overlap with this CASE-049 file scope. Its historical Case
Details files are not future correction authority. Therefore CASE-049 may
proceed in parallel; it need not wait for unrelated ENG-041 closeout.

Root authorizes pack_reconcile to obtain a fresh execution packet, create and
take the isolated CASE-049 branch/worktree from accepted dev19e6f523 (or its
fresh fetched descendant after confirming no scope drift), implement the
bounded native handoff and access changes, and freeze for root's focused checks.
No author build/test, self-review, merge, provider or cloud operation. Root
remains sole heavy verifier. Preserve current failed Glass proof and all foreign
historical claims; this ticket does not fix the Glass custody boundary.
