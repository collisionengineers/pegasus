# Files — CASE-043

## Pegasus.Core

`src/Pegasus.Core/Cases/CaseDataContracts.cs` — add the ten Case vehicle
fields and append editable values without shifting positional callers.

`src/Pegasus.Core/Cases/CaseDataOperations.cs` — normalize and validate the
new staff-editable field types and bounds.

`src/Pegasus.Core/Intake/IntakeContracts.cs` — extend `InstructionDraft` with
the extracted vehicle values.

`src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs` — apply the decided
completeness policy to the new fields.

`src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs` —
add labelled extraction definitions and draft projection for supported fields.

`src/Pegasus.Core/ProviderApi/ProviderInstructionJson.cs` — extend the single
provider vehicle wire schema and its parser.

`src/Pegasus.Core/ProviderApi/ProviderInstructionPolicy.cs` — normalize,
project, and provenance-label the added provider-declared fields.

`src/Pegasus.Core/Vehicle/LookupContracts.cs` — extend the existing lookup
result only for newly approved DVLA/DVSA response fields.

## Pegasus.Infrastructure

`src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs` — add the ten
constants to the one persisted field-name allow-list.

`src/Pegasus.Infrastructure/Persistence/CaseDataModelConfiguration.cs` —
regenerate the CaseData field-name check constraint from that list.

`src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs` — create
extraction-backed CaseData facts for the new instruction values.

`src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs` — save, rebuild,
and project each new Case vehicle field.

`src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs` — persist and
configure the extended instruction-draft columns.

`src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs` — map the
new instruction-draft values both directions.

`src/Pegasus.Infrastructure/Persistence/VehicleEntities.cs` — retain any
newly supported lookup-observation properties.

`src/Pegasus.Infrastructure/Persistence/VehicleModelConfiguration.cs` —
configure bounds/types for retained lookup-observation properties.

`src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs` — make
automatic lookup gap-fill provenance-backed, non-overwriting, and non-chip
based for CASE-043 fields.

`src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` — parse only
the approved additional DVLA/DVSA response fields into the existing adapter.

`src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseVehicleDataV2.cs` —
new additive migration: instruction-draft columns where required, replacement
CaseData field-name constraint, snapshot/backfill treatment if needed, and
runtime grant evidence.

`src/Pegasus.Infrastructure/Persistence/Migrations/<timestamp>_CaseVehicleDataV2.Designer.cs` —
EF-generated migration metadata.

`src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs` —
EF-generated current model and constraint snapshot.

## Tests

`tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs` — validation and
normalization coverage for new editable values.

`tests/Pegasus.Core.Tests/Intake/InstructionDraftCompletenessTests.cs` —
completeness policy and missing-field labels.

`tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs` —
labelled instruction extraction and provenance fixtures.

`tests/Pegasus.Core.Tests/ProviderApi/ProviderInstructionPolicyTests.cs` —
declared-instruction normalization, review fields, and draft projection.

`tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs` —
full CaseData extraction-to-projection round-trip and check-constraint proof.

`tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs` — lookup
gap-fill, lookup provenance, and non-overwrite behaviour.

`tests/Pegasus.IntegrationTests/ProductionVehicleLookupTests.cs` — additional
approved provider response-field parsing.

`tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs` — migration
constraint/column upgrade coverage.

`tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs` — update the
positional `CaseVehicleData` fixture after its contract expands.

## Governing documentation

`docs/frd/frd-02-intake-and-source-identity.md` — reconcile intake
population/completeness behaviour with D49.

`docs/frd/frd-06-vehicle-and-engineering-evidence.md` — reconcile the stated
automatic lookup behaviour and supported field set with the implemented caller.

## Shared-lock paths

`src/Pegasus.Infrastructure/Persistence/Migrations/**` — shared lock; required
and migrations remain serialized.

`src/Pegasus.Web/Presentation/OperatorLabels.cs` — no change expected.

`src/Pegasus.Web/Pages/Cases/Shared/*` — no change expected.

`src/Pegasus.Web/Pages/Shared/*` — no change expected.

`src/Pegasus.Web/wwwroot/css/site.css` — no change expected.

`src/Pegasus.Web/wwwroot/js/site.js` — no change expected.

`docs/design/test-ui/**` — no change expected.
