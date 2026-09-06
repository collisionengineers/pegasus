# B01 — PR 670 preservation table

Source PR: https://github.com/collisionengineers/pegasus/pull/670 (`task/case-029-valuation-lookup-chips`, CASE-029). Pinned tip `f22751cad3d5a713f39503ef48ff30422d67c97f`, refreshed from GitHub 2026-09-06 (OPEN, base dev, unchanged). Base D `3284f93fc3ea9fd3bbbea9405ec92dc7818378f2` is the merge base. Diff: 42 files, 8571 insertions, 582 deletions. Read-only inventory; no product file was changed. Porting commits land on `task/pegasus-v1-casework` only after Foundation F is fast-forwarded.

Dispositions: `ported` (B re-authors the hunk), `Foundation handoff` (A/F owns), `C handoff`, `rejected with UI-v3 reason`, `partial`.

## 1. Per-file table

| File | Owner | Disposition | Hunks | Dependencies |
| --- | --- | --- | --- | --- |
| `docs/design/test-ui/pages/case-details--conflict.html` | B | ported (regenerated) | Valuation panel becomes lazy placeholder `section-placeholder … data-lazy="valuation"` | regenerate after Details/_CaseValuation land |
| `docs/design/test-ui/pages/case-details--default.html` | B | ported (regenerated) | Single Vehicle panel with `Look up DVLA & MOT`, Experian gate; Valuation panel body | regenerate after B03 |
| `src/Pegasus.Core/Assessment/Valuations.cs` | B | ported | `ValuationDetails` gains `DateOnly? GuideMonth = null`; `ValidateDetails` rejects `Day != 1`; `RequireManuallyRecordableSource` limits `ValidateSave`/`ValidateEdit` to `Glasses`/`EngineersValue` | F GuideMonth column; B03 extends further (Brego/Super CAP manual sources) |
| `src/Pegasus.Core/Cases/CaseQueries.cs` | B | ported | `CaseRequestUploadSummary` gains `Recipient`, `Reason` | F Recipient/Reason columns |
| `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` | B | ported | Removes `VehicleSuggestionDecision.Correct` + `ConfirmedVehicleFieldConflictException`; adds `VehicleSuggestionField { Make, Model, Mileage }`; `AcceptVehicleSuggestionCommand.Field`; per-field accept | none (independent) |
| `src/Pegasus.Infrastructure/Persistence/EfCaseQueryStore.cs` | B | ported | projects `Recipient`/`Reason` | F columns |
| `src/Pegasus.Infrastructure/Persistence/EfValuationStore.cs` | B | ported | maps `GuideMonth` on save/edit/read | F column |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | B | ported | `AcceptAsync` resolves only the requested field's pending suggestion rows, writes that field, removes consumed rows; event kind `vehicle_suggestion_accepted`; drops `corrected` | VehicleWorkflow.cs |
| `src/Pegasus.Web/Pages/Cases/Custody.cshtml.cs` | B | ported | `OnPostCreateRequestUploadLinkAsync(… recipient, reason)` | C `RequestUploadPolicy` params |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml` | B | ported | renders `_CaseValuation` for `valuation` section | `_CaseValuation.cshtml` |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | B | ported | injects `IListCaseValuations`; `Valuations` property; `valuation` deferred section in full + fragment load | existing `IListCaseValuations` |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDocuments.cshtml` | B | ported | "Create upload request" gated on `mayEdit && uploadLimits is not null`; dialog with Recipient (required)/Reason + `dl.definition` policy values; Recipient/Reason columns | C policy/labels; F columns |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseHistory.cshtml` | B | ported | manual chase: required `recipient`, `note`→`content`, server time, `RecordChase` label | Tasks.cshtml.cs; C labels |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseValuation.cshtml` (new) | B | partial | read-only Valuation panel + `valuation-card` loop ports; the Add-valuation `<dialog>` form targets the rejected `/Cases/{id}/Valuation` route and must be re-homed on `DetailsModel` (B03) | C css/labels; B03 handler |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseVehicle.cshtml` | B | ported | single Vehicle panel; per-field `Use <value>` chips posting `field=Make|Model|Mileage`; one `Look up DVLA & MOT` button; drops checks panel/history table/Accept-Correct forms | VehicleWorkflow.cs; C `suggestion-chip`, labels |
| `src/Pegasus.Web/Pages/Cases/Tasks.cshtml.cs` | B | ported | `OnPostRecordManualChaseAsync` takes `TimeProvider`; `recipient`/`content`; server `attemptedAtUtc` | `_CaseHistory.cshtml` |
| `src/Pegasus.Web/Pages/Cases/Vehicle.cshtml.cs` | B | ported | `OnPostAcceptVehicleSuggestionAsync(VehicleSuggestionField field)`; fixed reason "Accepted vehicle lookup suggestion." | VehicleWorkflow.cs |
| `tests/Pegasus.Core.Tests/Assessment/ValuationTests.cs` | B | ported | GuideMonth day≠1 rejection/day-1 acceptance; `Cazana` save throws | Valuations.cs |
| `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` | B | ported | per-field tests; `Correction` → `ArgumentException`; undefined `Field` → `ArgumentOutOfRangeException` | VehicleWorkflow.cs |
| `tests/Pegasus.IntegrationTests/AssessmentPersistenceIntegrationTests.cs` | B | ported | `SaveAsync(guideMonth)`; Glasses with GuideMonth; edit carries GuideMonth | EfValuationStore; F column |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | B | ported | deferred list gains `valuation`; refused-key case dropped; chase form field renames; `RecordingCaseDetailsStore.Suggested<T>` + `IncludeVehicleSuggestions` | Details wiring |
| `tests/Pegasus.IntegrationTests/CaseVehicleWebTests.cs` | B | ported | one per-field accept test; single `LookupDvlaMot` button; 3 accept forms, no `Correct` | vehicle files |
| `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs` | B | ported | `AcceptingOneSuggestionClearsOnlyThatFieldAndMileageIsAtomic` | EfVehicleWorkflowStore |
| `docs/design/test-ui/catalogue.json` | A | Foundation handoff (caution) | adds `/Cases/{id:guid}/Valuation` protocol entry — must NOT be carried; route is rejected | — |
| `docs/design/test-ui/index.html` | A | Foundation handoff (caution) | same non-visual route row — drop | — |
| `src/Pegasus.Infrastructure/Persistence/AssessmentEntities.cs` | A | Foundation handoff | `CaseValuationEntity.GuideMonth : DateOnly?` | — |
| `src/Pegasus.Infrastructure/Persistence/AssessmentModelConfiguration.cs` | A | Foundation handoff | `GuideMonth` `HasColumnType("date")`, nullable, no index | — |
| `src/Pegasus.Infrastructure/Persistence/CustodyEntities.cs` | A | Foundation handoff | `RequestUploadLinkEntity.Recipient`, `.Reason` (string?) | — |
| `src/Pegasus.Infrastructure/Persistence/CustodyModelConfiguration.cs` | A | Foundation handoff | `Recipient` max 500; `Reason` max 1000; nullable | — |
| `…/Migrations/20260905173354_CaseValuationGuideMonthAndRequestUploadMetadata.cs` | A | Foundation handoff | Up: `RequestUploadLinks.Reason nvarchar(1000) null`, `RequestUploadLinks.Recipient nvarchar(500) null`, `CaseValuations.GuideMonth date null`; Down drops | F may supersede inside its single v1 migration |
| `…Designer.cs` | A | Foundation handoff (generated) | designer snapshot | regenerate |
| `…/Migrations/PegasusDbContextModelSnapshot.cs` | A | Foundation handoff (generated) | same three properties | regenerate |
| `tests/Pegasus.IntegrationTests/CaseCapabilityPagesTestSupport.cs` | A | Foundation handoff | `EnterEditModeAsync(configureWebHost)` optional | — |
| `tests/Pegasus.IntegrationTests/CaseCustodyWebTests.cs` | A | Foundation handoff | `CaseFilesSectionRendersWithUploadRequestCreationAbsentWhenLimitsAreNotAccepted` | B `_CaseDocuments` gate |
| `tests/Pegasus.IntegrationTests/DocumentCustodyDurabilityTests.cs` | A | Foundation handoff | `RequestUploadMetadataPersistsProjectsAndParticipatesInReplay` (create-replay compares creation-time snapshot) | C store; `LocalDbTestDatabase.CreateAsync(requestUploadLimitsFactory:)` |
| `src/Pegasus.Core/Documents/RequestUploadPolicy.cs` | C | C handoff | `RequestUploadLink`/`CreateRequestUploadLinkCommand` gain `Recipient`/`Reason`; `NormalizeCreate` trims, blank → `ArgumentException`, >500/>1000 → `ArgumentOutOfRangeException` | F columns |
| `src/Pegasus.Infrastructure/Persistence/EfDocumentRequestStore.cs` | C | C handoff | `NormalizeCreate` first; replay compares deserialized `RequestUploadHistoryValue` snapshot, persists Recipient/Reason into snapshot; mappings thread both | policy; F columns |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | C | C handoff | remove `VehicleChecksPanel`, `RefreshDvla`, `RefreshDvsaMot`, `VehicleChecksHistory`, `AcceptSuggestion`, `CorrectSuggestion`; add `LookupDvlaMot`, `AddValuation`, `CazanaCondition`, `Recipient`, `Reason`, `Content`, `RecordChase`, `UseSuggestion(value)`, `ValuationSourceLabel(ValuationSource)` | B partials reference these |
| `src/Pegasus.Web/wwwroot/css/site.css` | C | C handoff | `.suggestion-chip`, `.valuation-sources`, `.valuation-card`, `.valuation-card h3`, `.valuation-card .figures` | B partials use the classes (B may instead place Case-only rules in `case-workspace.css`, B08) |
| `tests/Pegasus.IntegrationTests/IntakePersistenceIntegrationTests.cs` | C | C handoff | migration name in applied list; `LocalDbTestDatabase` `requestUploadLimitsFactory` | F migration name |
| `src/Pegasus.Web/Pages/Cases/Valuation.cshtml` | — | rejected with UI-v3 reason | new standalone `@page "/Cases/{id:guid}/Valuation"`; v3 has one Case workspace with section handlers on `DetailsModel` | — |
| `src/Pegasus.Web/Pages/Cases/Valuation.cshtml.cs` | — | rejected with UI-v3 reason | `OnPostAddAsync` + `ParseGuideMonth("yyyy-MM")` → `ISaveValuation`; behaviour re-homed on `DetailsModel` in B03 | — |

## 2. Foundation handoff summary (B-F-07)

Migration `20260905173354_CaseValuationGuideMonthAndRequestUploadMetadata`: `CaseValuations.GuideMonth date NULL`; `RequestUploadLinks.Recipient nvarchar(500) NULL`; `RequestUploadLinks.Reason nvarchar(1000) NULL`; matching entity properties, configuration and snapshot. Test support: `LocalDbTestDatabase` optional `requestUploadLimitsFactory`; `EnterEditModeAsync(configureWebHost)`. Do not carry the `/Cases/{id:guid}/Valuation` catalogue/index entries.

## 3. C handoff summary

`RequestUploadPolicy` Recipient/Reason + `NormalizeCreate`; `EfDocumentRequestStore` replay-snapshot fix + fields; `OperatorLabels` removals/additions listed above; `site.css` chip/valuation-card rules (or B carries Case-only rules in `case-workspace.css`); `IntakePersistenceIntegrationTests` migration list + factory parameter.

## 4. Compile coupling and sequencing

- `EfValuationStore.cs` and `AssessmentPersistenceIntegrationTests.cs` need F's `GuideMonth` column.
- `EfCaseQueryStore.cs`, `_CaseDocuments.cshtml` need F's `Recipient`/`Reason` columns and C's policy/store/labels.
- `Custody.cshtml.cs` needs C's `CreateRequestUploadLinkCommand` parameters.
- `_CaseHistory.cshtml`/`Tasks.cshtml.cs` need C's `Recipient`/`Content`/`RecordChase` labels.
- `_CaseVehicle.cshtml`/`_CaseValuation.cshtml` need C's labels; CSS classes can be Case-only in `case-workspace.css`.
- Independent of F and C: `VehicleWorkflow.cs`, `EfVehicleWorkflowStore.cs`, `Vehicle.cshtml.cs`, `VehicleWorkflowTests.cs`, `CaseVehicleWebTests.cs` (vehicle part), `VehicleLookupGapFillTests.cs`, `Details.cshtml(.cs)` valuation section wiring.

Order: F first (schema), then check C's published labels/policy on `task/pegasus-v1-intake`; if C's hunks are absent when B ports, B keeps the label references out of its partials by using existing labels only where a C label is missing and records the gap here.

## 5. PR commits (evidence)

```
f22751cad Fix review-round blockers: create-replay snapshot compare and absent upload limits (CASE-029)
77f97c40a Regenerate Case Details Test UI snapshots after dev merge (CASE-029)
5c6f8334e Merge remote-tracking branch 'origin/dev' into task/case-029-valuation-lookup-chips
ffa1effed Regenerate Case Details Test UI snapshots and catalogue entry (CASE-029)
938018b57 Bind vehicle/valuation/custody/chase routes and wire the frame (CASE-029, step 4)
bfb2a72cd Render Case Vehicle/Valuation/Documents/History sections (CASE-029, step 3)
efbe55015 Add valuation guide month and request-upload Recipient/Reason (CASE-029, step 2)
0c82ec791 Consume per-field vehicle suggestion acceptance (CASE-029, step 1)
```

Closing PR 670 as superseded is a coordination/closeout action after B09 proves every ported hunk survives on the final B head; B does not close it.
