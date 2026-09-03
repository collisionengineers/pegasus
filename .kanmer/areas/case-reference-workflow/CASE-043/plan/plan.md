# Plan — CASE-043 (2026-09-03, gpt-5.6-terra xhigh)

Planning baseline: clean checkout at `07ac7f1be9fc9fc04814fd5347ae5da30aff62da`. The existing
automatic lookup is already invoked from intake reconciliation; the migration-grant
check currently passes (87 migrations). Live Kanmer refresh was unavailable (HTTP
429), so this plan relies on the supplied ticket packet for board state.

Working assumptions:

- All ten new fields are ordinary optional Case fields; they do not change
  instruction/case completeness unless the operator answers otherwise.
- Automatic enrichment uses only current `IVehicleLookupAdapter` output:
  fuel, engine capacity, manufacture year, and MOT expiry. Colour,
  transmission, body, first registration, tax expiry, and VIN remain
  extraction/manual-entry fields.
- Existing make/model/mileage suggestions remain CASE-029 behaviour. CASE-043
  creates no chips for its ten new fields.

Named dependencies, not CASE-043 implementation steps:

- `src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs`: only needed if the
  operator changes the optional-completeness assumption.
- `src/Pegasus.Core/ProviderApi/ProviderInstructionJson.cs` and
  `src/Pegasus.Core/ProviderApi/ProviderInstructionPolicy.cs`: needed only if
  declared-provider instruction intake must carry these fields.
- `docs/frd/frd-02-intake-and-source-identity.md` and
  `docs/frd/frd-06-vehicle-and-engineering-evidence.md`: reconcile their
  older suggestion/activation wording with settled D49 before delivery.

1. Extend the Core-owned vehicle record and its persisted projection.

   Reuse `CaseVehicleData`, `CaseEditableData`, `CaseDataPolicy.Normalize`,
   `CaseDataFieldNames`, `CaseDataModelConfiguration`, and
   `EfCaseDataStore`'s confirmed-value/projection helpers. Append, never
   insert, optional editable parameters to preserve positional callers.

   Files:

   - `src/Pegasus.Core/Cases/CaseDataContracts.cs`
   - `src/Pegasus.Core/Cases/CaseDataOperations.cs`
   - `src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs`
   - `src/Pegasus.Infrastructure/Persistence/CaseDataModelConfiguration.cs`
   - `src/Pegasus.Infrastructure/Persistence/EfCaseDataStore.cs`
   - `tests/Pegasus.Core.Tests/Cases/CaseDataOperationsTests.cs`
   - `tests/Pegasus.IntegrationTests/AssessmentWorkspaceTestData.cs`

   Add colour, fuel, engine capacity, transmission, body, manufacture year,
   first registration, tax expiry, MOT expiry, and VIN as typed
   `CaseVehicleData` fields. Use text, integer, and `DateOnly` types already
   supported by CaseData; normalize text with existing helpers, reject
   non-positive numeric values, validate dates with the existing date helper,
   and do not invent a VIN format. Add the ten names only to
   `CaseDataFieldNames.All`; the existing check-constraint generator remains
   the sole field-name list.

   Done when staff save, editable reconstruction, and Case projection
   round-trip every new field without shifting existing callers.

2. Carry instruction-extracted values through the shared draft into CaseData.

   Reuse `InstructionDraft`, QDOS `FieldDefinition`,
   `CaseDataSnapshotFactory.AddInstructionSuggestions`/
   `AddExtractedValue`, `InstructionDraftEntity`, and
   `EfIntakeReceiptStore`'s bidirectional mapping.

   Files:

   - `src/Pegasus.Core/Intake/IntakeContracts.cs`
   - `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
   - `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
   - `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs`
   - `src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs`
   - `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs`
   - `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs`

   Append nullable draft/entity properties, configure their existing SQL types
   and bounds, add labelled QDOS extraction definitions, and map present values
   as `Fact` fields through `AddExtractedValue`. Preserve extraction provenance
   and leave absent values absent. Do not alter completeness under the stated
   assumption.

   Done when an instruction containing all ten values produces corresponding
   CaseData facts without a lookup.

3. Fill only supported missing fields through the existing automatic lookup
   route.

   Reuse `ReconcileAutomaticVehicleLookups` and its existing
   `IntakeFunctions` caller without modifying either: the current route queues
   lookup work after intake, rather than adding a second HTTP client or timer.
   Reuse `IVehicleLookupAdapter`, `VehicleLookupResult`,
   `MotTestObservation`, and `EfVehicleLookupWorkStore`.

   Files:

   - `src/Pegasus.Core/Vehicle/LookupContracts.cs`
   - `src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs`
   - `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs`
   - `tests/Pegasus.IntegrationTests/AutomaticVehicleLookupTests.cs`
   - `tests/Pegasus.IntegrationTests/ProductionVehicleLookupTests.cs`

   Keep current make/model/mileage suggestion behaviour unchanged. Add a
   Core-owned deterministic selection of MOT expiry from the newest valid
   observation with an expiry date. In the lookup work store, write lookup
   `Fact` rows only for fuel, engine capacity, manufacture year, and MOT
   expiry, with `vehicle_lookup` provenance. Before writing, inspect Fact and
   Confirmed rows and skip the field if either exists; this avoids the
   `(CaseId, FieldName, ValueKind)` collision and never overwrites extracted
   or operator-entered data. Do not create suggestions/chips for CASE-043
   fields.

   Done when lookup-origin fields are facts with source attribution, extracted
   or confirmed values remain untouched, repeats do not duplicate values, and
   unsupported fields remain absent.

4. Ship the additive schema change, generated artifacts, and runtime-grant
   evidence together.

   Reuse the constraint replacement pattern in
   `20260828185508_ProviderDeclaredInstruction`, the Worker-role convention in
   `20260814092852_AddWorkerCaseCreationGrants`, and
   `TypedCaseDataMigrationTests`.

   Files:

   - `src/Pegasus.Infrastructure/Persistence/Migrations/*_CaseVehicleDataV2.cs`
   - `src/Pegasus.Infrastructure/Persistence/Migrations/*_CaseVehicleDataV2.Designer.cs`
   - `src/Pegasus.Infrastructure/Persistence/Migrations/PegasusDbContextModelSnapshot.cs`
   - `tests/Pegasus.IntegrationTests/TypedCaseDataMigrationTests.cs`
   - `tests/Pegasus.IntegrationTests/AzureSqlRuntimeRoleMigrationTests.cs`

   Add nullable `InstructionDrafts` columns, drop/recreate only
   `CK_CaseDataFields_FieldName`, and regenerate EF metadata. Carry the
   existing Worker `SELECT, INSERT, UPDATE` grants for `InstructionDrafts` and
   `CaseDataFields` explicitly in the migration's SQL Server path; do not
   revoke them in `Down`, because they predate this additive change. Extend the
   runtime-role test to prove those permissions under the Worker role.

   No backfill is needed: the fields are optional. Forward migration is
   non-destructive; `Down` drops newly captured optional values, so rollback
   after writes requires an explicit restore decision.

5. Verify the complete caller path and prepare the review hand-off.

   Reuse `CaseDataCompletenessPersistenceTests`,
   `VehicleLookupGapFillTests`, `ProductionVehicleLookupTests`, and
   `AzureSqlRuntimeRoleMigrationTests`; do not add packages, UI code, labels,
   or a second field-name list.

   Acceptance conditions:

   - All ten fields survive Core contract, persistence, constraint, and
     projection round-trips.
   - Extracted fields appear as extraction-backed facts without lookup.
   - Supported lookup values are automatic, attributed lookup facts, never
     chips, and never replace extracted or confirmed values.
   - The existing intake reconciliation remains the only automatic lookup
     caller; no new client, schedule, or queue is introduced.
   - Migration, runtime grant evidence, snapshot, and generated designer ship
     together.
   - No routed Razor page changes. Therefore Test UI snapshot commands are
     inapplicable unless scope changes.

Binding design rules: no explanatory copy; labels remain only in
`Presentation/OperatorLabels.cs`; no UI state is added—unsupported fields are
absent, not disabled; exact existing lifecycle labels remain unchanged; Core
owns validation and lookup-selection policy; `CaseDataFieldNames` remains the
one persisted vocabulary; no packages; and the migration/grants travel in one
diff.

Stop condition: after the operator questions and named dependencies are
resolved, open the CASE-043 PR against `dev`, move the ticket one boundary to
Review only when live gates permit it, and stop. Do not merge.
