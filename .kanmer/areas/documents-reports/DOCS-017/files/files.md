# Files — DOCS-017 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | change | Replace the fixed accepted-signatory dictionary and key-based validation with the Case/account-supplied report-signatory tuple; permit absent qualifications. | `ReportEngineer`, `AssessmentReportSnapshot.Validate()` |
| `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | change | Carry the signatory tuple through `AssessmentReportProjectionInput` into the immutable report snapshot; remove dependence on assessment signatory fields. | `AssessmentReportProjection`, `IAssessmentReportProjectionSource` |
| `src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs` | change | Compose the projection tuple from the Case sign-off Engineer and PLAT-068 account setting once those dependencies land. | Existing `IGetAssessmentWorkspace` and report projection source |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | change | Render the supplied signature image and name; produce name-only output when qualifications are empty. | Scriban context assembly and `ResourceDataUri` pattern |
| `docs/design/assets/report-renderer/templates/assessment_report.scriban` | change | Remove the unconditional ` — {{ qualifications }}` separator. | Existing signature block |
| `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | change | Remove the obsolete Andy-only embedded-signature registration when the renderer stops resolving a compile-time signature key. | Existing report asset registrations |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` | change | Replace fixed-tuple rejection tests with tuple validity and blank-qualification rendering-contract tests. | Existing snapshot/fake renderer fixture |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` | change | Prove Ed reaches the snapshot and empty qualifications remain valid; remove old assessment-field signatory readiness assertion. | `ReadyInput`, `AssessmentReportProjection.Project` |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | change | Prove the production projection source consumes the dependencies' persisted signatory data when available. | Existing `EfAssessmentReportProjectionSource` harness |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | change | Prove generated PDF text contains Ed's tuple and name-only Neil; retire the Andy-only embedded-resource assertion. | Existing PDF/text renderer coverage |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportDraftWebTests.cs` | change | Update fake `AssessmentReportProjectionInput` construction for the expanded contract. | Existing fake projection source |
| `tests/Pegasus.IntegrationTests/Browser/AssessmentReadinessSummaryBrowserTests.cs` | change | Update its report-projection test fixture (target-typed `new(` at line 104) if the input contract changes. | Existing fake projection source |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | change | Replace D18-era fixed-tuple policy with D31 sign-off projection/rendering behaviour while retaining deterministic/versioned/review-gated rules (signatory section only; a governing doc — shared lock, coordinate with [[DELIV-041]]). | Existing renderer contract section |

No migration belongs to this ticket: the Case sign-off column is [[CASE-040]]
and the staff flag / qualifications / signature image are [[PLAT-068]].

Files DOCS-017 must **not** touch because another EPIC-012 lane owns them:

- CASE-040 Case-field/default/selection/EVA work:
  `src/Pegasus.Core/Lifecycle/CaseLifecycle.cs`,
  `src/Pegasus.Core/Lifecycle/CaseCommandSeams.cs`,
  `src/Pegasus.Infrastructure/Persistence/CaseWorkflowEntities.cs`,
  `src/Pegasus.Infrastructure/Persistence/CaseWorkflowModelConfiguration.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs`,
  `src/Pegasus.Web/Pages/Cases/Workflow.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Workflow.cshtml.cs`,
  `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml`, and
  `src/Pegasus.Web/Pages/Cases/Eva/Send.cshtml.cs`.

- PLAT-068 staff-account setting and signature storage:
  `src/Pegasus.Core/Identity/**`,
  `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfStaffAccountAdministration.cs`,
  `src/Pegasus.Infrastructure/Persistence/EfStaffAccountQueries.cs`,
  `src/Pegasus.Web/Pages/Administration/Accounts/**`, and
  `src/Pegasus.Infrastructure/Persistence/Migrations/**`.

- EPIC-012 shared-lock/UI ownership:
  `src/Pegasus.Web/Presentation/OperatorLabels.cs`,
  `src/Pegasus.Web/Pages/Shared/**`,
  `src/Pegasus.Web/Pages/Cases/Shared/**`,
  `src/Pegasus.Web/Pages/Administration/Shared/**`,
  `src/Pegasus.Web/wwwroot/css/site.css`,
  `src/Pegasus.Web/wwwroot/js/site.js`, and
  `docs/design/test-ui/**`.
