# EXT-01 completion file map

## Modified

- `src/Pegasus.Web/Program.cs` — `else` branch on the existing availability registration: the production profile composes `VehicleLookupAvailability.ProductionLive` (Web only enqueues; the Worker owns the live adapter and already composes it in production).
- `src/Pegasus.Web/Pages/Cases/Shared/_CaseWorkflow.cshtml` — render `observation.Vehicle` (make, model, manufacture year, engine capacity, fuel type) in the latest-observation block so the operator sees the returned details before accepting/correcting.
- `tests/Pegasus.Core.Tests/Vehicle/VehicleWorkflowTests.cs` — extend the availability test: `ProductionLive` permits requests (alongside the existing Unavailable-refuses and DevelopmentOfflineReplay-permits assertions).
- `docs/current-architecture.md` — refresh the as-built sentence (line ~471): the Web-triggered lookup path is now composed in the production profile; live acceptance evidence remains a separate gate.

## Read, unchanged (seams relied on)

- `src/Pegasus.Core/Vehicle/VehicleWorkflow.cs` (`VehicleLookupAvailability` record — `ProductionLive` already defined), `src/Pegasus.Infrastructure/DependencyInjection.cs` (Worker-side `AddProductionExternalAdapters`), `src/Pegasus.Infrastructure/Vehicle/DvlaDvsaProductionAdapter.cs`, `src/Pegasus.Worker/WorkerDependencyInjection.cs`, `src/Pegasus.Worker/Functions/ExternalWorkFunctions.cs`, `tests/Pegasus.IntegrationTests/VehicleWorkflowTerminalTests.cs` (already override availability explicitly).
- No migrations, no new port/store/flag, no adapter changes, no capabilities.md/FRD change.
