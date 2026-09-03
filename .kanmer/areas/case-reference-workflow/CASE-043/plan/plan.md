# Plan — CASE-043 (2026-09-03, gpt-5.6-terra xhigh; corrected 2026-09-03 after cross-model review)

Planning baseline: clean checkout at `07ac7f1be9fc9fc04814fd5347ae5da30aff62da`. The existing
automatic lookup is already invoked from intake reconciliation; the migration-grant
check currently passes (87 migrations).

Correction of a ticket-body premise: `IVehicleLookupAdapter` and
`VehicleLookupResult` (`src/Pegasus.Core/Vehicle/LookupContracts.cs`) and the
production `DvlaDvsaProductionAdapter` **already exist and predate CASE-029**;
CASE-029 does not add the port and its board `Owns` list does not claim that
file. CASE-043 still runs after CASE-029 for the Vehicle-section surface, but
the port is not a shared-lock path between them.

Working assumptions (both still gated by the open questions):

- All ten new fields are ordinary optional Case fields; they do not change
  instruction/case completeness unless the operator answers otherwise. Because
  `InstructionFieldEngine.FieldDefinition.IsRequired` defaults to `true`
  (`src/Pegasus.Core/Intake/InstructionFieldExtraction.cs:13`), every new QDOS
  definition is written `IsRequired: false` explicitly, and no name is added to
  `InstructionDraftCompleteness.MissingFieldNames`.
- Automatic enrichment uses only current `IVehicleLookupAdapter` output:
  fuel, engine capacity, manufacture year, and MOT expiry. Colour,
  transmission, body, first registration, tax expiry, and VIN remain
  extraction / provider-declared / manual-entry fields. Expanding the DVLA
  parser beyond `make`, `model`, `yearOfManufacture`, `engineCapacity`,
  `fuelType` is out of scope unless the second open question is answered
  otherwise.

Coordination notes (not steps):

- `src/Pegasus.Core/Cases/CaseDataOperations.cs` reads
  `RequireStaffImageReviewBeforeEngineerAssignment` at line 85, which PLAT-070
  removes under D44. CASE-043 merges after PLAT-070 or refreshes over it; it
  neither adds nor preserves a staff review flag of its own.
- Step 1b touches `src/Pegasus.Core/Assessment/AssessmentContracts.cs`, which
  also carries `assessment.impact_location` / `impact_severity`. Confirm with
  the ENG-036 damage lane before taking that file; it is not on the EPIC-012
  capacity-one list, but the two lanes can meet there.
- `src/Pegasus.Infrastructure/Persistence/Migrations/**` and the governing docs
  are capacity-one: take the migration slot and the FRD-06 slot in turn.

1. Extend the Core-owned vehicle record and its persisted projection.

   Reuse `CaseVehicleData`, `CaseDataPolicy.Normalize`,
   `CaseDataPolicy.ValidateDate`, `CaseDataFieldNames`,
   `CaseDataModelConfiguration`, and `EfCaseDataStore`'s projection helpers.

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
   `CaseVehicleData` fields, using the `CaseDataCodes.Text` / `.Integer` /
   `.Date` types CaseData already supports. Normalize text with the existing
   helpers, reject non-positive engine capacity and manufacture year, validate
   dates with `ValidateDate`, and do not invent a VIN format. Add the ten names
   only to `CaseDataFieldNames.All`; the existing check-constraint generator
   remains the sole field-name list.

   **`CaseEditableData` is not expanded by this step** — see open question 3.
   `EfCaseDataStore.SetConfirmed` deletes the `Confirmed` row when its
   parameter is `null` (`EfCaseDataStore.cs:368-387`), and every production
   save caller — `Pages/Cases/Details.cshtml.cs` `OnPostSaveAsync`,
   `Pages/Cases/Shared/_CaseDataHiddenFields.cshtml`, and
   `Mcp/AssessmentMcpTools.cs` — constructs the record from the current twenty
   fields. Appending ten more without changing all three would let an ordinary
   unrelated save silently clear confirmed CASE-043 values. Whichever way
   question 3 is answered, an integration test must prove that a save of an
   unrelated field retains all ten values.

   Done when extraction, lookup and Case projection round-trip every new field
   and the check constraint accepts exactly the extended list.

1b. Retire the four duplicated Assessment field names into the Case record.

   `AssessmentVocabulary` already owns `vehicle.year`, `vehicle.vin`,
   `vehicle.engine_cc` and `vehicle.fuel`
   (`src/Pegasus.Core/Assessment/AssessmentContracts.cs:33-38`), and
   `AssessmentReportProjection.BuildVehicle` reads them from that store while
   reading registration, make, model and mileage from `CaseOwned`
   (`src/Pegasus.Core/Reports/AssessmentReportProjection.cs:228-241`). Adding
   the same four to CaseData without removing them there would leave two
   persisted owners of one concept — a stop condition.

   Reuse the existing `AssessmentCaseOwnedData` seam, which is exactly how
   registration, make, model and mileage are already Case-owned inside the
   assessment projection.

   Files:

   - `src/Pegasus.Core/Assessment/AssessmentContracts.cs`
   - `src/Pegasus.Core/Reports/AssessmentReportProjection.cs`
   - `src/Pegasus.Infrastructure/Persistence/EfCaseAssessmentStore.cs`
     (`MapCaseOwned`)
   - `src/Pegasus.Web/Mcp/AssessmentMcpTools.cs` (`MapCaseOwned`)
   - the assessment field-name check constraint and its model configuration
   - the corresponding Core and integration tests

   Remove the four names from the `AssessmentVocabulary` definition list, carry
   them on `AssessmentCaseOwnedData`, and repoint the report projection and the
   MCP tool projection at `CaseOwned`. The migration (step 4) deletes existing
   assessment field rows for those four names in the same diff before narrowing
   the assessment constraint — "no backfill needed" does not cover a narrowed
   constraint over rows that already exist.

   Rejected alternative: leaving the four in Assessment and adding only six new
   Case names. It fails the ticket's "every listed field has a Core owner on
   the case record" and cannot satisfy the population order, because assessment
   data does not exist at intake.

   Done when exactly one persisted vocabulary owns each of the four, the report
   renders them from `CaseOwned`, and no assessment row for those names remains.

2. Carry instruction-extracted values through the shared draft into CaseData,
   on both instruction paths.

   Reuse `InstructionDraft`, QDOS `FieldDefinition`,
   `CaseDataSnapshotFactory.AddInstructionSuggestions` / `AddExtractedValue`,
   `InstructionDraftEntity`, and `EfIntakeReceiptStore`'s bidirectional
   mapping.

   Files:

   - `src/Pegasus.Core/Intake/IntakeContracts.cs`
   - `src/Pegasus.Core/Intake/DirectProviders/Qdos/QdosInstructionExtractionPolicy.cs`
   - `src/Pegasus.Core/ProviderApi/ProviderInstructionJson.cs`
   - `src/Pegasus.Core/ProviderApi/ProviderInstructionPolicy.cs`
   - `src/Pegasus.Infrastructure/Persistence/PegasusDbContext.cs`
   - `src/Pegasus.Infrastructure/Persistence/EfIntakeReceiptStore.cs`
   - `src/Pegasus.Infrastructure/Persistence/CaseDataSnapshotFactory.cs`
   - `tests/Pegasus.Core.Tests/Intake/Qdos/QdosInstructionExtractionPolicyTests.cs`
   - `tests/Pegasus.Core.Tests/ProviderApi/ProviderInstructionPolicyTests.cs`
   - `tests/Pegasus.IntegrationTests/CaseDataCompletenessPersistenceTests.cs`

   Append nullable draft/entity properties, configure their existing SQL types
   and bounds, add labelled QDOS extraction definitions with `IsRequired:
   false`, and map present values as `Fact` fields through `AddExtractedValue`.

   Provider-declared intake is a step, not a deferred dependency: D49 says
   "extraction from the supplied instruction **or data** first", and
   `ProviderInstructionVehicleBody` is the declared-instruction vehicle shape
   (`ProviderInstructionJson.cs:170-175`) normalized and projected by
   `ProviderInstructionPolicy` (lines 129-256). Leaving it unchanged would give
   a case a different vehicle record depending on its intake channel. Extend
   the wire body, its parser, normalization, `ToDraft` and its review fields.

   Preserve extraction provenance and leave absent values absent. Do not alter
   completeness under the stated assumption.

   Done when an instruction — QDOS-extracted or provider-declared — containing
   all ten values produces corresponding CaseData facts without a lookup.

3. Fill the supported missing fields through the existing automatic lookup
   route, as suggestions.

   Reuse `ReconcileAutomaticVehicleLookups` and its existing `IntakeFunctions`
   caller without modifying either: the current route queues lookup work after
   intake rather than adding a second HTTP client or timer. Reuse
   `IVehicleLookupAdapter`, `VehicleLookupResult`, `MotTestObservation`, and
   `EfVehicleLookupWorkStore.AddLookupSuggestionsAsync`.

   Files:

   - `src/Pegasus.Core/Vehicle/VehicleMotExpiryPolicy.cs` (new, small)
   - `src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs`
   - `tests/Pegasus.Core.Tests/Vehicle/VehicleMotExpiryPolicyTests.cs` (new)
   - `tests/Pegasus.IntegrationTests/VehicleLookupGapFillTests.cs`
   - `tests/Pegasus.IntegrationTests/AutomaticVehicleLookupTests.cs`

   **Lookup values stay `Suggestion` rows, not `Fact` rows.**
   `CaseField<T>.Current => Confirmed ?? Fact ?? Suggestion`
   (`CaseDataContracts.cs:61`) already guarantees that a lookup value can never
   displace an extracted fact or a staff-confirmed value — the ENG-013 rule the
   existing `AddLookupSuggestionsAsync` comment states. Writing lookup output
   as `Fact` instead would mislabel it as extraction-backed, and the bespoke
   "inspect Fact and Confirmed rows and skip" guard would be a second
   precedence mechanism beside the one Core already owns. So the change here is
   only to extend the existing `Suggest(...)` calls to
   `VehicleFuel`, `VehicleEngineCapacity`, `VehicleManufactureYear` and
   `VehicleMotExpiry`, keeping `SourceKind = CaseDataCodes.VehicleLookup`
   provenance and the existing "skip if a suggestion already exists" behaviour.

   D49's "chips for make, model and mileage only" is a statement about the
   Vehicle section's UI, satisfied by CASE-029 rendering chips for those three
   only. It is not a reason to change the persisted value kind.

   MOT expiry selection is the one genuinely new business rule, and no existing
   selector fits: `VehicleMileagePolicy.Calculate` selects mileage, not an
   expiry date. It therefore gets its own small Core policy — never the EF
   store — defining: consider observations whose `TestStatus` is a pass and
   whose `ExpiryDate` is non-null; take the latest `TestDate`; on a same-date
   conflict with different expiry dates, abstain; abstain when none qualifies.
   Abstention writes nothing.

   Done when lookup-origin fields appear as attributed lookup suggestions,
   extracted or confirmed values remain the current value, repeats add nothing,
   unsupported fields remain absent, and the selector's abstention cases are
   proved by Core tests.

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

   Add nullable `InstructionDrafts` columns, drop/recreate
   `CK_CaseDataFields_FieldName`, and — for step 1b — delete existing
   assessment field rows named `vehicle.year`, `vehicle.vin`,
   `vehicle.engine_cc` and `vehicle.fuel` **before** narrowing the assessment
   field-name constraint, then regenerate EF metadata. Carry the existing
   Worker `SELECT, INSERT, UPDATE` grants for `InstructionDrafts` and
   `CaseDataFields` explicitly in the migration's SQL Server path; do not
   revoke them in `Down`, because they predate this additive change. Extend the
   runtime-role test to prove those permissions under the Worker role.

   `Down` drops newly captured optional values and cannot restore the deleted
   assessment rows, so rollback after writes requires an explicit restore
   decision.

5. Reconcile FRD-06 with D49 in the same diff.

   `docs/frd/frd-06-vehicle-and-engineering-evidence.md` still describes every
   lookup value as a staff-reviewed suggestion, and its Excluded clause (line
   231) forbids "no provider adapter, scheduled lookup, cohort dataset,
   automatic external call, or unreviewed Case mutation" — which the ticket's
   automatic-on-intake population directly contradicts. Correct both, scoped to
   D49 and this ticket's behaviour only; do not absorb D44 or D45 remediation,
   and take the governing-doc capacity-one slot.

   `docs/frd/frd-02-intake-and-source-identity.md` line 357 already states
   "Vehicle details are extracted from the instruction where available,
   otherwise obtained from the applicable DVLA/MOT source" — it needs the new
   field list only if the operator resolves question 2 by expanding the
   adapter. No other FRD-02 edit belongs to this ticket.

6. Verify the complete caller path and prepare the review hand-off.

   Reuse `CaseDataCompletenessPersistenceTests`, `VehicleLookupGapFillTests`,
   `AzureSqlRuntimeRoleMigrationTests`, and `TypedCaseDataMigrationTests`; do
   not add packages, UI code, labels, or a second field-name list.

   Acceptance conditions:

   - All ten fields survive Core contract, persistence, constraint, and
     projection round-trips.
   - Exactly one persisted vocabulary owns manufacture year, VIN, engine
     capacity and fuel, and the report renders them from `CaseOwned`.
   - Extracted fields appear as extraction-backed facts without a lookup, from
     both the QDOS extraction path and the provider-declared path.
   - Supported lookup values are automatic, attributed lookup suggestions,
     never chips, and never become the current value over an extracted or
     confirmed one.
   - A save of an unrelated case field retains all ten values.
   - The existing intake reconciliation remains the only automatic lookup
     caller; no new client, schedule, or queue is introduced.
   - Migration, assessment-row disposal, runtime grant evidence, snapshot and
     generated designer ship together.
   - No routed Razor page changes, so the Test UI snapshot commands do not run.

Binding design rules: no explanatory copy; labels remain only in
`Presentation/OperatorLabels.cs`; no UI state is added — unsupported fields are
absent, not disabled; Core owns validation and the MOT-expiry selection policy;
`CaseDataFieldNames` remains the one persisted vocabulary; no packages; and the
migration, its grants and the assessment-row disposal travel in one diff.

Stop condition: after the operator questions are resolved, open the CASE-043 PR
against `dev`, move the ticket one boundary to Review only when live gates
permit it, and stop. Do not merge.

## Simplification pass

Not yet run — this is a plan, not a branch diff. It runs over the branch's own
diff before the PR opens, per the repository task workflow.

## Plan review (2026-09-03, gpt-5.6-sol xhigh; dispositions Claude Opus)

Verdict as read: REQUEST CHANGES. Eight findings; six fixed, one fixed and
escalated, one rejected with reason. Three further findings were raised by the
dispositioning pass and fixed.

| # | Severity | Step | Finding | Disposition |
| --- | --- | --- | --- | --- |
| 1 | blocker | assumptions, 2 | Both gate-blocking questions are unresolved but the plan silently answers them; `FieldDefinition.IsRequired` defaults to `true`, so new QDOS definitions would become required by accident. | **Fixed.** The default is confirmed at `InstructionFieldExtraction.cs:13`; the plan now requires `IsRequired: false` explicitly and states that no name enters `InstructionDraftCompleteness.MissingFieldNames`. The two questions stay open and still gate the boundary. |
| 2 | blocker | 1, 4 | Manufacture year, VIN, engine capacity and fuel already exist as `vehicle.year` / `vehicle.vin` / `vehicle.engine_cc` / `vehicle.fuel` in `AssessmentVocabulary`; adding them to CaseData creates two persisted owners. | **Fixed.** Verified at `AssessmentContracts.cs:33-38` and `AssessmentReportProjection.cs:228-241`. New step 1b retires the four into the existing `AssessmentCaseOwnedData` seam, repoints the report and MCP projections, and step 4 deletes the existing assessment rows before narrowing that constraint. The "keep six only" alternative is recorded and rejected. |
| 3 | blocker | 1 | Appending to `CaseEditableData` keeps positional alignment but not save semantics: `SetConfirmed` deletes the confirmed row on `null`, and no production save caller supplies the ten new values, so an unrelated save would clear them. | **Fixed and escalated.** Verified at `EfCaseDataStore.cs:368-387`, `Details.cshtml.cs:357`, `_CaseDataHiddenFields.cshtml`, `AssessmentMcpTools.cs:439`. The plan drops the `CaseEditableData` expansion from step 1 and requires a retention regression test either way; who ships the staff-editable path is open question 3. |
| 4 | blocker | 2 | Provider-declared instruction intake is treated as optional follow-up, though D49 says "supplied instruction **or data**"; a case's vehicle record would differ by intake channel. | **Fixed.** `ProviderInstructionJson.cs` and `ProviderInstructionPolicy.cs` and their tests are promoted from "named dependency" into step 2. |
| 5 | blocker | 3 | `src/Pegasus.Core/Vehicle/LookupContracts.cs` is CASE-029's lookup port, so the lanes overlap. | **Rejected, with one correction adopted.** CASE-029's board `Owns` list is `_CaseVehicle.cshtml`, `_CaseValuation.cshtml`, `Pages/Cases/Vehicle.*`, `Custody.*` and `RequestUploadPolicy.cs`; it does not claim `LookupContracts.cs`, which predates both tickets (`DvlaDvsaProductionAdapter` and `ReconcileAutomaticVehicleLookups` are already wired at `DependencyInjection.cs:683` and `IntakeFunctions.cs:154`). No lane overlap. The ticket-body claim that the port was "added by CASE-029" is wrong and is corrected at the head of this plan. After finding C1 the plan no longer edits `LookupContracts.cs` or `ProductionVehicleLookupTests.cs` at all. |
| 6 | should-fix | 3 | "Newest valid observation with an expiry date" is new business-selection policy with no named home or edge cases. | **Fixed.** Step 3 gives it its own Core policy with explicit ordering, pass-status filter, same-date-conflict abstention and no-qualifying-observation abstention, plus its own Core tests. `VehicleMileagePolicy` is named as the related-but-unfit selector. |
| 7 | should-fix | dependencies | FRD-06 is known stale but its correction is neither a step nor a checklist item. | **Fixed.** New step 5, scoped to D49, with a checklist item and the governing-doc capacity-one note. The dispositioning pass added the sharper conflict: FRD-06's Excluded clause (line 231) forbids the automatic external call this ticket requires. FRD-02:357 already matches D49 and needs no edit. |
| 8 | should-fix | 6, checklist | The test command excludes Browser tests, but the delivery gate is `--filter "Category!=Corpus"`; the conditional Test UI item is dead when routed Razor changes are forbidden. | **Fixed.** The checklist uses the canonical command and the dead conditional is removed. |
| C1 | blocker (dispositioning pass) | 3 | Writing lookup output as `Fact` with a bespoke "skip if Fact or Confirmed exists" guard duplicates the precedence `CaseField<T>.Current => Confirmed ?? Fact ?? Suggestion` already enforces, and mislabels lookup output as extraction-backed. | **Fixed.** Verified at `CaseDataContracts.cs:61` and the ENG-013 comment at `EfVehicleLookupWorkStore.cs:284-293`. Step 3 now extends the existing `AddLookupSuggestionsAsync` calls and drops the guard entirely. |
| C2 | should-fix (dispositioning pass) | 3 | Field-selection policy was to be authored inside `EfVehicleLookupWorkStore`, an Infrastructure file, breaching Core-owns-policy. | **Fixed.** With C1 the store keeps only mapping; the sole new rule (MOT expiry selection) lives in Core. |
| C3 | nit (dispositioning pass) | 1 | `CaseDataOperations.cs:85` reads `RequireStaffImageReviewBeforeEngineerAssignment`, which PLAT-070 removes under D44. | **Fixed.** Recorded as a merge-ordering coordination note. Answering the reviewer's question (f) directly: nothing in this plan assumes a staff review flag or a damage type; "staff save" is ordinary case editing. |

Files the reviewer confirmed real: every named helper, store, test file and
migration precedent; the automatic reconciliation caller; the 87-migration
count. No package is added and no unnecessary service is introduced.
