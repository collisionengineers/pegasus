# Files — CASE-008

| File | Change |
| --- | --- |
| `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` | New `ReconcileAutomaticVehicleLookups` use case + `IAutomaticVehicleLookupStore` port (availability-gated, batch-capped) |
| `src/Pegasus.Infrastructure/Persistence/EfVehicleWorkflowStore.cs` | Implement the port: find eligible cases (current registration, no request row for that registration, not archived/terminal), insert work item + request rows under the Automation actor, leaseless |
| `src/Pegasus.Infrastructure/DependencyInjection.cs` | Register the use case/port in both compositions |
| `src/Pegasus.Worker/IntakeFunctions.cs` | Third reconcile call on the existing `%IntakeStagedArtifactReconciliationSchedule%` timer |
| `src/Pegasus.Worker/WorkerDependencyInjection.cs` | Compose the use case for the worker if not already reachable |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml` | Vehicle section: "Mileage" + "Source" labels, hints dropped, `value=`/`selected` prefill |
| `src/Pegasus.Web/Pages/Cases/Assessment/Index.cshtml.cs` | Load `CaseVehicleEvidence` via `IVehicleEvidenceQueries`; expose prefill values (saved assessment field first, else estimate) |
| `tests/Pegasus.IntegrationTests/` (new `AutomaticVehicleLookupTests.cs`) | Sweep behaviour on LocalDB |
| `tests/Pegasus.IntegrationTests/` (assessment web test file) | Prefill render assertions |

No migration: existing tables carry everything (request row + external work item).
