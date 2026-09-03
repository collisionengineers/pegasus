# Files — ENG-031 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

## Planned change set

**VERIFIED — `Test-Path \
src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImages.cshtml; Test-Path \
src/Pegasus.Web/wwwroot/js/cropper.js`** Both explicitly owned Web files are
absent and will be created.

**ASSUMED — supplied EPIC-012 ownership context** Conditional report-approval
rows below depend on resolving Research question one; all other rows are the
minimum mapped change set. The wrapper confirmed every "change" path below
exists on `origin/dev` 897db953.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Reports/ReportImageCuration.cs` | create | Core-owned roles, crop geometry validation, ordering, curation versions, commands, and ports. | `CaseEditAuthority`; repair-specification versioning. |
| `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | change | Require valid curation before generation and project selected source versions in deterministic order; rewrite the stale "Report images" remark. | `IAssessmentReportProjectionSource`; `AssessmentReportProjection`. |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | change | Carry role, normalized rendition geometry, source-version identity, and source hash through the immutable rendering snapshot. | `ReportImageEvidence.Validate`. |
| `src/Pegasus.Core/Workflow/CaseWorkflowContracts.cs` | change, conditional | Bind the approved report artifact to its immutable curation snapshot if approval is the issue boundary. | `ReportApprovalEvidence`; `RecordCaseReportApprovalRequest`. |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` | change | Add curation-version, selected-image, and immutable-snapshot entities. | Existing assessment entity convention. |
| `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` | change | Add table mappings, value bounds, role constraints, unique ordering, foreign keys, and concurrency constraints. | `CaseRepairSpecifications` mapping. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | change | Expose new curation entity sets and include their configuration. | Existing assessment model registration. |
| `src/Pegasus.Infrastructure/Persistence/EfReportImageCurationStore.cs` | create | Persist/query curation under the Case mutation guard, with replay, history, and version protection. | `CaseMutationGuard`; `EfCaseAssessmentStore`. |
| `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs` | change | Select only curated, eligible source versions and pass their original bytes/hashes to rendering. | `IDocumentContentStore.ReadVersionsAsync`; occurrence query. |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | change | Render the selected roles in saved order and apply crop/rotation only to the in-memory rendition. | Existing renderer; SkiaSharp; Playwright. |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs` | change, conditional | Persist curation-snapshot linkage on the approval record if approval issues the report. | `CaseReportApprovalEntity`. |
| `src/Pegasus.Infrastructure/Persistence/CaseWorkflowModelConfiguration.cs` | change, conditional | Configure that approval-to-snapshot link and delete restrictions. | `CaseReportApprovals` mapping. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs` | change, conditional | Validate and persist the approved artifact's referenced curation snapshot. | `RecordReportApprovalAsync`. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | change | Register the curation persistence adapter behind its Core port. | Existing scoped-store registrations. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/[timestamp]_ReportImageCuration.cs` | create | Create curation/snapshot schema and exact runtime-role grants (capacity-one lock path). | Current migration/grant pattern. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/[timestamp]_ReportImageCuration.Designer.cs` | create | EF migration metadata. | EF-generated migration convention. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | Reflect the new EF model. | EF-generated snapshot convention. |
| `scripts/Invoke-AzureDatabaseBootstrap.ps1` | change | Add the new grant-carrying migration to the production runtime permission census. | Existing `$expected` permission matrix. |
| `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` | change | Add new tables to the expected schema/runtime-role verification. | Existing migration-role tests. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change | Define every operator-visible role/action label in its single owner (capacity-one lock path). | `OperatorLabels.CaseWorkspace`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImages.cshtml` | create | Report-section partial for image selection, order controls, crop action, and readiness state (capacity-one lock path). | Evidence viewer URLs; existing form/lease fields. |
| `src/Pegasus.Web/wwwroot/js/cropper.js` | create | Pointer, keyboard, aspect, preview, crop, rotation, reset, and ordering client behaviour. | `site.js` IIFE and accessibility patterns. |
| `tests/Pegasus.Core.Tests/Reports/ReportImageCurationTests.cs` | create | Cover eligibility, distinct primary roles, order, geometry, rotation, snapshots, and invalid input. | Existing Core report-test conventions. |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` | change | Prove report readiness and projection use curation rather than occurrence order. | `ReadyInput` fixtures. |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` | change | Prove source hashes remain source hashes while rendition metadata is accepted. | `ReportImageEvidence` fixtures. |
| `tests/Pegasus.IntegrationTests/ReportImageCurationPersistenceTests.cs` | create | Prove migration mapping, stale writes, lease enforcement, snapshots, and source-byte preservation. | Assessment persistence test support. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | change | Prove selected report image order and cropped/rotated rendition output. | Existing Playwright renderer provider. |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | change | Update draft readiness and generated-report assertions for required curation. | Existing report-draft Web fixtures. |
| `tests/Pegasus.IntegrationTests/CaseReportApprovalWebTests.cs` | change, conditional | Prove approved artifact/snapshot linkage if approval is the issue boundary. | Existing approval replay test. |

Wrapper note: the `report-image` and `cropper` classes named by
`docs/design/README.md` have no rules in `wwwroot/css/site.css`. That file is
a shared-lock path owned by another lane below; the plan must either obtain the
lock in the EPIC-012 order or record a hand-off so the partial ships no inline
styles (production CSP).

## Must not touch

**ASSUMED — supplied EPIC-012 dependency map** The following files belong to
the sections-move, CASE-027 evidence, or UIIMP-014 lane. ENG-031 must wait for
their hand-off rather than modify them.

- `src/Pegasus.Web/Pages/Cases/Details.cshtml`
- `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml`
- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml`
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml`
- `src/Pegasus.Web/Pages/Cases/Vehicle.cshtml`
- `src/Pegasus.Web/Pages/Cases/Vehicle.cshtml.cs`
- `src/Pegasus.Web/Pages/Cases/Custody.cshtml`
- `src/Pegasus.Web/Pages/Cases/Custody.cshtml.cs`
- `src/Pegasus.Web/wwwroot/css/site.css`
- `src/Pegasus.Web/wwwroot/js/site.js`
- `docs/design/test-ui/catalogue.json`
- `docs/design/test-ui/**`

**VERIFIED — `rg -n -C 4 'Assessment|Case record|301' \
docs/frd/frd-12-operator-experience.md docs/design/README.md`** Those files
remain the dependency lane's responsibility because it must move the Report
section onto the Case page, make Assessment a 301, and regenerate the resulting
routed-page Test UI snapshots.
