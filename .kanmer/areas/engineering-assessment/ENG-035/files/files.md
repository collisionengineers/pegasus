# Files — ENG-035 (2026-09-02, gpt-5.6-terra xhigh, wrapper-checked)

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | change | Add the closed new vocabulary, zone/type structures, and reportable assessment shapes. | `AssessmentVocabulary`, `AssessmentFieldDefinition` |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | change | Validate structured damage values, retain fail-closed paths, and derive headline impact values. | `ValidateAndNormalize`, `NormalizeFieldValue`, `EstimateTotals` shape |
| `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` | change | Apply Core-produced derived paths against the merged field map before generic persistence. | Existing merge, transaction, `AssessmentFieldWriter` |
| `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | change | Project expanded vehicle, damage, settlement, and fee facts. | `Project`, `BuildVehicle`, `CostsOf` |
| `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | change | Extend the immutable report snapshot and Core-owned derived report values. | `AssessmentReportSnapshot`, `AssessmentReportPresentation` |
| `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | change | Map the expanded snapshot into the existing Scriban context. | `VehicleRows`, `VehicleDataRows`, `Rows` |
| `docs/design/assets/report-renderer/templates/assessment_report.scriban` | change | Render the new projected sections and tables (embedded resource; the template lives here, not under `Infrastructure/Reports`). | Existing embedded template slots |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_ExtendAssessmentVocabulary.cs` | create | Alter `CK_CaseAssessmentFields_FieldPath` for the new closed paths (shared lock, capacity one; serialized). | Existing assessment migration convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_ExtendAssessmentVocabulary.Designer.cs` | create | Generated EF migration metadata. | Existing migration convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | Record the expanded check constraint in the EF snapshot. | EF generated snapshot |
| `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs` | change | Prove new paths round trip, malformed structures fail closed, and multiple zones derive `Multiple`. | Existing vocabulary policy tests |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` | change | Assert projection of every new report field and derived values. | `ReadyInput`, existing projection tests |
| `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` | change | Validate expanded snapshot and derived report presentation rules. | Existing renderer-contract tests |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | change | Prove the new map rows persist through the database constraint and projection source. | Existing EF assessment harness |
| `tests/Pegasus.IntegrationTests/AutomationAssessmentIngressTests.cs` | change | Prove a new vocabulary path round-trips through `pegasus_assessment_update` without changing the tool. | Existing MCP HTTP ingress tests |
| `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | change | Prove rendered PDFs include representative expanded report content. | Existing application-composition renderer test |

- **ASSUMED** — the migration timestamp is assigned only when the migration is
  generated. No new entity, `DbSet`, table, renderer port, report-source
  adapter, or project-file change is needed.

## Files ENG-035 must not touch

- `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` — AUTO-015 owns overwrite,
  clear, and confirmation behaviour; vocabulary admission is already generic.

- `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml`,
  `_CaseValuation.cshtml`, `_CaseEstimate.cshtml`,
  `_CaseSettlement.cshtml`, and `_CaseReport.cshtml` — ENG-034 owns
  extraction; ENG-029 owns Settlement/Report bodies; CASE-029 owns Valuation.

- `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` and
  `Index.cshtml.cs` — ENG-034 owns the route move and Case-page handlers.

- `src/Pegasus.Web/wwwroot/js/damage-diagram.js`, damage SVG/CSS, and browser
  diagram tests — ENG-036 owns the component. ENG-035 supplies only Core zone
  vocabulary.

- `src/Pegasus.Core/Assessment/Valuations.cs` and
  `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs` — ENG-027 owns
  valuation persistence and Engineer's Value writes.

- `src/Pegasus.Core/Assessment/Estimates.cs`,
  `EstimateImport.cs`, and `RepairSpecifications.cs` — existing repair
  duration and estimate ownership remain intact; do not duplicate them.

- Report-image crop, role, and order files — ENG-031 owns curation.

- Case/staff sign-off Engineer files — the D31 lane owns the Case field,
  account flag, qualifications, and signature assets.

- `src/Pegasus.Web/Presentation/OperatorLabels.cs` — shared lock; ENG-029 or
  ENG-034 must add the UI labels when it renders the new fields.

- `docs/frd/frd-06-vehicle-and-engineering-evidence.md`,
  `docs/frd/frd-10-mcp-automation-and-actor-boundary.md`, and
  `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` — the
  EPIC-012 Phase 0 documentation chore owns recording D29-D43.
