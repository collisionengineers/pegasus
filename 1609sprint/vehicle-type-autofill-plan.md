\# Auto-fill "Vehicle type" from the DVLA lookup



\## Context



The Case record's \*\*Vehicle type\*\* select (`vehicle.vehicle\_type`, codes

`car|van|motorcycle|scooter|bicycle|trailer|caravan|other`) is required for

report readiness but nothing ever fills it: staff must pick it by hand on every

Case. The combined DVLA VES + DVSA MOT lookup already runs at Case creation and

on demand, and DVLA's response carries `typeApproval` (M1 car, N1 van,

L-class two-wheelers, N2/N3 HGV, O trailers…), `wheelplan` and `revenueWeight` —

but the adapter reads only make/model/year/engine/fuel and drops the rest

(`src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs:170-176`).



Outcome: every Case whose registration resolves at DVLA gets Vehicle type

filled automatically, tagged \*\*Lookup\*\* (the chip already exists in

`\_CaseVehicle.cshtml:160`), and the staff Save re-stamps it as confirmed exactly

as VIN/type/body saves work today. Staff-entered values are never overwritten.



Operator decisions (2026-09-16): L1e/L2e → `scooter`, other L-class →

`motorcycle`; fall back on wheelplan + revenue weight when type approval is

absent; HGV/bus/tractor → `other` (code list unchanged).



\## Design



Same shape as the mileage derivation: the adapter captures raw provider

signals, a Core policy derives a deterministic code from them, the fill step

writes it only where the Case does not already answer.



\### 1. Carry the signals through the lookup contract (Core)



`src/Pegasus.Core/Vehicle/LookupContracts.cs`

\- Extend `VehicleDetails` with `string? TypeApproval, string? Wheelplan, int? RevenueWeightKg`.

\- `VehicleLookupResult.EnsureValidFor`: blank `TypeApproval`/`Wheelplan` and

&#x20; `RevenueWeightKg <= 0` are invalid, mirroring `FuelType`/`EngineCapacityCc`.



`src/Pegasus.Core/Vehicle/VehicleLookupFillPolicy.cs`

\- `Merge`: DVLA-first for the three new members (DVSA never supplies them);

&#x20; extend the all-null → null check.



\### 2. New Core policy: `src/Pegasus.Core/Vehicle/VehicleTypePolicy.cs`



`public static class VehicleTypePolicy { MethodKey = "dvla-type-approval"; MethodVersion = 1; static string? Classify(VehicleDetails? vehicle) }`

— pure, returns one of the `vehicle.vehicle\_type` codes or null. Normalise

input (trim, upper-case, remove spaces), then:



1\. \*\*Type approval\*\* (decisive when recognised)

&#x20;  - `M1` → `car`; `M2`,`M3` → `other`

&#x20;  - `N1` → `van`; `N2`,`N3` → `other`

&#x20;  - `L1`,`L2` (with or without `e`) → `scooter`; `L3`–`L7` → `motorcycle`

&#x20;  - `O1`–`O4` → `trailer`

&#x20;  - `T…`, `C…`, `R…`, `S…` (agricultural/special) → `other`

&#x20;  - anything else / absent → tier 2

2\. \*\*Wheelplan\*\*

&#x20;  - contains `2-WHEEL` or `3-WHEEL` → `motorcycle`

&#x20;  - contains `ARTIC` or `MULTI AXLE` → `other`

&#x20;  - contains `RIGID BODY` → tier 3

&#x20;  - else (`NON STANDARD`, `CRAWLER`, absent) → null

3\. \*\*Revenue weight\*\* (rigid-body only): absent → `car`; 1–3500 kg → `van`; >3500 kg → `other`.



Tier 3 is the operator-approved heuristic; the raw signals are persisted (step

3\) so the rule can be re-run if real VES bodies show cars with a revenue weight.



\### 3. Adapter + persistence (Infrastructure)



\- `DvlaDvsaProductionAdapter.ReadDvlaAsync` (`:170-176`): also read

&#x20; `Text(root,"typeApproval")`, `Text(root,"wheelplan")`, `Number(root,"revenueWeight")`.

&#x20; `ParseDvsaVehicle` (`:377-383`) passes nulls for the new members.

\- `DvlaDvsaAdapters.cs` replay fixture: `ReplayFixture.Vehicle` is the same

&#x20; record, so fixtures may optionally carry the new members; no change needed

&#x20; beyond confirming `JsonUnmappedMemberHandling.Disallow` still passes.

\- `VehicleEntities.cs` `VehicleLookupObservationEntity`: add

&#x20; `TypeApproval`, `Wheelplan`, `RevenueWeightKg`; configure lengths in

&#x20; `VehicleModelConfiguration.cs`; set them in `EfVehicleLookupWorkStore.RecordOutcomeAsync` (`:184-188`)

&#x20; and read them back in `MapObservation` if `VehicleLookupObservation` (Core)

&#x20; exposes vehicle details.

\- \*\*Additive migration\*\* (e.g. `VehicleLookupTypeSignals`): three nullable

&#x20; columns on the observations table; update the model snapshot. No grants, no

&#x20; data movement.



\### 4. Fill the assessment field



`EfVehicleLookupWorkStore.FillEmptyVehicleFieldsAsync` (`:308-404`) — after the

CaseDataFields fills, derive `var code = VehicleTypePolicy.Classify(result.Vehicle)` and, when

non-null:

\- load the existing `CaseAssessmentFields` row for `AssessmentVocabulary.VehicleType`;

\- apply the one fill rule: `VehicleLookupFillPolicy.Fills(hasFact: false, hasConfirmed: existing?.ConfirmedBy is not null)`

&#x20; — a staff-confirmed value is left alone; an earlier unconfirmed lookup value

&#x20; is re-stamped only if the code changed (same skip-if-unchanged rule as

&#x20; `AssessmentWriteSet.Apply`, `AssessmentFieldWriter.cs:154-165`);

\- write through `AssessmentFieldWriter.Write(context, owningCase, caseId, existing, path, code, ActorKind.Automation, "vehicle-lookup", recordedAtUtc, confirmedBy: null)`

&#x20; (load the `CaseEntity` the way `EfValuationStore.cs:584` does);

\- count it in `filled` so the report generation goes stale in the same transaction.



Nothing in Web changes: `\_CaseVehicle.cshtml:152-170` already pre-selects the

recorded code, shows the \*\*Lookup\*\* chip for `RecordedByKind == Automation`,

and the `save\_case` handler → `CaseWorkspace.AssessmentFields` → `AssessmentWriteSet.Apply`

re-stamps it as staff-confirmed on Save. Readiness (`AssessmentPolicy.cs:296`)

is unchanged and now passes automatically.



\### 5. Existing Cases



No backfill migration. The existing \*\*Look up DVLA \& MOT\*\* action re-runs the

lookup and now fills the empty type; the estate is a regularly wiped test

estate, so new Cases cover the rest.



\### 6. Documentation



\- `docs/frd/frd-06-vehicle-and-engineering-evidence.md` §Look up DVLA \& MOT

&#x20; (`:166-191`): extend "fills Make, Model, Year, or Mileage" to include the

&#x20; derived Vehicle type, name the derivation rule (type approval → wheelplan →

&#x20; revenue weight, mopeds → scooter, heavy/PSV → other) and its Lookup

&#x20; provenance / staff re-stamp on Save (D34 amended 2026-09-16).

\- `docs/current-architecture.md` only if it enumerates Core Vehicle policies.

\- Run `scripts/Test-MarkdownPlacement.ps1` base..head for the doc edit.



\## Files



| Area | Path |

|---|---|

| Contract | `src/Pegasus.Core/Vehicle/LookupContracts.cs` |

| Merge | `src/Pegasus.Core/Vehicle/VehicleLookupFillPolicy.cs` |

| New policy | `src/Pegasus.Core/Vehicle/VehicleTypePolicy.cs` |

| Adapter | `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs` |

| Entity/config | `src/Pegasus.Infrastructure/Persistence/VehicleEntities.cs`, `VehicleModelConfiguration.cs`, new migration + snapshot |

| Fill | `src/Pegasus.Infrastructure/Persistence/EfVehicleLookupWorkStore.cs` |

| FRD | `docs/frd/frd-06-vehicle-and-engineering-evidence.md` |



\## Tests



\- `tests/Pegasus.Core.Tests/Vehicle/VehicleTypePolicyTests.cs` (new): table

&#x20; over type approvals (`M1`,`N1`,`L1e`,`L3e`,`N3`,`O2`,`T1`), wheelplan-only

&#x20; cases (`2-WHEEL`, `2 AXLE RIGID BODY` with 0/1800/7500 kg revenue weight,

&#x20; `NON STANDARD` → null), casing/whitespace, null vehicle → null; and every

&#x20; non-null output is in `AssessmentVocabulary.Definitions\[VehicleType].Codes`.

\- `VehicleLookupFillPolicyTests.cs`: Merge carries the new members, DVLA first.

\- `tests/Pegasus.IntegrationTests/ProductionVehicleLookupTests.cs`: VES stub

&#x20; body with `typeApproval`/`wheelplan`/`revenueWeight` → `result.Vehicle` holds them.

\- `VehicleLookupGapFillTests.cs`: RecordOutcome writes

&#x20; `vehicle.vehicle\_type = "car"` as Automation/unconfirmed; a staff-confirmed

&#x20; row survives a re-lookup; an unconfirmed lookup row is re-stamped when the

&#x20; code changes and untouched when it doesn't; a null classification writes nothing.

\- Existing `CaseRecordGapsV26WebTests.TheVehicleSectionEditsVinTypeAndBodyOutsideTheEngineerSections`

&#x20; keeps covering the staff Save/confirm path.

\- Migration/architecture tests pick up the new snapshot.



\## Verification



1\. `dotnet build` the solution (host slot: this session; check free disk first).

2\. `dotnet test tests/Pegasus.Core.Tests --filter "FullyQualifiedName\~Vehicle|FullyQualifiedName\~Assessment"`.

3\. `dotnet test tests/Pegasus.IntegrationTests --filter "FullyQualifiedName\~VehicleLookup|FullyQualifiedName\~ProductionVehicleLookup|FullyQualifiedName\~CaseRecordGapsV26|FullyQualifiedName\~Migration"` (one suite at a time on LocalDB; keep the log).

4\. Local visual check: run Web with `DevelopmentOffline` against LocalDB, add a

&#x20;  replay fixture whose `vehicle` carries `typeApproval: "N1"`, create a Case

&#x20;  with that registration, confirm the Vehicle section shows \*\*Van · Lookup\*\*,

&#x20;  then Save and confirm the chip clears.

5\. `scripts/Test-MarkdownPlacement.ps1` for the FRD edit; PR → CI for full evidence.



