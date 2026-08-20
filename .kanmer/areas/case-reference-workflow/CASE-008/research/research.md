# Research — CASE-008

All premises below verified by read-only checks on dev (grep/read); none assumed.

## The existing lookup pipeline (reused end-to-end)

- Staff request: `RequestVehicleLookup` (`src/Pegasus.Core/Vehicle/VehicleWorkflow.cs:164`) → `EfVehicleWorkflowStore.RequestAsync` — Serializable txn, replay by (CaseId, OperationKey) with fingerprint check, requires **exactly one Confirmed** registration, **requires an edit lease**, bumps case version, inserts `ExternalWorkItems` (Kind `vehicle_lookup`, state pending, due now) + `VehicleLookupRequestEntity` (WorkItemId, CaseId, Registration, OperationKey, RequestFingerprint, RequestedBy*, ResultingCaseVersion).
- Dispatch: `DispatchPendingWork` (timer `%PendingWorkDispatchSchedule%`, IntakeFunctions) pushes pending external work to the `external-work` queue; `ExternalWorkFunction` routes Kind `vehicle_lookup` → `ProcessQueuedVehicleLookup` (`LookupWorkItem.cs`) → `IVehicleLookupAdapter` (DVLA/DVSA production adapter or replay) → `VehicleMileagePolicy` computes the estimate → observation recorded. Retries/poison handled.
- Availability: `VehicleLookupAvailability` singleton — `Unavailable` in the offline composition (`DependencyInjection.cs:60`), `ProductionLive` in the live one (`:553`). The Core staff path throws when `RequestsEnabled` is false.
- `ActionActor.Automation(actorId)` exists (`IdentityContracts.cs:78`) and `StaffAuthorization` grants Automation the ordinary operational rights (`StaffAuthorization.cs:41`).

## Why the staff path can't be the trigger

It demands a Confirmed registration, an edit lease, and a staff click. Cases arrive with the registration at **Fact** tier (INTK-021 extraction) and no lease. So the automatic path must accept the *current* registration (Confirmed else Fact) and run leaseless under the Automation actor.

## Trigger shape: reconciliation sweep, not per-site hooks

Registration becomes known at ≥4 sites (acceptance snapshot, staff confirm/correct, image-intake registration, group registration). A per-site hook is four call sites; a sweep is one owner. Precedent: `ReconcileStagedArtifacts` + `ReconcileGroupedImageIntake` already share the `%IntakeStagedArtifactReconciliationSchedule%` timer in `IntakeFunctions.cs:74–95` (batch 50). Adding a third reconcile call there needs **no new app setting** (deployment-risk relevant: worker config ships as a config zip).

Idempotence: `VehicleLookupRequestEntity` (CaseId, Registration) rows persist through success *and* failure, so "no request row for this case+registration" is a stable already-done marker; a corrected registration is a new pair → one new lookup. Sweep filter: case not archived, lifecycle non-terminal, current registration exists and parses as `VehicleLookupRequest`.

## Assessment page facts

- The vehicle-section inputs in `Pages/Cases/Assessment/Index.cshtml` (~lines 290–355) are **unbound design markup** (model doc comment says so; "Save vehicle" posts nowhere — UI-15 owns activation). Prefill is therefore `value=` rendering, not a save path.
- Saved values do exist via `IGetCaseAssessment` → `CaseAssessmentProjection.Field("vehicle.odometer_miles" / "vehicle.mileage_source")` (AI-written per ADR-0021).
- Vocabulary paths `vehicle.odometer_miles`, `vehicle.mileage_source` are Core contract (`AssessmentContracts.cs:40,136`) — labels change in UI only.
- Mileage estimate source: `IVehicleEvidenceQueries.GetAsync(caseId)` → `CaseVehicleEvidence.LatestObservation.Mileage` (`VehicleMileageCalculation`) and `.Vehicle` (`VehicleDetails` make/model etc.).

## Test surfaces

`CaseVehicleWebTests` (substitute `IRequestVehicleLookup`), `VehicleWorkflowTerminalTests`, `ProductionVehicleLookupTests`; store-level tests hit LocalDB. New sweep gets store-level integration tests (enqueue-on-fact, skip-when-requested, skip-invalid/archived/terminal, replay-stable) + an assessment-page render test for the prefill.
