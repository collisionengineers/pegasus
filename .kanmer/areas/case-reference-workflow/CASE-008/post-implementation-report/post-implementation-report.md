# Post-implementation report — CASE-008

Branch task/case-008-auto-vehicle-lookup (218709e9). Delivered both limbs:

- **Automatic lookup**: `ReconcileAutomaticVehicleLookups` (Core, availability-gated) + `IAutomaticVehicleLookupStore` implemented on `EfVehicleWorkflowStore` — every active (non-archived, non-terminal) case whose current registration (Confirmed else Fact; single unambiguous normalized value) has no `VehicleLookupRequests` row gets one pending external work item + request row under `ActionActor.Automation("vehicle-lookup-reconciliation")`, op key `vehicle-lookup:auto:{registration}`, leaseless, no version bump. Runs as the fourth reconcile on the existing `%IntakeStagedArtifactReconciliationSchedule%` timer (no new app setting); the existing dispatcher, queue, DVSA/DVLA adapters and `VehicleMileagePolicy` do the rest. Idempotent through success and failure (the request row is the durable marker; the (CaseId, OperationKey) unique index guards concurrent sweeps); a corrected registration enqueues exactly one new lookup.
- **Assessment prefill**: vehicle section relabelled "Mileage" + "Source", hint sentences on those controls removed; Mileage prefills saved assessment value → confirmed evidence → DVSA estimate (miles only), Source preselects saved value → Online data; make/model/year/engine/fuel prefill saved → lookup details. Display-only prefill — the section's save path remains the UI-15 activation scope.

Tests: new `AutomaticVehicleLookupTests` 5/5 (enqueue-on-fact + idempotence, confirmed-over-fact + ambiguity skip, terminal/unusable skip, corrected-registration, unavailable no-op); new `AssessmentVehiclePrefillWebTests` 1/1; adjacent Vehicle/Assessment suites 25/25; architecture timer test updated (4 log states, constructor shape) 2/2; Core 853/853; Release build 0/0.

Deviation: subagents barred — self-reviewed.

## Verification hand-off
Post-deploy: a case allocated with a registration gains a lookup observation and mileage estimate without staff action within the reconcile+dispatch cadence; assessment page shows Mileage/Source prefilled; re-sweep enqueues nothing.
