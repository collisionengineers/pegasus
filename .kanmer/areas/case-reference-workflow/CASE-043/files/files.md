# Files — CASE-043

Revised 2026-09-03 after the cross-model plan review: the Assessment-vocabulary
retirement (plan step 1b) and the provider-declared instruction path (step 2)
are in scope; `CaseEditableData` and the lookup-port files are not.

## Pegasus.Core

`src/Pegasus.Core/Cases/CaseDataContracts.cs` — add the ten Case vehicle
fields to `CaseVehicleData`. `CaseEditableData` is **not** extended here; see
open question 3.

`src/Pegasus.Core/Cases/CaseDataOperations.cs` — normalize and validate the
new field types and bounds through `CaseDataPolicy`.

`src/Pegasus.Core/Assessment/AssessmentContracts.cs` — remove `vehicle.year`,
`vehicle.vin`, `vehicle.engine_cc` and `vehicle.fuel` from
`AssessmentVocabulary` and carry them on `AssessmentCaseOwnedData`.

`src/Pegasus.Core/Reports/AssessmentReportProjection.cs` — read those four
from `CaseOwned` in `BuildVehicle`.

`src/Pegasus.Core/Intake/IntakeContracts.cs` — extend `InstructionDraft` with
the extracted vehicle values.

`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` —
add labelled extraction definitions with `IsRequired: false` and draft
projection.

`src/Pegasus.Core/ProviderApi/ProviderInstructionJson.cs` — extend the single
provider vehicle wire schema and its parser.

`src/Pegasus.Core/ProviderApi/ProviderInstructionPolicy.cs` — normalize,
project and provenance-label the added provider-declared fields.

`src/Pegasus.Core/Vehicle/VehicleMotExpiryPolicy.cs` (new) — the MOT-expiry
selection rule and its abstention cases.

`src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs` — touched only if
open question 1 makes any new field required.

## Pegasus.Infrastructure

`src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs` — add the ten
constants to the one persisted field-name allow-list.

`src/Pegasus.Infrastructure/Persistence/CaseDataModelConfiguration.cs` —
regenerate the CaseData field-name check constraint from that list.

`src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs` — create
extraction-backed CaseData facts for the new instruction values.

`src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` — project each new
Case vehicle field.

`src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs` —
`MapCaseOwned` carries the four retired assessment fields; the assessment
field-name constraint narrows accordingly.

`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — persist and
configure the extended instruction-draft columns.

`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` — map the
new instruction-draft values both directions.

`src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs` — extend
the existing `AddLookupSuggestionsAsync` calls to fuel, engine capacity,
manufacture year and MOT expiry. Mapping only; no policy.

`src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseVehicleDataV2.cs` —
additive migration: instruction-draft columns, replacement CaseData field-name
constraint, disposal of the four assessment rows before the narrowed assessment
constraint, and runtime grant evidence.

`src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseVehicleDataV2.Designer.cs` —
EF-generated migration metadata.

`src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` —
EF-generated current model and constraint snapshot.

## Pegasus.Web

`src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` — `MapCaseOwned` projects the four
retired fields from `CaseOwned`.

## Tests

`tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` — validation and
normalization coverage for the new values.

`tests/Pegasus.Core.Tests/Vehicle/VehicleMotExpiryPolicyTests.cs` (new) —
ordering, pass-status filter and both abstention cases.

`tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` —
labelled instruction extraction, provenance, and that no new field is required.

`tests/Pegasus.Core.Tests/ProviderApi/ProviderInstructionPolicyTests.cs` —
declared-instruction normalization, review fields, and draft projection.

`tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs` —
extraction-to-projection round-trip, check-constraint proof, and the
save-retention regression.

`tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs` — lookup
suggestion provenance and precedence behaviour.

`tests/Pegasus.IntegrationTests/AutomaticVehicleLookupTests.cs` — the intake
reconciliation caller remains the only automatic route.

`tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs` — migration
constraint/column upgrade coverage, both constraints.

`tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs` —
Worker-role permission proof.

`tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs` — update the
positional `CaseVehicleData` fixture after its contract expands.

## Governing documentation

`docs/frd/frd-06-vehicle-and-engineering-evidence.md` — reconcile the
suggestion-chip wording and the Excluded clause (line 231) with D49 and the
automatic intake lookup.

`docs/frd/frd-02-intake-and-source-identity.md` — no edit unless open question
2 expands the adapter; line 357 already matches D49.

## Shared-lock and neighbour-lane paths

`src/Pegasus.Infrastructure/Persistence/Migrations/**` — capacity one;
migrations stay serialized.

`docs/frd/**` — capacity one; take the FRD-06 slot in turn.

`src/Pegasus.Core/Assessment/AssessmentContracts.cs` — not a listed lock, but
ENG-036 may also need it for the damage vocabulary; coordinate before taking.

`src/Pegasus.Core/Cases/CaseDataOperations.cs` — PLAT-070 removes the staff
review flag it reads at line 85; merge after PLAT-070 or refresh over it.

`src/Pegasus.Core/Vehicle/LookupContracts.cs`,
`src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs`,
`tests/Pegasus.IntegrationTests/ProductionVehicleLookupTests.cs` — no change
expected; the adapter already returns the four supported fields.

`src/Pegasus.Web/Pages/Cases/Shared/*`, `src/Pegasus.Web/Pages/Shared/*`,
`src/Pegasus.Web/Presentation/OperatorLabels.cs`,
`src/Pegasus.Web/wwwroot/css/site.css`, `src/Pegasus.Web/wwwroot/js/site.js`,
`docs/design/test-ui/**` — no change expected.
