# Files — CASE-041 (2026-09-02, gpt-5.6-terra medium, wrapper-checked)

Storage-location persistence is listed below as Codex proposed it (a
`Cases` column). The research's wrapper check records the equally valid
alternative — a `storage_location` name in `CaseDataFields` behind
`CK_CaseDataFields_FieldName`, which then replaces the `PegasusDbContext.cs`
row with `CaseDataEntities.cs` (`CaseDataFieldNames`) and
`CaseDataSnapshotFactory.cs`. The plan decides and records why.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Address/InspectionAddressResolution.cs` | change | Add the narrow address-choice query contract and choice projection used by the Case inspection UI. | Existing inspection-address vocabulary and Image Based Assessment constant. |
| `src/Pegasus.Core/Cases/CaseDataContracts.cs` | change | Add nullable Case storage location to editable/projection inspection data without a new table. | `CaseEditableData`, `CaseInspectionData`. |
| `src/Pegasus.Core/Cases/CaseDataOperations.cs` | change | Normalize and bound storage location with existing Case text rules. | `Text`, `CaseDataPolicy.Normalize`. |
| `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` | change | Add nullable `Cases.StorageLocation` property and EF mapping (column route only). | `CaseEntity` and existing Case property configuration. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` | change | Persist, restore, and project storage location through the normal save transaction. | `ApplyEditableData`, `EditableData`, `Map`. |
| `src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs` | create | Query current claimant/storage values and distinct newest-first confirmed inspection addresses for the same principal. | `EfCaseDataStore` Case/Principal/field-query pattern. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | change | Register the new focused Core query port with its EF adapter. | Existing scoped port registrations. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | change | Load choices for the inspection section; bind and preserve storage location during full saves. | Current section routing and `OnPostSaveAsync`. |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | change | Preserve proposed storage-location input after a refused save. | `RetainableFormFields`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDataHiddenFields.cshtml` | change | Carry storage location through section saves that do not visibly edit it. | Existing full-save hidden-field convention. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml` | change | Render the fast-update select, disabled unavailable choices, conditional address input, and read-mode source. | Existing partial, hidden fields, and one-lease edit form. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change | Add CASE-041 label constants only. Shared-lock coordination required with CASE-038. | Existing `CaseWorkspace` constants and `InspectionMode`. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.cs` | create | Add the storage-location storage (column, or the new field name in `CK_CaseDataFields_FieldName`); retain/reassert Web and Worker Case grants with runtime-role validation. | Latest grant migration pattern; `20260828185508_ProviderDeclaredInstruction` for the constraint pattern. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.Designer.cs` | create | EF-generated companion for the single migration. | EF migration generation convention. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | Record the new Case column or constraint in the EF model snapshot. | EF migration generation convention. |
| `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` | change | Prove storage-location normalization and non-regression of address/mode invariants. | Existing CaseDataPolicy tests. |
| `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs` | create | Prove same-principal history is distinct, excludes blanks/current Case, and is newest first; prove storage persistence. | SQL-server persistence harness pattern. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | change | Prove select ordering, disabled unavailable options, source/read-mode output, and Details query-port composition. | `RecordingCaseDetailsStore` and DI substitution seam. |
| `tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs` | change | Update the focused partial test for the select/conditional input while retaining complete-save posting proof. | `InspectionAddressEditorPostsEveryEditableValueWithTheTypedAddressFirst`. |

## Must not touch

- `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`,
  `src/Pegasus.Web/wwwroot/css/site.css`, and
  `src/Pegasus.Web/wwwroot/js/site.js` — CASE-038 frame lane.
- CASE-038's ownership of `OperatorLabels.cs` is a shared-lock conflict, not
  permission to duplicate labels elsewhere; coordinate the one serialized
  CASE-041 label edit.
- Engineer-notes table, migration, and section files — CASE-039.
- Sign-off Engineer, account-setting, EVA/Send, and report-signature files —
  CASE-040, PLAT-068, and DOCS-017.
- Valuation, DVLA/MOT, and upload-request-dialog files — CASE-029.
- Awaiting-instruction queue files — CASE-042.
- Assessment move, `AssessmentContracts.cs`, report projection, and damage-map
  files — ENG-034, ENG-035, and ENG-036.
- Market-research job files — AUTO-018.
- Operations service-health files — PLAT-069.
- `docs/design/test-ui/**` snapshots and catalogue — UIIMP-014.
- `docs/frd/**`, `docs/prd/**`, `docs/adr/**`, `docs/capabilities.md`, and
  `docs/operator-notes.md` — DELIV-041 / protected governing documentation.
- `corpus/` — local, ignored, immutable.
