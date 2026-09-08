# Files — ENG-031 current v1 visual image preparation

Research: `research/research.md`@`5c388de7a0cd23f7`.
Source: accepted dev `aefe4c32d078ad79c0368666b5666032e6865248`.
This replaces the September 2 future-edit map; its versioned history remains.
Planning only. No path is executable until root approves and ownership clears.

## Expected edits

| Path | Exact responsibility / risk |
| --- | --- |
| src/Pegasus.Core/Cases/CaseWorkspace.cs | Optional existing CaseAssetPreparationEdit list, IsEmpty/normalization and Engineer authority; no new image DTO. |
| src/Pegasus.Core/Documents/CaseAssetPreparation.cs | Remove sole-caller immediate Save/Reset request and store interface after census; retain crop/role/policy/query vocabulary. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkspaceStore.cs | Invoke existing transaction-compatible preparation helper; include image values in request hash and before/after history; one existing commit/version/invalidation. |
| src/Pegasus.Infrastructure/Persistence/EfCaseAssetPreparationStore.cs | Retain queries/mapping/PrepareSaveAsync; remove obsolete immediate transactions/replay/history helpers; require every edited exact current confirmed source. No new schema. |
| src/Pegasus.Infrastructure/DependencyInjection.cs | Remove only obsolete mutation registration; preserve query registration. INTK-064 owns this until handoff. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml.cs | Global Save binds image edits; existing ClaimLease stages first crop without saving; remove immediate image handlers/dependency; preserve exact submitted versions and refusal values. ENG-029 overlap. |
| src/Pegasus.Web/Pages/Cases/Details.cshtml | Render one crop dialog and its pending payload; retain current section/script owners. |
| src/Pegasus.Web/Pages/Cases/Shared/ReportImagePreparationView.cs | Separate ability to open/stage from held-lease editing; exact authorized image URLs/identity and one canonical form value set. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseReportImagePreparation.cshtml | Canonical image controls on eager Report section, associated with case-edit-form; Files view targets the same values; no immediate forms. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseReport.cshtml | Only host the existing shared preparation editor; include unused candidates when editing, retain used report order. ENG-029 overlap. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseFiles.cshtml | Current confirmed case-image identity in gallery; view/crop target only, not duplicate editable values. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseImageCrop.cshtml | New bounded native dialog partial: frame/8 handles/aspect/quarter rotation/reset/preview; one instance. No new page. |
| src/Pegasus.Web/Pages/Shared/_EvidenceViewer.cshtml | Hidden-by-default Crop control targeting current eligible case image; no generic viewer redesign. |
| src/Pegasus.Web/Pages/Shared/_ImageGallery.cshtml | Optional case preparation target attribute; preserve native links for other galleries. |
| src/Pegasus.Web/Presentation/GalleryImage.cs | Optional occurrence target at end of current record; all noncase callers stay unchanged/no crop target. |
| src/Pegasus.Web/Presentation/CaseWorkspaceLabels.cs | Crop/action/state labels in existing ReportImages owner; no explanatory panels. ENG-029 overlap. |
| src/Pegasus.Web/wwwroot/js/case-workspace.js | Single Case-only crop geometry/preview and local preparation staging; Supporting reorder stops submitting independently; root-scoped idempotent mounting. |
| src/Pegasus.Web/wwwroot/js/site.js | Only viewer's current crop target/close-to-editor handoff and existing dialog/dirty mount interoperability if required; no crop business policy. |
| src/Pegasus.Web/wwwroot/css/case-workspace.css | Case-only frame, handles, preview and responsive layout using existing tokens; no site.css redesign. |
| tests/Pegasus.Core.Tests/Cases/CaseWorkspaceTests.cs | Image-only payload, unchanged-null semantics, Engineer/Automation refusal and crop validation. |
| tests/Pegasus.IntegrationTests/CaseAssetPreparationPersistenceTests.cs | Migrate obsolete command tests to existing global store; retain source/hash/order/replay/rollback/geometry/lease assertions. |
| tests/Pegasus.IntegrationTests/CaseWorkspacePersistenceTests.cs | Actual mixed Case+image transaction, once-only history/version/staleness and whole rollback. |
| tests/Pegasus.IntegrationTests/CaseAssetPreparationWebTests.cs | Existing partial CaseDetailsWebTests: staged/global payload, shared values, lazy acquire/refusals, no separate image write handlers. |
| tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs | Query-store constructor caller only if removing unused TimeProvider parameter; no report scenario weakening. |
| tests/Pegasus.IntegrationTests/ImageViewingWebTests.cs | Retained image viewing without lease and no crop targets on unrelated/unavailable documents. |
| tests/Pegasus.IntegrationTests/Browser/ReportImageCropBrowserTests.cs | One new focused class using existing BrowserTestSupport and genuine retained image bytes; actual pointer/keyboard/form/Discard behavior. No new host. |
| tests/Pegasus.IntegrationTests/Reports/CaseReportGenerationPersistenceTests.cs | Existing freeze fixture: global crop save stales current generation, never alters frozen image tuple/artifact. No renderer changes. |
| docs/frd/frd-06-vehicle-and-engineering-evidence.md | Clarify existing curation through global Save, D46 preview and unchanged originals; retain engineering import scope. ENG-029 overlap. |
| docs/frd/frd-12-operator-experience.md | D46 entrypoints/global Save/Discard and script-off numeric fallback; preserve other sections. |
| docs/design/README.md | Only photo editor/Files/Report contract, using existing one-editor requirement. ENG-029/PLAT-050 overlap. |
| docs/design/test-ui/pages/case-details--default.html | Generated only by root scoped actual route capture. |
| docs/design/test-ui/pages/case-details--conflict.html | Generated only by root scoped actual route capture. |
| docs/design/test-ui/pages/case-details--unavailable.html | Generated only by root scoped actual route capture. |
| docs/design/test-ui/index.html | Conditional generated change only; root-serialized ownership. |

## Context only — do not modify

| Path / record | Why read |
| --- | --- |
| docs/operator-notes.md | Protected operator truth; no meaning change. |
| docs/frd/frd-11-reports-correspondence-and-reviewed-proposals.md | Existing generation freeze/readiness/source invalidation authority; no new approval snapshot. |
| src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs | Reuse ClaimLeaseAsync/RestoreLeaseState/authority/failure handling without another lease protocol. Not in edit scope. |
| src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml | ENG-029's one case-edit-form; no second form or rewrite. |
| src/Pegasus.Core/Assessment/AssessmentWorkspace.cs | Existing persisted-state read-only policy; no copied state list. |
| src/Pegasus.Infrastructure/Persistence/EfCaseWorkflowStore.cs | ClaimAsync preserves Case version; correct holder/version/archive/replay authority. |
| src/Pegasus.Infrastructure/Persistence/EfCaseReportGenerationStore.cs | Existing MarkStale and frozen immutable tuple; no new freeze writer. |
| src/Pegasus.Infrastructure/Persistence/EfAssessmentReportProjectionSource.cs | Existing prepared projection/current source owner. |
| src/Pegasus.Infrastructure/Reports/PlaywrightAssessmentReportRenderer.cs | Existing rotation/crop renderer retained unchanged. |
| src/Pegasus.Web/Pages/Cases/Documents/Download.cshtml.cs | Reuse exact authorized occurrence/version inline source; no new image endpoint. |
| pegasus_pack/ui/in-progress-ui-work/Pegasus_UI_v2_src/src/24-cropper.js | Supplied interaction/geometry evidence, not production state/history/policy. |
| pegasus_pack/astra_output/v1_implementation_plans/streams/B-casework.md | B06 global Save/Discard and immutable sources. |
| EPIC-012/context.md D46; EPIC-014/context.md | Binding visual behavior and root-only verification/current remediation constraints. |

No migrations, model snapshot, grants, bootstrap tables, packages, services,
queues, stores, derivative files, external writes or unrelated galleries.
Current tables/permissions already carry these fields and existing global
history. No obsolete schema compatibility machinery.
