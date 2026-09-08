# Files — ENG-036 current damage diagram delivery

Prepared 2026-09-08 by intake_audit; preparation only. This replaces obsolete
execution mapping `4eba4179ea4c56d7`, retained in ticket history. Exact
source/corpus evidence is in research; no listed file is claimed or edited.

## Where the change lands

| Action | Repo-root-relative path | Responsibility |
| --- | --- | --- |
| Add | `docs/design/assets/report-renderer/templates/damage-diagram.svg` | Single supplied 23-region geometry; current Core data-zone keys. |
| Modify | `src/Pegasus.Infrastructure/Pegasus.Infrastructure.csproj` | Embed that one asset with existing report resources. |
| Add | `src/Pegasus.Infrastructure/Reports/DamageDiagramMarkup.cs` | Narrow encoded composition reused by the actual Case and PDF callers; reuse existing resource loader. |
| Modify | `src/Pegasus.Core/Reports/AssessmentReportRendering.cs` | Keep ReportImpact's three members, retain canonical codes; advance existing TemplateVersion. |
| Modify | `src/Pegasus.Core/Reports/AssessmentReportProjection.cs` | BuildDamage retains accepted canonical identities; existing presentation owner supplies labels later. |
| Modify | `src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs` | Call shared diagram composer and convert impact labels through existing Presentation methods. |
| Modify | `docs/design/assets/report-renderer/templates/assessment_report.scriban` | One marked-diagram slot in the existing Damage block; retain table and other report content. |
| Modify | `docs/design/assets/report-renderer/templates/report.css` | Bounded print sizing and visible selected markers. |
| Modify | `src/Pegasus.Web/Pages/Cases/Shared/_CaseDamage.cshtml` | Full recorded Damage view and native form-associated engineering editor. |
| Modify | `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | Extend accepted ENG-029 OnPostSaveAsync with typed Damage, posted-presence and existing authority checks. |
| Modify | `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | Retain bounded proposed damage row/scalar values in existing refusal UI; never authority tokens. |
| Modify | `src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs` | Extend existing editor-path and retained-label ownership; reuse Core zone/severity/code vocabularies. |
| Modify | `src/Pegasus.Web/wwwroot/js/case-workspace.js` | Idempotent diagram editing enhancement; native controls and existing form dirty events. |
| Modify | `src/Pegasus.Web/wwwroot/css/case-workspace.css` | Case-only responsive diagram, impact rows, tyre cards and focus states. |
| Modify | `tests/Pegasus.Core.Tests/Reports/AssessmentReportProjectionTests.cs` | Canonical-code projection and saved-impact semantics. |
| Modify | `tests/Pegasus.Core.Tests/Reports/AssessmentReportRenderingTests.cs` | Existing contract/version and all fixture consumers updated without extra fields. |
| Modify | `tests/Pegasus.IntegrationTests/Reports/AssessmentReportRendererTests.cs` | Existing harness: exact SVG marker membership/encoding and actual rendered PDF. |
| Modify | `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | Existing actual Save route: typed fields, explicit clear versus omission, refusal/authority retention. |
| Modify | `tests/Pegasus.IntegrationTests/CaseEngineerSectionsWebTests.cs` | Existing recorded-value fixture: full read-only Damage and lawful edit-state controls. |
| Modify | `tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs` | Existing SQL harness: saved canonical Damage reaches frozen report; edits stale current output without rewriting prior snapshot. |
| Add | `tests/Pegasus.IntegrationTests/Browser/CaseDamageDiagramBrowserTests.cs` | Existing BrowserTestSupport/estate: click/Enter/Space, Save/Discard and 3-width editable/read-only evidence; no new host. |
| Modify | `docs/design/README.md` | Only Damage bullet: current 23 detailed, 8 broad, 3 auxiliary scope and severity/note; retain current global Save layout. |
| Generated | `docs/design/test-ui/pages/case-details--default.html` | Regenerate only canonical default Case capture. |
| Generated | `docs/design/test-ui/pages/case-details--conflict.html` | Regenerate only canonical conflict capture. |
| Generated | `docs/design/test-ui/pages/case-details--unavailable.html` | Regenerate canonical unavailable capture if bytes change. |
| Generated if changed | `docs/design/test-ui/index.html` | Scoped catalogue generation only, after explicit index ownership handoff. |

## Context files

| Path | What the implementer must preserve |
| --- | --- |
| `src/Pegasus.Core/Assessment/AssessmentContracts.cs` | Sole 34-zone, severity, tyre/restraint, path and limit vocabulary. No edits needed. |
| `src/Pegasus.Core/Assessment/AssessmentPolicy.cs` | Canonical validation and derived headlines; no browser or Infrastructure copy. |
| `src/Pegasus.Core/Cases/CaseWorkspace.cs` | Existing typed Damage and null-versus-explicit-empty patch contract. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs` | Single save transaction, current version/lease and report staleness. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | Existing eager Damage host, case-edit-form and Case-only asset loading; no markup edit planned. |
| `src/Pegasus.Web/wwwroot/js/site.js` | Native form-associated input dirty tracking and existing mount binder; no edit needed. |
| `tests/Pegasus.Core.Tests/Assessment/AssessmentPolicyTests.cs` | Existing 23/8 census and canonical invalid-data cases; do not duplicate in a new test class. |
| `tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs` | Existing one-save/version/history, replay and partial-write refusal evidence. |
| `tests/Pegasus.IntegrationTests/Browser/BrowserTestSupport.cs` | Reuse current actual browser host/estate and artifact conventions. |
| `docs/frd/frd-06-vehicle-and-engineering-evidence.md` | Current Damage contract; no new taxonomy or FRD change. |
| `docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md` | Printed diagram and retained generation requirements. |
| `docs/frd/frd-12-operator-experience.md` | One workspace Save/Discard, lifecycle/role rules and viewable sections. |
| `pegasus_pack/more_docs/Pegasus_UI_v3.html` | Hash-bound supplied geometry only; immutable, not copied wholesale. |
| `pegasus_pack/astra_output/v1_implementation_plans/streams/B-casework.md` | B02/B05 damage and report scope; B06 belongs to image preparation. |

## Ownership and ripple effects

Execution is stopped until root records release or narrow handoff of
ENG-029's DetailsModel, retention, labels, projection, Case tests, README and
captures. ENG-031 is still Preparing but overlaps Case JS/CSS, writer,
labels, report-generation fixture and generated artifacts: serialize these
edits, do not silently take its future scope. Historical ENG-034/CASE-038
Verifying claims are preserved; their accepted source is not a release.
The root must resolve exact applicable paths before take.

All ReportImpact callers are the production projection/renderer and the
three explicitly mapped fixture classes. Labels move to the existing
Presentation conversion, not a reverse lookup. The embedded asset must be
loadable from the built Infrastructure artifact by both callers; no new
static route, linked Web content item or copied SVG is required.

## Out of scope

No Core assessment vocabulary/policy changes, DB/schema/migration/grants,
store/DI/worker, extra Save route, site.js/site.css, Case shell rewrite,
estimate import or repair duration, report/settlement editor duplication,
image crop/rotation/order (ENG-031), vehicle history, new package, artificial
domain instructions or edits to supplied originals. No unrelated generated
page, prototype, canonical docs, board claim, stage or dependency cleanup.
