# Research — CASE-043 (2026-09-03, gpt-5.6-terra high)

## Method and state

Read-only checkout research only. `git status --short` was clean at
`07ac7f1be9fc9fc04814fd5347ae5da30aff62da`. I used `rg`, numbered
`Get-Content`, `git log`, and ran `scripts/Test-MigrationGrants.ps1`
(success: 87 migrations checked). The Kanmer tunnel was unavailable (404/429),
so no board documents were read or written.

## Verified findings

- `CaseVehicleData` currently owns only registration, make, model, mileage,
  and mileage unit in
  `src/Pegasus.Core/Cases/CaseDataContracts.cs:91-96`. The editable contract
  similarly exposes only those vehicle fields at lines 142-165.

- The one persisted field-name list is
  `CaseDataFieldNames` in
  `src/Pegasus.Infrastructure/Persistence/CaseDataEntities.cs:40-87`.
  `CaseDataModelConfiguration` derives
  `CK_CaseDataFields_FieldName` from that list at lines 39-79. The live
  model snapshot still permits only the current list at
  `Migrations/PegasusDbContextModelSnapshot.cs:1213-1225`.

- `CaseDataSnapshotFactory.AddInstructionSuggestions` maps accepted
  instruction-draft values into CaseData fields at
  `CaseDataSnapshotFactory.cs:188-292`; `AddExtractedValue` writes them as
  `Fact`, preserving extraction/provider provenance at lines 390-448.
  `EfCaseDataStore` must be extended in all three directions: staff save
  (`339-365`), editable-data reconstruction (`428-448`), and projection
  mapping (`617-643`).

- Instruction data has one shared carrier:
  `InstructionDraft` in `IntakeContracts.cs:379-403`; its SQL entity and EF
  mapping are in `PegasusDbContext.cs:247-273` and `1419-1447`; load/save
  mapping is in `EfIntakeReceiptStore.cs:593-613` and `842-883`.

- There is no LLM prompt. The current extraction vocabulary is deterministic
  QDOS `FieldDefinition` data in
  `QdosInstructionExtractionPolicy.cs:42-111`, presently covering
  registration, make, model, mileage and other non-vehicle fields only.
  The draft conversion is at lines 700-721. Its fixtures are the QDOS
  extraction tests and the synthetic instruction bodies in
  `tests/Pegasus.IntegrationTests/InstructionDraftWebTests.cs`.

- Provider-API intake is a second supported instruction source. Its vehicle
  wire shape has only registration/make/model/mileage/unit in
  `ProviderInstructionJson.cs:170-175`, maps at lines 67-95, and is
  normalized/projected by `ProviderInstructionPolicy.cs:12-34`,
  `129-171`, and `198-256`. It needs the same extension if "every other
  case field" includes declared-provider intake.

- The completeness rule is `InstructionDraftCompleteness` at
  `src/Pegasus.Core/Intake/InstructionDraftCompleteness.cs:18-122`.
  It currently treats ten fields as required, while only claimant, claim
  number, and registration are identity-critical. No CASE-043 field is
  included today.

- The existing lookup port is `IVehicleLookupAdapter` in
  `LookupContracts.cs:39-69` and `173-178`. It currently returns only make,
  model, manufacture year, engine capacity, fuel, and MOT observations.
  The production adapter reads only DVLA `make`, `model`,
  `yearOfManufacture`, `engineCapacity`, and `fuelType` at
  `DvlaDvsaProductionAdapter.cs:129-168`; DVSA supplies MOT expiry per test
  at `315-330`.

- Automatic lookup already exists, but as a Worker reconciliation sweep rather
  than directly inside extraction: `ReconcileAutomaticVehicleLookups` at
  `VehicleWorkflow.cs:444-477`, invoked by
  `Worker/IntakeFunctions.cs:151-205`. The persistence sweep selects an
  accepted/extracted registration and creates an idempotent work item in
  `EfVehicleWorkflowStore.cs:793-920`.

- Lookup outcome persistence already records manufacture year, engine capacity,
  and fuel in `VehicleLookupObservations`
  (`EfVehicleLookupWorkStore.cs:171-210`), but gap-fills only make, model,
  mileage and mileage unit (`296-364`). It writes `Suggestion` rows, including
  a competing suggestion when an extracted fact exists; the existing
  integration test deliberately proves that behaviour at
  `VehicleLookupGapFillTests.cs:43-61`. That conflicts with D49's
  "fill what extraction did not" and "no chips/no operator confirmation":
  CASE-043 needs a distinct automatic gap-fill policy that creates no value
  when a Fact or Confirmed value already exists, and records any value it does
  add as lookup provenance.

- Existing lookup input cannot populate VIN, body, transmission, first
  registration, colour, or tax expiry; it also does not expose a direct MOT
  expiry field beyond the latest MOT observation. Reuse the existing port and
  client, but extend their result shape/parser only where the approved DVLA/DVSA
  response contract supports a field. Do not add another client.

- Migration precedent is
  `20260828185508_ProviderDeclaredInstruction.cs:23-108`, which drops and
  recreates the CaseData field-name constraint. Current runtime permissions
  already grant Worker `SELECT, INSERT, UPDATE` on `CaseDataFields` and
  `SELECT, INSERT` on `CaseDataSnapshots` in
  `20260814092852_AddWorkerCaseCreationGrants.cs:58-59`; new columns on
  existing tables do not inherently need a new table grant. The CASE-043
  migration must nevertheless retain the appropriate existing-role grant
  evidence in its own diff. `Test-MigrationGrants.ps1` only detects tables
  created in `Up`, not altered constraints/columns.

## Reuse candidates and risks

- Reuse `CaseDataFieldNames` and `CaseDataModelConfiguration`; do not create a
  second field-name list.
- Reuse `InstructionDraft`, `AddExtractedValue`, and the QDOS
  `FieldDefinition` vocabulary; no extraction helper needs replacing.
- Reuse `IVehicleLookupAdapter`, `DvlaDvsaProductionAdapter`,
  `ReconcileAutomaticVehicleLookups`, and
  `EfVehicleLookupWorkStore.AddLookupSuggestionsAsync`; rename/refactor the
  latter only if necessary to express automatic fact gap-fill accurately.
- Reuse the existing migration constraint replacement and runtime-role grant
  conventions.
- Main risk: an automatic lookup `Suggestion` does not meet the ticket's
  no-chip/no-confirmation behaviour, while an automatic `Fact` cannot coexist
  with an extracted Fact because `(CaseId, FieldName, ValueKind)` is the key.
  The write must therefore skip any extracted or operator-confirmed value.
- Main source risk: the currently implemented external contract cannot supply
  all ten fields. The existing FRDs also describe provider selection/live
  activation as unresolved, despite the current production adapter.

## Out-of-scope vocabulary check

| Mockup field | Current Core owner |
| --- | --- |
| Class | Assessment `vehicle.vehicle_type`, not CaseData (`AssessmentContracts.cs:35-77`) |
| Condition | Assessment `vehicle.condition`, not CaseData |
| VIN/year/engine/fuel | Assessment vocabulary has independent fields; CASE-043 makes these Case-owned too |
| Mods, fault codes, airbags | No CaseData or Assessment vocabulary match found |
| Roadworthiness/temp repair/history | Triage/Assessment concepts exist, but not Case vehicle-record fields |

## Verified versus assumed

Verified: all code locations above, existing automatic lookup route, current
lookup field limits, migration/grant convention, and grant-test result.

Assumed pending operator confirmation: whether all ten new values are required
for instruction/case completeness; and whether the approved DVLA/DVSA contract
is expected to supply fields its present adapter does not read or model.
