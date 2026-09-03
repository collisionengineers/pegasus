# Files — CASE-041 (2026-09-02, gpt-5.6-terra medium; corrected 2026-09-03 after plan review)

The plan chose the `storage_location` name in `CaseDataFields` behind
`CK_CaseDataFields_FieldName` over a `Cases` column, so the `PegasusDbContext.cs`
row is removed. `CaseDataSnapshotFactory.cs` is also removed: it runs only at
case acceptance and has no storage-location source. Rows added by the
2026-09-03 review: `_CaseWorkflow.cshtml`, `Mcp/AssessmentMcpTools.cs`,
`wwwroot/js/site.js`, `AssessmentWorkspaceTestData.cs`, the Browser test, and
the two Test UI snapshot files.

| Path | Action (create/change) | Why | Reuses |
| --- | --- | --- | --- |
| `src/Pegasus.Core/Address/InspectionAddressResolution.cs` | change | Add the narrow address-choice query contract and choice projection used by the Case inspection UI. | Existing inspection-address vocabulary and Image Based Assessment constant. |
| `src/Pegasus.Core/Cases/CaseDataContracts.cs` | change | Add nullable Case storage location to editable/projection inspection data without a new table. | `CaseEditableData`, `CaseInspectionData`. |
| `src/Pegasus.Core/Cases/CaseDataOperations.cs` | change | Normalize and bound storage location with existing Case text rules. | `Text`, `CaseDataPolicy.Normalize`. |
| `src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs` | change | Add `storage_location` to the single `CaseDataFieldNames.All` list, which drives `CK_CaseDataFields_FieldName`. | `CaseDataFieldNames`; `CaseDataModelConfiguration.cs` needs no edit. |
| `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` | change | Persist, restore, and project storage location through the normal save transaction. | `ApplyEditableData`, `EditableData`, `Map`. |
| `src/Pegasus.Infrastructure/Persistence/InspectionAddressChoicesQueries.cs` | create | Query current claimant/storage values and distinct newest-first confirmed inspection addresses for the same principal. | `EfCaseDataStore` Case/Principal/field-query pattern. |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | change (serialized with CASE-039) | Register the new focused Core query port with its EF adapter. | Existing scoped port registrations. |
| `src/Pegasus.Web/Pages/Cases/Details.cshtml.cs` | change (serialized with CASE-039) | Load choices for the `inspection` section; bind and preserve storage location during full saves. | Current section routing and `OnPostSaveAsync`. |
| `src/Pegasus.Web/Pages/Cases/CaseMutationPageModel.cs` | change | Preserve proposed storage-location input after a refused save. | `RetainableFormFields`. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseDataHiddenFields.cshtml` | change | Carry storage location through Inspection-form saves. | Existing full-save hidden-field convention. |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` | change | The Overview/workflow form has its own hidden-field list; without a `storageLocation` input there an Overview save clears the value. | Same hidden-field convention (lines 162-183). |
| `src/Pegasus.Web/Pages/Cases/Shared/_CaseInspectionAddress.cshtml` | change | Render the fast-update select, disabled unavailable choices, conditional address input, and read-mode source. | Existing partial, hidden fields, `case-inspection-address-form`. |
| `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` | change | `pegasus_case_update_details` builds the whole replacement `CaseEditableData`; it must carry the current storage location or an automation update deletes the field. | Its existing `value ?? current...Confirmed?.Value` merge style. |
| `src/Pegasus.Web/wwwroot/js/site.js` | change (serialized after CASE-038) | The production CSP blocks inline script, so the Inspect at selection binder lives here; mounted through CASE-038's root-scoped idempotent `bind(root)`. | INTK-022 `form[data-auto-submit]` select handler; CASE-038 `bind(root)`. |
| `src/Pegasus.Web/Presentation/OperatorLabels.cs` | change (serialized after CASE-038) | Add CASE-041 label constants only. | Existing `CaseWorkspace` constants and `InspectionMode`. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.cs` | create | Add `storage_location` to `CK_CaseDataFields_FieldName` by drop/re-add. No grant SQL: no table is created and no permission delta exists. | `20260828185508_ProviderDeclaredInstruction` constraint pattern. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseInspectionAddressChoices.Designer.cs` | create | EF-generated companion for the single migration. | EF migration generation convention. |
| `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` | change | Record the new constraint value in the EF model snapshot. | EF migration generation convention. |
| `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` | change | Prove storage-location normalization and non-regression of address/mode invariants. | Existing CaseDataPolicy tests. |
| `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs` | change | It constructs `CaseInspectionData` directly and must carry the new member. | Existing workspace test data. |
| `tests/Pegasus.IntegrationTests/InspectionAddressChoicesPersistenceTests.cs` | create | Prove same-principal history is distinct, excludes blanks/current Case/sentinel, and is newest first; prove storage persistence. | `LocalDbTemplateDatabase`. |
| `tests/Pegasus.IntegrationTests/CaseDetailsWebTests.cs` | change (serialized with CASE-039/040/029) | Prove select ordering, disabled unavailable options, source/read-mode output, and Details query-port composition. | `RecordingCaseDetailsStore` and DI substitution seam. |
| `tests/Pegasus.IntegrationTests/CaseTasksWebTests.cs` | change | Update the focused partial test for the select/conditional input and prove storage location posts from all full-save callers. | `InspectionAddressEditorPostsEveryEditableValueWithTheTypedAddressFirst`. |
| `tests/Pegasus.IntegrationTests/Browser/InspectionAddressChoiceBrowserTests.cs` | create | Web tests read HTML only and cannot prove the selection interaction. | Existing `[Trait("Category", "Browser")]` Playwright pattern. |
| `docs/design/test-ui/pages/case-details--default.html` | change (regenerate) | AGENTS.md requires the snapshot to be committed with the routed Razor change; CI verifies every change set. Capacity-one lease. | Snapshot tooling (as CASE-038 does). |
| `docs/design/test-ui/pages/case-details--conflict.html` | change (regenerate) | Same reason. | Snapshot tooling. |

## Must not touch

- `src/Pegasus.Web/Pages/Cases/Details.cshtml`,
  `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkspaceNav.cshtml`, and
  `src/Pegasus.Web/wwwroot/css/site.css` — CASE-038 frame lane.
- `docs/design/test-ui/**` beyond the two regenerated Case-details snapshots
  above (new per-section catalogue states are UIIMP-014's).
- Engineer-notes table, migration, and section files — CASE-039.
- Sign-off Engineer, account-setting, EVA/Send, and report-signature files —
  CASE-040, PLAT-068, and DOCS-017.
- Valuation, DVLA/MOT, and upload-request-dialog files — CASE-029.
- Awaiting-instruction queue files — CASE-042.
- Assessment move, `AssessmentContracts.cs`, report projection, and damage-map
  files — ENG-034, ENG-035, and ENG-036.
- Market-research job files — AUTO-018.
- Operations service-health files — PLAT-069.
- Staff-review removal (`RequireStaffImageReviewBeforeEngineerAssignment`,
  `ImagesReviewedByStaff`, the Workflow configuration review panel) — PLAT-070,
  which merges before this ticket.
- `docs/frd/**`, `docs/prd/**`, `docs/adr/**`, `docs/capabilities.md`, and
  `docs/operator-notes.md` — DELIV-041 / protected governing documentation.
- `corpus/` — local, ignored, immutable.
