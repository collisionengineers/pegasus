# Files — CASE-029 (2026-09-02, gpt-5.6-terra high, wrapper-checked; corrected 2026-09-03 after plan review)

All "change" rows are implementation expectations, not completed work. Every
existing path below was confirmed present on `origin/dev` `897db953` with
`git cat-file -e`; the "create" rows are the only paths that do not exist yet.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` | change | Accept one looked-up field and clear only its suggestion. | `VehicleSuggestionAcceptancePolicy` |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | change | Persist per-field acceptance and clear only that suggestion row. | `CaseDataCodes.Suggestion` |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml` | change | One lookup action, per-field chips, no checks panel/table. | `.gated`, existing lease fields |
| `src/Pegasus.Web/Pages/Cases/Vehicle.cshtml.cs` | change | Bind the single lookup and field-level chip action. | `CaseMutationPageModel` |
| `src/Pegasus.Core/Assessment/Valuations.cs` | change | Add guide month; add the Core rule for manually recordable sources (Cazana refused). | `ValuationPolicy` |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` | change | Persist guide month. | `CaseValuationEntity` |
| `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` | change | Configure guide-month storage and constraints. | existing valuation mapping |
| `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs` | change | Save, edit, list, and order guide month. | lease/history guards |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseValuationGuideMonthAndRequestUploadMetadata.cs` | create | Add valuation guide month and request metadata columns (columns only, no new table). | EF migration convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseValuationGuideMonthAndRequestUploadMetadata.Designer.cs` | create | EF migration designer. | EF convention |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | Reflect the migration model. | EF convention |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml` | create | Render one card per persisted valuation row and the Add valuation dialog. | panel/dialog primitives |
| `src/Pegasus.Web/Pages/Cases/Valuation.cshtml` | create | Route valuation mutations. | `Vehicle.cshtml` route pattern |
| `src/Pegasus.Web/Pages/Cases/Valuation.cshtml.cs` | create | Save handler only; reading stays with CASE-038's `DetailsModel`. | `ISaveValuation` |
| `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` | change | Carry, normalise and validate Recipient and Reason as request metadata. | request-link policy |
| `src/Pegasus.Infrastructure/Persistence/CustodyEntities.cs` | change | Add persisted request metadata. | `RequestUploadLinkEntity` |
| `src/Pegasus.Infrastructure/Persistence/CustodyModelConfiguration.cs` | change | Configure metadata columns and lengths. | custody mapping |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | change | Persist metadata and include it in `RequestUploadHistoryValue` so `RequireExactReplay` refuses conflicting replay. | request transaction/history |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | change | Project Recipient/Reason to the Case page. | `CaseDetails` projection |
| `src/Pegasus.Web/Pages/Cases/Custody.cshtml.cs` | change | Bind Recipient/Reason into upload-link creation. | `ICreateRequestUploadLink` |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml` | change | Replace direct creation with the upload-request dialog and show metadata. | existing request list |
| `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs` | change | Map mockup Recipient/Content names to `ManualChaseRecord`. | existing chase handler |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` | change | Render the Record-chase dialog fields. | `ManualChaseRecord` |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change | Lookup, chip, valuation source and dialog labels (no AI market research label). | shared lock |
| `docs/design/test-ui/**` | change | Regenerated snapshots and catalogue output committed with the page change (AGENTS.md). | shared lock |
| `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` | change | Prove lease/version rejection and field-key validation. | existing workflow fakes |
| `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` | change | Prove guide month, refused Cazana, Engineer's Value authority. | existing valuation tests |
| `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs` | change | Prove persisted per-field acceptance, retained siblings, atomic mileage+unit, provenance, repeat-lookup rule. | existing EF lookup seam |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | change | Prove guide-month save/edit/list/order against `EfValuationStore`. | existing `EfValuationStore` seam |
| `tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs` | change | Prove one lookup action and chip form behaviour. | existing Case Web factory |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | change | Prove dialogs, field mapping, and PRG/lease behaviour. | existing recording store |
| `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | change | Prove persisted upload Recipient/Reason, identical replay, refused conflicting replay. | existing request durability tests |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | change | Pin the new migration in the migration list (line 116 holds that list, not `TypedCaseDataMigrationTests.cs`). | migration-list convention |

`src/Pegasus.Web/Pages/Cases/Custody.cshtml` is not in the table on purpose:
it holds only the `@page` and `@model` directives, so the ticket's "Custody.*
(dialog fields)" scope lands in `_CaseDocuments.cshtml` + `Custody.cshtml.cs`
(upload request) and `_CaseHistory.cshtml` + `Tasks.cshtml.cs` (record chase).

`src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs` is no
longer a change row: its private `AddLookupSuggestionsAsync` already writes the
suggestion rows this ticket consumes, and staying out of that file is what
keeps the suggestion writer single.

## Shared-lock or neighbour-lane edits this ticket needs

| Path | Owning ticket | Required hand-off |
| --- | --- | --- |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | [[CASE-038]] | Include Valuation in the single-scroll frame. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | [[CASE-038]] | Load valuation projection for `_CaseValuation`. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | shared lock | Add the one operator label set for lookup, valuation, chips, and dialogs. |
| `src/Pegasus.Web/wwwroot/css/site.css` | [[CASE-038]] | Add valuation-card/chip presentation if frame CSS does not supply it. |
| `docs/design/test-ui/**` | shared lock | This ticket regenerates and commits its own snapshots; [[UIIMP-014]] reconciles the catalogue across lanes. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/**` | shared lock | Serialize the required migration with other migration writers. |
| `src/Pegasus.Web/Pages/Cases/Shared/*` | shared lock | Serialize `_CaseVehicle`, `_CaseValuation`, `_CaseDocuments`, and `_CaseHistory`. |

## Files this ticket must not touch

- [[CASE-038]]: `Pages/Cases/Details.*`, frame `OperatorLabels` entries,
  `wwwroot/css/site.css`, `wwwroot/js/site.js`, sticky ribbon, jump-nav.
- [[ENG-034]]: `Pages/Cases/Assessment/Index.cshtml` and Damage, Estimate,
  Settlement, and Report partials.
- [[ENG-035]]: `Core/Assessment/AssessmentContracts.cs` and
  `Core/Reports/AssessmentReportProjection.cs`.
- [[AUTO-018]]: `MarketResearch` job-kind, the `MarketResearch` valuation
  source and its label, Automation Actor completion, the AI-created valuation
  row, the D35 request action, and the findings document.
- [[CASE-041]], [[CASE-039]], [[CASE-040]], [[ENG-036]], [[ENG-029]], and
  [[ENG-031]]: their named inspection, notes, sign-off, damage, settlement,
  report, and image-preparation files.
- [[CASE-042]], [[PLAT-068]], and [[DOCS-018]]: Cases index, Administration,
  and fee-note-preview files.
- [[TICK-083]] / EXT-10: valuation adjustments, rationale, and revaluation
  history.
